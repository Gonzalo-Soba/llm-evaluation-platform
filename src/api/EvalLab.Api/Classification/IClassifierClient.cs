namespace EvalLab.Api.Classification;

public interface IClassifierClient
{
    /// <exception cref="ClassifierServiceException">The classifier service call failed.</exception>
    Task<ClassificationResult> ClassifyAsync(ClassifyRequest request, CancellationToken cancellationToken);
}
