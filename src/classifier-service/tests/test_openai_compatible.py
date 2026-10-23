from types import SimpleNamespace
from typing import Any

import httpx
import openai
import pytest
from fastapi.testclient import TestClient

import app.main
from app.classifiers.openai_compatible import OpenAICompatibleClassifier
from app.main import app as fastapi_app
from app.models import Category, ClassifierConfig
from app.prompts import load_prompt

REQUEST = httpx.Request("POST", "https://llm.test/v1/chat/completions")


class FakeCompletions:
    """Stands in for client.chat.completions: records the call and returns or raises."""

    def __init__(self, content: str | None = None, error: Exception | None = None, usage: Any = "default") -> None:
        self.content = content
        self.error = error
        self.usage = SimpleNamespace(prompt_tokens=120, completion_tokens=8) if usage == "default" else usage
        self.calls: list[dict[str, Any]] = []

    def create(self, **kwargs: Any) -> Any:
        self.calls.append(kwargs)
        if self.error is not None:
            raise self.error
        message = SimpleNamespace(content=self.content)
        return SimpleNamespace(choices=[SimpleNamespace(message=message)], usage=self.usage)


def make_classifier(completions: FakeCompletions, default_model: str | None = "default-model") -> OpenAICompatibleClassifier:
    return OpenAICompatibleClassifier(SimpleNamespace(chat=SimpleNamespace(completions=completions)), default_model)


def config(**overrides: Any) -> ClassifierConfig:
    return ClassifierConfig(provider="openai_compatible", **overrides)


def status_error(cls: type[openai.APIStatusError], status: int, message: str = "error", body: Any = None) -> openai.APIStatusError:
    return cls(message, response=httpx.Response(status, request=REQUEST), body=body)


# --- Parsing ---------------------------------------------------------------


@pytest.mark.parametrize("category", [c for c in Category if c is not Category.INVALID])
def test_valid_json_returns_category(category: Category) -> None:
    raw = f'{{"category": "{category.value}"}}'
    classifier = make_classifier(FakeCompletions(raw))

    result = classifier.classify("texto", config())

    assert result.category == category
    assert result.valid is True
    assert result.raw_output == raw
    assert result.input_tokens == 120
    assert result.output_tokens == 8
    assert isinstance(result.latency_ms, int)


@pytest.mark.parametrize("raw", ['{"category": "BILLING"}', '{"category": "  Billing \\n"}', ' {"category":"billing"} '])
def test_category_is_normalized(raw: str) -> None:
    result = make_classifier(FakeCompletions(raw)).classify("texto", config())

    assert result.category == Category.BILLING
    assert result.valid is True
    assert result.raw_output == raw


@pytest.mark.parametrize(
    "raw",
    [
        '{"category": "sales"}',
        '{"category": "facturación"}',
        '{"category": "soporte_tecnico"}',
        '{"category": "invalid"}',
        '{"category": "bill"}',
        '{"category": 3}',
        '{"label": "billing"}',
        '["billing"]',
        '{"category": "billing"',
        "billing",
        "",
    ],
    ids=[
        "unknown_category",
        "translated_label",
        "spanish_label",
        "literal_invalid",
        "partial_label",
        "non_string",
        "wrong_key",
        "json_array",
        "malformed_json",
        "plain_text",
        "empty",
    ],
)
def test_unrecognized_output_is_invalid(raw: str) -> None:
    result = make_classifier(FakeCompletions(raw)).classify("texto", config())

    assert result.category == Category.INVALID
    assert result.valid is False
    assert result.raw_output == raw


def test_null_content_is_invalid_with_empty_raw_output() -> None:
    result = make_classifier(FakeCompletions(None)).classify("texto", config())

    assert result.category == Category.INVALID
    assert result.raw_output == ""


def test_missing_usage_gives_null_tokens() -> None:
    result = make_classifier(FakeCompletions('{"category": "billing"}', usage=None)).classify("texto", config())

    assert result.input_tokens is None
    assert result.output_tokens is None


def test_json_mode_validation_failure_is_invalid_not_provider_error() -> None:
    body = {"message": "Failed to generate JSON", "code": "json_validate_failed", "failed_generation": "Es billing"}
    error = status_error(openai.BadRequestError, 400, body=body)

    result = make_classifier(FakeCompletions(error=error)).classify("texto", config())

    assert result.category == Category.INVALID
    assert result.valid is False
    assert result.raw_output == "Es billing"


# --- Request sent to the provider -----------------------------------------


def test_sends_prompt_text_and_settings() -> None:
    completions = FakeCompletions('{"category": "billing"}')

    make_classifier(completions).classify("Me cobraron dos veces", config(temperature=0.3))

    call = completions.calls[0]
    assert call["model"] == "default-model"
    assert call["temperature"] == 0.3
    assert call["response_format"] == {"type": "json_object"}
    assert call["messages"] == [
        {"role": "system", "content": load_prompt("v1")},
        {"role": "user", "content": "Me cobraron dos veces"},
    ]


@pytest.mark.parametrize("version", ["v1", "v2"])
def test_prompt_version_selects_file(version: str) -> None:
    completions = FakeCompletions('{"category": "billing"}')

    make_classifier(completions).classify("texto", config(prompt_version=version))

    assert completions.calls[0]["messages"][0]["content"] == load_prompt(version)


def test_prompt_versions_differ_and_v2_has_boundary_rules() -> None:
    assert load_prompt("v1") != load_prompt("v2")
    assert "When a message could fit more than one category" in load_prompt("v2")
    assert "When a message could fit more than one category" not in load_prompt("v1")


def test_config_model_overrides_default_model() -> None:
    completions = FakeCompletions('{"category": "billing"}')

    make_classifier(completions).classify("texto", config(model="override-model"))

    assert completions.calls[0]["model"] == "override-model"


# --- API: validation and provider errors ----------------------------------


@pytest.fixture
def client() -> TestClient:
    return TestClient(fastapi_app)


def use_completions(monkeypatch: pytest.MonkeyPatch, completions: FakeCompletions) -> None:
    monkeypatch.setattr(app.main, "get_classifier", lambda provider: make_classifier(completions))


def post(client: TestClient, **config_fields: Any) -> httpx.Response:
    return client.post("/classify", json={"text": "texto", "config": {"provider": "openai_compatible", **config_fields}})


@pytest.mark.parametrize("version", ["v2", "v1"])
def test_api_accepts_existing_prompt_version(client: TestClient, monkeypatch: pytest.MonkeyPatch, version: str) -> None:
    use_completions(monkeypatch, FakeCompletions('{"category": "billing"}'))

    response = post(client, prompt_version=version)

    assert response.status_code == 200
    assert response.json()["category"] == "billing"


@pytest.mark.parametrize("version", ["v99", "V1", "1", "x1", "v1.txt", "../v1", "v1/../v2", ""])
def test_api_rejects_invalid_prompt_version(client: TestClient, version: str) -> None:
    response = post(client, prompt_version=version)

    assert response.status_code == 422


@pytest.mark.parametrize(
    "error",
    [
        status_error(openai.RateLimitError, 429),
        openai.APITimeoutError(request=REQUEST),
        openai.APIConnectionError(request=REQUEST),
    ],
    ids=["rate_limit", "timeout", "connection"],
)
def test_api_transient_provider_error_returns_503(client: TestClient, monkeypatch: pytest.MonkeyPatch, error: Exception) -> None:
    use_completions(monkeypatch, FakeCompletions(error=error))

    response = post(client)

    assert response.status_code == 503
    assert response.json()["detail"]


@pytest.mark.parametrize(
    "error",
    [
        status_error(openai.AuthenticationError, 401, "Invalid API Key: gsk_SECRET123"),
        status_error(openai.InternalServerError, 500),
        status_error(openai.BadRequestError, 400, body={"message": "bad model", "code": "model_not_found"}),
    ],
    ids=["authentication", "server_error", "bad_request"],
)
def test_api_other_provider_error_returns_502(client: TestClient, monkeypatch: pytest.MonkeyPatch, error: Exception) -> None:
    use_completions(monkeypatch, FakeCompletions(error=error))

    response = post(client)

    assert response.status_code == 502
    assert "SECRET" not in response.text


def test_api_auth_error_message_is_clear(client: TestClient, monkeypatch: pytest.MonkeyPatch) -> None:
    use_completions(monkeypatch, FakeCompletions(error=status_error(openai.AuthenticationError, 401)))

    response = post(client)

    assert "authentication failed" in response.json()["detail"]


def test_api_provider_not_configured_returns_503(client: TestClient, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.delenv("LLM_BASE_URL", raising=False)
    monkeypatch.delenv("LLM_API_KEY", raising=False)

    response = post(client)

    assert response.status_code == 503
    assert "LLM_BASE_URL" in response.json()["detail"]


def test_api_no_model_returns_503(client: TestClient, monkeypatch: pytest.MonkeyPatch) -> None:
    completions = FakeCompletions('{"category": "billing"}')
    monkeypatch.setattr(app.main, "get_classifier", lambda provider: make_classifier(completions, default_model=None))

    response = post(client)

    assert response.status_code == 503
    assert completions.calls == []
