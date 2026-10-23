from app.classifiers.base import Classifier
from app.classifiers.mock import MockClassifier


def get_classifier(provider: str) -> Classifier:
    if provider == "mock":
        return MockClassifier()
    raise ValueError(f"Unsupported provider: {provider}")
