import time

from app.models import Category, ClassificationResult, ClassifierConfig

# Checked in order; the first category with a matching keyword wins.
KEYWORDS: list[tuple[Category, tuple[str, ...]]] = [
    (
        Category.TECHNICAL_SUPPORT,
        ("error", "crash", "bug", "not working", "doesn't work", "no funciona", "falla"),
    ),
    (
        Category.BILLING,
        ("cobro", "charge", "invoice", "factura", "refund", "reembolso", "payment", "pago"),
    ),
    (
        Category.ACCOUNT_MANAGEMENT,
        ("password", "contraseña", "cuenta", "account", "login", "usuario", "username"),
    ),
]


class MockClassifier:
    """Deterministic keyword-based classifier. Case-insensitive, no external calls."""

    def classify(self, text: str, config: ClassifierConfig) -> ClassificationResult:
        start = time.perf_counter()
        category = self._match(text)
        latency_ms = round((time.perf_counter() - start) * 1000)

        return ClassificationResult(
            category=category,
            valid=True,
            raw_output=category.value,
            latency_ms=latency_ms,
        )

    @staticmethod
    def _match(text: str) -> Category:
        normalized = text.casefold()
        for category, keywords in KEYWORDS:
            if any(keyword in normalized for keyword in keywords):
                return category
        return Category.GENERAL_INQUIRY
