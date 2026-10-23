using Microsoft.AspNetCore.Mvc;

namespace EvalLab.Api.Classification;

[ApiController]
[Route("api/classify")]
public sealed class ClassificationController(IClassifierClient classifierClient) : ControllerBase
{
    // Classifier failures are thrown as ClassifierServiceException and mapped by ClassifierExceptionHandler.
    [HttpPost]
    public async Task<ActionResult<ClassificationResult>> Classify(ClassifyRequest request, CancellationToken cancellationToken)
    {
        var classifierRequest = request with { Config = request.Config ?? ClassifierConfig.Default };
        return await classifierClient.ClassifyAsync(classifierRequest, cancellationToken);
    }
}
