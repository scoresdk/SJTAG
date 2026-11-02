namespace SJTag;

public sealed partial class DebugSession
{
    public void Jump(uint address, bool compact = false, bool resume = true,
        CancellationToken cancellationToken = default) =>
        Jump(CreateJumpCounter(address, compact), resume, cancellationToken);

    public void Jump(DebugProgramCounter destination, bool resume = true,
        CancellationToken cancellationToken = default)
    {
        PrepareJump(destination, cancellationToken);

        if (resume)
            Resume(cancellationToken);
    }

    public void JumpAndWait(uint address, bool compact = false, CancellationToken cancellationToken = default)
    {
        PrepareJump(CreateJumpCounter(address, compact), cancellationToken);

        RunUntilDebugEntry(cancellationToken);
    }

    private void PrepareJump(DebugProgramCounter destination, CancellationToken cancellationToken)
    {
        RequireReady();

        if (!destination.CompactInstructionMode && destination.Address % 4 != 0)
            throw new ArgumentException("A 32-bit instruction destination must be word-aligned.", nameof(destination));

        cancellationToken.ThrowIfCancellationRequested();

        Context.ProgramCounter = destination;
        Context.SetSingleStep(false);
    }

    private static DebugProgramCounter CreateJumpCounter(uint address, bool compact)
    {
        if (address % (compact ? 2u : 4u) != 0)
            throw new ArgumentException("Pass an aligned byte address.", nameof(address));

        return new(address | (compact ? 1u : 0u));
    }
}
