namespace Jtag;

public readonly record struct JtagCycle
{
    public bool Tms { get; }
    public bool Tdi { get; }

    public JtagCycle(bool tms, bool tdi = false)
    {
        Tms = tms;
        Tdi = tdi;
    }
}
