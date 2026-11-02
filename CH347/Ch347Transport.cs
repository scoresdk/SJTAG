using Jtag;

namespace CH347;

public sealed partial class Ch347Transport : IJtagSelectiveTransport, IDisposable
{
    private readonly IUsbConnection connection;
    private bool disposed;
    private readonly Ch347Options options;
    private readonly bool largerPackets;

    public bool LargerPacketsEnabled => largerPackets;
    public byte ClockIndex { get; }
    public int ClockFrequencyHz { get; }

    public static Ch347Transport Open(Ch347Options? options = null) => new(LibUsbConnection.Open(), options);

    public Ch347Transport(IUsbConnection connection, Ch347Options? options = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        this.connection = connection;
        this.options = options ?? new();

        try
        {
            if (this.options.LargerPackets)
                largerPackets = Exchange(0xD0, [0, 9, 0x22, 0x22, 0x22, 0x22], 1)[0] != 0;

            int[] frequencies = largerPackets
                ? [468750, 937500, 1875000, 3750000, 7500000, 15000000, 30000000, 60000000]
                : [1875000, 3750000, 7500000, 15000000, 30000000, 60000000];

            if (this.options.ClockFrequencyHz is int frequency)
            {
                if (this.options.ClockIndex != 0)
                    throw new ArgumentException("Specify clock frequency OR clock index.", nameof(options));

                int index = Array.IndexOf(frequencies, frequency);

                if (index < 0)
                    throw new ArgumentOutOfRangeException(nameof(options), "Clock frequency is not supported by this firmware.");

                ClockIndex = (byte)index;
            }
            else
                ClockIndex = this.options.ClockIndex;

            if (ClockIndex >= frequencies.Length)
                throw new ArgumentOutOfRangeException(nameof(options), "Clock index exceeds the supported firmware range.");

            ClockFrequencyHz = frequencies[ClockIndex];

            Exchange(0xD0, [0, ClockIndex, 0x22, 0x22, 0x22, 0x22], 1);
        }
        catch (Exception error)
        {
            try
            {
                Dispose();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("CH347 initialization and cleanup failed.", error, cleanupError);
            }

            throw;
        }
    }

    public byte[] Clock(ReadOnlySpan<JtagCycle> cycles)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (options.PackedTransfers)
            return ClockPacked(cycles);

        var samples = new byte[cycles.Length];

        for (int offset = 0; offset < cycles.Length;)
        {
            int count = Math.Min(PacketCodec.MaximumCycles, cycles.Length - offset);
            byte[] response = Exchange(0xD2, PacketCodec.Cycles(cycles.Slice(offset, count)), count);

            for (int i = 0; i < count; i++)
                samples[offset + i] = (byte)(response[i] & 1);

            offset += count;
        }

        return samples;
    }

    public byte[] ClockSelected(ReadOnlySpan<JtagCycle> cycles, ReadOnlySpan<bool> capture)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (capture.Length != cycles.Length)
            throw new ArgumentException("Capture mask must have one entry per cycle.", nameof(capture));

        if (options.PackedTransfers)
            return ClockPacked(cycles, capture);

        byte[] samples = Clock(cycles);

        var result = new byte[capture.Count(true)];
        int offset = 0;

        for (int i = 0; i < capture.Length; i++)
        {
            if (capture[i])
                result[offset++] = samples[i];
        }

        return result;
    }

    private byte[] Exchange(byte command, ReadOnlySpan<byte> payload, int expectedLength)
    {
        byte[] packet = PacketCodec.Command(command, payload);

        if (connection.BulkTransfer(0x06, packet, 2000) != packet.Length)
            throw new IOException("Short CH347 USB write.");

        var response = new byte[512];
        int count = connection.BulkTransfer(0x86, response, 2000);

        if (count < 0 || count > response.Length)
            throw new IOException("Invalid CH347 USB read length.");

        return PacketCodec.Response(command, response.AsSpan(0, count), expectedLength);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        connection.Dispose();
    }
}
