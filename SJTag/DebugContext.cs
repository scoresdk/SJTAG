namespace SJTag;

public sealed partial class DebugContext
{
    private readonly DebugSession session;

    internal DebugContext(DebugSession session) => this.session = session;

    private uint Frame
    {
        get
        {
            var memory = session.ReadyMemory;

            _ = SavedRegisters.Decode(memory.Words);

            return memory.Read(Mailbox.FramePointer);
        }
    }

    public IReadOnlyDictionary<string, uint> ReadAll() => SavedRegisters.Decode(session.ReadyMemory.Words);

    public uint ReadGeneral(int index) => session.ReadyMemory.Read(Address(index, 32, 0x94));
    public void WriteGeneral(int index, uint value) => session.ReadyMemory.Write(Address(index, 32, 0x94), value);

    public uint ReadControl(int index) => session.ReadyMemory.Read(Address(index, 32, 0x14));
    public void WriteControl(int index, uint value) => session.ReadyMemory.Write(Address(index, 32, 0x14), value);

    public uint ReadSpecial(int index) => session.ReadyMemory.Read(Address(index, 3, 0));
    public void WriteSpecial(int index, uint value) => session.ReadyMemory.Write(Address(index, 3, 0), value);

    public uint Ceh
    {
        get => session.ReadyMemory.Read(Frame + 12);
        set => session.ReadyMemory.Write(Frame + 12, value);
    }

    public uint Cel
    {
        get => session.ReadyMemory.Read(Frame + 16);
        set => session.ReadyMemory.Write(Frame + 16, value);
    }

    public DebugProgramCounter ProgramCounter
    {
        get => new(ReadControl(30));
        set => WriteControl(30, value.Value);
    }

    public void SetSingleStep(bool enabled) =>
        WriteBit(ControlRegisterFields.Debug.SingleStepEnabled, enabled);

    public void SetInterrupts(bool enabled) =>
        WriteBit(ControlRegisterFields.ProcessorStatus.InterruptsEnabled, enabled);

    public void Restore(IReadOnlyDictionary<string, uint> registers)
    {
        ArgumentNullException.ThrowIfNull(registers);

        var names = new List<string> { "sr0", "sr1", "sr2", "ceh", "cel" };
        names.AddRange(Enumerable.Range(0, 32).Select(i => $"cr{i}"));
        names.AddRange(Enumerable.Range(0, 32).Select(i => $"r{i}"));

        _ = ReadAll();

        if (registers.Count != names.Count || names.Any(name => !registers.ContainsKey(name)))
            throw new ArgumentException("Requires all 69 saved slots.", nameof(registers));

        uint frame = Frame;

        for (int i = 0; i < names.Count; i++)
            session.ReadyMemory.Write(frame + (uint)i * 4, registers[names[i]]);
    }

    private uint Address(int index, int count, uint offset)
    {
        if ((uint)index >= count)
            throw new ArgumentOutOfRangeException(nameof(index));

        return Frame + offset + (uint)index * 4;
    }
}
