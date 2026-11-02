namespace SJTag;

internal static class ControlRequest
{
    public const uint Enable = 0x00020800;
    public const uint Status = 0x000A0800;
    public const uint Acknowledge = 0x000A0000;
    public const uint DebugInterrupt = 0x000B0800;

    public const uint DmaInitialize = 0x000A0840;
    public const uint DmaReadStart = 0x000A087C;
    public const uint DmaReadPoll = 0x000A084C;
    public const uint DmaWriteStart = 0x000A087E;
    public const uint DmaWritePoll = 0x000A084E;
}
