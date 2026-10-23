# /// script
# requires-python = ">=3.12"
# dependencies = []
# ///
"""Summarizes evaluation runs stored in docs/results/*.json as markdown.

Prints one row per run: config, split, accuracy, macro F1, invalid and error counts, total tokens,
mean end-to-end latency, and accuracy on clear vs ambiguous examples (difficulty is joined from the
dataset file by example id).

Run from the repository root:
    uv run scripts/summarize_runs.py
    uv run scripts/summarize_runs.py --details docs/results/test-v1-gpt-oss-120b.json
"""

import argparse
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RESULTS_DIR = ROOT / "docs" / "results"
DATA_DIR = ROOT / "data"


def load_difficulties(run: dict) -> dict[str, str]:
    path = DATA_DIR / f"{run['dataset_name']}.jsonl"
    if hashlib.sha256(path.read_bytes()).hexdigest() != run["dataset_sha256"]:
        print(f"warning: {path.name} changed since run {run['run_id']}; difficulty may not match", file=sys.stderr)
    examples = (json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip())
    return {example["id"]: example["difficulty"] for example in examples}


def config_label(run: dict) -> str:
    config = run["classifier_config"]
    if config["provider"] == "mock":
        return "mock"
    model = (config.get("model") or "default model").split("/")[-1]
    return f"{model}, prompt {config.get('prompt_version') or 'v1'}"


def accuracy_for(predictions: list[dict], difficulties: dict[str, str], difficulty: str) -> str:
    # Same convention as the metrics: Error predictions are excluded, Invalid ones count as wrong.
    scored = [p for p in predictions if p["status"] != "error" and difficulties.get(p["example_id"]) == difficulty]
    if not scored:
        return "–"
    correct = sum(p["predicted"] == p["expected"] for p in scored)
    return f"{correct / len(scored):.3f} ({correct}/{len(scored)})"


def summary_row(run: dict) -> list[str]:
    metrics = run["metrics"] or {}
    predictions = run["predictions"]
    difficulties = load_difficulties(run)
    tokens = [run["total_input_tokens"], run["total_output_tokens"]]
    latencies = [p["end_to_end_latency_ms"] for p in predictions]

    return [
        config_label(run),
        run["split"] or "all",
        f"{metrics['accuracy']:.3f}" if metrics else "–",
        f"{metrics['macro_f1']:.3f}" if metrics else "–",
        str(metrics.get("invalid_count", "–")),
        str(run["error_count"]),
        "–" if all(t is None for t in tokens) else f"{sum(t or 0 for t in tokens):,}",
        f"{sum(latencies) / len(latencies):.0f}" if latencies else "–",
        accuracy_for(predictions, difficulties, "clear"),
        accuracy_for(predictions, difficulties, "ambiguous"),
    ]


def table(header: list[str], rows: list[list[str]]) -> str:
    lines = ["| " + " | ".join(header) + " |", "| " + " | ".join("---" for _ in header) + " |"]
    lines += ["| " + " | ".join(row) + " |" for row in rows]
    return "\n".join(lines)


def print_summary(paths: list[Path]) -> None:
    runs = [json.loads(path.read_text(encoding="utf-8")) for path in paths]
    runs.sort(key=lambda run: (run["split"] or "", config_label(run)))
    header = [
        "Config", "Split", "Accuracy", "Macro F1", "Invalid", "Errors",
        "Total tokens", "Mean end-to-end latency (ms)", "Accuracy (clear)", "Accuracy (ambiguous)",
    ]
    print(table(header, [summary_row(run) for run in runs]))


def print_details(path: Path) -> None:
    run = json.loads(path.read_text(encoding="utf-8"))
    metrics = run["metrics"]
    difficulties = load_difficulties(run)
    print(f"Run `{path.name}`: {config_label(run)} on {run['split']}\n")

    print(table(
        ["Class", "Precision", "Recall", "F1", "Support"],
        [[c["label"], f"{c['precision']:.3f}", f"{c['recall']:.3f}", f"{c['f1']:.3f}", str(c["support"])]
         for c in metrics["per_class"]],
    ))
    print()

    matrix = metrics["confusion_matrix"]
    print(table(
        ["Expected \\ Predicted", *matrix["columns"]],
        [[row, *map(str, counts)] for row, counts in zip(matrix["rows"], matrix["counts"])],
    ))
    print()

    wrong = [p for p in run["predictions"] if p["status"] != "error" and p["predicted"] != p["expected"]]
    print(table(
        ["Id", "Difficulty", "Expected", "Predicted"],
        [[p["example_id"], difficulties.get(p["example_id"], "?"), p["expected"], p["predicted"]] for p in wrong],
    ) if wrong else "No misclassified examples.")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--details", type=Path, help="print per-class metrics, confusion matrix and errors for one run")
    args = parser.parse_args()

    if args.details:
        print_details(args.details)
    else:
        print_summary(sorted(RESULTS_DIR.glob("*.json")))


if __name__ == "__main__":
    main()
