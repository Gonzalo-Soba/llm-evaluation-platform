using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace EvalLab.Api.Evaluation;

public sealed class RunStoreOptions
{
    public const string SectionName = "Runs";

    /// <summary>Directory where run files are written. Relative paths are resolved against the content root.</summary>
    public string Directory { get; set; } = string.Empty;
}

/// <summary>Stores each run as an indented, snake_case JSON file: <c>&lt;Runs:Directory&gt;/&lt;runId&gt;.json</c>.</summary>
public sealed class JsonFileRunStore(IOptions<RunStoreOptions> options) : IRunStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private string Directory => Path.GetFullPath(options.Value.Directory);

    public async Task SaveAsync(EvaluationRun run, CancellationToken cancellationToken)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var path = PathFor(run.RunId);

        // Write to a temporary file and move it, so readers never see a partially written run.
        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, run, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    public async Task<EvaluationRun?> GetAsync(Guid runId, CancellationToken cancellationToken)
    {
        var path = PathFor(runId);
        return File.Exists(path) ? await ReadAsync(path, cancellationToken) : null;
    }

    public async Task<IReadOnlyList<EvaluationRun>> ListAsync(CancellationToken cancellationToken)
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return [];
        }

        var runs = new List<EvaluationRun>();
        foreach (var path in System.IO.Directory.EnumerateFiles(Directory, "*.json"))
        {
            runs.Add(await ReadAsync(path, cancellationToken));
        }

        return runs.OrderByDescending(run => run.CreatedAt).ToList();
    }

    private string PathFor(Guid runId) => Path.Combine(Directory, $"{runId:D}.json");

    private static async Task<EvaluationRun> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<EvaluationRun>(stream, JsonOptions, cancellationToken)
            ?? throw new JsonException($"Run file '{path}' is empty.");
    }
}
