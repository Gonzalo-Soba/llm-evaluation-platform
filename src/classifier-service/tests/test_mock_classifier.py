import pytest

from app.classifiers.mock import MockClassifier
from app.models import Category, ClassifierConfig

CONFIG = ClassifierConfig(provider="mock")


@pytest.fixture
def classifier() -> MockClassifier:
    return MockClassifier()


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("The app shows an error when I open it", Category.TECHNICAL_SUPPORT),
        ("La aplicación no funciona desde ayer", Category.TECHNICAL_SUPPORT),
        ("I was charged twice on my invoice", Category.BILLING),
        ("Tengo un cobro duplicado", Category.BILLING),
        ("I forgot my password", Category.ACCOUNT_MANAGEMENT),
        ("Quiero cambiar la contraseña de mi cuenta", Category.ACCOUNT_MANAGEMENT),
        ("What are your opening hours?", Category.GENERAL_INQUIRY),
    ],
)
def test_classifies_each_category(classifier: MockClassifier, text: str, expected: Category) -> None:
    result = classifier.classify(text, CONFIG)

    assert result.category == expected
    assert result.valid is True
    assert result.raw_output == expected.value
    assert result.input_tokens is None
    assert result.output_tokens is None


@pytest.mark.parametrize("text", ["REFUND please", "Refund please", "refund please"])
def test_is_case_insensitive(classifier: MockClassifier, text: str) -> None:
    assert classifier.classify(text, CONFIG).category == Category.BILLING


def test_falls_back_to_general_inquiry(classifier: MockClassifier) -> None:
    result = classifier.classify("Hola, ¿dónde están ubicados?", CONFIG)

    assert result.category == Category.GENERAL_INQUIRY
    assert result.valid is True


def test_is_deterministic(classifier: MockClassifier) -> None:
    text = "My account login keeps failing"

    results = {classifier.classify(text, CONFIG).category for _ in range(20)}

    assert results == {Category.ACCOUNT_MANAGEMENT}
