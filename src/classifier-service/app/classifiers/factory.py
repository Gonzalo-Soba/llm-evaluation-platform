from app.classifiers.base import Classifier
from app.classifiers.mock import MockClassifier
from app.classifiers.openai_compatible import OpenAICompatibleClassifier, create_client_from_env


def get_classifier(provider: str) -> Classifier:
    if provider == "mock":
        return MockClassifier()
    if provider == "openai_compatible":
        client, default_model = create_client_from_env()
        return OpenAICompatibleClassifier(client, default_model)
    raise ValueError(f"Unsupported provider: {provider}")
