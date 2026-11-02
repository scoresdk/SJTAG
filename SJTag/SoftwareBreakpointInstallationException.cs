namespace SJTag;

public sealed class SoftwareBreakpointInstallationException : IOException
{
    public SoftwareBreakpoint Breakpoint { get; }

    internal SoftwareBreakpointInstallationException(string message, SoftwareBreakpoint breakpoint, Exception inner)
        : base(message, inner) => Breakpoint = breakpoint;
}
