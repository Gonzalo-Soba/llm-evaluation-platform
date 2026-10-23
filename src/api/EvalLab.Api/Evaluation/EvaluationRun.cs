using EvalLab.Api.Classification;

namespace EvalLab.Api.Evaluation;

public enum RunStatus
{
    Queued,
    Running,

    /// <summary>Every prediction is Ok or Invalid.</summary>
    Completed,

    /// <summary>Some predictions are Error; metrics are computed over the rest.</summary>
    CompletedWithErrors,

    /// <summary>The dataset could not be loaded or every prediction is Error. No metrics.</summary>
    Failed,
}

/// <summary>One evaluation of a classifier configuration over a dataset (or one of its splits).</summary>
/// <param name="DatasetSha256">Hex SHA-256 of the dataset file, to detect if the dataset changed between runs.</param>
/// <param name="Split">The split evaluated, or null for the whole dataset.</param>
/// <param name="Predictions">One record per example, in dataset order.</param>
/// <param name="Metrics">Computed over Ok and Invalid predictions. Null until the run completes, and for failed runs.</param>
/// <param name="TotalInputTokens">Sum over predictions that reported tokens; null if none did.</param>
public sealed record EvaluationRun(
    Guid RunId,
    RunStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string DatasetName,
    string? DatasetSha256,
    string? Split,
    ClassifierConfig ClassifierConfig,
    IReadOnlyList<PredictionRecord> Predictions,
    EvaluationMetrics? Metrics,
    int ErrorCount,
    int? TotalInputTokens,
    int? TotalOutputTokens,
    string? FailureReason)
{
    public static EvaluationRun CreateQueued(
        string datasetName, string? split, ClassifierConfig classifierConfig, DateTimeOffset createdAt) =>
        new(
            RunId: Guid.NewGuid(),
            Status: RunStatus.Queued,
            CreatedAt: createdAt,
            StartedAt: null,
            CompletedAt: null,
            DatasetName: datasetName,
            DatasetSha256: null,
            Split: split,
            ClassifierConfig: classifierConfig,
            Predictions: [],
            Metrics: null,
            ErrorCount: 0,
            TotalInputTokens: null,
            TotalOutputTokens: null,
            FailureReason: null);
}
