namespace SJTag;

public sealed record RegisterField
{
    public ControlRegister Register { get; }
    public string Name { get; }
    public int Offset { get; }
    public int Width { get; }
    public bool Writable { get; }

    public uint ValueMask => Width == 32 ? uint.MaxValue : (1u << Width) - 1;
    public uint Mask => ValueMask << Offset;

    public RegisterField(ControlRegister register, string name, int offset, int width = 1, bool writable = true)
    {
        if ((uint)register >= 32)
            throw new ArgumentOutOfRangeException(nameof(register));

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (offset is < 0 or > 31 || width < 1 || width > 32 - offset)
            throw new ArgumentOutOfRangeException(nameof(width), "Field must fit within a 32-bit register.");

        Register = register;
        Name = name;
        Offset = offset;
        Width = width;
        Writable = writable;
    }

    public uint Extract(uint registerValue) => (registerValue >> Offset) & ValueMask;

    public uint Insert(uint registerValue, uint value)
    {
        if (!Writable)
            throw new InvalidOperationException($"{Register}.{Name} is documented read-only.");

        if (value > ValueMask)
            throw new ArgumentOutOfRangeException(nameof(value), "Value exceeds the field width.");

        return (registerValue & ~Mask) | (value << Offset);
    }
}
