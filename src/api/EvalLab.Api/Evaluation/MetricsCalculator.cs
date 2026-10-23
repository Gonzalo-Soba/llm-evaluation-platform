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
    /// <exception cref="ArgumentNullException"><paramref name="pairs"/> or <paramref name="labels"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="pairs"/> is empty;
    /// <paramref name="labels"/> contains a duplicated label or <see cref="InvalidLabel"/>;
    /// an expected label is not in <paramref name="labels"/>;
    /// or a predicted label is neither in <paramref name="labels"/> nor <see cref="InvalidLabel"/>.
    /// </exception>
    public static EvaluationMetrics Calculate(
        IReadOnlyList<(string Expected, string Predicted)> pairs,
        IReadOnlyList<string> labels)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        ArgumentNullException.ThrowIfNull(labels);

        if (pairs.Count == 0)
        {
            throw new ArgumentException("At least one prediction is required.", nameof(pairs));
        }

        var n = labels.Count;
        var labelSet = new HashSet<string>(labels);

        // A duplicated label would map two rows to the same column and silently corrupt the metrics.
        if (labelSet.Count != n)
        {
            throw new ArgumentException("Labels must be unique.", nameof(labels));
        }

        // "invalid" is reserved for the extra confusion matrix column; it can never be a class.
        if (labelSet.Contains(InvalidLabel))
        {
            throw new ArgumentException($"Labels must not contain '{InvalidLabel}'.", nameof(labels));
        }

        for (var i = 0; i < pairs.Count; i++)
        {
            var (expected, predicted) = pairs[i];

            if (!labelSet.Contains(expected))
            {
                throw new ArgumentException(
                    $"Pair {i}: expected label '{expected}' is not a known label.", nameof(pairs));
            }

            if (!labelSet.Contains(predicted) && predicted != InvalidLabel)
            {
                throw new ArgumentException(
                    $"Pair {i}: predicted label '{predicted}' is neither a known label nor '{InvalidLabel}'.", nameof(pairs));
            }
        }

        var columns = labels.Append(InvalidLabel).ToList();

        var columnIndex = new Dictionary<string, int>();
        for (var c = 0; c < columns.Count; c++)
        {
            columnIndex[columns[c]] = c;
        }

        // Rows are expected labels, so they share the first n column indexes.
        var counts = new int[n, columns.Count];

        foreach (var (expected, predicted) in pairs)
        {
            var row = columnIndex[expected];
            var col = columnIndex[predicted];
            counts[row, col]++;
        }

        // "invalid" predictions are off the diagonal, so they count as wrong.
        var correct = 0;
        for (var i = 0; i < n; i++)
        {
            correct += counts[i, i];
        }

        var accuracy = (double)correct / pairs.Count;

        var precision = new double[n];
        var recall = new double[n];
        var f1 = new double[n];
        var support = new int[n];

        for (var i = 0; i < n; i++)
        {
            var tp = counts[i, i];

            var predictedAsI = 0;
            for (var r = 0; r < n; r++)
            {
                predictedAsI += counts[r, i];
            }

            // Includes the "invalid" column: an invalid prediction is a false negative for the expected class.
            var actuallyI = 0;
            for (var c = 0; c < columns.Count; c++)
            {
                actuallyI += counts[i, c];
            }

            // Undefined ratios are 0, matching scikit-learn's zero_division=0.
            support[i] = actuallyI;
            precision[i] = predictedAsI == 0 ? 0 : (double)tp / predictedAsI;
            recall[i] = actuallyI == 0 ? 0 : (double)tp / actuallyI;
            f1[i] = precision[i] + recall[i] == 0
                ? 0
                : 2 * precision[i] * recall[i] / (precision[i] + recall[i]);
        }

        // Labels with support 0 are included, as scikit-learn does when the label set is given explicitly.
        var macroPrecision = precision.Average();
        var macroRecall = recall.Average();
        var macroF1 = f1.Average();

        double weightedPrecision = 0, weightedRecall = 0, weightedF1 = 0;
        for (var i = 0; i < n; i++)
        {
            weightedPrecision += precision[i] * support[i];
            weightedRecall += recall[i] * support[i];
            weightedF1 += f1[i] * support[i];
        }

        var totalSupport = support.Sum();
        weightedPrecision /= totalSupport;
        weightedRecall /= totalSupport;
        weightedF1 /= totalSupport;

        var invalidColumn = columns.Count - 1;
        var invalidCount = 0;
        for (var r = 0; r < n; r++)
        {
            invalidCount += counts[r, invalidColumn];
        }

        var perClass = labels
            .Select((label, i) => new ClassMetrics(label, precision[i], recall[i], f1[i], support[i]))
            .ToList();

        var matrixRows = Enumerable.Range(0, n)
            .Select(r => (IReadOnlyList<int>)Enumerable.Range(0, columns.Count).Select(c => counts[r, c]).ToList())
            .ToList();

        return new EvaluationMetrics(
            accuracy,
            invalidCount,
            perClass,
            macroPrecision,
            macroRecall,
            macroF1,
            weightedPrecision,
            weightedRecall,
            weightedF1,
            new ConfusionMatrix(labels.ToList(), columns, matrixRows));
    }
}
