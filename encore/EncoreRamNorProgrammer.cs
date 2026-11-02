using System.Buffers.Binary;
using System.Runtime.ExceptionServices;
using SJTag;

namespace Encore;

internal sealed class EncoreRamNorProgrammer
{
    private static readonly TimeSpan ProgressPollInterval = TimeSpan.FromMilliseconds(50);

    private readonly SjtagClient client;
    private readonly DebugSession session;
    private readonly NorFlash flash;
    private readonly NorFlashProfile flashProfile;
    private readonly EncoreRamNorProfile programProfile;

    public EncoreRamNorProgrammer(SjtagClient client, NorFlashProfile flashProfile, NorFlash flash)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(flashProfile);
        ArgumentNullException.ThrowIfNull(flash);
        this.client = client;
        session = client.Debug;
        this.flashProfile = flashProfile;
        this.flash = flash;
        programProfile = new EncoreRamNorProfile(flashProfile);
    }

    public int ProgramPrefix(byte[] desired, Action<int>? readProgress = null,
        Action<int>? writeProgress = null, Action<int>? verifyProgress = null,
        CancellationToken cancellationToken = default)
    {
        return FlashImageOperations.ProgramStaged(
            flash,
            desired,
            (image, progress, verification) =>
                ExecutePrepared(image, progress, verification, cancellationToken),
            readProgress,
            writeProgress,
            verifyProgress);
    }

    private void ExecutePrepared(ReadOnlyMemory<byte> desired, Action<int>? writeProgress,
        Action<int>? verifyProgress, CancellationToken cancellationToken)
    {
        byte[] image = desired.ToArray();
        cancellationToken.ThrowIfCancellationRequested();

        var registers = session.Context.ReadAll();
        var bus = new DebugSessionFlashBus(session);
        uint control = bus.ReadWord(flashProfile.CommandControlAddress);
        uint bank = bus.ReadWord(flashProfile.BankAddress);
        uint pinMux = bus.ReadWord(flashProfile.PinMuxAddress!.Value);
        if (control != 0 || bank != 0)
            throw new InvalidOperationException("Staged programming requires normal NOR controller mode and bank zero.");

        byte[] originalCode = client.ReadMemoryBatched(
            programProfile.Program.Address, programProfile.Program.Code.Length);
        byte[] originalResult = client.ReadMemoryBatched(
            programProfile.Program.CompletionAddress, EncoreRamNorProfile.ResultByteCount);
        byte[] originalStaging = client.ReadMemoryBatched(EncoreRamNorProfile.StagingAddress, image.Length);
        bool executionStarted = false;

        try
        {
            uint enabledPinMux = pinMux | flashProfile.PinMuxMask;
            bus.WriteWord(flashProfile.PinMuxAddress.Value, enabledPinMux);
            if (bus.ReadWord(flashProfile.PinMuxAddress.Value) != enabledPinMux)
                throw new IOException("NOR pin-mux setup readback failed.");

            client.WriteMemoryBatched(EncoreRamNorProfile.StagingAddress, image);
            if (!image.AsSpan().SequenceEqual(
                    client.ReadMemoryBatched(EncoreRamNorProfile.StagingAddress, image.Length)))
                throw new IOException("Staged firmware DMA verification failed.");
            session.FlushAndInvalidateCaches(cancellationToken);
            VerifyCpuStagingSamples(image);
            cancellationToken.ThrowIfCancellationRequested();

            session.RunRamProgram(
                programProfile.Program,
                new Dictionary<int, uint>
                {
                    [4] = EncoreRamNorProfile.ProgramOperation,
                    [5] = checked((uint)image.Length),
                    [7] = EncoreRamNorProfile.StagingAddress
                },
                cancellationToken,
                progress: completed =>
                {
                    if (completed > image.Length)
                        throw new IOException($"Target reported invalid progress {completed} for {image.Length} bytes.");
                    writeProgress?.Invoke(checked((int)completed));
                },
                pollInterval: ProgressPollInterval,
                started: () => executionStarted = true);

            FlashImageOperations.Verify(
                image,
                flash.Read(0, image.Length, verifyProgress, cancellationToken));
        }
        catch (Exception error)
        {
            if (executionStarted)
            {
                if (session.State == DebugSessionState.Ready)
                {
                    byte[] result;
                    try
                    {
                        result = session.ReadMemory(
                            programProfile.Program.CompletionAddress, EncoreRamNorProfile.ResultByteCount);
                    }
                    catch (Exception diagnosticError)
                    {
                        throw new AggregateException(
                            "Target programming failed and its result block could not be read; preserve the halted session.",
                            error,
                            diagnosticError);
                    }

                    uint marker = BinaryPrimitives.ReadUInt32LittleEndian(result);
                    if (marker == 0xBAD00001)
                    {
                        uint id = BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(8));
                        try
                        {
                            RestoreState(
                                originalStaging, originalCode, originalResult, pinMux, control, bank, registers);
                        }
                        catch (Exception restoreError)
                        {
                            throw new AggregateException(
                                $"NOR ID check failed (reported 0x{id:X8}); the flash was not programmed, but halted state restoration failed.",
                                error,
                                restoreError);
                        }
                        throw new IOException(
                            $"NOR ID check failed (reported 0x{id:X8}); no flash programming was performed.",
                            error);
                    }
                }

                throw CreateRecoveryException(error);
            }

            try
            {
                RestoreState(originalStaging, originalCode, originalResult, pinMux, control, bank, registers);
            }
            catch (Exception restoreError)
            {
                throw new AggregateException(
                    "Staged write setup failed and the original RAM/controller state could not be restored; keep the CPU halted.",
                    error,
                    restoreError);
            }

            ExceptionDispatchInfo.Capture(error).Throw();
            throw;
        }

        RestoreState(originalStaging, originalCode, originalResult, pinMux, control, bank, registers);
    }

    private void VerifyCpuStagingSamples(byte[] image)
    {
        int[] offsets = [0, (image.Length / 2) & ~3, image.Length - 4];
        foreach (int offset in offsets.Distinct())
        {
            byte[] actual = session.ReadMemory(EncoreRamNorProfile.StagingAddress + (uint)offset, 4);
            if (!image.AsSpan(offset, 4).SequenceEqual(actual))
                throw new IOException($"CPU cannot read staged firmware at offset 0x{offset:X}.");
        }
    }

    private void RestoreState(byte[] staging, byte[] code, byte[] result, uint pinMux,
        uint control, uint bank, IReadOnlyDictionary<string, uint> registers)
    {
        var failures = new List<Exception>();
        TryRestore(() =>
        {
            client.WriteMemoryBatched(EncoreRamNorProfile.StagingAddress, staging);
            if (!staging.AsSpan().SequenceEqual(
                    client.ReadMemoryBatched(EncoreRamNorProfile.StagingAddress, staging.Length)))
                throw new IOException("Original staging RAM restoration failed.");
        }, failures);
        TryRestore(() =>
            session.WriteMemoryVerified(programProfile.Program.Address, code, executable: true), failures);
        TryRestore(() =>
            session.WriteMemoryVerified(programProfile.Program.CompletionAddress, result), failures);
        TryRestore(() => session.FlushAndInvalidateCaches(), failures);
        TryRestore(() => session.WriteScalar(flashProfile.CommandControlAddress, control, MemoryAccessWidth.Word), failures);
        TryRestore(() => session.WriteScalar(flashProfile.BankAddress, bank, MemoryAccessWidth.Word), failures);
        TryRestore(() =>
            session.WriteScalar(flashProfile.PinMuxAddress!.Value, pinMux, MemoryAccessWidth.Word), failures);
        TryRestore(() => session.Context.Restore(registers), failures);
        TryRestore(() =>
        {
            if (session.ReadScalar(flashProfile.CommandControlAddress, MemoryAccessWidth.Word) != control ||
                session.ReadScalar(flashProfile.BankAddress, MemoryAccessWidth.Word) != bank ||
                session.ReadScalar(flashProfile.PinMuxAddress!.Value, MemoryAccessWidth.Word) != pinMux)
                throw new IOException("NOR controller/pin-mux restoration readback failed.");
        }, failures);
        TryRestore(() =>
        {
            IReadOnlyDictionary<string, uint> restored = session.Context.ReadAll();
            if (registers.Any(entry => restored.GetValueOrDefault(entry.Key) != entry.Value))
                throw new IOException("CPU register-context restoration failed.");
        }, failures);

        if (failures.Count != 0)
            throw new AggregateException(
                "Flash operation completed but halted CPU RAM/controller restoration failed; do not resume.",
                failures);
    }

    private Exception CreateRecoveryException(Exception error)
    {
        if (session.State != DebugSessionState.Ready)
            return new NorRecoveryRequiredException(
                "Target programming may be partial. The CPU/session state could not be confirmed; do not resume or power-cycle.",
                error);

        try
        {
            byte[] result = session.ReadMemory(
                programProfile.Program.CompletionAddress, EncoreRamNorProfile.ResultByteCount);
            uint marker = BinaryPrimitives.ReadUInt32LittleEndian(result);
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(8));
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(12));
            uint expected = BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(16));
            uint actual = BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(20));
            string detail = marker switch
            {
                0xBAD00001 => $"NOR ID check failed (reported 0x{id:X8}).",
                0xBAD00002 => $"NOR verify failed at byte offset 0x{offset:X} (expected 0x{expected:X4}, got 0x{actual:X4}).",
                _ => $"Target completion marker is 0x{marker:X8}."
            };
            return new NorRecoveryRequiredException(
                $"Target programming may be partial. CPU remains halted with the staged image and routine preserved. {detail}",
                error);
        }
        catch (Exception diagnosticError)
        {
            return new AggregateException(
                "Target programming may be partial. CPU remains halted; staged RAM is preserved, but target diagnostics could not be read.",
                error,
                diagnosticError);
        }
    }

    private static void TryRestore(Action action, List<Exception> failures)
    {
        try
        {
            action();
        }
        catch (Exception error)
        {
            failures.Add(error);
        }
    }
}
