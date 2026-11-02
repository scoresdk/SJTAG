namespace SJTag;

public static class ControlRegisterFields
{
    public static class ProcessorStatus
    {
        public static RegisterField InterruptsEnabled { get; } = new(ControlRegister.ProcessorStatus, "IEc", 0);
        public static RegisterField UserMode { get; } = new(ControlRegister.ProcessorStatus, "UMc", 1);
        public static RegisterField PreviousInterruptsEnabled { get; } = new(ControlRegister.ProcessorStatus, "IEs", 2);
        public static RegisterField PreviousUserMode { get; } = new(ControlRegister.ProcessorStatus, "UMs", 3);

        public static RegisterField BigEndian { get; } = new(ControlRegister.ProcessorStatus, "Endian", 15, writable: false);
        public static RegisterField SoftwareInterruptMask { get; } = new(ControlRegister.ProcessorStatus, "IM[1:0]", 16, 2);
        public static RegisterField HardwareInterruptThreshold { get; } = new(ControlRegister.ProcessorStatus, "IM[7:2]", 18, 6);
        public static RegisterField InterruptMask { get; } = new(ControlRegister.ProcessorStatus, "IM", 16, 8);

        public static RegisterField UserControlAccess { get; } = new(ControlRegister.ProcessorStatus, "CRA", 28);
        public static RegisterField CoprocessorEnable { get; } = new(ControlRegister.ProcessorStatus, "CU", 29, 3);
    }

    public static class Condition
    {
        public static RegisterField Overflow { get; } = new(ControlRegister.Condition, "Vc", 0);
        public static RegisterField Carry { get; } = new(ControlRegister.Condition, "Cc", 1);
        public static RegisterField Zero { get; } = new(ControlRegister.Condition, "Zc", 2);
        public static RegisterField Negative { get; } = new(ControlRegister.Condition, "Nc", 3);
        public static RegisterField ParallelCondition { get; } = new(ControlRegister.Condition, "Tc", 4);

        public static RegisterField PreviousOverflow { get; } = new(ControlRegister.Condition, "Vs", 5);
        public static RegisterField PreviousCarry { get; } = new(ControlRegister.Condition, "Cs", 6);
        public static RegisterField PreviousZero { get; } = new(ControlRegister.Condition, "Zs", 7);
        public static RegisterField PreviousNegative { get; } = new(ControlRegister.Condition, "Ns", 8);
        public static RegisterField PreviousParallelCondition { get; } = new(ControlRegister.Condition, "Ts", 9);
    }

    public static class Exception
    {
        public static RegisterField Code { get; } = new(ControlRegister.ExceptionCause, "Exc_code", 0, 5, false);
        public static RegisterField UnavailableCoprocessor { get; } = new(ControlRegister.ExceptionCause, "CE", 6, 2, false);
        public static RegisterField SoftwareInterruptPending { get; } = new(ControlRegister.ExceptionCause, "IP[1:0]", 16, 2);
        public static RegisterField HardwareInterruptPriority { get; } =
            new(ControlRegister.ExceptionCause, "IP[7:2]", 18, 6, false);

        public static RegisterField VectorWideStride { get; } = new(ControlRegister.ExceptionVector, "VO", 0);
        public static RegisterField VectorBase { get; } = new(ControlRegister.ExceptionVector, "EXCPVec_Base", 16, 16);

        public static RegisterField CompactMode { get; } = new(ControlRegister.ExceptionProgramCounter, "M", 0, writable: false);
        public static RegisterField Address { get; } = new(ControlRegister.ExceptionProgramCounter, "EPC", 1, 31, false);
    }

    public static class Cache
    {
        public static RegisterField WriteBufferEnabled { get; } = new(ControlRegister.CacheControl, "WB", 0, writable: false);

        public static RegisterField LocalInstructionEnabled { get; } = new(ControlRegister.CacheControl, "LIM", 2);
        public static RegisterField LocalDataEnabled { get; } = new(ControlRegister.CacheControl, "LDM", 3);

        public static RegisterField BurstEarlyTermination { get; } = new(ControlRegister.CacheControl, "BTEN", 4);
        public static RegisterField NopBubble { get; } = new(ControlRegister.CacheControl, "NOP", 5);
        public static RegisterField ReadBypassDisabled { get; } = new(ControlRegister.CacheControl, "RdBpDis", 6);

        public static RegisterField WriteBackEnabled { get; } = new(ControlRegister.CacheControl, "W-Back", 7, writable: false);
        public static RegisterField InstructionPrefetchEnabled { get; } =
            new(ControlRegister.CacheControl, "IPFB", 8, writable: false);
        public static RegisterField DataPrefetchEnabled { get; } = new(ControlRegister.CacheControl, "DPFB", 9, writable: false);
    }

    public static class Debug
    {
        public static RegisterField SingleStepHit { get; } = new(ControlRegister.DebugStatus, "DSS", 0, writable: false);
        public static RegisterField SoftwareBreakpointHit { get; } = new(ControlRegister.DebugStatus, "DBP", 1, writable: false);
        public static RegisterField LoadAddressHit { get; } = new(ControlRegister.DebugStatus, "DDBLA", 2, writable: false);
        public static RegisterField StoreHit { get; } = new(ControlRegister.DebugStatus, "DDBS", 3, writable: false);
        public static RegisterField InstructionBreakpointHit { get; } =
            new(ControlRegister.DebugStatus, "DIB", 4, writable: false);
        public static RegisterField InterruptHit { get; } = new(ControlRegister.DebugStatus, "DINT", 5, writable: false);
        public static RegisterField LoadValueHit { get; } = new(ControlRegister.DebugStatus, "DDBLV", 6, writable: false);

        public static RegisterField SingleStepEnabled { get; } = new(ControlRegister.DebugStatus, "SSEn", 8);
        public static RegisterField ExceptionCode { get; } = new(ControlRegister.DebugStatus, "DExcCode", 9, 5, false);

        public static RegisterField DataBusErrorPending { get; } =
            new(ControlRegister.DebugStatus, "DBusEP", 24, writable: false);
        public static RegisterField InstructionBusErrorPending { get; } =
            new(ControlRegister.DebugStatus, "IBusEP", 25, writable: false);
        public static RegisterField DebugMode { get; } = new(ControlRegister.DebugStatus, "DM", 26, writable: false);
        public static RegisterField ProbeEnabled { get; } = new(ControlRegister.DebugStatus, "ProbeEn", 27, writable: false);
        public static RegisterField Initialized { get; } = new(ControlRegister.DebugStatus, "InitOk", 28);
        public static RegisterField IceDis { get; } = new(ControlRegister.DebugStatus, "IceDis", 29);

        public static RegisterField CompactMode { get; } = new(ControlRegister.DebugProgramCounter, "M", 0, writable: false);
        public static RegisterField Address { get; } = new(ControlRegister.DebugProgramCounter, "DEPC", 1, 31);
    }

    public static RegisterField LocalInstructionFrame { get; } =
        new(ControlRegister.LocalInstructionFrame, "LIMPFN", 12, 20, false);
    public static RegisterField LocalDataFrame { get; } = new(ControlRegister.LocalDataFrame, "LDMPFN", 12, 20, false);

    public static RegisterField ProcessorRevision { get; } = new(ControlRegister.ProcessorRevision, "Prev", 0, 24, false);
}
