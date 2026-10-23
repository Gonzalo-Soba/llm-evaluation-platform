using Microsoft.AspNetCore.Diagnostics;

namespace EvalLab.Api.Classification;

/// <summary>Maps <see cref="ClassifierServiceException"/> types to ProblemDetails responses.</summary>
public sealed class ClassifierExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ClassifierServiceException classifierException)
        {
            return false;
        }

        var (status, title) = classifierException switch
        {
            ClassifierValidationException => (StatusCodes.Status400BadRequest, "Invalid classification request"),
            ClassifierUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Classifier service unavailable"),
            ClassifierTimeoutException => (StatusCodes.Status504GatewayTimeout, "Classifier service timeout"),
            _ => (StatusCodes.Status502BadGateway, "Bad response from classifier service"),
        };

        httpContext.Response.StatusCode = status;

        var context = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = status,
                Title = title,
                Detail = classifierException.Message,
            },
        };

        if (classifierException is ClassifierValidationException validationException)
        {
            context.ProblemDetails.Extensions["errors"] = validationException.Errors;
        }

        return await problemDetailsService.TryWriteAsync(context);
    }
}
