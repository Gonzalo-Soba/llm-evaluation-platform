namespace EvalLab.Api.Classification;

public sealed class ClassifierServiceOptions
{
    public const string SectionName = "ClassifierService";

    /// <summary>Environment variable that overrides <see cref="BaseUrl"/>.</summary>
    public const string BaseUrlEnvironmentVariable = "CLASSIFIER_SERVICE_URL";

    public string BaseUrl { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;
}
