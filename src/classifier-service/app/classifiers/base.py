from typing import Protocol

from app.models import ClassificationResult, ClassifierConfig


class Classifier(Protocol):
    def classify(self, text: str, config: ClassifierConfig) -> ClassificationResult: ...
