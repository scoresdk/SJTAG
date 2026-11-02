using System.Buffers.Binary;
using System.Security.Cryptography;

namespace SJTag;

public sealed class DebugServiceProfile
{
    public uint ImplementationCode { get; }
    public IReadOnlyDictionary<uint, byte> Image { get; }
    public TimeSpan CommandTimeout { get; }
    public string ImageSha256 { get; }

    public DebugServiceProfile(IReadOnlyDictionary<uint, byte> image,
        uint implementationCode = SjtagClient.ExpectedImplementation, TimeSpan? commandTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(image);

        for (uint address = 0xFF000000; address < 0xFF000004; address++)
        {
            if (!image.ContainsKey(address))
                throw new ArgumentException("DSR must contain its entry instruction.", nameof(image));
        }

        Image = new System.Collections.ObjectModel.ReadOnlyDictionary<uint, byte>(image.ToDictionary());

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> entry = stackalloc byte[5];

        foreach (var (address, value) in Image.OrderBy(pair => pair.Key))
        {
            BinaryPrimitives.WriteUInt32LittleEndian(entry, address);
            entry[4] = value;
            hash.AppendData(entry);
        }

        ImageSha256 = Convert.ToHexStringLower(hash.GetHashAndReset());

        ImplementationCode = implementationCode;
        CommandTimeout = commandTimeout ?? TimeSpan.FromSeconds(90);

        if (CommandTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(commandTimeout));

        _ = new ProbeMemory(Image, false);
    }

    public static DebugServiceProfile FromHex8(string path, TimeSpan? commandTimeout = null) =>
        new(ServiceImage.LoadHex8(path), commandTimeout: commandTimeout);
}
