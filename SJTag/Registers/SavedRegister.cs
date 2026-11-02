namespace SJTag;

public sealed class SavedRegister
{
    private readonly Func<uint> read;
    private readonly Action<uint> write;

    internal SavedRegister(Func<uint> read, Action<uint> write)
    {
        this.read = read;
        this.write = write;
    }

    public uint Value
    {
        get => read();
        set => write(value);
    }

    public bool this[int bit]
    {
        get => (Value & BitMask(bit)) != 0;
        set => UpdateBits(BitMask(bit), value ? BitMask(bit) : 0);
    }

    public uint ReadField(int offset, int width) =>
        new RegisterField(ControlRegister.ProcessorStatus, "raw", offset, width).Extract(Value);

    public void WriteField(int offset, int width, uint value) =>
        Value = new RegisterField(ControlRegister.ProcessorStatus, "raw", offset, width).Insert(Value, value);

    public void SetBits(uint mask) => UpdateBits(mask, mask);
    public void ClearBits(uint mask) => UpdateBits(mask, 0);

    public void UpdateBits(uint mask, uint value)
    {
        if ((value & ~mask) != 0)
            throw new ArgumentException("Value contains bits outside the update mask.", nameof(value));

        Value = (Value & ~mask) | value;
    }

    private static uint BitMask(int bit)
    {
        if ((uint)bit >= 32)
            throw new ArgumentOutOfRangeException(nameof(bit));

        return 1u << bit;
    }
}
