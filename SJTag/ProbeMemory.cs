namespace SJTag;

public sealed class ProbeMemory
{
    private readonly Dictionary<uint, byte> image;
    private readonly Dictionary<uint, uint> words = new();
    private readonly bool alternateLanes;

    public IReadOnlyDictionary<uint, uint> Words => words;

    public ProbeMemory(IReadOnlyDictionary<uint, byte> canonicalImage, bool alternateLanes)
    {
        ArgumentNullException.ThrowIfNull(canonicalImage);

        this.alternateLanes = alternateLanes;
        image = new Dictionary<uint, byte>(canonicalImage.Count);

        foreach (var (address, value) in canonicalImage)
        {
            if (address is < 0xFF000000 or >= 0xFF005000)
                throw new ArgumentOutOfRangeException(nameof(canonicalImage), "Service ROM address outside window.");

            image.Add(alternateLanes ? address ^ 3 : address, value);
        }

        for (uint address = 0xFF005000; address < 0xFF008000; address += 4)
            words.Add(address, 0);

        words[Mailbox.FramePointer] = Mailbox.Buffer;
        words.Remove(Mailbox.UnknownTlbGeometry);
    }

    public static ProbeMemory FromHex8(string path, bool alternateLanes) =>
        new(ServiceImage.LoadHex8(path), alternateLanes);

    public uint Read(uint address, int size = 4)
    {
        if (size is not (1 or 2 or 4) || address % size != 0)
            throw new ArgumentException("Unsupported processor read size/alignment.");

        if (address is >= 0xFF000000 and < 0xFF005000)
        {
            uint value = 0;

            for (int i = 0; i < size; i++)
            {
                if (!image.TryGetValue(address + (uint)i, out byte item))
                    throw new InvalidOperationException($"Service ROM hole at 0x{address + (uint)i:X8}.");

                value = alternateLanes
                    ? value | ((uint)item << (8 * i))
                    : (value << 8) | item;
            }

            return alternateLanes ? value : value << (8 * (4 - size));
        }

        if (size != 4 || !words.TryGetValue(address, out uint word))
            throw new InvalidOperationException($"Uninitialized mailbox read at 0x{address:X8}.");

        return word;
    }

    public void Write(uint address, uint value, int size = 4)
    {
        if (size != 4 || address % 4 != 0 || address is < 0xFF005000 or >= 0xFF008000 ||
            address == Mailbox.UnknownTlbGeometry)
            throw new InvalidOperationException($"Unsupported mailbox write at 0x{address:X8}.");

        words[address] = value;
    }
}
