namespace SJTag;

public sealed partial class DebugSession
{
    public byte[] ReadBytes(uint address, int byteCount, CancellationToken cancellationToken = default)
    {
        ValidateByteRange(address, byteCount);
        RequireReady();

        var result = new byte[byteCount];
        int offset = 0;

        while (offset < byteCount && (address + (uint)offset) % 4 != 0)
        {
            result[offset] = unchecked((byte)ReadScalar(address + (uint)offset, MemoryAccessWidth.Byte, cancellationToken));
            offset++;
        }

        int words = (byteCount - offset) & ~3;

        if (words > 0)
        {
            ReadMemory(address + (uint)offset, words, cancellationToken).CopyTo(result, offset);
            offset += words;
        }

        while (offset < byteCount)
        {
            result[offset] = unchecked((byte)ReadScalar(address + (uint)offset, MemoryAccessWidth.Byte, cancellationToken));
            offset++;
        }

        return result;
    }

    public void WriteBytes(uint address, ReadOnlySpan<byte> data, bool executable = false,
        CancellationToken cancellationToken = default)
    {
        ValidateByteRange(address, data.Length);
        RequireReady();

        int offset = 0;

        while (offset < data.Length && (address + (uint)offset) % 4 != 0)
        {
            WriteScalar(address + (uint)offset, data[offset], MemoryAccessWidth.Byte, cancellationToken);
            offset++;
        }

        int words = (data.Length - offset) & ~3;

        if (words > 0)
        {
            WriteMemory(address + (uint)offset, data.Slice(offset, words), executable, cancellationToken);
            offset += words;
        }

        while (offset < data.Length)
        {
            WriteScalar(address + (uint)offset, data[offset], MemoryAccessWidth.Byte, cancellationToken);
            offset++;
        }
    }

    public void WriteBytesVerified(uint address, ReadOnlySpan<byte> data, bool executable = false,
        CancellationToken cancellationToken = default)
    {
        WriteBytes(address, data, executable, cancellationToken);

        if (!data.SequenceEqual(ReadBytes(address, data.Length, cancellationToken)))
            throw new IOException($"CPU byte write/readback mismatch at 0x{address:X8}.");
    }

    private static void ValidateByteRange(uint address, int byteCount)
    {
        if (byteCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(byteCount), "Byte count must be positive.");

        if ((ulong)address + (ulong)byteCount > 0x100000000)
            throw new ArgumentOutOfRangeException(nameof(byteCount), "Range exceeds the 32-bit address space.");
    }
}
