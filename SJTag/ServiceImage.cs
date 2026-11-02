using System.Globalization;

namespace SJTag;

public static class ServiceImage
{
    public static IReadOnlyDictionary<uint, byte> LoadHex8(string path)
    {
        using var reader = File.OpenText(path);

        return LoadHex8(reader, path);
    }

    public static IReadOnlyDictionary<uint, byte> LoadHex8(TextReader reader, string sourceName = "service image")
    {
        ArgumentNullException.ThrowIfNull(reader);

        var image = new Dictionary<uint, byte>();
        int number = 0;

        while (reader.ReadLine() is string line)
        {
            number++;
            string text = line.Split("//", 2)[0].Trim();

            if (text.Length == 0)
                continue;

            string[] tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (!tokens[0].StartsWith('@') ||
                !uint.TryParse(tokens[0].AsSpan(1), NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture, out uint address))
                throw new FormatException($"{sourceName}:{number}: expected @address.");

            for (int i = 1; i < tokens.Length; i++)
            {
                if (!byte.TryParse(tokens[i], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte value) ||
                    address is < 0xFF000000 or >= 0xFF005000)
                    throw new FormatException($"{sourceName}:{number}: invalid service ROM byte.");

                if (!image.TryAdd(address, value))
                    throw new FormatException($"{sourceName}:{number}: duplicate ROM address.");

                address++;
            }
        }

        for (uint address = 0xFF000000; address < 0xFF000004; address++)
        {
            if (!image.ContainsKey(address))
                throw new FormatException("Service ROM has no complete entry instruction.");
        }

        return image;
    }
}
