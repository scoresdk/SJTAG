namespace SJTag;

public readonly record struct DebugStatus(uint Value)
{
    public bool DmaBusy => (Value & (1u << 5)) != 0;
    public bool CpuWrite => (Value & (1u << 8)) != 0;
    public int AccessSize => (int)((Value >> 9) & 3) + 1;

    public bool AccessPending => (Value & (1u << 11)) != 0;
    public bool ProbeEnabled => (Value & (1u << 17)) != 0;
    public bool DebugMode => (Value & (1u << 18)) != 0;
    public bool ProcessorAccessEnabled => (Value & (1u << 19)) != 0;

    public bool AlternateServiceLanes => (Value & (1u << 20)) != 0;
    public bool InitOk => (Value & (1u << 21)) != 0;

    public override string ToString() => $"SJCR=0x{Value:X8}";
}
