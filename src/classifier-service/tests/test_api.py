import pytest
from fastapi.testclient import TestClient

from app.main import app
from app.models import Category

client = TestClient(app)


def valid_payload(**overrides: object) -> dict:
    payload: dict = {
        "text": "I was charged twice",
        "config": {"provider": "mock", "model": None, "prompt_version": None, "temperature": 0.0},
    }
    payload.update(overrides)
    return payload


def test_health() -> None:
    response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


def test_classify_returns_contract_schema() -> None:
    response = client.post("/classify", json=valid_payload())

    assert response.status_code == 200
    body = response.json()
    assert set(body) == {"category", "valid", "raw_output", "latency_ms", "input_tokens", "output_tokens"}
    assert body["category"] == Category.BILLING.value
    assert body["category"] in {c.value for c in Category}
    assert body["valid"] is True
    assert isinstance(body["raw_output"], str)
    assert isinstance(body["latency_ms"], int)
    assert body["input_tokens"] is None
    assert body["output_tokens"] is None


def test_classify_uses_config_defaults() -> None:
    response = client.post("/classify", json={"text": "hello", "config": {"provider": "mock"}})

    assert response.status_code == 200


@pytest.mark.parametrize("text", ["", "   ", "a" * 2001], ids=["empty", "whitespace_only", "too_long"])
def test_classify_rejects_invalid_text(text: str) -> None:
    response = client.post("/classify", json=valid_payload(text=text))

    assert response.status_code == 422


def test_classify_rejects_unknown_provider() -> None:
    payload = valid_payload(config={"provider": "unknown"})

    response = client.post("/classify", json=payload)

    assert response.status_code == 422


@pytest.mark.parametrize("temperature", [-0.1, 2.1])
def test_classify_rejects_out_of_range_temperature(temperature: float) -> None:
    payload = valid_payload(config={"provider": "mock", "temperature": temperature})

    response = client.post("/classify", json=payload)

    assert response.status_code == 422
