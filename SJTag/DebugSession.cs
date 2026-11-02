using System.Diagnostics;
using System.Text.Json;

namespace SJTag;

public sealed partial class DebugSession
{
    private readonly SjtagClient client;
    private readonly DebugServiceProfile profile;
    private readonly bool alternateLanes;

    private ProbeMemory memory;
    private ProcessorAccessService service;

    public DebugSessionState State { get; private set; }
    public DebugContext Context { get; }
    public DebugReason Reason => (DebugReason)(Context.ReadControl(29) & 0x7F);

    internal bool OwnsClient(SjtagClient candidate) => ReferenceEquals(client, candidate);

    private DebugSession(SjtagClient client, DebugServiceProfile profile, bool alternateLanes)
    {
        this.client = client;
        this.profile = profile;
        this.alternateLanes = alternateLanes;

        memory = new ProbeMemory(profile.Image, alternateLanes);
        service = new ProcessorAccessService(client, memory);

        Context = new DebugContext(this);
        State = DebugSessionState.Servicing;
    }

    public static DebugSession Halt(SjtagClient client, DebugServiceProfile profile,
        CancellationToken cancellationToken = default, bool allowUninitialized = false)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(profile);

        DebugStatus status = client.GetStatus();

        if (status.DebugMode)
            throw new InvalidOperationException("CPU already halted.");

        uint implementation = client.Transfer(Instruction.ImplementationCode, profile.ImplementationCode);

        if (implementation != profile.ImplementationCode)
            throw new InvalidOperationException($"Unexpected implementation 0x{implementation:X8}.");

        var session = new DebugSession(client, profile, status.AlternateServiceLanes);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            client.HaltRaw(allowUninitialized);
            session.WaitReady(cancellationToken);
        }
        catch (Exception error)
        {
            session.State = DebugSessionState.Faulted;

            throw new DebugSessionException("Debug entry failed.", session, error);
        }

        return session;
    }

    public static DebugSession AdoptHalted(SjtagClient client, DebugServiceProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(profile);

        DebugStatus status = client.GetStatus();

        if (!status.DebugMode)
            throw new InvalidOperationException("Adoption requires a CPU already halted in SJTAG debug mode.");

        if (!status.AccessPending)
            throw new InvalidOperationException(
                $"Adoption requires a pending DSR access; observed {status}.");

        uint pendingAddress = client.Transfer(Instruction.Address, 0);
        bool serviceAddress = pendingAddress is >= 0xFF000000 and < 0xFF008000;

        if (!serviceAddress || (pendingAddress >= 0xFF005000 && pendingAddress % 4 != 0))
            throw new InvalidOperationException(
                $"Adoption requires a pending access in the SJTAG DSR/mailbox window: observed {status}, pending address 0x{pendingAddress:X8}.");

        uint implementation = client.Transfer(Instruction.ImplementationCode, profile.ImplementationCode);

        if (implementation != profile.ImplementationCode)
            throw new InvalidOperationException($"Unexpected implementation 0x{implementation:X8}.");

        var session = new DebugSession(client, profile, status.AlternateServiceLanes);
        session.memory.Write(Mailbox.FramePointer, Mailbox.Buffer - 276);
        session.service.AdoptMailboxPolling();

        try
        {
            session.WaitReady(cancellationToken);

            IReadOnlyDictionary<string, uint> registers = SavedRegisters.Decode(session.memory.Words);

            if (registers.Values.All(value => value == 0))
                throw new InvalidDataException(
                    "Adopted DSR has no recoverable register context.");
        }
        catch (Exception error)
        {
            session.State = DebugSessionState.Faulted;

            throw new DebugSessionException(
                "Adoption of an existing halted DSR session failed.",
                session,
                error);
        }

        return session;
    }

    public DebugSessionSnapshot Snapshot() =>
        new(State, alternateLanes, memory.Words.ToDictionary(), service.Events,
            service.SawMailboxClear, service.Ready, profile.ImageSha256, profile.ImplementationCode);

    public void SaveSnapshot(string path)
    {
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write);

        JsonSerializer.Serialize(output, Snapshot(), new JsonSerializerOptions { WriteIndented = true });

        output.Flush(true);
    }

    public static DebugSession Restore(SjtagClient client, DebugServiceProfile profile,
        DebugSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.ServiceImageSha256 != profile.ImageSha256 ||
            snapshot.ImplementationCode != profile.ImplementationCode)
            throw new ArgumentException("Snapshot DSR image/profile identity does not match.", nameof(snapshot));

        var status = client.GetStatus();

        if (!status.DebugMode || status.AlternateServiceLanes != snapshot.AlternateServiceLanes ||
            snapshot.State == DebugSessionState.Running)
            throw new InvalidOperationException("Snapshot is incompatible with the current halted CPU.");

        var session = new DebugSession(client, profile, snapshot.AlternateServiceLanes);

        if (!Enum.IsDefined(snapshot.State) || snapshot.Words.Count != session.memory.Words.Count ||
            session.memory.Words.Keys.Any(address => !snapshot.Words.ContainsKey(address)) ||
            (snapshot.State == DebugSessionState.Ready && !snapshot.MailboxReady))
            throw new ArgumentException("Incomplete or inconsistent session snapshot.", nameof(snapshot));

        if (client.Transfer(Instruction.ImplementationCode, profile.ImplementationCode) != profile.ImplementationCode)
            throw new InvalidOperationException("Snapshot target implementation does not match.");

        foreach (var (address, value) in snapshot.Words)
            session.memory.Write(address, value);

        if (snapshot.State == DebugSessionState.Ready)
        {
            _ = SavedRegisters.Decode(session.memory.Words);
        }

        session.service.RestoreReadiness(snapshot.Events, snapshot.SawMailboxClear, snapshot.MailboxReady);
        session.State = snapshot.State;

        return session;
    }

    public void ContinueService(CancellationToken cancellationToken = default)
    {
        if (State == DebugSessionState.Running)
            throw new InvalidOperationException("CPU is running.");

        State = DebugSessionState.Servicing;
        WaitReady(cancellationToken);
    }

    internal ProbeMemory ReadyMemory
    {
        get
        {
            RequireReady();

            return memory;
        }
    }

    private void RequireReady()
    {
        client.ThrowIfReleased();

        if (State != DebugSessionState.Ready || !service.Ready)
            throw new InvalidOperationException($"Debug session is not ready ({State})!");
    }

    private void WaitReady(CancellationToken cancellationToken)
    {
        long start = Stopwatch.GetTimestamp();

        try
        {
            while (!service.Ready)
            {
                CheckDeadline(start, cancellationToken);
                service.ServiceOne();
            }

            _ = SavedRegisters.Decode(memory.Words);
            State = DebugSessionState.Ready;
        }
        catch
        {
            State = DebugSessionState.Faulted;

            throw;
        }
    }

    private void Command(uint opcode, CancellationToken cancellationToken)
    {
        RequireReady();
        cancellationToken.ThrowIfCancellationRequested();

        memory.Write(Mailbox.Opcode, opcode);
        memory.Write(Mailbox.Flag, 1);

        service.ResetReadiness();
        State = DebugSessionState.Servicing;

        WaitReady(cancellationToken);

        uint status = memory.Read(Mailbox.Status);

        if (status != 1)
            throw new InvalidOperationException($"DSR opcode {opcode} returned status {status}.");
    }

    private void CheckDeadline(long start, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Stopwatch.GetElapsedTime(start) >= profile.CommandTimeout)
            throw new TimeoutException("DSR service timed out;.");
    }
}

public sealed class DebugSessionException : Exception
{
    public DebugSession Session { get; }

    internal DebugSessionException(string message, DebugSession session, Exception inner)
        : base(message, inner) => Session = session;
}
