using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace EvalLab.Api.Datasets;

public sealed class DatasetOptions
{
    public const string SectionName = "Datasets";

    /// <summary>Directory that contains the dataset files. Relative paths are resolved against the content root.</summary>
    public string Directory { get; set; } = string.Empty;
}

/// <summary>Maps a dataset name to its file. Datasets are referenced by name only, never by path.</summary>
public sealed partial class DatasetResolver(IOptions<DatasetOptions> options)
{
    /// <summary>Returns the full path of <c>&lt;Datasets:Directory&gt;/&lt;name&gt;.jsonl</c>. The file may not exist.</summary>
    /// <exception cref="ArgumentException">The name is not lowercase letters, digits and hyphens only.</exception>
    public string ResolvePath(string name)
    {
        // The strict pattern rules out path separators and "..", so the result always stays inside the directory.
        if (string.IsNullOrEmpty(name) || !NamePattern().IsMatch(name))
        {
            throw new ArgumentException(
                $"Invalid dataset name '{name}'. Use only lowercase letters, digits and hyphens.", nameof(name));
        }

        return Path.Combine(Path.GetFullPath(options.Value.Directory), name + ".jsonl");
    }

    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex NamePattern();
}
