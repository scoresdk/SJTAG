namespace SJTag;

public sealed class HardwareComparators
{
    private readonly DebugSession session;
    private readonly ComparatorProfile profile;

    public HardwareComparators(DebugSession session, ComparatorProfile profile)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);

        this.session = session;
        this.profile = profile;
    }

    public InstructionComparator ReadInstruction(int slot)
    {
        uint address = Address(slot, true);

        return new(Read(address), Read(address + 4), Read(address + 8), Read(address + 12));
    }

    public DataComparator ReadData(int slot)
    {
        uint address = Address(slot, false);

        return new(Read(address), Read(address + 4), Read(address + 8), Read(address + 12), Read(address + 16));
    }

    public void ConfigureInstruction(int slot, InstructionComparator configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        uint address = Address(slot, true);
        ValidateControl(configuration.Control, profile.InstructionControlMask);

        Write(address + 12, 0);

        Write(address, configuration.Address);
        Write(address + 4, configuration.AddressMask);
        Write(address + 8, configuration.Auxiliary);

        Write(address + 12, configuration.Control);

        if (ReadInstruction(slot) != configuration)
            throw new IOException("Instruction comparator readback mismatch.");
    }

    public void ConfigureData(int slot, DataComparator configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        uint address = Address(slot, false);
        ValidateControl(configuration.Control, profile.DataControlMask);

        Write(address + 12, 0);

        Write(address, configuration.Address);
        Write(address + 4, configuration.AddressMask);
        Write(address + 8, configuration.Auxiliary);
        Write(address + 16, configuration.CompareValue);

        Write(address + 12, configuration.Control);

        if (ReadData(slot) != configuration)
            throw new IOException("Data comparator readback mismatch.");
    }

    public void DisableInstruction(int slot) => Write(Address(slot, true) + 12, 0);

    public void DisableData(int slot) => Write(Address(slot, false) + 12, 0);

    private uint Address(int slot, bool instruction)
    {
        int count = instruction ? profile.InstructionSlots : profile.DataSlots;

        if ((uint)slot >= count)
            throw new ArgumentOutOfRangeException(nameof(slot), "Slot has not been established by the device profile.");

        return (instruction ? profile.InstructionBase : profile.DataBase) + (uint)slot * profile.SlotStride;
    }

    private static void ValidateControl(uint value, uint mask)
    {
        if ((value & ~mask) != 0)
            throw new ArgumentException("Control contains unsupported bits.");
    }

    private uint Read(uint address) => session.ReadScalar(address, MemoryAccessWidth.Word);
    private void Write(uint address, uint value) => session.WriteScalar(address, value, MemoryAccessWidth.Word);
}
