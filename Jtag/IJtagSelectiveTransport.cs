namespace Jtag;

public interface IJtagSelectiveTransport : IJtagTransport
{
    byte[] ClockSelected(ReadOnlySpan<JtagCycle> cycles, ReadOnlySpan<bool> capture);
}
