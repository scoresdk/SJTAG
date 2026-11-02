namespace SJTag;

public sealed class RamProgram
{
    public uint Address { get; }
    public ReadOnlyMemory<byte> Code { get; }

    public uint CompletionAddress { get; }
    public uint SuccessMarker { get; }
    public TimeSpan Timeout { get; }

    public RamProgram(uint address, ReadOnlyMemory<byte> code, uint completionAddress,
        uint successMarker, TimeSpan timeout)
    {
        MemoryRange.Validate(address, code.Length);
        MemoryRange.Validate(completionAddress, 4);

        if (timeout <= TimeSpan.Zero || successMarker == 0 ||
            ((ulong)completionAddress < (ulong)address + (ulong)code.Length &&
             (ulong)completionAddress + 4 > address))
            throw new ArgumentException("Invalid RAM program completion region or timeout.");

        Address = address;
        Code = code.ToArray();

        CompletionAddress = completionAddress;
        SuccessMarker = successMarker;
        Timeout = timeout;
    }
}
