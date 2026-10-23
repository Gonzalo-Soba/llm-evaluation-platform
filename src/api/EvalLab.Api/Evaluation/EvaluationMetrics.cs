namespace EvalLab.Api.Evaluation;

/// <summary>Metrics for one evaluation, computed over a fixed set of labels.</summary>
/// <param name="Accuracy">Fraction of correct predictions. "invalid" predictions count as wrong.</param>
/// <param name="InvalidCount">Number of predictions equal to <see cref="MetricsCalculator.InvalidLabel"/>.</param>
/// <param name="PerClass">One entry per label, in the order of the label set.</param>
/// <param name="MacroPrecision">Unweighted mean over all labels, including labels with support 0.</param>
/// <param name="WeightedPrecision">Mean over all labels weighted by support.</param>
public sealed record EvaluationMetrics(
    double Accuracy,
    int InvalidCount,
    IReadOnlyList<ClassMetrics> PerClass,
    double MacroPrecision,
    double MacroRecall,
    double MacroF1,
    double WeightedPrecision,
    double WeightedRecall,
    double WeightedF1,
    ConfusionMatrix ConfusionMatrix);

/// <summary>Precision, recall and F1 for one label. Undefined values (division by zero) are 0.</summary>
/// <param name="Support">Number of examples whose expected label is <paramref name="Label"/>.</param>
public sealed record ClassMetrics(
    string Label,
    double Precision,
    double Recall,
    double F1,
    int Support);

/// <summary>Counts of (expected, predicted) pairs.</summary>
/// <param name="Rows">Expected labels: the label set, in order.</param>
/// <param name="Columns">Predicted labels: the label set, in order, followed by "invalid".</param>
/// <param name="Counts">Counts[row][column]: examples with expected Rows[row] and predicted Columns[column].</param>
public sealed record ConfusionMatrix(
    IReadOnlyList<string> Rows,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<int>> Counts);
