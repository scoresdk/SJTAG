using Jtag;
using SJTag;

namespace Encore;

public class Hyperscan : IScoreDevice
{
    public uint GetFlashSize() => 0x100000;
    public uint GetRamSize() => 0x1000000;

    public SjtagClient Mount(IJtagTransport t)
    {
        ArgumentNullException.ThrowIfNull(t);

        SjtagClient sjtag = CreateClient(t);

        try
        {
            sjtag.Enable();
            sjtag.Halt();
        }
        catch (Exception e)
        {
            throw new DeviceNotReadyException(e.Message);
        }

        return sjtag;
    }

    public SjtagClient MountHaltedSession(IJtagTransport t)
    {
        ArgumentNullException.ThrowIfNull(t);
        SjtagClient sjtag = CreateClient(t);
        try
        {
            sjtag.Enable();
            sjtag.AdoptHaltedSession();
        }
        catch (Exception e)
        {
            throw new DeviceNotReadyException(e.Message);
        }
        return sjtag;
    }

    private static SjtagClient CreateClient(IJtagTransport t) =>
        new(t, new SjtagClientOptions
        {
            DmaScanMode = DmaScanMode.Update,
            AllowUninitialized = true
        });

    public override string ToString() => "Mattel Hyperscan";
}