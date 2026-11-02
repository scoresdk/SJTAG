namespace Jtag;

/// <summary>IEEE 1149.1 TAP control. SJTAG uses its own reset-ending scans instead.</summary>
public sealed class JtagTap
{
    private readonly IJtagTransport transport;
    private TapState? state;
    public TapState? State => state;

    public JtagTap(IJtagTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        this.transport = transport;
    }

    public void Reset()
    {
        Clock(Enumerable.Repeat(new JtagCycle(true), 8).ToArray());
        state = TapState.TestLogicReset;
    }

    public void MoveTo(TapState target)
    {
        if (!Enum.IsDefined(target))
            throw new ArgumentOutOfRangeException(nameof(target));
        if (state is null)
            Reset();
        var queue = new Queue<(TapState State, JtagCycle[] Path)>();
        var visited = new HashSet<TapState>();
        queue.Enqueue((state!.Value, []));
        while (queue.TryDequeue(out var item))
        {
            if (!visited.Add(item.State))
                continue;
            if (item.State == target)
            {
                Clock(item.Path);
                state = target;
                return;
            }
            foreach (bool tms in new[] { false, true })
                queue.Enqueue((Next(item.State, tms), [.. item.Path, new JtagCycle(tms)]));
        }
        throw new InvalidOperationException("TAP state is unreachable.");
    }

    public byte[] ShiftInstruction(ReadOnlySpan<byte> bits, TapState endState = TapState.RunTestIdle) =>
        Shift(bits, true, endState);

    public byte[] ShiftData(ReadOnlySpan<byte> bits, TapState endState = TapState.RunTestIdle) =>
        Shift(bits, false, endState);

    private byte[] Shift(ReadOnlySpan<byte> bits, bool instruction, TapState endState)
    {
        if (bits.IsEmpty || !Enum.IsDefined(endState))
            throw new ArgumentException("A nonempty bit sequence and valid ending state are required.");
        var cycles = new JtagCycle[bits.Length];
        for (int i = 0; i < bits.Length; i++)
        {
            if (bits[i] > 1)
                throw new ArgumentException("Bits must be logical zero or one.", nameof(bits));
            cycles[i] = new JtagCycle(i == bits.Length - 1, bits[i] != 0);
        }
        MoveTo(instruction ? TapState.ShiftIr : TapState.ShiftDr);
        byte[] result = Clock(cycles);
        state = instruction ? TapState.Exit1Ir : TapState.Exit1Dr;
        MoveTo(endState);
        return result;
    }

    private byte[] Clock(ReadOnlySpan<JtagCycle> cycles)
    {
        try
        {
            byte[] bits = transport.Clock(cycles);
            if (bits.Length != cycles.Length || bits.Any(bit => bit > 1))
                throw new InvalidDataException("Transport returned invalid TDO bits.");
            return bits;
        }
        catch
        {
            state = null;
            throw;
        }
    }

    public static TapState Next(TapState state, bool tms) => state switch
    {
        TapState.TestLogicReset => tms ? TapState.TestLogicReset : TapState.RunTestIdle,
        TapState.RunTestIdle => tms ? TapState.SelectDrScan : TapState.RunTestIdle,
        TapState.SelectDrScan => tms ? TapState.SelectIrScan : TapState.CaptureDr,
        TapState.CaptureDr => tms ? TapState.Exit1Dr : TapState.ShiftDr,
        TapState.ShiftDr => tms ? TapState.Exit1Dr : TapState.ShiftDr,
        TapState.Exit1Dr => tms ? TapState.UpdateDr : TapState.PauseDr,
        TapState.PauseDr => tms ? TapState.Exit2Dr : TapState.PauseDr,
        TapState.Exit2Dr => tms ? TapState.UpdateDr : TapState.ShiftDr,
        TapState.UpdateDr => tms ? TapState.SelectDrScan : TapState.RunTestIdle,
        TapState.SelectIrScan => tms ? TapState.TestLogicReset : TapState.CaptureIr,
        TapState.CaptureIr => tms ? TapState.Exit1Ir : TapState.ShiftIr,
        TapState.ShiftIr => tms ? TapState.Exit1Ir : TapState.ShiftIr,
        TapState.Exit1Ir => tms ? TapState.UpdateIr : TapState.PauseIr,
        TapState.PauseIr => tms ? TapState.Exit2Ir : TapState.PauseIr,
        TapState.Exit2Ir => tms ? TapState.UpdateIr : TapState.ShiftIr,
        TapState.UpdateIr => tms ? TapState.SelectDrScan : TapState.RunTestIdle,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
}
