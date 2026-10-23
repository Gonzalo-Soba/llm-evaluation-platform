using System.ComponentModel.DataAnnotations;
using EvalLab.Api.Classification;

namespace EvalLab.Api.Evaluation;

/// <param name="Dataset">Dataset name, e.g. "support-messages-v0" (resolved inside Datasets:Directory).</param>
/// <param name="Split">"dev", "test", or null for the whole dataset.</param>
/// <param name="Config">Classifier config; defaults to the mock provider when missing.</param>
public sealed record CreateRunRequest(
    [Required] string Dataset,
    string? Split = null,
    ClassifierConfig? Config = null);

public sealed record CreateRunResponse(Guid RunId, RunStatus Status);

public sealed record RunSummary(
    Guid RunId,
    RunStatus Status,
    string Dataset,
    string? Split,
    DateTimeOffset CreatedAt,
    double? Accuracy,
    double? MacroF1,
    int ErrorCount)
{
    public static RunSummary From(EvaluationRun run) =>
        new(
            run.RunId,
            run.Status,
            run.DatasetName,
            run.Split,
            run.CreatedAt,
            run.Metrics?.Accuracy,
            run.Metrics?.MacroF1,
            run.ErrorCount);
}
