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
        // 1. Tiene que haber al menos un par
        if (pairs.Count == 0)
            throw new ArgumentException("At least one prediction is required.", nameof(pairs));

        // Un HashSet permite preguntar "¿esta etiqueta existe?" de forma rápida
        var labelSet = new HashSet<string>(labels);

        for (var i = 0; i < pairs.Count; i++)
        {
            // Separa el par en dos variables
            var (expected, predicted) = pairs[i];

            // 2. La etiqueta correcta tiene que ser una categoría
            if (!labelSet.Contains(expected))
                throw new ArgumentException(
                    $"Pair {i}: expected label '{expected}' is not a known label.", nameof(pairs));

            // 3. La predicción tiene que ser una categoría o "invalid"
            if (!labelSet.Contains(predicted) && predicted != InvalidLabel)
                throw new ArgumentException(
                    $"Pair {i}: predicted label '{predicted}' is neither a known label nor '{InvalidLabel}'.", nameof(pairs));
        }

        // Las columnas son las categorías más "invalid", en ese orden
        var columns = labels.Append(InvalidLabel).ToList();

        // Diccionario para saber en qué posición está cada etiqueta
        var index = new Dictionary<string, int>();
        for (var c = 0; c < columns.Count; c++)
            index[columns[c]] = c;

        // La matriz: una fila por categoría, una columna por categoría + "invalid"
        var counts = new int[labels.Count, columns.Count];

        // Por cada par, sumamos 1 en la celda [esperado, predicho]
        foreach (var (expected, predicted) in pairs)
        {
            var row = index[expected];
            var col = index[predicted];
            counts[row, col]++;
        }

        // Punto 3: accuracy = aciertos (la diagonal) / total
        var correct = 0;
        for (var i = 0; i < labels.Count; i++)
            correct += counts[i, i];

        var accuracy = (double)correct / pairs.Count;

        // Punto 4: métricas por clase, guardadas en arrays (una posición por categoría)
        var n = labels.Count;
        var precision = new double[n];
        var recall = new double[n];
        var f1 = new double[n];
        var support = new int[n];

        for (var i = 0; i < n; i++)
        {
            var tp = counts[i, i];

            // Cuántos se predijeron como clase i: suma de la COLUMNA i
            var predictedAsI = 0;
            for (var r = 0; r < n; r++)
                predictedAsI += counts[r, i];

            // Cuántos eran realmente clase i: suma de la FILA i, incluida la columna "invalid"
            var actuallyI = 0;
            for (var c = 0; c < columns.Count; c++)
                actuallyI += counts[i, c];

            support[i] = actuallyI;
            precision[i] = predictedAsI == 0 ? 0 : (double)tp / predictedAsI;
            recall[i] = actuallyI == 0 ? 0 : (double)tp / actuallyI;
            f1[i] = precision[i] + recall[i] == 0
                ? 0
                : 2 * precision[i] * recall[i] / (precision[i] + recall[i]);
        }

        // Punto 5: macro = promedio simple de las clases (todas pesan igual)
        var macroPrecision = precision.Average();
        var macroRecall = recall.Average();
        var macroF1 = f1.Average();

        // Punto 6: weighted = promedio ponderado por support (las clases con más ejemplos pesan más)
        double weightedPrecision = 0, weightedRecall = 0, weightedF1 = 0;
        for (var i = 0; i < n; i++)
        {
            weightedPrecision += precision[i] * support[i];
            weightedRecall += recall[i] * support[i];
            weightedF1 += f1[i] * support[i];
        }
        weightedPrecision /= pairs.Count;
        weightedRecall /= pairs.Count;
        weightedF1 /= pairs.Count;

        // Punto 7: cuántas predicciones fueron "invalid" = suma de la última columna
        var invalidColumn = columns.Count - 1;
        var invalidCount = 0;
        for (var r = 0; r < n; r++)
            invalidCount += counts[r, invalidColumn];

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
