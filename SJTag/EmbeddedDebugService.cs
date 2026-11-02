namespace SJTag;

public static class EmbeddedDebugService
{
    private const string ResourceName = "SJTag.Score7.Service.hex8";
    private static readonly Lazy<DebugServiceProfile> cached = new(Load);

    public static bool IsAvailable => typeof(EmbeddedDebugService).Assembly
        .GetManifestResourceInfo(ResourceName) is not null;
    public static DebugServiceProfile Profile => cached.Value;

    private static DebugServiceProfile Load()
    {
        using var stream = typeof(EmbeddedDebugService).Assembly.GetManifestResourceStream(ResourceName) ??
            throw new InvalidOperationException(
                "No embedded SCORE7 debug service. Place your image at SJTag/res/score-routine.hex8 and rebuild, " +
                "build with -p:SjtagServiceImage=/path/to/your/service.hex8, " +
                "or supply a DebugServiceProfile to SjtagClient.");

        using var reader = new StreamReader(stream);

        return new DebugServiceProfile(ServiceImage.LoadHex8(reader, ResourceName));
    }
}
