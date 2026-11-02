namespace SJTag;

public sealed partial class SjtagClient
{
    public DebugSession? ActiveSession { get; private set; }
    public DebugServiceProfile ServiceProfile => serviceProfile ?? EmbeddedDebugService.Profile;
    public DebugSession Debug => ActiveSession ??
        throw new InvalidOperationException("No owned debug session. Call Halt() or RestoreSession first.");

    public DebugContext Registers => Debug.Context;

    public DebugProgramCounter ProgramCounter
    {
        get => Registers.ProgramCounter;
        set => Registers.ProgramCounter = value;
    }

    public DebugSession Halt(CancellationToken cancellationToken = default, bool allowUninitialized = false) =>
        Halt(ServiceProfile, cancellationToken, allowUninitialized);

    public DebugSession Halt(DebugServiceProfile profile, CancellationToken cancellationToken = default,
        bool allowUninitialized = false)
    {
        RequireNoHaltedSession();

        try
        {
            ActiveSession = DebugSession.Halt(this, profile, cancellationToken, allowUninitialized);

            return ActiveSession;
        }
        catch (DebugSessionException error)
        {
            ActiveSession = error.Session;

            throw;
        }
    }

    public DebugSession AdoptHaltedSession(CancellationToken cancellationToken = default)
    {
        RequireNoHaltedSession();

        try
        {
            ActiveSession = DebugSession.AdoptHalted(this, ServiceProfile, cancellationToken);

            return ActiveSession;
        }
        catch (DebugSessionException error)
        {
            ActiveSession = error.Session;

            throw;
        }
    }

    public DebugSession RestoreSession(DebugServiceProfile profile, DebugSessionSnapshot snapshot)
    {
        RequireNoHaltedSession();

        ActiveSession = DebugSession.Restore(this, profile, snapshot);

        return ActiveSession;
    }

    public DebugSession RestoreSession(DebugSessionSnapshot snapshot) => RestoreSession(ServiceProfile, snapshot);

    public void Resume(CancellationToken cancellationToken = default) => Debug.Resume(cancellationToken);

    public DebugReason Step(CancellationToken cancellationToken = default) => Debug.Step(cancellationToken);

    public void RunUntilDebugEntry(CancellationToken cancellationToken = default) =>
        Debug.RunUntilDebugEntry(cancellationToken);

    public void HaltAgain(CancellationToken cancellationToken = default, bool allowUninitialized = false) =>
        Debug.HaltAgain(cancellationToken, allowUninitialized);

    public void ContinueService(CancellationToken cancellationToken = default) => Debug.ContinueService(cancellationToken);

    public void SaveSnapshot(string path) => Debug.SaveSnapshot(path);

    public DebugSessionSnapshot Snapshot() => Debug.Snapshot();

    public IReadOnlyDictionary<string, uint> ReadRegisters() => Registers.ReadAll();
    public void RestoreRegisters(IReadOnlyDictionary<string, uint> registers) => Registers.Restore(registers);

    public uint ReadRegister(int index) => Registers.ReadGeneral(index);
    public void WriteRegister(int index, uint value) => Registers.WriteGeneral(index, value);

    public uint ReadControlRegister(int index) => Registers.ReadControl(index);
    public void WriteControlRegister(int index, uint value) => Registers.WriteControl(index, value);

    public uint ReadControlRegister(ControlRegister register) => Registers.ReadControl((int)register);
    public void WriteControlRegister(ControlRegister register, uint value) =>
        Registers.WriteControl((int)register, value);

    public void SetSingleStep(bool enabled) => Registers.SetSingleStep(enabled);

    public void SetInterrupts(bool enabled) => Registers.SetInterrupts(enabled);

    public uint ReadControlField(RegisterField field) => Registers.ReadField(field);
    public void WriteControlField(RegisterField field, uint value) => Registers.WriteField(field, value);

    public bool ReadControlBit(RegisterField field) => Registers.ReadBit(field);
    public void WriteControlBit(RegisterField field, bool value) => Registers.WriteBit(field, value);

    private void RequireNoHaltedSession()
    {
        ThrowIfReleased();

        if (ActiveSession is not null && ActiveSession.State != DebugSessionState.Running)
            throw new InvalidOperationException("Client already owns halted backing.");
    }
}
