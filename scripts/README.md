# Scripts

Development scripts. They are self-contained [uv scripts](https://docs.astral.sh/uv/guides/scripts/): dependencies are declared inline (PEP 723), and uv installs them in an isolated environment on each run. No virtual environment or manual install is needed.

## generate_metrics_fixtures.py

Generates the reference values for the .NET `MetricsCalculator` tests, using scikit-learn as the reference implementation.

From the repository root:

```bash
uv run scripts/generate_metrics_fixtures.py
```

The script writes `src/api/EvalLab.Api.Tests/Evaluation/Fixtures/metrics-cases.json`. Commit the regenerated file together with any change to the script, then run the .NET tests:

```bash
cd src/api
dotnet test EvalLab.sln
```

Conventions encoded in the fixture (the calculator must follow them):

- Metrics are computed over the 4 categories only (`labels=[the 4 categories]`, `zero_division=0`).
- `invalid` predictions count as wrong for accuracy and as false negatives for the expected class. They never form a class of their own.
- Macro averages include every label, even labels with support 0. Weighted averages weight each label by its support.
- Confusion matrix rows are the expected labels (the 4 categories). Columns are the 4 categories followed by `invalid`.

To add a case, append it to `CASES` in the script and regenerate. The .NET tests pick up every case in the file automatically.
