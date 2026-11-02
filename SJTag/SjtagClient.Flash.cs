namespace SJTag;

public sealed partial class SjtagClient
{
    public NorFlash GetFlash(NorFlashProfile profile) => new(new DebugSessionFlashBus(Debug), profile);

    public RamNorFlash GetRamFlash(NorFlashProfile profile, RamNorFlashAlgorithm algorithm) =>
        new(this, Debug, profile, algorithm);

    public uint IdentifyFlash(NorFlashProfile profile) => GetFlash(profile).Identify();

    public void ResetFlash(NorFlashProfile profile) => GetFlash(profile).Reset();

    public byte[] ReadFlash(NorFlashProfile profile) => GetFlash(profile).ReadImage();
    public byte[] ReadFlash(NorFlashProfile profile, int byteOffset, int byteCount) =>
        GetFlash(profile).Read(byteOffset, byteCount);

    public byte[] ReadFlashFast(NorFlashProfile profile, Action<int>? progress = null,
        CancellationToken cancellationToken = default) =>
        new NorFlash(new DmaFlashBus(this), profile).ReadImage(progress, cancellationToken);

    public byte[] ReadFlashFast(NorFlashProfile profile, int byteOffset, int byteCount,
        Action<int>? progress = null, CancellationToken cancellationToken = default) =>
        new NorFlash(new DmaFlashBus(this), profile).Read(byteOffset, byteCount, progress, cancellationToken);

    public NorEraseResult EraseFlash(NorFlashProfile profile, ReadOnlyMemory<byte> originalImage,
        Action<byte[]> persistBackup, CancellationToken cancellationToken = default) =>
        GetFlash(profile).Erase(originalImage, persistBackup, cancellationToken);

    public NorReprogramResult EraseAndReprogramFlash(NorFlashProfile profile, ReadOnlyMemory<byte> originalImage,
        Action<byte[]> persistBackup, Action<int>? progress = null, CancellationToken cancellationToken = default) =>
        GetFlash(profile).EraseAndReprogram(originalImage, persistBackup, progress, cancellationToken);

    public NorReprogramResult ProgramFlash(NorFlashProfile profile, ReadOnlyMemory<byte> image,
        Action<int>? progress = null, CancellationToken cancellationToken = default) =>
        GetFlash(profile).ProgramImage(image, progress, cancellationToken);
}
