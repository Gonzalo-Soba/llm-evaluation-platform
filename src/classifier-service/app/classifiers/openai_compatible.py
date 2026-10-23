import json
import logging
import os
import time
from typing import Any

import openai

from app.errors import ProviderError, ProviderUnavailableError
from app.models import Category, ClassificationResult, ClassifierConfig
from app.prompts import load_prompt

logger = logging.getLogger(__name__)

# Per attempt. With max_retries=2 the worst case stays close to the API's 30-second timeout.
REQUEST_TIMEOUT_SECONDS = 8.0
MAX_RETRIES = 2

VALID_CATEGORIES = {c.value for c in Category if c is not Category.INVALID}


def create_client_from_env() -> tuple[openai.OpenAI, str | None]:
    """Builds the SDK client from LLM_BASE_URL and LLM_API_KEY. Returns it with the default model (LLM_MODEL)."""
    base_url = os.environ.get("LLM_BASE_URL", "").strip()
    api_key = os.environ.get("LLM_API_KEY", "").strip()
    missing = [name for name, value in (("LLM_BASE_URL", base_url), ("LLM_API_KEY", api_key)) if not value]
    if missing:
        raise ProviderUnavailableError(f"The LLM provider is not configured: {', '.join(missing)} not set.")

    client = openai.OpenAI(
        base_url=base_url, api_key=api_key, timeout=REQUEST_TIMEOUT_SECONDS, max_retries=MAX_RETRIES
    )
    return client, os.environ.get("LLM_MODEL", "").strip() or None


class OpenAICompatibleClassifier:
    """Classifies with any OpenAI-compatible chat completions API, using a versioned system prompt."""

    def __init__(self, client: Any, default_model: str | None) -> None:
        self._client = client
        self._default_model = default_model

    def classify(self, text: str, config: ClassifierConfig) -> ClassificationResult:
        model = config.model or self._default_model
        if not model:
            raise ProviderUnavailableError("No model configured: set LLM_MODEL or config.model.")

        messages = [
            {"role": "system", "content": load_prompt(config.prompt_version)},
            {"role": "user", "content": text},
        ]

        start = time.perf_counter()
        try:
            completion = self._client.chat.completions.create(
                model=model,
                messages=messages,
                temperature=config.temperature,
                response_format={"type": "json_object"},
            )
        except openai.BadRequestError as error:
            # JSON mode on some providers (e.g. Groq) rejects output that is not valid JSON.
            # That is a model error, not a provider error: report it as an invalid classification.
            failed_generation = _failed_generation(error)
            if failed_generation is None:
                raise _provider_error(error) from error
            return _result(failed_generation, _elapsed_ms(start), None, None)
        except openai.APIError as error:
            raise _provider_error(error) from error
        latency_ms = _elapsed_ms(start)

        raw_output = completion.choices[0].message.content or ""
        usage = completion.usage
        return _result(
            raw_output,
            latency_ms,
            usage.prompt_tokens if usage else None,
            usage.completion_tokens if usage else None,
        )


def parse_category(raw_output: str) -> Category | None:
    """Returns the category if the output is a JSON object with a known category; None otherwise. No fuzzy matching."""
    try:
        data = json.loads(raw_output)
    except json.JSONDecodeError:
        return None
    if not isinstance(data, dict) or not isinstance(data.get("category"), str):
        return None
    category = data["category"].strip().lower()
    return Category(category) if category in VALID_CATEGORIES else None


def _result(raw_output: str, latency_ms: int, input_tokens: int | None, output_tokens: int | None) -> ClassificationResult:
    category = parse_category(raw_output)
    return ClassificationResult(
        category=category or Category.INVALID,
        valid=category is not None,
        raw_output=raw_output,
        latency_ms=latency_ms,
        input_tokens=input_tokens,
        output_tokens=output_tokens,
    )


def _elapsed_ms(start: float) -> int:
    return round((time.perf_counter() - start) * 1000)


def _failed_generation(error: openai.BadRequestError) -> str | None:
    if error.code != "json_validate_failed":
        return None
    body = error.body if isinstance(error.body, dict) else {}
    return str(body.get("failed_generation") or "")


def _provider_error(error: openai.APIError) -> ProviderError:
    # Messages are written here instead of forwarding the provider's text, which may echo part of the key.
    logger.warning("LLM provider call failed: %s", type(error).__name__)
    if isinstance(error, openai.RateLimitError):
        return ProviderUnavailableError("The LLM provider rate limit was exceeded.")
    if isinstance(error, openai.APITimeoutError):
        return ProviderUnavailableError("The LLM provider did not respond in time.")
    if isinstance(error, openai.APIConnectionError):
        return ProviderUnavailableError("The LLM provider could not be reached.")
    if isinstance(error, openai.AuthenticationError):
        return ProviderError("The LLM provider rejected the credentials (authentication failed).")
    if isinstance(error, openai.APIStatusError):
        return ProviderError(f"The LLM provider returned an error (status {error.status_code}).")
    return ProviderError("The LLM provider returned an unexpected error.")
