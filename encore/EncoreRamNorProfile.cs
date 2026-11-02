using System.Reflection;
using SJTag;

namespace Encore;

internal sealed class EncoreRamNorProfile
{
    public const uint ProgramAddress = 0xA0001000;
    public const uint ResultAddress = 0xA0001800;
    public const uint StagingAddress = 0xA0100000;
    public const int ResultByteCount = 24;
    public const uint SuccessMarker = 0xC0DEC0DE;
    public const uint ProgramOperation = 2;

    public RamProgram Program { get; }

    public EncoreRamNorProfile(NorFlashProfile flash)
    {
        ArgumentNullException.ThrowIfNull(flash);
        if (flash.BaseAddress != 0x9E000000 || flash.ByteCount != 0x100000 ||
            flash.CommandControlAddress != 0x880700BC || flash.BankAddress != 0x880700C4 ||
            flash.PinMuxAddress != 0x88200004 || flash.PinMuxMask != (1u << 24) ||
            flash.ExpectedId != 0x278100BF)
            throw new ArgumentException("The staged Encore programmer requires the verified SST800 profile.", nameof(flash));

        using Stream image = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Encore.NorProgram.bin")
            ?? throw new InvalidOperationException("The assembled Encore NOR program resource is missing.");
        if (image.Length is <= 0 or > 0x800)
            throw new InvalidDataException("The assembled Encore NOR program must fit before its result block.");

        byte[] code = new byte[checked((int)image.Length)];
        image.ReadExactly(code);
        Program = new RamProgram(
            ProgramAddress,
            code,
            ResultAddress,
            SuccessMarker,
            TimeSpan.FromHours(2));
        if ((ulong)ProgramAddress + (uint)code.Length > ResultAddress ||
            RangesOverlap(StagingAddress, (uint)flash.ByteCount, ProgramAddress, (uint)code.Length) ||
            RangesOverlap(StagingAddress, (uint)flash.ByteCount, ResultAddress, ResultByteCount) ||
            (ulong)StagingAddress + (uint)flash.ByteCount > (ulong)uint.MaxValue + 1 ||
            (ulong)ResultAddress + ResultByteCount > (ulong)uint.MaxValue + 1)
            throw new InvalidOperationException("Encore NOR code, result, and staging ranges overlap or exceed the address space.");
    }

    private static bool RangesOverlap(uint first, uint firstSize, uint second, uint secondSize) =>
        (ulong)first < (ulong)second + secondSize &&
        (ulong)second < (ulong)first + firstSize;
}
