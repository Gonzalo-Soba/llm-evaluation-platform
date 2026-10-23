namespace EvalLab.Api.Evaluation;

public enum PredictionStatus
{
    /// <summary>The classifier returned a valid category.</summary>
    Ok,

    /// <summary>The classifier responded, but its output was not a valid category. Counted as wrong in the metrics.</summary>
    Invalid,

    /// <summary>The classifier call failed. Excluded from the metrics and counted separately.</summary>
    Error,
}

/// <summary>The outcome of classifying one dataset example.</summary>
/// <param name="Predicted">The predicted category, "invalid" for <see cref="PredictionStatus.Invalid"/>, or null for <see cref="PredictionStatus.Error"/>.</param>
/// <param name="ClassifierLatencyMs">Latency reported by the classifier service. Null when the call failed.</param>
/// <param name="EndToEndLatencyMs">Time measured in the API around the classifier call, including the network.</param>
public sealed record PredictionRecord(
    string ExampleId,
    string Text,
    string Expected,
    string? Predicted,
    PredictionStatus Status,
    string? RawOutput,
    int? ClassifierLatencyMs,
    long EndToEndLatencyMs,
    int? InputTokens,
    int? OutputTokens,
    string? ErrorMessage);
