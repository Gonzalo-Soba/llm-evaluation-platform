from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

from app.classifiers.factory import get_classifier
from app.errors import ProviderError
from app.models import ClassificationResult, ClassifyRequest

app = FastAPI(title="Classifier Service")


@app.exception_handler(ProviderError)
async def provider_error_handler(_: Request, error: ProviderError) -> JSONResponse:
    return JSONResponse(status_code=error.status_code, content={"detail": str(error)})


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/classify")
def classify(request: ClassifyRequest) -> ClassificationResult:
    classifier = get_classifier(request.config.provider)
    return classifier.classify(request.text, request.config)
