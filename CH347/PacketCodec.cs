using System.Buffers.Binary;
using Jtag;

namespace CH347;

internal static class PacketCodec
{
    public const int MaximumCycles = 248;

    public static byte[] Command(byte command, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(payload));

        var packet = new byte[payload.Length + 3];

        packet[0] = command;
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(1, 2), (ushort)payload.Length);
        payload.CopyTo(packet.AsSpan(3));

        return packet;
    }

    public static byte[] Response(byte command, ReadOnlySpan<byte> packet, int expectedLength)
    {
        if (packet.Length != expectedLength + 3 || packet[0] != command ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(1, 2)) != expectedLength)
            throw new InvalidDataException($"Invalid CH347 response: {Convert.ToHexString(packet)}.");

        return packet[3..].ToArray();
    }

    public static byte[] Cycles(ReadOnlySpan<JtagCycle> cycles)
    {
        if (cycles.Length is < 1 or > MaximumCycles)
            throw new ArgumentOutOfRangeException(nameof(cycles));

        var payload = new byte[cycles.Length * 2 + 1];

        for (int i = 0; i < cycles.Length; i++)
        {
            byte low = (byte)(0x20 | (cycles[i].Tms ? 2 : 0) | (cycles[i].Tdi ? 0x10 : 0));

            payload[i * 2] = low;
            payload[i * 2 + 1] = (byte)(low | 1);
            payload[^1] = low;
        }

        return payload;
    }
}
