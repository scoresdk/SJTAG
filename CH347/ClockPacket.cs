using Jtag;

namespace CH347;

internal sealed record ClockPacket(byte Command, byte[] Payload, int Offset, int BitCount)
{
    public bool Captures => Command is 0xD2 or 0xD4;
    public int ResponseLength => !Captures
        ? 0
        : Command == 0xD4 ? BitCount / 8 : BitCount;

    public static List<ClockPacket> Create(ReadOnlySpan<JtagCycle> cycles, ReadOnlySpan<bool> capture = default)
    {
        var packets = new List<ClockPacket>();
        int offset = 0;
        bool lowTmsEstablished = false;

        while (offset < cycles.Length)
        {
            bool captures = capture.IsEmpty || capture[offset];
            int run = 0;

            if (lowTmsEstablished)
            {
                while (offset + run < cycles.Length && !cycles[offset + run].Tms && run < 504 * 8 &&
                    (capture.IsEmpty || capture[offset + run] == captures))
                    run++;
            }

            int packedBits = run & ~7;

            if (packedBits >= 8)
            {
                var bytes = new byte[packedBits / 8];

                for (int bit = 0; bit < packedBits; bit++)
                {
                    if (cycles[offset + bit].Tdi)
                        bytes[bit / 8] |= (byte)(1 << (bit % 8));
                }

                packets.Add(new(captures ? (byte)0xD4 : (byte)0xD3, bytes, offset, packedBits));
                offset += packedBits;

                continue;
            }

            int start = offset;

            do
            {
                lowTmsEstablished = !cycles[offset].Tms;
                offset++;

                if (!capture.IsEmpty && offset < cycles.Length && capture[offset] != captures)
                    break;

                if (lowTmsEstablished && offset + 8 <= cycles.Length)
                {
                    bool packable = true;

                    for (int i = 0; i < 8; i++)
                        packable &= !cycles[offset + i].Tms && (capture.IsEmpty || capture[offset + i] == captures);

                    if (packable)
                        break;
                }
            } while (offset < cycles.Length && offset - start < PacketCodec.MaximumCycles);

            packets.Add(new(captures ? (byte)0xD2 : (byte)0xD1,
                PacketCodec.Cycles(cycles.Slice(start, offset - start)), start, offset - start));
        }

        return packets;
    }
}
