# Classifier service

Python 3.12 + FastAPI service that classifies customer support messages. It only classifies text: it has no knowledge of datasets, labels, or metrics.

Supported providers: `mock` (deterministic, keyword-based, no external calls).

## Install

Requires [uv](https://docs.astral.sh/uv/). From `src/classifier-service/`:

```bash
uv sync
```

## Run

```bash
uv run uvicorn app.main:app --reload --port 8000
```

Then:

```bash
curl http://localhost:8000/health

curl -X POST http://localhost:8000/classify \
  -H "Content-Type: application/json" \
  -d '{"text": "I was charged twice", "config": {"provider": "mock"}}'
```

Interactive API docs are available at `http://localhost:8000/docs`.

## Docker

```bash
docker build -t classifier-service:dev .
docker run --rm -p 8000:8000 classifier-service:dev
```

The image is based on `python:3.12-slim`. It installs dependencies with uv from `uv.lock`, without dev dependencies, and runs uvicorn on port 8000 as a non-root user.

## Test

```bash
uv run pytest
```

## API

- `GET /health` returns `{"status": "ok"}`.
- `POST /classify` takes `text` (1-2000 characters after trimming) and `config` (`provider`, `model`, `prompt_version`, `temperature` from 0.0 to 2.0). It returns `category`, `valid`, `raw_output`, `latency_ms`, `input_tokens` and `output_tokens`.
- If the classifier produces something that is not a valid category, the response has `category: "invalid"` and `valid: false`. These results are always returned, never dropped.
- Invalid requests return `422`.
