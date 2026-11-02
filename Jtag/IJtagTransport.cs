namespace Jtag;

public interface IJtagTransport
{
    byte[] Clock(ReadOnlySpan<JtagCycle> cycles);
}
