using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EvalLab.Api.Classification;

public sealed class ClassifierClient(HttpClient httpClient) : IClassifierClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // Omitted fields take the classifier service defaults; an explicit null would fail its validation.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // A response missing a field or with null in a non-nullable field is treated as malformed.
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    public async Task<ClassificationResult> ClassifyAsync(ClassifyRequest request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            throw new ClassifierValidationException(await ReadErrorsAsync(response, cancellationToken));
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ClassifierBadResponseException(
                $"The classifier service returned an unexpected status code {(int)response.StatusCode}.");
        }

        return await ReadResultAsync(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(ClassifyRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.PostAsJsonAsync("/classify", request, JsonOptions, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.HttpRequestError is HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError)
        {
            throw new ClassifierUnavailableException(ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ClassifierBadResponseException("The classifier service call failed.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout elapsed. Cancellation requested by the caller propagates unchanged.
            throw new ClassifierTimeoutException(ex);
        }
    }

    private static async Task<ClassificationResult> ReadResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<ClassificationResult>(stream, JsonOptions, cancellationToken)
                ?? throw new ClassifierBadResponseException("The classifier service returned an empty response.");
        }
        catch (JsonException ex)
        {
            throw new ClassifierBadResponseException("The classifier service returned a malformed response.", ex);
        }
    }

    private static async Task<JsonElement> ReadErrorsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("detail", out var detail)
                ? detail.Clone()
                : document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(body);
        }
    }
}
