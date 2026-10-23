using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EvalLab.Api.Classification;
using EvalLab.Api.Tests.Classification;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace EvalLab.Api.Tests.Evaluation;

public sealed class RunsEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(10);

    private static readonly string[] Dataset =
    [
        """{"id": "ex-1", "text": "text 1", "label": "technical_support", "split": "dev", "difficulty": "clear"}""",
        """{"id": "ex-2", "text": "text 2", "label": "billing", "split": "test", "difficulty": "clear"}""",
        """{"id": "ex-3", "text": "text 3", "label": "general_inquiry", "split": "test", "difficulty": "ambiguous"}""",
        """{"id": "ex-4", "text": "text 4", "label": "account_management", "split": "test", "difficulty": "clear"}""",
    ];

    private static readonly Dictionary<string, string> LabelByText = new()
    {
        ["text 1"] = "technical_support",
        ["text 2"] = "billing",
        ["text 3"] = "general_inquiry",
        ["text 4"] = "account_management",
    };

    private readonly TempDirectory directory = new();

    public void Dispose() => directory.Dispose();

    [Fact]
    public async Task Create_Returns202AndRunEventuallyCompletes()
    {
        var client = CreateClient(new FakeClassifierClient(request =>
            new ClassificationResult(LabelByText[request.Text], true, LabelByText[request.Text], 1, null, null)));

        var response = await client.PostAsJsonAsync("/api/runs", new { dataset = "sample", split = "test" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var runId = created.GetProperty("run_id").GetGuid();
        Assert.Equal("queued", created.GetProperty("status").GetString());
        Assert.Equal($"/api/runs/{runId}", response.Headers.Location?.AbsolutePath);

        var run = await PollUntilFinishedAsync(client, runId);

        Assert.Equal("completed", run.GetProperty("status").GetString());
        Assert.Equal("test", run.GetProperty("split").GetString());
        Assert.Equal(3, run.GetProperty("predictions").GetArrayLength());
        Assert.Equal(1.0, run.GetProperty("metrics").GetProperty("accuracy").GetDouble());
        Assert.Equal("mock", run.GetProperty("classifier_config").GetProperty("provider").GetString());
        Assert.False(string.IsNullOrEmpty(run.GetProperty("dataset_sha256").GetString()));
    }

    [Fact]
    public async Task Create_ClassifierAlwaysFails_RunEventuallyFails()
    {
        var client = CreateClient(FakeClassifierClient.Throwing(
            new ClassifierUnavailableException(new HttpRequestException("Connection refused."))));

        var response = await client.PostAsJsonAsync("/api/runs", new { dataset = "sample" });
        var runId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("run_id").GetGuid();

        var run = await PollUntilFinishedAsync(client, runId);

        Assert.Equal("failed", run.GetProperty("status").GetString());
        Assert.Equal(4, run.GetProperty("error_count").GetInt32());
        Assert.Equal(JsonValueKind.Null, run.GetProperty("metrics").ValueKind);
    }

    [Theory]
    [InlineData("../sample", null, "dataset")]
    [InlineData("sub/sample", null, "dataset")]
    [InlineData("Sample", null, "dataset")]
    [InlineData("does-not-exist", null, "dataset")]
    [InlineData("sample", "train", "split")]
    public async Task Create_InvalidRequest_Returns400(string dataset, string? split, string errorField)
    {
        var client = CreateClient(FakeClassifierClient.Throwing(new InvalidOperationException("Must not be called.")));

        var response = await client.PostAsJsonAsync("/api/runs", new { dataset, split });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty(errorField, out _));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.json", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Get_UnknownRun_Returns404()
    {
        var client = CreateClient(FakeClassifierClient.Throwing(new InvalidOperationException("Must not be called.")));

        var response = await client.GetAsync($"/api/runs/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task List_ReturnsSummariesNewestFirst()
    {
        var client = CreateClient(new FakeClassifierClient(request =>
            new ClassificationResult(LabelByText[request.Text], true, LabelByText[request.Text], 1, null, null)));

        var first = await CreateAndWaitAsync(client, "dev");
        var second = await CreateAndWaitAsync(client, "test");

        var summaries = await client.GetFromJsonAsync<JsonElement>("/api/runs");

        Assert.Equal([second, first], summaries.EnumerateArray().Select(s => s.GetProperty("run_id").GetGuid()));
        var summary = summaries[0];
        Assert.Equal(
            ["run_id", "status", "dataset", "split", "created_at", "accuracy", "macro_f1", "error_count"],
            summary.EnumerateObject().Select(p => p.Name));
        Assert.Equal("sample", summary.GetProperty("dataset").GetString());
        Assert.Equal(1.0, summary.GetProperty("accuracy").GetDouble());
    }

    private HttpClient CreateClient(IClassifierClient classifierClient)
    {
        File.WriteAllLines(directory.File("sample.jsonl"), Dataset);

        return factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Datasets:Directory", directory.Path);
                builder.UseSetting("Runs:Directory", directory.File("runs"));
                builder.ConfigureTestServices(services => services.AddSingleton(classifierClient));
            })
            .CreateClient();
    }

    private static async Task<Guid> CreateAndWaitAsync(HttpClient client, string split)
    {
        var response = await client.PostAsJsonAsync("/api/runs", new { dataset = "sample", split });
        var runId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("run_id").GetGuid();
        await PollUntilFinishedAsync(client, runId);
        return runId;
    }

    private static async Task<JsonElement> PollUntilFinishedAsync(HttpClient client, Guid runId)
    {
        using var timeout = new CancellationTokenSource(PollTimeout);
        while (true)
        {
            var response = await client.GetAsync($"/api/runs/{runId}", timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            Assert.True(response.IsSuccessStatusCode, $"GET /api/runs/{runId} returned {(int)response.StatusCode}: {body}");

            var run = JsonDocument.Parse(body).RootElement;
            if (run.GetProperty("status").GetString() is "completed" or "completed_with_errors" or "failed")
            {
                return run;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }
}
