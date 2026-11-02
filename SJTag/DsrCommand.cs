namespace SJTag;

public enum DsrCommand : uint
{
    Return = 0,
    ReadMemory = 1,
    WriteMemory = 2,
    InvalidateCaches = 3,
    FlushDataCache = 4,
    ProbeCapabilities = 5,
    SaveCoprocessors = 6,
    NoOperation = 7,
    WriteCoprocessor = 8,
    SelfLoop = 9,
    SumWords = 10
}
