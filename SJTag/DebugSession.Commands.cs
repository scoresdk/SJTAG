namespace SJTag;

public sealed partial class DebugSession
{
    public void NoOperation(CancellationToken cancellationToken = default) => Command(7, cancellationToken);

    public uint SumWords(uint address, int wordCount, CancellationToken cancellationToken = default)
    {
        if (wordCount <= 0 || wordCount > 1024)
            throw new ArgumentOutOfRangeException(nameof(wordCount));

        MemoryRange.Validate(address, checked((wordCount + 1) * 4));

        SetTransfer(address, wordCount, MemoryAccessWidth.Word);
        Command(10, cancellationToken);

        return memory.Read(Mailbox.Buffer);
    }

    public uint[] ProbeCapabilities(CancellationToken cancellationToken = default)
    {
        Command(5, cancellationToken);

        return [memory.Read(0xFF0050C0), memory.Read(0xFF0050C4), memory.Read(0xFF0050C8)];
    }

    public void SaveWithCoprocessorsDisabled(CancellationToken cancellationToken = default)
    {
        RequireReady();

        foreach (uint address in new uint[] { 0xFF005030, 0xFF005034, 0xFF005038 })
            memory.Write(address, 0);

        Command(6, cancellationToken);
    }

    public void FlushDataCache(CancellationToken cancellationToken = default) => Command(4, cancellationToken);

    public void FlushAndInvalidateCaches(CancellationToken cancellationToken = default)
    {
        Command(4, cancellationToken);

        Command(3, cancellationToken);
    }
}
