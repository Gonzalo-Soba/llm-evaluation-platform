using System.ComponentModel.DataAnnotations;

namespace EvalLab.Api.Classification;

// Field names are serialized in snake_case to match the classifier service contract.
// Only Text is validated here; everything else is validated by the classifier service.

public sealed record ClassifierConfig(
    string? Provider,
    string? Model = null,
    string? PromptVersion = null,
    double? Temperature = null);

public sealed record ClassifyRequest(
    [Required] string Text,
    ClassifierConfig? Config = null);

public sealed record ClassificationResult(
    string Category,
    bool Valid,
    string RawOutput,
    int LatencyMs,
    int? InputTokens,
    int? OutputTokens);
