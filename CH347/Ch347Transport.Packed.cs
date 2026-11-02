using Jtag;

namespace CH347;

public sealed partial class Ch347Transport
{
    private byte[] ClockPacked(ReadOnlySpan<JtagCycle> cycles, ReadOnlySpan<bool> capture = default)
    {
        var result = new byte[capture.IsEmpty ? cycles.Length : capture.Count(true)];
        int resultOffset = 0;
        bool[]? padded = null;

        if (!capture.IsEmpty)
        {
            padded = capture.ToArray();

            for (int start = 0; start < cycles.Length;)
            {
                if (!capture[start] || cycles[start].Tms)
                {
                    start++;

                    continue;
                }

                int end = start;

                while (end < cycles.Length && capture[end] && !cycles[end].Tms)
                    end++;

                int padding = (8 - (end - start) % 8) % 8;
                int available = 0;

                while (available < padding && start - available > 1 &&
                    !capture[start - available - 1] && !cycles[start - available - 1].Tms &&
                    !cycles[start - available - 2].Tms)
                    available++;

                if (available == padding)
                    padded.AsSpan(start - padding, padding).Fill(true);

                start = end;
            }
        }

        var packets = ClockPacket.Create(cycles, padded is null ? capture : padded);

        for (int start = 0; start < packets.Count;)
        {
            int end = start,
                writeSize = 0,
                readSize = 0,
                bitCount = 0;

            while (end < packets.Count)
            {
                var next = packets[end];
                int responseSize = next.Captures ? next.ResponseLength + 3 : 0;

                if (writeSize + next.Payload.Length + 3 > (largerPackets ? 51200 : 512) ||
                    (!largerPackets && readSize + responseSize > 512) ||
                    (largerPackets && bitCount + (next.Captures ? next.BitCount : 0) > 4096))
                    break;

                writeSize += next.Payload.Length + 3;
                readSize += responseSize;
                bitCount += next.Captures ? next.BitCount : 0;
                end++;
            }

            if (end == start)
                throw new InvalidOperationException("CH347 clock packet exceeds USB transfer limits.");

            var write = new byte[writeSize];
            int offset = 0;

            for (int i = start; i < end; i++)
            {
                byte[] packet = PacketCodec.Command(packets[i].Command, packets[i].Payload);
                packet.CopyTo(write, offset);
                offset += packet.Length;
            }

            if (connection.BulkTransfer(0x06, write, 2000) != write.Length)
                throw new IOException("Short CH347 packed USB write.");

            var read = new byte[readSize];
            int received = 0;

            while (received < readSize)
            {
                var buffer = new byte[readSize - received];
                int count = connection.BulkTransfer(0x86, buffer, 2000);

                if (count <= 0 || count > buffer.Length)
                    throw new IOException("Invalid CH347 packed USB read length.");

                buffer.AsSpan(0, count).CopyTo(read.AsSpan(received));
                received += count;
            }

            offset = 0;

            for (int i = start; i < end; i++)
            {
                var packet = packets[i];

                if (!packet.Captures)
                    continue;

                int length = packet.ResponseLength + 3;
                var data = PacketCodec.Response(packet.Command, read.AsSpan(offset, length), packet.ResponseLength);

                for (int bit = 0; bit < packet.BitCount; bit++)
                {
                    if (capture.IsEmpty || capture[packet.Offset + bit])
                        result[resultOffset++] = packet.Command == 0xD4
                            ? (byte)((data[bit / 8] >> (bit % 8)) & 1)
                            : (byte)(data[bit] & 1);
                }

                offset += length;
            }

            start = end;
        }

        return result;
    }
}
