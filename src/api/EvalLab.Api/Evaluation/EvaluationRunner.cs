using System.Diagnostics;
using System.Security.Cryptography;
using EvalLab.Api.Classification;
using EvalLab.Api.Datasets;
using Microsoft.Extensions.Options;

namespace EvalLab.Api.Evaluation;

public sealed class EvaluationOptions
{
    public const string SectionName = "Evaluation";

    public int MaxParallelism { get; set; } = 4;
}

/// <summary>Runs a classifier configuration over a dataset and computes the metrics.</summary>
public sealed class EvaluationRunner(
    IClassifierClient classifierClient,
    DatasetResolver datasetResolver,
    IOptions<EvaluationOptions> options,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Executes a queued run and returns it completed or failed. Classifier failures never throw:
    /// they are recorded as Error predictions.
    /// </summary>
    /// <exception cref="ArgumentException">The dataset name or split is invalid.</exception>
    /// <exception cref="OperationCanceledException">The run was cancelled.</exception>
    public async Task<EvaluationRun> RunAsync(EvaluationRun run, CancellationToken cancellationToken)
    {
        var path = datasetResolver.ResolvePath(run.DatasetName);
        run = run with { Status = RunStatus.Running, StartedAt = timeProvider.GetUtcNow() };

        IReadOnlyList<LabeledExample> examples;
        try
        {
            run = run with { DatasetSha256 = await ComputeSha256Async(path, cancellationToken) };
            examples = await DatasetLoader.LoadAsync(path, run.Split, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DatasetValidationException)
        {
            return Fail(run, $"The dataset could not be loaded: {ex.Message}");
        }

        if (examples.Count == 0)
        {
            return Fail(run, "The dataset has no examples for the requested split.");
        }

        // Results are written by index, so they stay in dataset order whatever the completion order.
        var predictions = new PredictionRecord[examples.Count];
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.Value.MaxParallelism,
            CancellationToken = cancellationToken,
        };
        await Parallel.ForEachAsync(Enumerable.Range(0, examples.Count), parallelOptions, async (i, token) =>
            predictions[i] = await PredictAsync(examples[i], run.ClassifierConfig, token));

        return Complete(run, predictions);
    }

    private async Task<PredictionRecord> PredictAsync(LabeledExample example, ClassifierConfig config, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await classifierClient.ClassifyAsync(new ClassifyRequest(example.Text, config), cancellationToken);
            stopwatch.Stop();

            // A "valid" category outside the label set is treated as invalid too, so it can never break the metrics.
            var isValid = result.Valid && DatasetLoader.Labels.Contains(result.Category);

            return new PredictionRecord(
                example.Id,
                example.Text,
                example.Label,
                isValid ? result.Category : MetricsCalculator.InvalidLabel,
                isValid ? PredictionStatus.Ok : PredictionStatus.Invalid,
                result.RawOutput,
                result.LatencyMs,
                stopwatch.ElapsedMilliseconds,
                result.InputTokens,
                result.OutputTokens,
                ErrorMessage: null);
        }
        catch (ClassifierServiceException ex)
        {
            stopwatch.Stop();
            var message = ex is ClassifierValidationException validation
                ? $"{ex.Message} {validation.Errors.GetRawText()}"
                : ex.Message;

            return new PredictionRecord(
                example.Id,
                example.Text,
                example.Label,
                Predicted: null,
                PredictionStatus.Error,
                RawOutput: null,
                ClassifierLatencyMs: null,
                stopwatch.ElapsedMilliseconds,
                InputTokens: null,
                OutputTokens: null,
                message);
        }
    }

    private EvaluationRun Complete(EvaluationRun run, IReadOnlyList<PredictionRecord> predictions)
    {
        var errorCount = predictions.Count(p => p.Status == PredictionStatus.Error);
        run = run with
        {
            Predictions = predictions,
            ErrorCount = errorCount,
            TotalInputTokens = SumReported(predictions.Select(p => p.InputTokens)),
            TotalOutputTokens = SumReported(predictions.Select(p => p.OutputTokens)),
        };

        if (errorCount == predictions.Count)
        {
            return Fail(run, "Every prediction failed.");
        }

        var pairs = predictions
            .Where(p => p.Status != PredictionStatus.Error)
            .Select(p => (p.Expected, p.Predicted!))
            .ToList();

        return run with
        {
            Status = errorCount == 0 ? RunStatus.Completed : RunStatus.CompletedWithErrors,
            CompletedAt = timeProvider.GetUtcNow(),
            Metrics = MetricsCalculator.Calculate(pairs, DatasetLoader.Labels),
        };
    }

    private EvaluationRun Fail(EvaluationRun run, string reason) =>
        run with { Status = RunStatus.Failed, CompletedAt = timeProvider.GetUtcNow(), FailureReason = reason };

    private static int? SumReported(IEnumerable<int?> values)
    {
        var reported = values.Where(v => v.HasValue).ToList();
        return reported.Count == 0 ? null : reported.Sum();
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }
}
