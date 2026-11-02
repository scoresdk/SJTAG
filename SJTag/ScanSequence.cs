namespace SJTag;

public static class ScanSequence
{
    public const uint UnlockKey = 0x82345678;

    public static JtagCycle[] Reset() => Enumerable.Repeat(new JtagCycle(true), 8).ToArray();

    public static JtagCycle[] Unlock(uint key = UnlockKey)
    {
        var cycles = new List<JtagCycle>(128);

        for (int bit = 0; bit < 32; bit++)
        {
            if ((key & (1u << bit)) != 0)
                cycles.Add(new JtagCycle(false));

            int highCount = (key & (1u << bit)) != 0 ? 3 : 1;

            for (int i = 0; i < highCount; i++)
                cycles.Add(new JtagCycle(true));
        }

        return cycles.ToArray();
    }

    public static (JtagCycle[] Cycles, int PayloadOffset) Create(uint value, int width)
    {
        if (width is not (5 or 32))
            throw new ArgumentOutOfRangeException(nameof(width), "IR is 5 bits; DR is 32 bits.");

        if (width == 5 && value >= 32)
            throw new ArgumentOutOfRangeException(nameof(value), "IR value exceeds 5 bits.");

        bool[] prefix = width == 5 ? [false, true, true, false, false] : [false, true, false, false];
        int suffixLength = width == 5 ? 6 : 7;
        var cycles = new JtagCycle[prefix.Length + width + suffixLength];

        for (int i = 0; i < prefix.Length; i++)
            cycles[i] = new JtagCycle(prefix[i]);

        for (int bit = 0; bit < width; bit++)
            cycles[prefix.Length + bit] = new JtagCycle(bit == width - 1, (value & (1u << bit)) != 0);

        for (int i = prefix.Length + width; i < cycles.Length; i++)
            cycles[i] = new JtagCycle(true);

        return (cycles, prefix.Length);
    }

    public static uint Decode(ReadOnlySpan<byte> bits)
    {
        if (bits.Length > 32)
            throw new ArgumentOutOfRangeException(nameof(bits));

        uint value = 0;

        for (int bit = 0; bit < bits.Length; bit++)
        {
            if (bits[bit] > 1)
                throw new InvalidDataException("TDO samples must be logical bits.");

            value |= (uint)bits[bit] << bit;
        }

        return value;
    }
}
