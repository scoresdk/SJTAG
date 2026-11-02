namespace SJTag;

public sealed class NorRecoveryRequiredException : IOException
{
    public NorRecoveryRequiredException(string message, Exception inner) : base(message, inner) { }
}
