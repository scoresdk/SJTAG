namespace SJTag;

public sealed partial class DebugSession
{
    public void ExecuteRawCommand(uint opcode, IReadOnlyDictionary<uint, uint> parameters,
        bool allowUnverified, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        RequireReady();

        if (!allowUnverified)
            throw new InvalidOperationException("Raw DSR commands require explicit unverified-operation consent.");

        if (opcode == 0)
            throw new ArgumentException("Use Resume/RunUntilDebugEntry for return and backing ownership.", nameof(opcode));

        foreach (uint address in parameters.Keys)
        {
            if (address % 4 != 0 || address is Mailbox.Flag or Mailbox.Opcode or Mailbox.FramePointer or Mailbox.TlbSaveBytes ||
                !memory.Words.ContainsKey(address))
                throw new ArgumentException($"Invalid or lifecycle-owned backing address 0x{address:X8}.", nameof(parameters));
        }

        cancellationToken.ThrowIfCancellationRequested();

        foreach (var (address, value) in parameters)
            memory.Write(address, value);

        Command(opcode, cancellationToken);
    }

    public uint ReadServiceWord(uint address)
    {
        RequireReady();

        if (address % 4 != 0 || !memory.Words.ContainsKey(address))
            throw new ArgumentOutOfRangeException(nameof(address), "Not a writable DSR backing word.");

        return memory.Read(address);
    }
}
