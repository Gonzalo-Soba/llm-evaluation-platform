using System.Text.Json;

namespace EvalLab.Api.Classification;

/// <summary>Base type for failures when calling the classifier service. Mapped to HTTP responses by <see cref="ClassifierExceptionHandler"/>.</summary>
public abstract class ClassifierServiceException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>The classifier service rejected the request (HTTP 422).</summary>
public sealed class ClassifierValidationException(JsonElement errors)
    : ClassifierServiceException("The classifier service rejected the request.")
{
    public JsonElement Errors { get; } = errors;
}

/// <summary>The classifier service could not be reached (connection refused, DNS failure).</summary>
public sealed class ClassifierUnavailableException(Exception innerException)
    : ClassifierServiceException("The classifier service could not be reached.", innerException);

/// <summary>The classifier service did not respond within the configured timeout.</summary>
public sealed class ClassifierTimeoutException(Exception innerException)
    : ClassifierServiceException("The classifier service did not respond in time.", innerException);

/// <summary>The classifier service returned an unexpected status code or a malformed response.</summary>
public sealed class ClassifierBadResponseException(string message, Exception? innerException = null)
    : ClassifierServiceException(message, innerException);
