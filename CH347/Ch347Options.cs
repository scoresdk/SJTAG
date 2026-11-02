namespace CH347;

public sealed record Ch347Options
{
    public static Ch347Options Fast { get; } = new()
    {
        PackedTransfers = true,
        LargerPackets = true,
        ClockFrequencyHz = 15_000_000
    };

    public bool PackedTransfers { get; init; }

    public byte ClockIndex { get; init; }
    public int? ClockFrequencyHz { get; init; }

    public bool LargerPackets { get; init; }
}
