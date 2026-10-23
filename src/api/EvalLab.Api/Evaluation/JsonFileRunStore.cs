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

/// <summary>
/// Stores each run as an indented, snake_case JSON file: <c>&lt;Runs:Directory&gt;/&lt;runId&gt;.json</c>.
/// Register as a singleton: file access is serialized within the process.
/// </summary>
public sealed class JsonFileRunStore(IOptions<RunStoreOptions> options) : IRunStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    // On Windows a file cannot be replaced while another handle reads it, so reads and writes take turns.
    private readonly SemaphoreSlim gate = new(1, 1);

    private string Directory => Path.GetFullPath(options.Value.Directory);

    public async Task SaveAsync(EvaluationRun run, CancellationToken cancellationToken)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var path = PathFor(run.RunId);

        // Write to a temporary file and move it, so a crash never leaves a partially written run.
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, run, JsonOptions, cancellationToken);
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<EvaluationRun?> GetAsync(Guid runId, CancellationToken cancellationToken)
    {
        var path = PathFor(runId);

        await gate.WaitAsync(cancellationToken);
        try
        {
            return File.Exists(path) ? await ReadAsync(path, cancellationToken) : null;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<EvaluationRun>> ListAsync(CancellationToken cancellationToken)
    {
        var runs = new List<EvaluationRun>();

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (System.IO.Directory.Exists(Directory))
            {
                foreach (var path in System.IO.Directory.EnumerateFiles(Directory, "*.json"))
                {
                    runs.Add(await ReadAsync(path, cancellationToken));
                }
            }
        }
        finally
        {
            gate.Release();
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
