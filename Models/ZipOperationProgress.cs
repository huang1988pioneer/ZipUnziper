namespace ZipUnziper.Models;

public sealed class ZipOperationProgress
{
    public string Message { get; init; } = string.Empty;
    public double Percent { get; init; }
    public bool IsIndeterminate { get; init; }
}
