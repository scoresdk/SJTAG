namespace SJTag;

public enum ControlRegister
{
    ProcessorStatus = 0,
    Condition = 1,
    ExceptionCause = 2,
    ExceptionVector = 3,
    CacheControl = 4,
    ExceptionProgramCounter = 5,
    ExceptionMemoryAddress = 6,
    LocalInstructionFrame = 15,
    LocalDataFrame = 16,
    ProcessorRevision = 18,
    DebugStatus = 29,
    DebugProgramCounter = 30,
    DebugSave = 31
}
