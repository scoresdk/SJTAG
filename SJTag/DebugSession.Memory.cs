using System.Buffers.Binary;

namespace SJTag;

public sealed partial class DebugSession
{
    public byte[] ReadMemory(uint address, int byteCount, CancellationToken cancellationToken = default)
    {
        MemoryRange.Validate(address, byteCount);
        RequireReady();

        var result = new byte[byteCount];

        for (int offset = 0; offset < byteCount; offset += 4096)
        {
            int size = Math.Min(4096, byteCount - offset);

            SetTransfer(address + (uint)offset, size / 4, MemoryAccessWidth.Word);
            Command(1, cancellationToken);

            for (int i = 0; i < size; i += 4)
                BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset + i, 4),
                    memory.Read(Mailbox.Buffer + (uint)i));
        }

        return result;
    }

    public uint ReadScalar(uint address, MemoryAccessWidth width, CancellationToken cancellationToken = default)
    {
        ValidateScalar(address, width);

        SetTransfer(address, 1, width);
        Command(1, cancellationToken);

        return memory.Read(Mailbox.Buffer);
    }

    public void WriteScalar(uint address, uint value, MemoryAccessWidth width,
        CancellationToken cancellationToken = default)
    {
        ValidateScalar(address, width);

        SetTransfer(address, 1, width);
        memory.Write(Mailbox.Buffer, value);

        Command(2, cancellationToken);
    }

    public void WriteMemory(uint address, ReadOnlySpan<byte> data, bool executable = false,
        CancellationToken cancellationToken = default)
    {
        MemoryRange.Validate(address, data.Length);
        RequireReady();

        int chunkSize = executable ? 4 : 4096;

        for (int offset = 0; offset < data.Length; offset += chunkSize)
        {
            int size = Math.Min(chunkSize, data.Length - offset);

            SetTransfer(address + (uint)offset, size / 4, MemoryAccessWidth.Word);

            for (int i = 0; i < size; i += 4)
                memory.Write(Mailbox.Buffer + (uint)i,
                    BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + i, 4)));

            Command(2, cancellationToken);
        }
    }

    public void WriteMemoryVerified(uint address, ReadOnlySpan<byte> data, bool executable = false,
        CancellationToken cancellationToken = default)
    {
        WriteMemory(address, data, executable, cancellationToken);

        if (!data.SequenceEqual(ReadMemory(address, data.Length, cancellationToken)))
            throw new IOException($"CPU write/readback mismatch at 0x{address:X8}.");
    }

    private void SetTransfer(uint address, int words, MemoryAccessWidth width)
    {
        RequireReady();

        memory.Write(Mailbox.Target, address);
        memory.Write(Mailbox.Count, (uint)words);
        memory.Write(Mailbox.Width, (uint)width);
    }

    private static void ValidateScalar(uint address, MemoryAccessWidth width)
    {
        int bytes = width switch
        {
            MemoryAccessWidth.Byte => 1,
            MemoryAccessWidth.Halfword => 2,
            MemoryAccessWidth.Word => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(width))
        };

        if (address % bytes != 0)
            throw new ArgumentException("Unaligned scalar address.", nameof(address));
    }
}
