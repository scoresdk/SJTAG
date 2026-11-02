namespace SJTag;

internal static class MemoryRange
{
    public static void Validate(uint address, int byteCount)
    {
        if (address % 4 != 0 || byteCount <= 0 || byteCount % 4 != 0)
            throw new ArgumentException("Address and positive byte count must be word-aligned.");

        if ((ulong)address + (ulong)byteCount > 1UL << 32)
            throw new ArgumentOutOfRangeException(nameof(byteCount), "Range exceeds the 32-bit address space.");
    }
}
