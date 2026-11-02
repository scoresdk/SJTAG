namespace SJTag;

public enum DebugSessionState
{
    Servicing,
    Ready,
    Running,
    Faulted
}

public sealed record DebugSessionSnapshot(
    DebugSessionState State,
    bool AlternateServiceLanes,
    Dictionary<uint, uint> Words,
    long Events,
    bool SawMailboxClear = false,
    bool MailboxReady = false,
    string? ServiceImageSha256 = null,
    uint? ImplementationCode = null);

public enum MemoryAccessWidth
{
    Byte = 0,
    Halfword = 1,
    Word = 3
}

[Flags]
public enum DebugReason : uint
{
    None = 0,
    SingleStep = 1,
    SoftwareBreakpoint = 2,
    LoadAddress = 4,
    Store = 8,
    InstructionBreakpoint = 16,
    DebugInterrupt = 32,
    LoadValue = 64
}
