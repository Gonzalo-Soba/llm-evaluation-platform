namespace EvalLab.Api.Datasets;

public sealed record LabeledExample(
    string Id,
    string Text,
    string Label,
    string Split,
    string Difficulty);
