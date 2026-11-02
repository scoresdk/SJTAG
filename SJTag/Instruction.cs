namespace SJTag;

public enum Instruction : byte
{
    IdCode = 0x01,
    ImplementationCode = 0x03,
    Address = 0x08,
    Data = 0x09,
    Control = 0x0A
}
