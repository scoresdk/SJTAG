namespace SJTag;

public readonly record struct SjtagClientOptions
{
    public bool AllowUninitialized { get; init; }
    public TimeSpan? Timeout { get; init; }
    public DebugServiceProfile? ServiceProfile { get; init; }

    public DmaScanMode DmaScanMode { get; init; }
}
