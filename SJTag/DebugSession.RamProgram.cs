using System.Buffers.Binary;
using System.Diagnostics;

namespace SJTag;

public sealed partial class DebugSession
{
    public void HaltAgain(CancellationToken cancellationToken = default, bool allowUninitialized = false)
    {
        if (State != DebugSessionState.Running || client.GetStatus().DebugMode)
            throw new InvalidOperationException("Rehalt requires a running CPU.");

        if (client.GetStatus().AlternateServiceLanes != alternateLanes)
            throw new InvalidOperationException("CPU service lane profile changed.");

        memory = new ProbeMemory(profile.Image, alternateLanes);
        service = new ProcessorAccessService(client, memory);
        State = DebugSessionState.Servicing;

        try
        {
            client.HaltRaw(allowUninitialized);
            WaitReady(cancellationToken);
        }
        catch
        {
            State = DebugSessionState.Faulted;

            throw;
        }
    }

    public void RunRamProgram(RamProgram program, IReadOnlyDictionary<int, uint>? arguments = null,
        CancellationToken cancellationToken = default) =>
        RunRamProgram(program, arguments, cancellationToken, null, null, null);

    public void RunRamProgram(RamProgram program, IReadOnlyDictionary<int, uint>? arguments,
        CancellationToken cancellationToken, Action<uint>? progress, TimeSpan? pollInterval = null,
        Action? started = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        RequireReady();

        if (arguments is not null && arguments.Keys.Any(index => index is < 0 or > 31))
            throw new ArgumentException("Invalid argument register index.", nameof(arguments));

        TimeSpan interval = pollInterval ?? TimeSpan.FromMilliseconds(1);

        if (interval <= TimeSpan.Zero || interval.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(pollInterval), "Poll interval must fit Thread.Sleep and be positive.");

        if (progress is not null)
        {
            MemoryRange.Validate(program.CompletionAddress, 8);

            if ((ulong)program.CompletionAddress < (ulong)program.Address + (uint)program.Code.Length &&
                (ulong)program.Address < (ulong)program.CompletionAddress + 8)
                throw new ArgumentException("RAM program progress block overlaps executable code.", nameof(program));
        }

        WriteMemoryVerified(program.Address, program.Code.Span, executable: true, cancellationToken);

        WriteScalar(program.CompletionAddress, 0, MemoryAccessWidth.Word, cancellationToken);
        if (progress is not null)
            WriteScalar(program.CompletionAddress + 4, 0, MemoryAccessWidth.Word, cancellationToken);

        byte[] clearedResult = ReadMemory(program.CompletionAddress, progress is null ? 4 : 8, cancellationToken);

        if (clearedResult.Any(value => value != 0))
            throw new IOException("RAM program completion block did not clear and read back before execution.");

        if (arguments is not null)
        {
            foreach (var (index, value) in arguments)
                Context.WriteGeneral(index, value);
        }

        Context.ProgramCounter = new(program.Address);
        Context.SetSingleStep(false);
        Context.SetInterrupts(false);

        FlushAndInvalidateCaches(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        Return(false, CancellationToken.None, started);

        long start = Stopwatch.GetTimestamp();
        uint? previousProgress = null;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (client.GetStatus().DebugMode)
                    RehaltRamProgram();

                byte[] status = client.ReadMemory(program.CompletionAddress, progress is null ? 4 : 8);
                uint marker = BinaryPrimitives.ReadUInt32LittleEndian(status);

                if (progress is not null)
                {
                    uint completed = BinaryPrimitives.ReadUInt32LittleEndian(status.AsSpan(4));

                    if (completed != previousProgress)
                    {
                        progress(completed);
                        previousProgress = completed;
                    }
                }

                if (marker == program.SuccessMarker)
                    break;

                if (marker != 0)
                    throw new IOException($"RAM program failure marker 0x{marker:X8}.");

                if (Stopwatch.GetElapsedTime(start) >= program.Timeout)
                    throw new TimeoutException("RAM program completion timed out.");

                Thread.Sleep(interval);
            }
        }
        catch (Exception error)
        {
            if (State == DebugSessionState.Running)
            {
                try
                {
                    RehaltRamProgram();
                }
                catch (Exception recovery)
                {
                    throw new AggregateException("RAM program and rehalt failed.", error, recovery);
                }
            }

            throw;
        }

        RehaltRamProgram();

        if (ReadScalar(program.CompletionAddress, MemoryAccessWidth.Word) != program.SuccessMarker)
            throw new IOException("CPU and DMA completion markers disagree.");
    }

    private void RehaltRamProgram()
    {
        if (!client.GetStatus().DebugMode)
        {
            HaltAgain(allowUninitialized: client.Options.AllowUninitialized);

            return;
        }

        memory = new ProbeMemory(profile.Image, alternateLanes);
        service = new ProcessorAccessService(client, memory);
        State = DebugSessionState.Servicing;
        WaitReady(CancellationToken.None);

        throw new InvalidOperationException("RAM program entered debug unexpectedly.");
    }
}
