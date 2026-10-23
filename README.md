# AI Evaluation Lab

A modular platform for running AI modules over labeled datasets and evaluating their results systematically, built with .NET and Python.

## Overview

The platform runs an AI module against a labeled dataset, collects its predictions, and computes evaluation metrics so different modules, models, or prompts can be compared on the same data.

The first module is an LLM-based classifier for customer support messages, which assigns each message one of these categories:

- `technical_support`
- `billing`
- `general_inquiry`
- `account_management`

## Architecture

| Component | Stack | Responsibility |
| --- | --- | --- |
| `src/api` | ASP.NET Core Web API (.NET 10) | Orchestration, datasets, evaluation runs, metrics |
| `src/classifier-service` | Python 3.12 + FastAPI | Classifies text only. Provides a deterministic mock classifier and an LLM classifier |
| `data/` | — | Labeled datasets |

- Metrics are computed in .NET, not in Python: the module under test does not evaluate itself.
- Invalid LLM outputs are counted as errors, never silently dropped.
- No database in the MVP: evaluation runs are stored as JSON files.

See [docs/decisions/](docs/decisions/) for architecture decision records.

## Getting started

### Run with Docker Compose

Requires Docker with Compose v2. From the repository root:

```bash
docker compose up --build
```

This starts both services:

- `api` on `http://localhost:8080`
- `classifier-service`, reachable only from the API inside the compose network

The API waits until the classifier service passes its `/health` check before starting.

```bash
curl http://localhost:8080/health

curl -X POST http://localhost:8080/api/classify \
  -H "Content-Type: application/json" \
  -d '{"text": "I was charged twice"}'
```

Stop everything with `docker compose down`.

### Run locally

To run each service without Docker, see [src/classifier-service/README.md](src/classifier-service/README.md) and [src/api/README.md](src/api/README.md). Configuration variables are listed in [.env.example](.env.example).

## Evaluation

TODO: how to run an evaluation and which metrics are reported.

## Project status

TODO: early development. The repository skeleton is in place; no services are implemented yet.

## Roadmap

TODO
