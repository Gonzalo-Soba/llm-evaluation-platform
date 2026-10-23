# Classifier service

Python 3.12 + FastAPI service that classifies customer support messages. It only classifies text: it has no knowledge of datasets, labels, or metrics.

## Providers

The provider is chosen per request in `config.provider`:

- `mock`: deterministic, keyword-based, no external calls.
- `openai_compatible`: an LLM behind any OpenAI-compatible chat completions API (for example Groq), called with the official `openai` SDK.

### openai_compatible

Configured with environment variables (see the repository's `.env.example`):

| Variable | Description |
| --- | --- |
| `LLM_BASE_URL` | Base URL of the API, e.g. `https://api.groq.com/openai/v1`. |
| `LLM_API_KEY` | API key. Never logged or returned in error messages. |
| `LLM_MODEL` | Default model. `config.model` in the request overrides it. |

How it works:

- **Prompt.** The system prompt is the file `prompts/<prompt_version>.txt`, and the user message is the text to classify.
  - `v1` contains the four category definitions from `docs/labeling-guidelines.md`.
  - `v2` adds the boundary rules.
  - Neither includes the guidelines' example messages, because some are close to dataset messages.
  - `config.prompt_version` defaults to `v1`. It must match `^v[0-9]+$` and name an existing file; otherwise the request returns `422`.
- **Request.** The model is asked for `{"category": "..."}` with JSON response mode (`response_format: json_object`). `config.temperature` is passed through.
- **Parsing.** The category is trimmed, lowercased and matched exactly against the four categories. Anything else returns `category: "invalid"` and `valid: false`, with the model's exact text in `raw_output`. This includes other labels, translations, malformed JSON and empty output. There is no fuzzy matching. When JSON mode rejects the model's output (Groq's `json_validate_failed`), the failed generation is also reported as invalid, not as a provider error.
- **Metrics.** `latency_ms` measures only the model call, including SDK retries. Tokens come from the API's `usage`.
- **Timeouts and retries.** Each attempt times out after 8 seconds, with `max_retries=2`, so the worst case stays close to the .NET API's 30-second timeout.

Provider errors, as opposed to model errors, return a short message without the API key:

| Situation | Status |
| --- | --- |
| Rate limit, timeout, connection error | 503 |
| Provider not configured (`LLM_BASE_URL`, `LLM_API_KEY` or model missing) | 503 |
| Authentication error, any other API error | 502 |

## Install

Requires [uv](https://docs.astral.sh/uv/). From `src/classifier-service/`:

```bash
uv sync
```

## Run

```bash
uv run uvicorn app.main:app --reload --port 8000
```

For the `openai_compatible` provider, export `LLM_BASE_URL`, `LLM_API_KEY` and `LLM_MODEL` first. Then:

```bash
curl http://localhost:8000/health

curl -X POST http://localhost:8000/classify \
  -H "Content-Type: application/json" \
  -d '{"text": "I was charged twice", "config": {"provider": "mock"}}'

curl -X POST http://localhost:8000/classify \
  -H "Content-Type: application/json" \
  -d '{"text": "Me cobraron dos veces", "config": {"provider": "openai_compatible", "prompt_version": "v2"}}'
```

Interactive API docs are available at `http://localhost:8000/docs`.

## Docker

```bash
docker build -t classifier-service:dev .
docker run --rm -p 8000:8000 --env-file ../../.env classifier-service:dev
```

The image is based on `python:3.12-slim`. It installs dependencies with uv from `uv.lock`, without dev dependencies, and runs uvicorn on port 8000 as a non-root user. In `docker compose`, the service reads the repository's `.env` (`env_file`), and its port is not exposed to the host.

## Test

```bash
uv run pytest
```

The tests never call the network: the OpenAI client is replaced by a fake. `tests/test_llm_integration.py` calls the real provider and is skipped unless `LLM_API_KEY` is set. To run it, export `LLM_BASE_URL`, `LLM_API_KEY` and `LLM_MODEL`.

## API

- `GET /health` returns `{"status": "ok"}`.
- `POST /classify` takes `text` (1-2000 characters after trimming) and `config` (`provider`, `model`, `prompt_version`, `temperature` from 0.0 to 2.0). It returns `category`, `valid`, `raw_output`, `latency_ms`, `input_tokens` and `output_tokens`.
- If the classifier produces something that is not a valid category, the response has `category: "invalid"` and `valid: false`. These results are always returned, never dropped.
- Invalid requests return `422`. Provider failures return `502` or `503` (see above).
