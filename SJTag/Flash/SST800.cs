namespace SJTag;

public sealed record SST800 : NorFlashProfile
{
    public SST800() : base(
        0x9E000000, 0x100000, 0x880700BC, 0x880700C4, 0x278100BF, 4,
        TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5),
        0x88200004, 1u << 24)
    {
    }
}
