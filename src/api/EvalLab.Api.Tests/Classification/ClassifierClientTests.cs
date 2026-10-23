using System.Net;
using System.Text;
using System.Text.Json;
using EvalLab.Api.Classification;

namespace EvalLab.Api.Tests.Classification;

public sealed class ClassifierClientTests
{
    private const string ValidResponse = """
        {
          "category": "billing",
          "valid": true,
          "raw_output": "billing",
          "latency_ms": 12,
          "input_tokens": null,
          "output_tokens": null
        }
        """;

    private static readonly ClassifyRequest Request = new("I was charged twice", new ClassifierConfig("mock"));

    [Fact]
    public async Task ClassifyAsync_Success_ReturnsResult()
    {
        var handler = Respond(HttpStatusCode.OK, ValidResponse);
        var client = CreateClient(handler);

        var result = await client.ClassifyAsync(Request, CancellationToken.None);

        Assert.Equal(new ClassificationResult("billing", true, "billing", 12, null, null), result);
    }

    [Fact]
    public async Task ClassifyAsync_SendsSnakeCaseBodyWithoutNullFields()
    {
        var handler = Respond(HttpStatusCode.OK, ValidResponse);
        var client = CreateClient(handler);
        var request = new ClassifyRequest("hello", new ClassifierConfig("mock", PromptVersion: "v1"));

        await client.ClassifyAsync(request, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("http://classifier.test/classify", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("""{"text":"hello","config":{"provider":"mock","prompt_version":"v1"}}""", handler.LastRequestBody);
    }

    [Fact]
    public async Task ClassifyAsync_Status422_ThrowsValidationExceptionWithDownstreamDetail()
    {
        const string body = """{"detail":[{"type":"string_too_short","loc":["body","text"],"msg":"String should have at least 1 character"}]}""";
        var client = CreateClient(Respond(HttpStatusCode.UnprocessableEntity, body));

        var exception = await Assert.ThrowsAsync<ClassifierValidationException>(
            () => client.ClassifyAsync(Request, CancellationToken.None));

        Assert.Equal(JsonValueKind.Array, exception.Errors.ValueKind);
        Assert.Equal("string_too_short", exception.Errors[0].GetProperty("type").GetString());
    }

    [Theory]
    [InlineData(HttpRequestError.ConnectionError)]
    [InlineData(HttpRequestError.NameResolutionError)]
    public async Task ClassifyAsync_ConnectionFailure_ThrowsUnavailableException(HttpRequestError error)
    {
        var client = CreateClient(new FakeHttpMessageHandler(
            (_, _) => throw new HttpRequestException(error, "Connection failed.")));

        await Assert.ThrowsAsync<ClassifierUnavailableException>(
            () => client.ClassifyAsync(Request, CancellationToken.None));
    }

    [Fact]
    public async Task ClassifyAsync_Timeout_ThrowsTimeoutException()
    {
        var handler = new FakeHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        });
        var client = CreateClient(handler, TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<ClassifierTimeoutException>(
            () => client.ClassifyAsync(Request, CancellationToken.None));
    }

    [Fact]
    public async Task ClassifyAsync_CallerCancels_PropagatesCancellation()
    {
        var handler = new FakeHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        });
        var client = CreateClient(handler);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.ClassifyAsync(Request, cancellation.Token));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"category": "billing"}""")]
    [InlineData("""{"category": null, "valid": true, "raw_output": "x", "latency_ms": 1, "input_tokens": null, "output_tokens": null}""")]
    [InlineData("null")]
    public async Task ClassifyAsync_MalformedResponse_ThrowsBadResponseException(string body)
    {
        var client = CreateClient(Respond(HttpStatusCode.OK, body));

        await Assert.ThrowsAsync<ClassifierBadResponseException>(
            () => client.ClassifyAsync(Request, CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task ClassifyAsync_UnexpectedStatus_ThrowsBadResponseException(HttpStatusCode status)
    {
        var client = CreateClient(Respond(status, "{}"));

        await Assert.ThrowsAsync<ClassifierBadResponseException>(
            () => client.ClassifyAsync(Request, CancellationToken.None));
    }

    private static FakeHttpMessageHandler Respond(HttpStatusCode status, string body) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));

    private static ClassifierClient CreateClient(HttpMessageHandler handler, TimeSpan? timeout = null) =>
        new(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://classifier.test"),
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
        });
}
