namespace SJTag;

public sealed class DmaFlashBus : INorFlashBus
{
    private readonly SjtagClient client;
    private readonly DebugSessionFlashBus controller;

    public DmaFlashBus(SjtagClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        this.client = client;
        controller = new DebugSessionFlashBus(client.Debug);
    }

    public uint ReadWord(uint address) => controller.ReadWord(address);
    public void WriteWord(uint address, uint value) => controller.WriteWord(address, value);

    public void WriteHalfword(uint address, ushort value) => controller.WriteHalfword(address, value);

    public byte[] Read(uint address, int byteCount) => client.ReadMemoryBatched(address, byteCount);
}
