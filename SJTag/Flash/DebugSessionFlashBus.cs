namespace SJTag;

public sealed class DebugSessionFlashBus : INorFlashBus
{
    private readonly DebugSession session;

    public DebugSessionFlashBus(DebugSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        this.session = session;
    }

    public uint ReadWord(uint address) => session.ReadScalar(address, MemoryAccessWidth.Word);
    public void WriteWord(uint address, uint value) => session.WriteScalar(address, value, MemoryAccessWidth.Word);

    public void WriteHalfword(uint address, ushort value) =>
        session.WriteScalar(address, value, MemoryAccessWidth.Halfword);

    public byte[] Read(uint address, int byteCount) => session.ReadMemory(address, byteCount);
}
