# EvalLab API

ASP.NET Core Web API (.NET 10) that orchestrates the platform. It forwards classification requests to the [classifier service](../classifier-service/README.md) and runs evaluations of a classifier configuration over a labeled dataset.

## Projects

- `EvalLab.Api/`: the Web API, organized by feature folders (`Classification/`, `Datasets/`, `Evaluation/`, `Health/`).
- `EvalLab.Api.Tests/`: xUnit tests. They use fakes and never call the real classifier service.

## Configuration

| Key | Default | Description |
| --- | --- | --- |
| `ClassifierService:BaseUrl` | `http://localhost:8000` | Base URL of the classifier service. Overridden by the `CLASSIFIER_SERVICE_URL` environment variable. |
| `ClassifierService:TimeoutSeconds` | `30` | Timeout for calls to the classifier service. |
| `Datasets:Directory` | `../../../data` | Directory with the `<name>.jsonl` dataset files. |
| `Runs:Directory` | `../../../runs` | Directory where evaluation runs are stored as `<run_id>.json`. |
| `Evaluation:MaxParallelism` | `4` | Maximum concurrent classifier calls within one run. |

Relative directories are resolved against the project directory (the content root), not the current working directory. In environment variables, use `__` as the separator, for example `Datasets__Directory`.

## Build

From `src/api/`:

```bash
dotnet build EvalLab.sln
```

## Run

Start the classifier service first (see its README), then:

```bash
dotnet run --project EvalLab.Api
```

The API listens on `http://localhost:5146`. Example requests are in `EvalLab.Api/EvalLab.Api.http`.

```bash
curl http://localhost:5146/health

curl -X POST http://localhost:5146/api/classify \
  -H "Content-Type: application/json" \
  -d '{"text": "I was charged twice"}'
```

`config` is optional. If it is missing, `{"provider": "mock"}` is sent to the classifier service.

## Evaluation runs

| Endpoint | Description |
| --- | --- |
| `POST /api/runs` | Queues a run. Body: `{"dataset": "support-messages-v0", "split": "test", "config": {...}}`. `split` and `config` are optional. Returns `202` with `{"run_id", "status": "queued"}` and a `Location` header. |
| `GET /api/runs/{id}` | The full run: status, timestamps, dataset SHA-256, every prediction and the metrics. `404` if it does not exist. |
| `GET /api/runs` | Summaries of all runs, newest first: id, status, dataset, split, created_at, accuracy, macro F1 and error count. |

```bash
curl -i -X POST http://localhost:5146/api/runs \
  -H "Content-Type: application/json" \
  -d '{"dataset": "support-messages-v0", "split": "test"}'

curl http://localhost:5146/api/runs/<run_id>
```

The dataset name must match `^[a-z0-9-]+$` and refer to an existing file in `Datasets:Directory`. An invalid or unknown dataset, or an unknown split, returns `400`.

Runs are processed in the background, one at a time. A run moves from `queued` to `running` and ends as `completed`, `completed_with_errors` (some classifier calls failed; metrics are computed over the rest) or `failed` (the dataset could not be loaded or every call failed). The run file is saved after each status change.

> **Limitation:** the queue is in memory. Runs that are `queued` or `running` when the API stops are not resumed after a restart: their files stay in that status indefinitely. Completed runs are not affected.

## Docker

The image is a multi-stage build: the SDK image publishes the API, and the ASP.NET runtime image runs it as a non-root user on port 8080. To run it together with the classifier service, use `docker compose up --build` from the repository root (see the root README). Compose mounts `./data` read-only at `/data` and `./runs` read-write at `/runs`.

The container runs as a non-root user (UID 1654). On Linux hosts, `./runs` must be writable by that user, for example `mkdir -p runs && sudo chown 1654 runs`. Docker Desktop on Windows and macOS does not need this.

## Test

```bash
dotnet test EvalLab.sln
```

## Error responses

Errors are returned as ProblemDetails (`application/problem+json`):

| Situation | Status |
| --- | --- |
| `text` missing, empty or whitespace | 400 |
| Classifier service rejects the request (422) | 400, with the downstream errors in `errors` |
| Classifier service unreachable (connection refused, DNS) | 503 |
| Classifier service timeout | 504 |
| Any other non-success status or a malformed response | 502 |
