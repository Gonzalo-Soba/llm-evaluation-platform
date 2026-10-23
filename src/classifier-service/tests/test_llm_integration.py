"""Calls the real LLM provider. Skipped unless LLM_API_KEY is set (also needs LLM_BASE_URL and LLM_MODEL)."""

import os

import pytest

from app.classifiers.openai_compatible import OpenAICompatibleClassifier, create_client_from_env
from app.models import Category, ClassifierConfig

pytestmark = pytest.mark.skipif(not os.environ.get("LLM_API_KEY"), reason="LLM_API_KEY is not set")


def test_real_provider_classifies_a_clear_message() -> None:
    client, default_model = create_client_from_env()
    classifier = OpenAICompatibleClassifier(client, default_model)

    result = classifier.classify(
        "Me cobraron dos veces la cuota de este mes, quiero que me devuelvan la plata.",
        ClassifierConfig(provider="openai_compatible", prompt_version="v1"),
    )

    # The model's exact answer may vary; the contract is a valid, parsed category with token usage.
    assert result.valid is True, f"raw output: {result.raw_output!r}"
    assert result.category in {c for c in Category if c is not Category.INVALID}
    assert result.latency_ms > 0
