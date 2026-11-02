namespace SJTag;

public sealed record RamNorFlashBackup(
    byte[] OriginalFlash,
    byte[] StagingRam,
    byte[] CodeRam,
    byte[] ResultRam,
    IReadOnlyDictionary<string, uint> Registers,
    uint? PinMux);
