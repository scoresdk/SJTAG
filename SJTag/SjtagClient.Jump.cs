namespace SJTag;

public sealed partial class SjtagClient
{
    public void Jump(uint address, bool compact = false, bool resume = true,
        CancellationToken cancellationToken = default) =>
        Debug.Jump(address, compact, resume, cancellationToken);

    public void Jump(DebugProgramCounter destination, bool resume = true,
        CancellationToken cancellationToken = default) =>
        Debug.Jump(destination, resume, cancellationToken);

    public void JumpAndWait(uint address, bool compact = false, CancellationToken cancellationToken = default) =>
        Debug.JumpAndWait(address, compact, cancellationToken);
}
