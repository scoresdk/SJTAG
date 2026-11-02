using System.Diagnostics;

namespace SJTag;

public sealed partial class SjtagClient : IDisposable
{
    public const uint ExpectedImplementation = 0x11112401;

    private readonly IJtagTransport transport;
    private readonly TimeSpan timeout;
    private readonly DebugServiceProfile? serviceProfile;

    public bool IsReleased { get; private set; }
    public DmaScanMode DmaScanMode { get; }
    public SjtagClientOptions Options { get; }

    public SjtagClient(
        IJtagTransport transport,
        TimeSpan? timeout = null,
        DebugServiceProfile? serviceProfile = null,
        DmaScanMode dmaScanMode = DmaScanMode.VendorReset)
        : this(transport, new SjtagClientOptions
        {
            Timeout = timeout,
            ServiceProfile = serviceProfile,
            DmaScanMode = dmaScanMode
        })
    {
    }

    public SjtagClient(IJtagTransport transport, SjtagClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(transport);

        if (!Enum.IsDefined(options.DmaScanMode))
            throw new ArgumentOutOfRangeException(nameof(options), "Invalid DMA scan mode.");

        this.transport = transport;
        Options = options;
        DmaScanMode = options.DmaScanMode;
        timeout = options.Timeout ?? TimeSpan.FromSeconds(3);

        if (this.timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Timeout must be positive.");

        serviceProfile = options.ServiceProfile ?? (EmbeddedDebugService.IsAvailable ? EmbeddedDebugService.Profile : null);
    }

    public void ResetTap() => ClockWriteOnly(ScanSequence.Reset());

    public void Enable()
    {
        ResetTap();
        ClockWriteOnly(ScanSequence.Unlock());
        Transfer(Instruction.Control, ControlRequest.Enable);

        RequireImplementation();
    }

    public uint Scan(uint value, int width)
    {
        var (cycles, offset) = ScanSequence.Create(value, width);

        return ScanSequence.Decode(Clock(cycles).AsSpan(offset, width));
    }

    public uint Transfer(Instruction instruction, uint value)
    {
        if (transport is IJtagSelectiveTransport)
        {
            var (irCycles, _) = ScanSequence.Create((uint)instruction, 5);
            var (drCycles, offset) = ScanSequence.Create(value, 32);

            var cycles = new JtagCycle[irCycles.Length + drCycles.Length];
            irCycles.CopyTo(cycles, 0);
            drCycles.CopyTo(cycles, irCycles.Length);

            var capture = new bool[cycles.Length];
            capture.AsSpan(irCycles.Length + offset, 32).Fill(true);

            return ScanSequence.Decode(ClockSelected(cycles, capture));
        }

        Scan((uint)instruction, 5);

        return Scan(value, 32);
    }

    public DebugStatus GetStatus() => new(Transfer(Instruction.Control, ControlRequest.Status));

    public uint RequireImplementation()
    {
        uint value = Transfer(Instruction.ImplementationCode, ExpectedImplementation);

        if (value != ExpectedImplementation)
            throw new InvalidOperationException(
                $"IMPCODE=0x{value:X8}, expected 0x{ExpectedImplementation:X8}. Pelease stop before halt/DMA.");

        return value;
    }

    public DebugStatus HaltRaw(bool allowUninitialized = false)
    {
        if (!allowUninitialized)
            RequireInitialized(GetStatus());

        DebugStatus status = Poll(ControlRequest.DebugInterrupt, static s => s.DebugMode, allowUninitialized);
        GetStatus();

        return status;
    }

    private DebugStatus Poll(uint request, Func<DebugStatus, bool> complete, bool allowUninitialized = false)
    {
        long start = Stopwatch.GetTimestamp();

        while (true)
        {
            var status = new DebugStatus(Transfer(Instruction.Control, request));

            if (!allowUninitialized)
                RequireInitialized(status);

            if (complete(status))
                return status;

            if (Stopwatch.GetElapsedTime(start) >= timeout)
                throw new TimeoutException($"SJTAG status polling timed out: {status}.");
        }
    }

    private byte[] Clock(ReadOnlySpan<JtagCycle> cycles)
    {
        ThrowIfReleased();

        byte[] samples = transport.Clock(cycles);

        if (samples.Length != cycles.Length)
            throw new InvalidDataException("Incorrect number of TDO samples.");

        return samples;
    }

    private byte[] ClockSelected(ReadOnlySpan<JtagCycle> cycles, ReadOnlySpan<bool> capture)
    {
        ThrowIfReleased();

        if (transport is not IJtagSelectiveTransport selective)
            throw new InvalidOperationException("Transport does not support selective capture.");

        byte[] samples = selective.ClockSelected(cycles, capture);

        if (samples.Length != capture.Count(true))
            throw new InvalidDataException("Incorrect number of selected TDO samples.");

        return samples;
    }

    private void ClockWriteOnly(ReadOnlySpan<JtagCycle> cycles)
    {
        if (transport is IJtagSelectiveTransport)
            ClockSelected(cycles, new bool[cycles.Length]);
        else
            Clock(cycles);
    }

    private byte[] ClockDmaBatch(ReadOnlySpan<JtagCycle> cycles, ReadOnlySpan<int> payloadOffsets)
    {
        if (DmaScanMode is DmaScanMode.Idle or DmaScanMode.Update)
        {
            var bounded = new JtagCycle[cycles.Length + 9];

            bounded[0] = new(false);
            cycles.CopyTo(bounded.AsSpan(1));
            ScanSequence.Reset().CopyTo(bounded, cycles.Length + 1);

            int[] shifted = payloadOffsets.ToArray();

            for (int i = 0; i < shifted.Length; i++)
                shifted[i]++;

            return ClockDmaPayload(bounded, shifted).AsSpan(1, cycles.Length).ToArray();
        }

        return ClockDmaPayload(cycles, payloadOffsets);
    }

    private byte[] ClockDmaPayload(ReadOnlySpan<JtagCycle> cycles, ReadOnlySpan<int> payloadOffsets)
    {
        if (transport is not IJtagSelectiveTransport)
            return Clock(cycles);

        var capture = new bool[cycles.Length];

        foreach (int offset in payloadOffsets)
            capture.AsSpan(offset, 32).Fill(true);

        byte[] selected = ClockSelected(cycles, capture);

        var result = new byte[cycles.Length];
        int index = 0;

        for (int i = 0; i < capture.Length; i++)
        {
            if (capture[i])
                result[i] = selected[index++];
        }

        return result;
    }

    private int AppendDmaTransfer(List<JtagCycle> cycles, Instruction ir, uint value, ref Instruction? previous)
    {
        if (DmaScanMode != DmaScanMode.CompactReset || previous != ir)
            cycles.AddRange(CreateDmaScan((uint)ir, 5).Cycles);

        var (dr, offset) = CreateDmaScan(value, 32);
        int sample = cycles.Count + offset;

        cycles.AddRange(dr);
        previous = ir;

        return sample;
    }

    private (JtagCycle[] Cycles, int Offset) CreateDmaScan(uint value, int width)
    {
        var (vendor, offset) = ScanSequence.Create(value, width);

        if (DmaScanMode == DmaScanMode.CompactReset)
            return (vendor[..^(width == 5 ? 1 : 2)], offset);

        if (DmaScanMode == DmaScanMode.VendorReset)
            return (vendor, offset);

        var idle = new JtagCycle[offset - 1 + width + 2];

        vendor.AsSpan(1, offset - 1 + width).CopyTo(idle);
        idle[^2] = new(true);
        idle[^1] = new(false);

        return (DmaScanMode == DmaScanMode.Update ? idle[..^1] : idle, offset - 1);
    }

    internal void ThrowIfReleased() => ObjectDisposedException.ThrowIf(IsReleased, this);

    private void RequireInitialized(DebugStatus status)
    {
        if (!Options.AllowUninitialized && !status.InitOk)
            throw new InvalidOperationException($"InitOk is clear: {status}. Board initialization is required.");
    }
}
