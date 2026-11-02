namespace SJTag;

public sealed record InstructionComparator(uint Address, uint AddressMask = 0, uint Auxiliary = 0,
    uint Control = 1);

public sealed record DataComparator(uint Address, uint AddressMask, uint Auxiliary, uint Control,
    uint CompareValue);

public static class VerifiedWatchpoint
{
    public static DataComparator WordAddress(uint address, bool load = true, bool store = true,
        uint addressMask = 0) =>
        new(address, addressMask, 0, 0xCF01u | (load ? 0u : 0x40u) | (store ? 0u : 0x80u), 0);

    public static DataComparator WordValue(uint address, uint value, bool load = true,
        bool store = true, byte ignoredValueBytes = 0, uint addressMask = 0)
    {
        if (ignoredValueBytes > 0xF)
            throw new ArgumentOutOfRangeException(nameof(ignoredValueBytes));

        return new(address, addressMask, 0,
            0xC001u | ((uint)ignoredValueBytes << 8) | (load ? 0u : 0x40u) | (store ? 0u : 0x80u),
            value);
    }
}
