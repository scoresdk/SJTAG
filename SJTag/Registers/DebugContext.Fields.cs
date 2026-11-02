namespace SJTag;

public sealed partial class DebugContext
{
    public SavedRegister General(int index) => CreateView(index, 32, ReadGeneral, WriteGeneral);
    public SavedRegister Control(int index) => CreateView(index, 32, ReadControl, WriteControl);
    public SavedRegister Control(ControlRegister register) => Control((int)register);

    public SavedRegister Special(int index) => CreateView(index, 3, ReadSpecial, WriteSpecial);

    public uint ReadControl(ControlRegister register) => ReadControl((int)register);
    public void WriteControl(ControlRegister register, uint value) => WriteControl((int)register, value);

    public uint ReadField(RegisterField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        return field.Extract(ReadControl(field.Register));
    }

    public void WriteField(RegisterField field, uint value)
    {
        ArgumentNullException.ThrowIfNull(field);

        WriteControl(field.Register, field.Insert(ReadControl(field.Register), value));
    }

    public bool ReadBit(RegisterField field)
    {
        RequireBit(field);

        return ReadField(field) != 0;
    }

    public void WriteBit(RegisterField field, bool value)
    {
        RequireBit(field);

        WriteField(field, value ? 1u : 0u);
    }

    private static SavedRegister CreateView(int index, int count, Func<int, uint> read, Action<int, uint> write)
    {
        if ((uint)index >= count)
            throw new ArgumentOutOfRangeException(nameof(index));

        return new(() => read(index), value => write(index, value));
    }

    private static void RequireBit(RegisterField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (field.Width != 1)
            throw new ArgumentException("Use field access for a multi-bit field.", nameof(field));
    }
}
