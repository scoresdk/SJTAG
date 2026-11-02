namespace SJTag;

public readonly record struct DebugProgramCounter(uint Value)
{
    public uint Address => Value & ~1u;
    public bool CompactInstructionMode => (Value & 1) != 0;

    public override string ToString() =>
        $"DEPC=0x{Value:X8}, address=0x{Address:X8}, compact={CompactInstructionMode}";
}
