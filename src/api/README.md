# EvalLab API

ASP.NET Core Web API (.NET 10) that orchestrates the platform. It currently exposes a classification endpoint that forwards requests to the [classifier service](../classifier-service/README.md).

## Projects

- `EvalLab.Api/`: the Web API, organized by feature folders (`Classification/`, `Health/`).
- `EvalLab.Api.Tests/`: xUnit tests. They use fakes and never call the real classifier service.

## Configuration

| Key | Default | Description |
| --- | --- | --- |
| `ClassifierService:BaseUrl` | `http://localhost:8000` | Base URL of the classifier service. Overridden by the `CLASSIFIER_SERVICE_URL` environment variable. |
| `ClassifierService:TimeoutSeconds` | `30` | Timeout for calls to the classifier service. |

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

## Docker

The image is a multi-stage build: the SDK image publishes the API, and the ASP.NET runtime image runs it as a non-root user on port 8080. To run it together with the classifier service, use `docker compose up --build` from the repository root (see the root README).

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
