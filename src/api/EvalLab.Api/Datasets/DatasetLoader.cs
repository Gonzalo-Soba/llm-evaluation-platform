using System.Text.Json;

namespace EvalLab.Api.Datasets;

/// <summary>Loads labeled examples from a JSONL file (one JSON object per line).</summary>
public static class DatasetLoader
{
    public static readonly IReadOnlySet<string> Labels =
        new HashSet<string> { "technical_support", "billing", "general_inquiry", "account_management" };

    public static readonly IReadOnlySet<string> Splits = new HashSet<string> { "dev", "test" };

    public static readonly IReadOnlySet<string> Difficulties = new HashSet<string> { "clear", "ambiguous" };

    /// <summary>
    /// Reads and validates the whole file, then returns the examples, optionally only those in <paramref name="split"/>.
    /// Blank lines are ignored.
    /// </summary>
    /// <exception cref="DatasetValidationException">One or more lines are invalid. All errors are reported together.</exception>
    public static async Task<IReadOnlyList<LabeledExample>> LoadAsync(
        string path, string? split = null, CancellationToken cancellationToken = default)
    {
        if (split is not null && !Splits.Contains(split))
        {
            throw new ArgumentException($"Unknown split '{split}'. Expected one of: {string.Join(", ", Splits)}.", nameof(split));
        }

        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        var examples = new List<LabeledExample>();
        var errors = new List<DatasetValidationError>();
        var firstLineById = new Dictionary<string, int>();

        for (var index = 0; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            var lineErrors = new List<string>();
            var (id, example) = ParseLine(lines[index], lineErrors);

            // Checked even when other fields are invalid, so every error on the line is reported.
            if (id is not null)
            {
                if (firstLineById.TryGetValue(id, out var firstLine))
                {
                    lineErrors.Add($"duplicate id '{id}' (first seen on line {firstLine}).");
                }
                else
                {
                    firstLineById[id] = lineNumber;
                }
            }

            errors.AddRange(lineErrors.Select(message => new DatasetValidationError(lineNumber, message)));

            if (example is not null && lineErrors.Count == 0)
            {
                examples.Add(example);
            }
        }

        if (errors.Count > 0)
        {
            throw new DatasetValidationException(path, errors);
        }

        return split is null ? examples : examples.Where(example => example.Split == split).ToList();
    }

    /// <summary>
    /// Returns the id (if readable) and the example (null if any field is invalid). Adds a message for each problem.
    /// </summary>
    private static (string? Id, LabeledExample? Example) ParseLine(string line, List<string> errors)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException ex)
        {
            errors.Add($"malformed JSON ({ex.Message}).");
            return (null, null);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                errors.Add("expected a JSON object.");
                return (null, null);
            }

            var id = ReadRequiredString(root, "id", errors);
            var text = ReadRequiredString(root, "text", errors);
            var label = ReadAllowedValue(root, "label", Labels, errors);
            var split = ReadAllowedValue(root, "split", Splits, errors);
            var difficulty = ReadAllowedValue(root, "difficulty", Difficulties, errors);

            var example = id is null || text is null || label is null || split is null || difficulty is null
                ? null
                : new LabeledExample(id, text, label, split, difficulty);
            return (id, example);
        }
    }

    private static string? ReadRequiredString(JsonElement root, string field, List<string> errors)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            errors.Add($"field '{field}' is missing.");
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            errors.Add($"field '{field}' must be a string.");
            return null;
        }

        var text = value.GetString()!;
        if (string.IsNullOrWhiteSpace(text))
        {
            errors.Add($"field '{field}' is empty.");
            return null;
        }

        return text;
    }

    private static string? ReadAllowedValue(JsonElement root, string field, IReadOnlySet<string> allowed, List<string> errors)
    {
        var value = ReadRequiredString(root, field, errors);
        if (value is not null && !allowed.Contains(value))
        {
            errors.Add($"unknown {field} '{value}'. Expected one of: {string.Join(", ", allowed)}.");
            return null;
        }

        return value;
    }
}
