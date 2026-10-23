using Microsoft.AspNetCore.Mvc;

namespace EvalLab.Api.Classification;

[ApiController]
[Route("api/classify")]
public sealed class ClassificationController(IClassifierClient classifierClient) : ControllerBase
{
    private static readonly ClassifierConfig DefaultConfig = new(Provider: "mock");

    // Classifier failures are thrown as ClassifierServiceException and mapped by ClassifierExceptionHandler.
    [HttpPost]
    public async Task<ActionResult<ClassificationResult>> Classify(ClassifyRequest request, CancellationToken cancellationToken)
    {
        var classifierRequest = request with { Config = request.Config ?? DefaultConfig };
        return await classifierClient.ClassifyAsync(classifierRequest, cancellationToken);
    }
}
