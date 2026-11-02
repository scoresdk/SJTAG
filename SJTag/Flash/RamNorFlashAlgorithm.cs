namespace SJTag;

public sealed record RamNorFlashAlgorithm
{
    public RamProgram Program { get; }
    public uint StagingAddress { get; }
    public int OperationRegister { get; }
    public int CountRegister { get; }
    public int StagingRegister { get; }
    public int ResultByteCount { get; }

    public uint IdentifyOperation { get; }
    public uint EraseOperation { get; }
    public uint ProgramOperation { get; }

    public RamNorFlashAlgorithm(RamProgram program, uint stagingAddress, int resultByteCount = 24,
        int operationRegister = 4, int countRegister = 5, int stagingRegister = 7,
        uint identifyOperation = 0, uint eraseOperation = 1, uint programOperation = 2)
    {
        ArgumentNullException.ThrowIfNull(program);

        MemoryRange.Validate(stagingAddress, 4);
        MemoryRange.Validate(program.CompletionAddress, resultByteCount);

        int[] registers = [operationRegister, countRegister, stagingRegister];

        if (registers.Any(index => index is < 0 or > 31) || registers.Distinct().Count() != 3 ||
            new[] { identifyOperation, eraseOperation, programOperation }.Distinct().Count() != 3)
            throw new ArgumentException("Invalid RAM algorithm ABI.");

        Program = program;
        StagingAddress = stagingAddress;
        ResultByteCount = resultByteCount;

        OperationRegister = operationRegister;
        CountRegister = countRegister;
        StagingRegister = stagingRegister;

        IdentifyOperation = identifyOperation;
        EraseOperation = eraseOperation;
        ProgramOperation = programOperation;
    }
}
