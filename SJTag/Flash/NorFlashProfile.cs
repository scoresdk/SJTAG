namespace SJTag;

public record NorFlashProfile
{
    public uint BaseAddress { get; }
    public int ByteCount { get; }
    public uint CommandControlAddress { get; }
    public uint BankAddress { get; }

    public uint? PinMuxAddress { get; }
    public uint PinMuxMask { get; }

    public uint ExpectedId { get; }
    public int WordAddressShift { get; }

    public TimeSpan CommandDelay { get; }
    public TimeSpan ProgramTimeout { get; }
    public TimeSpan EraseTimeout { get; }

    public NorFlashProfile(uint baseAddress, int byteCount, uint commandControlAddress,
        uint bankAddress, uint expectedId, int wordAddressShift,
        TimeSpan commandDelay, TimeSpan programTimeout, TimeSpan eraseTimeout,
        uint? pinMuxAddress = null, uint pinMuxMask = 0)
    {
        MemoryRange.Validate(baseAddress, byteCount);

        if (commandControlAddress % 4 != 0 || bankAddress % 4 != 0 ||
            (pinMuxAddress is uint pin && pin % 4 != 0) ||
            wordAddressShift is < 2 or > 16 || byteCount > 0x200000 ||
            commandDelay < TimeSpan.Zero || programTimeout <= TimeSpan.Zero || eraseTimeout <= TimeSpan.Zero ||
            (ulong)baseAddress + ((ulong)Math.Max(byteCount / 2 - 1, 0x5555) << wordAddressShift) > uint.MaxValue)
            throw new ArgumentException("Invalid NOR profile.");

        BaseAddress = baseAddress;
        ByteCount = byteCount;

        CommandControlAddress = commandControlAddress;
        BankAddress = bankAddress;

        ExpectedId = expectedId;
        WordAddressShift = wordAddressShift;

        CommandDelay = commandDelay;
        ProgramTimeout = programTimeout;
        EraseTimeout = eraseTimeout;

        PinMuxAddress = pinMuxAddress;
        PinMuxMask = pinMuxMask;
    }

    public uint CommandAddress(uint halfwordIndex)
    {
        ulong address = (ulong)BaseAddress + ((ulong)halfwordIndex << WordAddressShift);

        if (address > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(halfwordIndex));

        return (uint)address;
    }

    public static NorFlashProfile VerifiedSst800 { get; } = new SST800();
}
