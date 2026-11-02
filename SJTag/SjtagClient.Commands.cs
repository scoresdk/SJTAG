namespace SJTag;

public sealed partial class SjtagClient
{
    public uint ReadIdCode() => Transfer(Instruction.IdCode, 0);
    public uint ReadImplementationCode() => Transfer(Instruction.ImplementationCode, 0);

    public void NoOperation(CancellationToken cancellationToken = default) => Debug.NoOperation(cancellationToken);

    public void FlushDataCache(CancellationToken cancellationToken = default) => Debug.FlushDataCache(cancellationToken);

    public void FlushAndInvalidateCaches(CancellationToken cancellationToken = default) =>
        Debug.FlushAndInvalidateCaches(cancellationToken);

    public uint[] ProbeCapabilities(CancellationToken cancellationToken = default) => Debug.ProbeCapabilities(cancellationToken);

    public void SaveWithCoprocessorsDisabled(CancellationToken cancellationToken = default) =>
        Debug.SaveWithCoprocessorsDisabled(cancellationToken);

    public uint SumWords(uint address, int wordCount, CancellationToken cancellationToken = default) =>
        Debug.SumWords(address, wordCount, cancellationToken);

    public void ExecuteRawCommand(uint opcode, IReadOnlyDictionary<uint, uint> parameters,
        bool allowUnverified, CancellationToken cancellationToken = default) =>
        Debug.ExecuteRawCommand(opcode, parameters, allowUnverified, cancellationToken);

    public uint ReadServiceWord(uint address) => Debug.ReadServiceWord(address);

    public void RunRamProgram(RamProgram program, IReadOnlyDictionary<int, uint>? arguments = null,
        CancellationToken cancellationToken = default) =>
        Debug.RunRamProgram(program, arguments, cancellationToken);

    public void RunRamProgram(RamProgram program, IReadOnlyDictionary<int, uint>? arguments,
        CancellationToken cancellationToken, Action<uint>? progress, TimeSpan? pollInterval = null,
        Action? started = null) =>
        Debug.RunRamProgram(program, arguments, cancellationToken, progress, pollInterval, started);

    public HardwareComparators GetComparators(ComparatorProfile profile) => new(Debug, profile);

    public SoftwareBreakpoint PrepareSoftwareBreakpoint(uint address, bool compact = false) =>
        SoftwareBreakpoint.Prepare(Debug, address, compact);

    public SoftwareBreakpoint InstallSoftwareBreakpoint(uint address, bool compact = false) =>
        SoftwareBreakpoint.Install(Debug, address, compact);
}
