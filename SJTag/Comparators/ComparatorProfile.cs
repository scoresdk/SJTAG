namespace SJTag;

public sealed record ComparatorProfile
{
    public uint InstructionBase { get; }
    public uint DataBase { get; }
    public uint SlotStride { get; }
    public int InstructionSlots { get; }
    public int DataSlots { get; }
    public uint InstructionControlMask { get; }
    public uint DataControlMask { get; }

    public ComparatorProfile(uint instructionBase, uint dataBase, uint slotStride,
        int instructionSlots, int dataSlots, uint instructionControlMask, uint dataControlMask)
    {
        if (instructionBase % 4 != 0 || dataBase % 4 != 0 || slotStride < 20 || slotStride % 4 != 0 ||
            instructionSlots < 0 || dataSlots < 0 ||
            (instructionSlots > 0 && (ulong)instructionBase + (ulong)(instructionSlots - 1) * slotStride + 16 > 0x100000000) ||
            (dataSlots > 0 && (ulong)dataBase + (ulong)(dataSlots - 1) * slotStride + 20 > 0x100000000))
            throw new ArgumentException("Invalid comparator layout.");

        InstructionBase = instructionBase;
        DataBase = dataBase;
        SlotStride = slotStride;

        InstructionSlots = instructionSlots;
        DataSlots = dataSlots;

        InstructionControlMask = instructionControlMask;
        DataControlMask = dataControlMask;
    }

    public static ComparatorProfile VerifiedSlotZero { get; } =
        new(0xFFF02000, 0xFFF04000, 0x100, 1, 1, 1, 0x000FEFC1);
}
