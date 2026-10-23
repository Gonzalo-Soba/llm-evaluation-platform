using EvalLab.Api.Datasets;
using Microsoft.Extensions.Options;

namespace EvalLab.Api.Tests.Datasets;

public sealed class DatasetResolverTests
{
    private static readonly string Directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "datasets"));

    private readonly DatasetResolver resolver = new(Options.Create(new DatasetOptions { Directory = Directory }));

    [Theory]
    [InlineData("support-messages-v0")]
    [InlineData("v1")]
    public void ResolvePath_ValidName_ReturnsJsonlFileInsideDirectory(string name)
    {
        var path = resolver.ResolvePath(name);

        Assert.Equal(Path.Combine(Directory, name + ".jsonl"), path);
    }

    [Theory]
    [InlineData("../secrets")]
    [InlineData("..")]
    [InlineData("sub/dataset")]
    [InlineData("/etc/passwd")]
    [InlineData("..\\dataset")]
    [InlineData("C:\\data\\dataset")]
    [InlineData("support-messages-v0.jsonl")]
    [InlineData("Support-Messages")]
    [InlineData("")]
    public void ResolvePath_InvalidName_ThrowsArgumentException(string name)
    {
        Assert.Throws<ArgumentException>(() => resolver.ResolvePath(name));
    }
}
