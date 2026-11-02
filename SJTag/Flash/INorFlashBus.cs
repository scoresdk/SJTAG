namespace SJTag;

public interface INorFlashBus
{
    uint ReadWord(uint address);
    void WriteWord(uint address, uint value);

    void WriteHalfword(uint address, ushort value);

    byte[] Read(uint address, int byteCount);
}
