using System.Buffers.Binary;

namespace SJTag;

public sealed partial class SjtagClient
{
    public void WriteWord(uint address, uint value, int preincrement = 4)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];

        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);

        WriteMemory(address, bytes, preincrement);
    }

    public void WriteMemory(uint address, ReadOnlySpan<byte> data, int preincrement = 4)
    {
        ValidateDmaRange(address, data.Length, preincrement);
        RequireHaltedForWrite();

        InitializeDma(address, preincrement);

        for (int offset = 0; offset < data.Length; offset += 4)
        {
            Transfer(Instruction.Data, BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4)));
            Transfer(Instruction.Control, ControlRequest.DmaWriteStart);

            Poll(ControlRequest.DmaWritePoll, status =>
            {
                RequireHaltedForWrite(status);

                return !status.DmaBusy;
            });
        }
    }

    public void WriteMemoryBatched(uint address, ReadOnlySpan<byte> data, int preincrement = 4,
        int batchWords = 64)
    {
        ValidateDmaRange(address, data.Length, preincrement);

        if (batchWords is < 1 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(batchWords));

        RequireHaltedForWrite();
        InitializeDma(address, preincrement);

        for (int offset = 0; offset < data.Length;)
        {
            int words = Math.Min(batchWords, (data.Length - offset) / 4);
            var cycles = new List<JtagCycle>(words * 177);
            var polls = new List<int>(words);

            for (int i = 0; i < words; i++)
            {
                Instruction? previous = null;
                uint word = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + i * 4, 4));

                foreach (var (ir, value) in new[]
                {
                    (Instruction.Data, word),
                    (Instruction.Control, ControlRequest.DmaWriteStart),
                    (Instruction.Control, ControlRequest.DmaWritePoll)
                })
                {
                    int sample = AppendDmaTransfer(cycles, ir, value, ref previous);

                    if (ir == Instruction.Control && value == ControlRequest.DmaWritePoll)
                        polls.Add(sample);
                }
            }

            byte[] samples = ClockDmaBatch(cycles.ToArray(), polls.ToArray());

            foreach (int sample in polls)
            {
                var status = new DebugStatus(ScanSequence.Decode(samples.AsSpan(sample, 32)));

                RequireInitialized(status);
                RequireHaltedForWrite(status);

                if (status.DmaBusy)
                    throw new InvalidOperationException(
                        $"DMA write batch incomplete near 0x{address + (uint)offset:X8}: {status}. Target may be partially written.");
            }

            offset += words * 4;
        }
    }

    private void RequireHaltedForWrite() => RequireHaltedForWrite(GetStatus());

    private static void RequireHaltedForWrite(DebugStatus status)
    {
        if (!status.DebugMode)
            throw new InvalidOperationException($"Raw DMA writes require a halted CPU: {status}.");
    }
}
