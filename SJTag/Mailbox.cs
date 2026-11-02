namespace SJTag;

public static class Mailbox
{
    public const uint Flag = 0xFF005000;
    public const uint Opcode = 0xFF005004;
    public const uint Status = 0xFF005008;
    public const uint FramePointer = 0xFF005010;
    public const uint TlbSaveBytes = 0xFF00502C;

    public const uint UnknownTlbGeometry = 0xFF0050CC;

    public const uint Target = 0xFF005080;
    public const uint Count = 0xFF005084;
    public const uint Width = 0xFF005088;

    public const uint Buffer = 0xFF006000;
}
