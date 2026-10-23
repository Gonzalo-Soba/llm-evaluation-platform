using EvalLab.Api.Classification;
using EvalLab.Api.Evaluation;
using Microsoft.Extensions.Options;

namespace EvalLab.Api.Tests.Evaluation;

public sealed class JsonFileRunStoreTests : IDisposable
{
    private readonly TempDirectory directory = new();

    private JsonFileRunStore Store => new(Options.Create(new RunStoreOptions { Directory = directory.File("runs") }));

    public void Dispose() => directory.Dispose();

    [Fact]
    public async Task SaveAndGet_RoundTripsTheWholeRun()
    {
        var run = CompletedRun(DateTimeOffset.Parse("2026-10-23T10:00:00Z"));

        await Store.SaveAsync(run, CancellationToken.None);
        var loaded = await Store.GetAsync(run.RunId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equivalent(run, loaded, strict: true);
    }

    [Fact]
    public async Task Save_WritesIndentedSnakeCaseJson()
    {
        var run = CompletedRun(DateTimeOffset.UtcNow);

        await Store.SaveAsync(run, CancellationToken.None);

        var json = await File.ReadAllTextAsync(Path.Combine(directory.File("runs"), $"{run.RunId:D}.json"));
        Assert.Contains("\n  \"run_id\": ", json);
        Assert.Contains("\"status\": \"completed_with_errors\"", json);
        Assert.Contains("\"end_to_end_latency_ms\"", json);
        Assert.Contains("\"prompt_version\": \"v1\"", json);
    }

    [Fact]
    public async Task Save_Twice_ReplacesTheRun()
    {
        var queued = EvaluationRun.CreateQueued("sample", null, new ClassifierConfig("mock"), DateTimeOffset.UtcNow);
        var running = queued with { Status = RunStatus.Running };

        await Store.SaveAsync(queued, CancellationToken.None);
        await Store.SaveAsync(running, CancellationToken.None);

        var loaded = await Store.GetAsync(queued.RunId, CancellationToken.None);
        Assert.Equal(RunStatus.Running, loaded!.Status);
        Assert.Single(Directory.GetFiles(directory.File("runs")));
    }

    [Fact]
    public async Task Get_UnknownRun_ReturnsNull()
    {
        Assert.Null(await Store.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task List_ReturnsRunsNewestFirst()
    {
        var older = CompletedRun(DateTimeOffset.Parse("2026-10-20T10:00:00Z"));
        var newer = CompletedRun(DateTimeOffset.Parse("2026-10-22T10:00:00Z"));
        await Store.SaveAsync(older, CancellationToken.None);
        await Store.SaveAsync(newer, CancellationToken.None);

        var runs = await Store.ListAsync(CancellationToken.None);

        Assert.Equal([newer.RunId, older.RunId], runs.Select(r => r.RunId));
    }

    [Fact]
    public async Task List_NoDirectory_ReturnsEmpty()
    {
        Assert.Empty(await Store.ListAsync(CancellationToken.None));
    }

    private static EvaluationRun CompletedRun(DateTimeOffset createdAt)
    {
        string[] labels = ["technical_support", "billing", "general_inquiry", "account_management"];
        PredictionRecord[] predictions =
        [
            new("ex-1", "La app se cierra", "technical_support", "technical_support", PredictionStatus.Ok,
                "technical_support", 12, 30, 40, 2, null),
            new("ex-2", "Me cobraron dos veces", "billing", MetricsCalculator.InvalidLabel, PredictionStatus.Invalid,
                "money?", 15, 33, null, null, null),
            new("ex-3", "¿Horarios?", "general_inquiry", null, PredictionStatus.Error,
                null, null, 5000, null, null, "The classifier service did not respond in time."),
        ];
        var metrics = MetricsCalculator.Calculate(
            [("technical_support", "technical_support"), ("billing", MetricsCalculator.InvalidLabel)], labels);

        return EvaluationRun.CreateQueued("support-messages-v0", "dev", new ClassifierConfig("mock", "m", "v1", 0.2), createdAt) with
        {
            Status = RunStatus.CompletedWithErrors,
            StartedAt = createdAt.AddSeconds(1),
            CompletedAt = createdAt.AddSeconds(9),
            DatasetSha256 = "ab12",
            Predictions = predictions,
            Metrics = metrics,
            ErrorCount = 1,
            TotalInputTokens = 40,
            TotalOutputTokens = 2,
        };
    }
}
