using EvalLab.Api.Datasets;

namespace EvalLab.Api.Tests.Datasets;

public sealed class DatasetLoaderTests
{
    [Fact]
    public async Task LoadAsync_ValidFile_ReturnsAllExamplesAndSkipsBlankLines()
    {
        var examples = await DatasetLoader.LoadAsync(Fixture("valid.jsonl"));

        Assert.Equal(["ex-1", "ex-2", "ex-3", "ex-4"], examples.Select(e => e.Id));
        Assert.Equal(
            new LabeledExample("ex-3", "¿Tienen app para tablet?", "general_inquiry", "dev", "ambiguous"),
            examples[2]);
    }

    [Theory]
    [InlineData("dev", new[] { "ex-1", "ex-3" })]
    [InlineData("test", new[] { "ex-2", "ex-4" })]
    public async Task LoadAsync_WithSplit_ReturnsOnlyThatSplit(string split, string[] expectedIds)
    {
        var examples = await DatasetLoader.LoadAsync(Fixture("valid.jsonl"), split);

        Assert.Equal(expectedIds, examples.Select(e => e.Id));
    }

    [Fact]
    public async Task LoadAsync_UnknownSplitFilter_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => DatasetLoader.LoadAsync(Fixture("valid.jsonl"), "train"));
    }

    [Theory]
    [InlineData("malformed-json.jsonl", 2, "malformed JSON")]
    [InlineData("missing-field.jsonl", 2, "field 'label' is missing")]
    [InlineData("empty-field.jsonl", 2, "field 'text' is empty")]
    [InlineData("unknown-label.jsonl", 2, "unknown label 'sales'")]
    [InlineData("unknown-split.jsonl", 2, "unknown split 'train'")]
    [InlineData("unknown-difficulty.jsonl", 2, "unknown difficulty 'hard'")]
    [InlineData("duplicate-ids.jsonl", 3, "duplicate id 'ex-1' (first seen on line 1)")]
    public async Task LoadAsync_InvalidLine_ReportsErrorWithLineNumber(string fixture, int expectedLine, string expectedMessage)
    {
        var exception = await Assert.ThrowsAsync<DatasetValidationException>(() => DatasetLoader.LoadAsync(Fixture(fixture)));

        var error = Assert.Single(exception.Errors);
        Assert.Equal(expectedLine, error.LineNumber);
        Assert.StartsWith(expectedMessage, error.Message);
    }

    [Fact]
    public async Task LoadAsync_SeveralInvalidLines_ReportsAllErrors()
    {
        var exception = await Assert.ThrowsAsync<DatasetValidationException>(
            () => DatasetLoader.LoadAsync(Fixture("multiple-errors.jsonl")));

        Assert.Collection(
            exception.Errors,
            error => AssertError(error, 2, "malformed JSON"),
            error => AssertError(error, 3, "field 'text' is missing"),
            error => AssertError(error, 4, "unknown label 'sales'"),
            error => AssertError(error, 4, "unknown split 'train'"),
            error => AssertError(error, 5, "unknown difficulty 'hard'"),
            error => AssertError(error, 5, "duplicate id 'ex-1' (first seen on line 1)"));
        Assert.Contains("6 validation error(s)", exception.Message);
        Assert.Contains("Line 4: unknown split 'train'", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_SupportMessagesV0_IsValidAndNotEmpty()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "support-messages-v0.jsonl");

        // Only validity is checked: label corrections may legitimately change the distribution.
        var examples = await DatasetLoader.LoadAsync(path);

        Assert.NotEmpty(examples);
    }

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Datasets", "Fixtures", name);

    private static void AssertError(DatasetValidationError error, int expectedLine, string expectedMessage)
    {
        Assert.Equal(expectedLine, error.LineNumber);
        Assert.StartsWith(expectedMessage, error.Message);
    }
}
