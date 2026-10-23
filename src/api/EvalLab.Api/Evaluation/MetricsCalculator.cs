namespace EvalLab.Api.Evaluation;

/// <summary>
/// Computes classification metrics. Reference values come from scikit-learn
/// (see scripts/generate_metrics_fixtures.py): metrics are computed over <c>labels</c> only, with zero_division=0.
/// </summary>
public static class MetricsCalculator
{
    /// <summary>Predicted label used when the classifier output is not a valid category.</summary>
    public const string InvalidLabel = "invalid";

    /// <param name="pairs">Expected and predicted label for each example.</param>
    /// <param name="labels">The label set, in the order used for per-class metrics and the confusion matrix.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="pairs"/> is empty, or an expected label is not in <paramref name="labels"/>.
    /// </exception>
    public static EvaluationMetrics Calculate(
        IReadOnlyList<(string Expected, string Predicted)> pairs,
        IReadOnlyList<string> labels)
    {
        throw new NotImplementedException();
    }
}
