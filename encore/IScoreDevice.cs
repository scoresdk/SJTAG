using Jtag;
using SJTag;

namespace Encore;

public interface IScoreDevice
{
    public SjtagClient Mount(IJtagTransport t);
    public uint GetFlashSize();
    public uint GetRamSize();
}