namespace SJTag;

public sealed class SoftwareBreakpoint
{
    private readonly DebugSession session;
    private readonly MemoryAccessWidth width;
    private readonly uint original;
    private readonly uint opcode;

    public uint Address { get; }
    public bool Compact { get; }
    public bool Installed { get; private set; }
    public uint OriginalInstruction => original;

    private SoftwareBreakpoint(DebugSession session, uint address, bool compact)
    {
        this.session = session;
        Address = address;
        Compact = compact;

        width = compact ? MemoryAccessWidth.Halfword : MemoryAccessWidth.Word;
        uint mask = compact ? 0xFFFFu : uint.MaxValue;
        original = session.ReadScalar(address, width) & mask;
        opcode = compact ? 0x6002u : 0x80008006u;

        if (original == opcode)
            throw new InvalidOperationException("Instruction already contains a breakpoint.");
    }

    public static SoftwareBreakpoint Install(DebugSession session, uint address, bool compact = false)
    {
        var breakpoint = Prepare(session, address, compact);

        breakpoint.Install();

        return breakpoint;
    }

    public static SoftwareBreakpoint Prepare(DebugSession session, uint address, bool compact = false)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new SoftwareBreakpoint(session, address, compact);
    }

    public void Install()
    {
        if (Installed)
            throw new InvalidOperationException("Breakpoint is already installed or needs recovery.");

        if ((session.ReadScalar(Address, width) & (Compact ? 0xFFFFu : uint.MaxValue)) != original)
            throw new InvalidOperationException("Instruction changed since the breakpoint was prepared.");

        Installed = true;

        try
        {
            session.WriteScalar(Address, opcode, width);

            if ((session.ReadScalar(Address, width) & (Compact ? 0xFFFFu : uint.MaxValue)) != opcode)
                throw new IOException("Software breakpoint readback failed.");
        }
        catch (Exception error)
        {
            throw new SoftwareBreakpointInstallationException(
                "Breakpoint write may have happened.", this, error);
        }
    }

    public void Remove()
    {
        if (!Installed)
            throw new InvalidOperationException("Breakpoint is not installed.");

        uint current = session.ReadScalar(Address, width) & (Compact ? 0xFFFFu : uint.MaxValue);

        if (current == original)
        {
            Installed = false;

            return;
        }

        if (current != opcode)
            throw new InvalidOperationException("Breakpoint was changed by another owner.");

        session.WriteScalar(Address, original, width);

        if ((session.ReadScalar(Address, width) & (Compact ? 0xFFFFu : uint.MaxValue)) != original)
            throw new IOException("Original instruction restoration failed.");

        Installed = false;
    }
}
