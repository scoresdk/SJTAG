using System.Security.Cryptography;

namespace SJTag;

public sealed class RamNorFlash
{
    private readonly SjtagClient client;
    private readonly DebugSession session;
    private readonly NorFlashProfile profile;
    private readonly RamNorFlashAlgorithm algorithm;

    public RamNorFlash(SjtagClient client, DebugSession session, NorFlashProfile profile,
        RamNorFlashAlgorithm algorithm)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(algorithm);

        if (!session.OwnsClient(client))
            throw new ArgumentException("DMA and debug session must share the same SJTAG client.", nameof(client));

        this.client = client;
        this.session = session;
        this.profile = profile;
        this.algorithm = algorithm;

        MemoryRange.Validate(algorithm.StagingAddress, profile.ByteCount);

        (uint Address, ulong Size)[] ranges =
        [
            (algorithm.StagingAddress, (ulong)profile.ByteCount),
            (algorithm.Program.Address, (ulong)algorithm.Program.Code.Length),
            (algorithm.Program.CompletionAddress, (ulong)algorithm.ResultByteCount),
            (profile.BaseAddress, (ulong)profile.ByteCount)
        ];

        for (int i = 0; i < ranges.Length; i++)
        {
            for (int j = i + 1; j < ranges.Length; j++)
            {
                if ((ulong)ranges[i].Address < (ulong)ranges[j].Address + ranges[j].Size &&
                    (ulong)ranges[j].Address < (ulong)ranges[i].Address + ranges[i].Size)
                    throw new ArgumentException("Flash, staging, code and result ranges must not overlap.");
            }
        }
    }

    public NorReprogramResult EraseAndReprogram(ReadOnlyMemory<byte> originalImage,
        Action<RamNorFlashBackup> persistBackup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(persistBackup);

        if (originalImage.Length != profile.ByteCount)
            throw new ArgumentException("Requires the complete original image.", nameof(originalImage));

        byte[] image = originalImage.ToArray();
        var registers = session.Context.ReadAll();
        var bus = new DebugSessionFlashBus(session);

        if (bus.ReadWord(profile.CommandControlAddress) != 0 || bus.ReadWord(profile.BankAddress) != 0)
            throw new InvalidOperationException("RAM flash workflow requires normal controller mode and bank zero.");

        uint? pins = profile.PinMuxAddress is uint pin ? bus.ReadWord(pin) : null;
        uint id = new NorFlash(bus, profile).Identify();

        if (id != profile.ExpectedId)
            throw new InvalidOperationException($"Unexpected flash ID 0x{id:X8}.");

        session.FlushAndInvalidateCaches(cancellationToken);

        var backup = new RamNorFlashBackup(image.ToArray(),
            client.ReadMemoryBatched(algorithm.StagingAddress, image.Length),
            session.ReadMemory(algorithm.Program.Address, algorithm.Program.Code.Length, cancellationToken),
            session.ReadMemory(algorithm.Program.CompletionAddress, algorithm.ResultByteCount, cancellationToken),
            registers, pins);

        bool eraseStarted = false;
        NorReprogramResult result;

        try
        {
            if (profile.PinMuxAddress is uint address)
                bus.WriteWord(address, pins!.Value | profile.PinMuxMask);

            Verify(image);
            Verify(image);

            persistBackup(backup with
            {
                OriginalFlash = backup.OriginalFlash.ToArray(),
                StagingRam = backup.StagingRam.ToArray(),
                CodeRam = backup.CodeRam.ToArray(),
                ResultRam = backup.ResultRam.ToArray(),
                Registers = backup.Registers.ToDictionary()
            });

            client.WriteMemoryBatched(algorithm.StagingAddress, image);
            session.FlushDataCache(cancellationToken);

            if (!image.AsSpan().SequenceEqual(client.ReadMemoryBatched(algorithm.StagingAddress, image.Length)) ||
                !image.AsSpan().SequenceEqual(session.ReadMemory(algorithm.StagingAddress, image.Length, cancellationToken)))
                throw new IOException("Staged RAM firmware verification failed.");

            Run(algorithm.IdentifyOperation, cancellationToken);

            eraseStarted = true;
            Run(algorithm.EraseOperation, cancellationToken);

            byte[] erased = client.ReadMemoryBatched(profile.BaseAddress, image.Length);

            if (erased.Any(value => value != 0xFF))
                throw new IOException("Entire NOR did not verify erased.");

            Run(algorithm.ProgramOperation, cancellationToken);

            Verify(image);
            Verify(image);

            if (!image.AsSpan().SequenceEqual(session.ReadMemory(profile.BaseAddress, image.Length, cancellationToken)))
                throw new IOException("CPU NOR verification failed.");

            result = new(id, Convert.ToHexStringLower(SHA256.HashData(image)), image.Length);
        }
        catch (Exception error)
        {
            if (eraseStarted)
                throw new NorRecoveryRequiredException(
                    "Retain halted session.", error);

            try
            {
                RestoreBackup(backup);
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("RAM flash operation and restoration both failed.", error, cleanupError);
            }

            throw;
        }

        RestoreBackup(backup);

        return result;
    }

    private void RestoreBackup(RamNorFlashBackup backup)
    {
        if (session.State != DebugSessionState.Ready)
            throw new InvalidOperationException("RAM restoration requires ready backing.");

        session.FlushDataCache();
        client.WriteMemoryBatched(algorithm.StagingAddress, backup.StagingRam);
        session.FlushAndInvalidateCaches();

        if (!backup.StagingRam.AsSpan().SequenceEqual(
            client.ReadMemoryBatched(algorithm.StagingAddress, backup.StagingRam.Length)))
            throw new IOException("Original staging RAM restoration failed.");

        if (!backup.StagingRam.AsSpan().SequenceEqual(
            session.ReadMemory(algorithm.StagingAddress, backup.StagingRam.Length)))
            throw new IOException("CPU staging RAM restoration verification failed.");

        session.WriteMemoryVerified(algorithm.Program.Address, backup.CodeRam, executable: true);
        session.WriteMemoryVerified(algorithm.Program.CompletionAddress, backup.ResultRam);

        var bus = new DebugSessionFlashBus(session);

        bus.WriteWord(profile.CommandControlAddress, 0);
        bus.WriteWord(profile.BankAddress, 0);
        if (profile.PinMuxAddress is uint address)
            bus.WriteWord(address, backup.PinMux!.Value);

        session.Context.Restore(backup.Registers);
    }

    private void Run(uint operation, CancellationToken token) =>
        session.RunRamProgram(algorithm.Program, new Dictionary<int, uint>
        {
            [algorithm.OperationRegister] = operation,
            [algorithm.CountRegister] = (uint)profile.ByteCount,
            [algorithm.StagingRegister] = algorithm.StagingAddress
        }, token);

    private void Verify(byte[] image)
    {
        if (!image.AsSpan().SequenceEqual(client.ReadMemoryBatched(profile.BaseAddress, image.Length)))
            throw new IOException("Full NOR readback does not match original image.");
    }
}
