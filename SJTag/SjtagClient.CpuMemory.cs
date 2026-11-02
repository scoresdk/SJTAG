namespace SJTag;

public sealed partial class SjtagClient
{
    public byte[] ReadCpuMemory(uint address, int byteCount, CancellationToken cancellationToken = default) =>
        Debug.ReadMemory(address, byteCount, cancellationToken);

    public byte[] ReadCpuBytes(uint address, int byteCount, CancellationToken cancellationToken = default) =>
        Debug.ReadBytes(address, byteCount, cancellationToken);

    public uint ReadCpuScalar(uint address, MemoryAccessWidth width, CancellationToken cancellationToken = default) =>
        Debug.ReadScalar(address, width, cancellationToken);

    public byte ReadByte(uint address, CancellationToken cancellationToken = default) =>
        unchecked((byte)ReadCpuScalar(address, MemoryAccessWidth.Byte, cancellationToken));

    public ushort ReadHalfword(uint address, CancellationToken cancellationToken = default) =>
        unchecked((ushort)ReadCpuScalar(address, MemoryAccessWidth.Halfword, cancellationToken));

    public uint ReadCpuWord(uint address, CancellationToken cancellationToken = default) =>
        ReadCpuScalar(address, MemoryAccessWidth.Word, cancellationToken);

    public void WriteByte(uint address, byte value, CancellationToken cancellationToken = default) =>
        WriteCpuScalar(address, value, MemoryAccessWidth.Byte, cancellationToken);

    public void WriteHalfword(uint address, ushort value, CancellationToken cancellationToken = default) =>
        WriteCpuScalar(address, value, MemoryAccessWidth.Halfword, cancellationToken);

    public void WriteCpuWord(uint address, uint value, CancellationToken cancellationToken = default) =>
        WriteCpuScalar(address, value, MemoryAccessWidth.Word, cancellationToken);

    public void WriteCpuScalar(uint address, uint value, MemoryAccessWidth width,
        CancellationToken cancellationToken = default) =>
        Debug.WriteScalar(address, value, width, cancellationToken);

    public void WriteCpuMemory(uint address, ReadOnlySpan<byte> data, bool executable = false,
        CancellationToken cancellationToken = default) =>
        Debug.WriteMemory(address, data, executable, cancellationToken);

    public void WriteCpuMemoryVerified(uint address, ReadOnlySpan<byte> data, bool executable = false,
        CancellationToken cancellationToken = default) =>
        Debug.WriteMemoryVerified(address, data, executable, cancellationToken);

    public void WriteCpuBytes(uint address, ReadOnlySpan<byte> data, bool executable = false,
        CancellationToken cancellationToken = default) =>
        Debug.WriteBytes(address, data, executable, cancellationToken);

    public void WriteCpuBytesVerified(uint address, ReadOnlySpan<byte> data, bool executable = false,
        CancellationToken cancellationToken = default) =>
        Debug.WriteBytesVerified(address, data, executable, cancellationToken);
}
