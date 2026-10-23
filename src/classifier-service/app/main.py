from fastapi import FastAPI

from app.classifiers.factory import get_classifier
from app.models import ClassificationResult, ClassifyRequest

app = FastAPI(title="Classifier Service")


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/classify")
def classify(request: ClassifyRequest) -> ClassificationResult:
    classifier = get_classifier(request.config.provider)
    return classifier.classify(request.text, request.config)
