using EvalLab.Api.Classification;

namespace EvalLab.Api.Tests.Classification;

internal sealed class FakeClassifierClient(Func<ClassifyRequest, ClassificationResult> classify) : IClassifierClient
{
    public ClassifyRequest? LastRequest { get; private set; }

    public static FakeClassifierClient Returning(ClassificationResult result) => new(_ => result);

    public static FakeClassifierClient Throwing(Exception exception) => new(_ => throw exception);

    public Task<ClassificationResult> ClassifyAsync(ClassifyRequest request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(classify(request));
    }
}
