# /// script
# requires-python = ">=3.12"
# dependencies = [
#     "scikit-learn>=1.5",
# ]
# ///
"""Generates the reference values used to test the .NET MetricsCalculator.

scikit-learn is the reference implementation. The output is written to
src/api/EvalLab.Api.Tests/Evaluation/Fixtures/metrics-cases.json.

Conventions (must match the .NET calculator):
- Metrics are computed over the 4 categories only (labels=LABELS, zero_division=0).
- "invalid" predictions count as wrong for accuracy and as false negatives for the
  expected class, but are never a class of their own.
- Macro averages include every label, even those with support 0.
- Confusion matrix rows are the expected labels (the 4 categories); columns are the
  4 categories followed by "invalid".

Run from the repository root:
    uv run scripts/generate_metrics_fixtures.py
"""

import json
from pathlib import Path

import sklearn
from sklearn.metrics import accuracy_score, confusion_matrix, precision_recall_fscore_support

T, B, G, A = "technical_support", "billing", "general_inquiry", "account_management"
INVALID = "invalid"
LABELS = [T, B, G, A]
MATRIX_COLUMNS = [*LABELS, INVALID]

OUTPUT = Path(__file__).resolve().parents[1] / "src/api/EvalLab.Api.Tests/Evaluation/Fixtures/metrics-cases.json"

# (name, description, [(expected, predicted), ...])
CASES = [
    (
        "perfect_predictions",
        "Every prediction matches the expected label.",
        [(T, T), (T, T), (B, B), (B, B), (G, G), (G, G), (A, A), (A, A)],
    ),
    (
        "all_wrong",
        "Every prediction is a valid category, but never the expected one.",
        [(T, B), (T, G), (B, G), (B, A), (G, A), (G, T), (A, T), (A, B)],
    ),
    (
        "imbalanced_classes",
        "Billing dominates the expected labels; the other classes have few examples.",
        [
            (B, B), (B, B), (B, B), (B, B), (B, B), (B, B), (B, B), (B, T), (B, A), (B, G),
            (T, T), (T, T), (T, B),
            (A, A), (A, B),
            (G, G),
        ],
    ),
    (
        "class_never_predicted",
        "general_inquiry appears in the expected labels but is never predicted.",
        [(T, T), (T, T), (B, B), (B, T), (G, B), (G, A), (G, T), (A, A), (A, A)],
    ),
    (
        "class_absent_from_expected",
        "account_management is never expected but is predicted (support 0).",
        [(T, T), (T, A), (B, B), (B, B), (B, A), (G, G), (G, T)],
    ),
    (
        "with_invalid_predictions",
        "Some predictions are 'invalid'; they count as wrong and never as a class.",
        [(T, T), (T, INVALID), (B, B), (B, B), (B, INVALID), (G, G), (G, A), (A, A), (A, INVALID), (A, INVALID)],
    ),
]


def build_case(name: str, description: str, pairs: list[tuple[str, str]]) -> dict:
    expected = [e for e, _ in pairs]
    predicted = [p for _, p in pairs]

    precision, recall, f1, support = precision_recall_fscore_support(
        expected, predicted, labels=LABELS, average=None, zero_division=0
    )

    def averaged(average: str) -> dict:
        p, r, f, _ = precision_recall_fscore_support(
            expected, predicted, labels=LABELS, average=average, zero_division=0
        )
        return {"precision": float(p), "recall": float(r), "f1": float(f)}

    matrix = confusion_matrix(expected, predicted, labels=MATRIX_COLUMNS)[: len(LABELS)]

    return {
        "name": name,
        "description": description,
        "expected": expected,
        "predicted": predicted,
        "accuracy": float(accuracy_score(expected, predicted)),
        "invalid_count": predicted.count(INVALID),
        "per_class": [
            {
                "label": label,
                "precision": float(precision[i]),
                "recall": float(recall[i]),
                "f1": float(f1[i]),
                "support": int(support[i]),
            }
            for i, label in enumerate(LABELS)
        ],
        "macro": averaged("macro"),
        "weighted": averaged("weighted"),
        "confusion_matrix": {
            "rows": LABELS,
            "columns": MATRIX_COLUMNS,
            "counts": [[int(value) for value in row] for row in matrix],
        },
    }


def main() -> None:
    for name, _, pairs in CASES:
        assert all(e in LABELS for e, _ in pairs), f"{name}: expected labels must be categories"
        assert all(p in MATRIX_COLUMNS for _, p in pairs), f"{name}: unknown predicted label"

    fixture = {
        "generated_by": "scripts/generate_metrics_fixtures.py",
        "scikit_learn_version": sklearn.__version__,
        "labels": LABELS,
        "cases": [build_case(*case) for case in CASES],
    }

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(fixture, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Wrote {len(CASES)} cases to {OUTPUT}")


if __name__ == "__main__":
    main()
