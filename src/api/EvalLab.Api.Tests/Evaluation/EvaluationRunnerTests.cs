using System.Security.Cryptography;
using System.Text.Json;
using EvalLab.Api.Classification;
using EvalLab.Api.Datasets;
using EvalLab.Api.Evaluation;
using Microsoft.Extensions.Options;

namespace EvalLab.Api.Tests.Evaluation;

public sealed class EvaluationRunnerTests : IDisposable
{
    private const string DatasetName = "sample";

    // Two examples per category; ids follow file order.
    private static readonly LabeledExample[] Examples =
    [
        new("ex-1", "text 1", "technical_support", "dev", "clear"),
        new("ex-2", "text 2", "billing", "dev", "clear"),
        new("ex-3", "text 3", "general_inquiry", "dev", "clear"),
        new("ex-4", "text 4", "account_management", "dev", "ambiguous"),
        new("ex-5", "text 5", "technical_support", "test", "clear"),
        new("ex-6", "text 6", "billing", "test", "clear"),
        new("ex-7", "text 7", "general_inquiry", "test", "ambiguous"),
        new("ex-8", "text 8", "account_management", "test", "clear"),
    ];

    private static readonly Dictionary<string, string> LabelByText = Examples.ToDictionary(e => e.Text, e => e.Label);

    private readonly TempDirectory directory = new();

    public EvaluationRunnerTests()
    {
        WriteDataset(DatasetName, Examples);
    }

    public void Dispose() => directory.Dispose();

    [Fact]
    public async Task RunAsync_AllOk_CompletesWithPerfectMetrics()
    {
        var runner = CreateRunner(Respond(request => Correct(request.Text)));

        var run = await runner.RunAsync(QueuedRun(), CancellationToken.None);

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.NotNull(run.StartedAt);
        Assert.NotNull(run.CompletedAt);
        Assert.Null(run.FailureReason);
        Assert.Equal(8, run.Predictions.Count);
        Assert.All(run.Predictions, p =>
        {
            Assert.Equal(PredictionStatus.Ok, p.Status);
            Assert.Equal(p.Expected, p.Predicted);
            Assert.Equal(5, p.ClassifierLatencyMs);
            Assert.True(p.EndToEndLatencyMs >= 0);
            Assert.Null(p.ErrorMessage);
        });
        Assert.Equal(0, run.ErrorCount);
        Assert.NotNull(run.Metrics);
        Assert.Equal(1.0, run.Metrics.Accuracy);
        Assert.Equal(DatasetLoader.Labels, run.Metrics.PerClass.Select(c => c.Label));
    }

    [Fact]
    public async Task RunAsync_SomeInvalid_CountsThemAsWrong()
    {
        var runner = CreateRunner(Respond(request => request.Text is "text 1" or "text 2"
            ? new ClassificationResult("invalid", false, "I think it is about money", 5, null, null)
            : Correct(request.Text)));

        var run = await runner.RunAsync(QueuedRun(), CancellationToken.None);

        Assert.Equal(RunStatus.Completed, run.Status);
        var invalid = run.Predictions.Where(p => p.Status == PredictionStatus.Invalid).ToList();
        Assert.Equal(["ex-1", "ex-2"], invalid.Select(p => p.ExampleId));
        Assert.All(invalid, p =>
        {
            Assert.Equal(MetricsCalculator.InvalidLabel, p.Predicted);
            Assert.Equal("I think it is about money", p.RawOutput);
        });
        Assert.Equal(0, run.ErrorCount);
        Assert.NotNull(run.Metrics);
        Assert.Equal(2, run.Metrics.InvalidCount);
        Assert.Equal(6.0 / 8, run.Metrics.Accuracy);
    }

    [Fact]
    public async Task RunAsync_ValidResponseWithUnknownCategory_IsInvalid()
    {
        var runner = CreateRunner(Respond(request => request.Text == "text 1"
            ? new ClassificationResult("sales", true, "sales", 5, null, null)
            : Correct(request.Text)));

        var run = await runner.RunAsync(QueuedRun(), CancellationToken.None);

        var prediction = run.Predictions[0];
        Assert.Equal(PredictionStatus.Invalid, prediction.Status);
        Assert.Equal(MetricsCalculator.InvalidLabel, prediction.Predicted);
        Assert.Equal("sales", prediction.RawOutput);
    }

    [Fact]
    public async Task RunAsync_SomeErrors_ExcludesThemAndCompletesWithErrors()
    {
        var runner = CreateRunner(Respond(request => request.Text is "text 3" or "text 7"
            ? throw new ClassifierUnavailableException(new HttpRequestException("Connection refused."))
            : Correct(request.Text)));

        var run = await runner.RunAsync(QueuedRun(), CancellationToken.None);

        Assert.Equal(RunStatus.CompletedWithErrors, run.Status);
        Assert.Equal(2, run.ErrorCount);
        var errors = run.Predictions.Where(p => p.Status == PredictionStatus.Error).ToList();
        Assert.Equal(["ex-3", "ex-7"], errors.Select(p => p.ExampleId));
        Assert.All(errors, p =>
        {
            Assert.Null(p.Predicted);
            Assert.Null(p.ClassifierLatencyMs);
            Assert.Equal("The classifier service could not be reached.", p.ErrorMessage);
        });
        Assert.NotNull(run.Metrics);
        Assert.Equal(6, run.Metrics.PerClass.Sum(c => c.Support));
        Assert.Equal(1.0, run.Metrics.Accuracy);
    }

    [Fact]
    public async Task RunAsync_AllErrors_Fails()
    {
        var runner = CreateRunner(Respond(_ => throw new ClassifierTimeoutException(new TaskCanceledException())));

        var run = await runner.RunAsync(QueuedRun(), CancellationToken.None);

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal(8, run.ErrorCount);
        Assert.Equal(8, run.Predictions.Count);
        Assert.Null(run.Metrics);
        Assert.NotNull(run.FailureReason);
        Assert.NotNull(run.CompletedAt);
    }

    [Fact]
    public async Task RunAsync_ClassifierValidationError_IncludesDownstreamDetail()
    {
        // Parsed like ClassifierClient does with the response body, which keeps the text unescaped.
        var errors = JsonDocument.Parse("""[{"msg": "Input should be 'mock'"}]""").RootElement.Clone();
        var runner = CreateRunner(Respond(_ => throw new ClassifierValidationException(errors)));

        var run = await runner.RunAsync(QueuedRun(), CancellationToken.None);

        Assert.Contains("Input should be 'mock'", run.Predictions[0].ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_ParallelCompletion_KeepsDatasetOrder()
    {
        // Earlier examples take longer, so they finish after later ones.
        var runner = CreateRunner(async (request, cancellationToken) =>
        {
            var position = int.Parse(request.Text["text ".Length..]);
            await Task.Delay((Examples.Length - position + 1) * 20, cancellationToken);
            return Correct(request.Text);
        });

        var run = await runner.RunAsync(QueuedRun(), CancellationToken.None);

        Assert.Equal(Examples.Select(e => e.Id), run.Predictions.Select(p => p.ExampleId));
    }

    [Fact]
    public async Task RunAsync_RespectsMaxParallelism()
    {
        var current = 0;
        var maximum = 0;
        var runner = CreateRunner(async (request, cancellationToken) =>
        {
            var now = Interlocked.Increment(ref current);
            InterlockedMax(ref maximum, now);
            await Task.Delay(30, cancellationToken);
            Interlocked.Decrement(ref current);
            return Correct(request.Text);
        }, maxParallelism: 2);

        await runner.RunAsync(QueuedRun(), CancellationToken.None);

        Assert.Equal(2, maximum);
    }

    [Fact]
    public async Task RunAsync_WithSplit_EvaluatesOnlyThatSplit()
    {
        var runner = CreateRunner(Respond(request => Correct(request.Text)));

        var run = await runner.RunAsync(QueuedRun(split: "test"), CancellationToken.None);

        Assert.Equal(["ex-5", "ex-6", "ex-7", "ex-8"], run.Predictions.Select(p => p.ExampleId));
        Assert.Equal("test", run.Split);
    }

    [Fact]
    public async Task RunAsync_DatasetHash_IsStableAndMatchesFileContent()
    {
        var runner = CreateRunner(Respond(request => Correct(request.Text)));
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(directory.File("sample.jsonl"))));

        var first = await runner.RunAsync(QueuedRun(), CancellationToken.None);
        var second = await runner.RunAsync(QueuedRun(), CancellationToken.None);

        Assert.Equal(expectedHash, first.DatasetSha256);
        Assert.Equal(first.DatasetSha256, second.DatasetSha256);
    }

    [Theory]
    [InlineData("../sample")]
    [InlineData("sub/sample")]
    [InlineData("/sample")]
    public async Task RunAsync_DatasetNameWithPath_ThrowsArgumentException(string datasetName)
    {
        var runner = CreateRunner(Respond(request => Correct(request.Text)));

        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(QueuedRun(datasetName), CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_DatasetFileMissing_Fails()
    {
        var runner = CreateRunner(Respond(request => Correct(request.Text)));

        var run = await runner.RunAsync(QueuedRun("does-not-exist"), CancellationToken.None);

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Contains("could not be loaded", run.FailureReason);
        Assert.Empty(run.Predictions);
    }

    [Fact]
    public async Task RunAsync_DatasetInvalid_Fails()
    {
        File.WriteAllText(directory.File("broken.jsonl"), "not json\n");
        var runner = CreateRunner(Respond(request => Correct(request.Text)));

        var run = await runner.RunAsync(QueuedRun("broken"), CancellationToken.None);

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Contains("Line 1", run.FailureReason);
    }

    [Fact]
    public async Task RunAsync_SendsRunClassifierConfig()
    {
        var config = new ClassifierConfig("mock", "model-a", "v2", 0.3);
        var received = new List<ClassifierConfig?>();
        var runner = CreateRunner(Respond(request =>
        {
            lock (received)
            {
                received.Add(request.Config);
            }

            return Correct(request.Text);
        }));

        await runner.RunAsync(QueuedRun(config: config), CancellationToken.None);

        Assert.All(received, c => Assert.Equal(config, c));
    }

    private EvaluationRunner CreateRunner(
        Func<ClassifyRequest, CancellationToken, Task<ClassificationResult>> classify, int maxParallelism = 4) =>
        new(
            new DelegateClassifierClient(classify),
            new DatasetResolver(Options.Create(new DatasetOptions { Directory = directory.Path })),
            Options.Create(new EvaluationOptions { MaxParallelism = maxParallelism }),
            TimeProvider.System);

    private static Func<ClassifyRequest, CancellationToken, Task<ClassificationResult>> Respond(
        Func<ClassifyRequest, ClassificationResult> classify) =>
        (request, _) => Task.FromResult(classify(request));

    private static ClassificationResult Correct(string text) =>
        new(LabelByText[text], true, LabelByText[text], 5, null, null);

    private static EvaluationRun QueuedRun(string datasetName = DatasetName, string? split = null, ClassifierConfig? config = null) =>
        EvaluationRun.CreateQueued(datasetName, split, config ?? new ClassifierConfig("mock"), DateTimeOffset.UtcNow);

    private void WriteDataset(string name, IEnumerable<LabeledExample> examples)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        File.WriteAllLines(directory.File(name + ".jsonl"), examples.Select(e => JsonSerializer.Serialize(e, options)));
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value
            && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }

    private sealed class DelegateClassifierClient(
        Func<ClassifyRequest, CancellationToken, Task<ClassificationResult>> classify) : IClassifierClient
    {
        public Task<ClassificationResult> ClassifyAsync(ClassifyRequest request, CancellationToken cancellationToken) =>
            classify(request, cancellationToken);
    }
}
