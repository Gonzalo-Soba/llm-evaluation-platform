namespace EvalLab.Api.Datasets;

public sealed record DatasetValidationError(int LineNumber, string Message)
{
    public override string ToString() => $"Line {LineNumber}: {Message}";
}

/// <summary>The dataset file has one or more invalid lines. <see cref="Errors"/> lists all of them.</summary>
public sealed class DatasetValidationException(string path, IReadOnlyList<DatasetValidationError> errors)
    : Exception(BuildMessage(path, errors))
{
    public IReadOnlyList<DatasetValidationError> Errors { get; } = errors;

    private static string BuildMessage(string path, IReadOnlyList<DatasetValidationError> errors) =>
        $"Dataset '{path}' has {errors.Count} validation error(s):{Environment.NewLine}"
        + string.Join(Environment.NewLine, errors);
}
