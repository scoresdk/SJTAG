using System.Buffers.Binary;

namespace SJTag;

public sealed partial class SjtagClient
{
    public uint ReadWord(uint address, int preincrement = 4)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(ReadMemory(address, sizeof(uint), preincrement));
    }

    public byte[] ReadMemory(uint address, int byteCount, int preincrement = 4)
    {
        ValidateDmaRange(address, byteCount, preincrement);

        InitializeDma(address, preincrement);

        var result = new byte[byteCount];

        for (int offset = 0; offset < byteCount; offset += 4)
        {
            Transfer(Instruction.Control, ControlRequest.DmaReadStart);
            Poll(ControlRequest.DmaReadPoll, static s => !s.DmaBusy);

            uint word = Transfer(Instruction.Data, ControlRequest.DmaReadPoll);
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset, 4), word);
        }

        return result;
    }

    public byte[] ReadMemoryBatched(uint address, int byteCount, int preincrement = 4, int batchWords = 64)
    {
        ValidateDmaRange(address, byteCount, preincrement);

        if (batchWords is < 1 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(batchWords));

        var wordCycles = new List<JtagCycle>(177);
        var payloadOffsets = new List<int>(3);

        (Instruction Ir, uint Value)[] transfers =
        [
            (Instruction.Control, ControlRequest.DmaReadStart),
            (Instruction.Control, ControlRequest.DmaReadPoll),
            (Instruction.Data, ControlRequest.DmaReadPoll)
        ];
        Instruction? previous = null;

        foreach (var (ir, value) in transfers)
        {
            payloadOffsets.Add(AppendDmaTransfer(wordCycles, ir, value, ref previous));
        }

        InitializeDma(address, preincrement);

        var result = new byte[byteCount];

        for (int offset = 0; offset < byteCount / 4;)
        {
            int words = Math.Min(batchWords, byteCount / 4 - offset);
            var cycles = new JtagCycle[words * wordCycles.Count];
            var captures = new int[words * 2];

            for (int i = 0; i < words; i++)
            {
                wordCycles.CopyTo(cycles, i * wordCycles.Count);
                captures[i * 2] = i * wordCycles.Count + payloadOffsets[1];
                captures[i * 2 + 1] = i * wordCycles.Count + payloadOffsets[2];
            }

            byte[] samples = ClockDmaBatch(cycles, captures);

            for (int i = 0; i < words; i++)
            {
                int start = i * wordCycles.Count;
                var status = new DebugStatus(ScanSequence.Decode(samples.AsSpan(start + payloadOffsets[1], 32)));

                if (status.DmaBusy || (!Options.AllowUninitialized && !status.InitOk))
                    throw new InvalidOperationException(
                        $"DMA batch incomplete at 0x{(ulong)address + (ulong)(offset + i) * 4:X8}: {status}; discard batch.");

                uint word = ScanSequence.Decode(samples.AsSpan(start + payloadOffsets[2], 32));
                BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan((offset + i) * 4, 4), word);
            }

            offset += words;
        }

        return result;
    }

    private void InitializeDma(uint address, int preincrement)
    {
        RequireInitialized(GetStatus());

        Transfer(Instruction.Control, ControlRequest.DmaInitialize);
        Transfer(Instruction.Address, address - (uint)preincrement);
    }

    private static void ValidateDmaRange(uint address, int byteCount, int preincrement)
    {
        MemoryRange.Validate(address, byteCount);

        if (preincrement is not (0 or 4) || address < (uint)preincrement)
            throw new ArgumentOutOfRangeException(nameof(preincrement), "Use 0 or 4 without address underflow.");
    }
}
