using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EvalLab.Api.Classification;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace EvalLab.Api.Tests.Classification;

public sealed class ClassifyEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly ClassificationResult BillingResult = new("billing", true, "billing", 12, null, null);

    [Fact]
    public async Task Classify_Success_ReturnsSnakeCaseResult()
    {
        var fake = FakeClassifierClient.Returning(BillingResult);
        var client = CreateClient(fake);

        var response = await client.PostAsJsonAsync("/api/classify", new { text = "I was charged twice" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("billing", body.GetProperty("category").GetString());
        Assert.True(body.GetProperty("valid").GetBoolean());
        Assert.Equal("billing", body.GetProperty("raw_output").GetString());
        Assert.Equal(12, body.GetProperty("latency_ms").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("input_tokens").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("output_tokens").ValueKind);
    }

    [Fact]
    public async Task Classify_WithoutConfig_SendsMockProvider()
    {
        var fake = FakeClassifierClient.Returning(BillingResult);
        var client = CreateClient(fake);

        await client.PostAsJsonAsync("/api/classify", new { text = "hello" });

        Assert.Equal(new ClassifyRequest("hello", new ClassifierConfig("mock")), fake.LastRequest);
    }

    [Fact]
    public async Task Classify_WithConfig_ForwardsConfig()
    {
        var fake = FakeClassifierClient.Returning(BillingResult);
        var client = CreateClient(fake);

        await client.PostAsJsonAsync("/api/classify", new
        {
            text = "hello",
            config = new { provider = "mock", model = "m1", prompt_version = "v2", temperature = 0.5 },
        });

        Assert.Equal(new ClassifierConfig("mock", "m1", "v2", 0.5), fake.LastRequest!.Config);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Classify_EmptyText_Returns400WithoutCallingClassifier(string? text)
    {
        var fake = FakeClassifierClient.Returning(BillingResult);
        var client = CreateClient(fake);

        var response = await client.PostAsJsonAsync("/api/classify", new { text });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("errors").TryGetProperty("Text", out _));
        Assert.Null(fake.LastRequest);
    }

    [Fact]
    public async Task Classify_ClassifierValidationError_Returns400WithDownstreamDetail()
    {
        var errors = JsonSerializer.SerializeToElement(new[] { new { type = "literal_error", msg = "Input should be 'mock'" } });
        var client = CreateClient(FakeClassifierClient.Throwing(new ClassifierValidationException(errors)));

        var response = await client.PostAsJsonAsync("/api/classify", new { text = "hello", config = new { provider = "other" } });

        var body = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("literal_error", body.GetProperty("errors")[0].GetProperty("type").GetString());
    }

    public static TheoryData<Exception, HttpStatusCode> ClassifierFailures => new()
    {
        { new ClassifierUnavailableException(new HttpRequestException("Connection refused.")), HttpStatusCode.ServiceUnavailable },
        { new ClassifierTimeoutException(new TaskCanceledException()), HttpStatusCode.GatewayTimeout },
        { new ClassifierBadResponseException("Malformed response."), HttpStatusCode.BadGateway },
    };

    [Theory]
    [MemberData(nameof(ClassifierFailures))]
    public async Task Classify_ClassifierFailure_MapsToProblemDetails(Exception exception, HttpStatusCode expectedStatus)
    {
        var client = CreateClient(FakeClassifierClient.Throwing(exception));

        var response = await client.PostAsJsonAsync("/api/classify", new { text = "hello" });

        await AssertProblemAsync(response, expectedStatus);
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = CreateClient(FakeClassifierClient.Returning(BillingResult));

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", (await ReadJsonAsync(response)).GetProperty("status").GetString());
    }

    private HttpClient CreateClient(IClassifierClient classifierClient) =>
        factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton(classifierClient)))
            .CreateClient();

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await ReadJsonAsync(response);
        Assert.Equal((int)expectedStatus, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("title").GetString()));
        return body;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}
