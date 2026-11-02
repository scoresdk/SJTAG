using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;

namespace SJTag;

public sealed class NorFlash
{
    private readonly INorFlashBus bus;
    private readonly NorFlashProfile profile;

    public int ByteCount => profile.ByteCount;

    public NorFlash(INorFlashBus bus, NorFlashProfile profile)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(profile);

        this.bus = bus;
        this.profile = profile;
    }

    public byte[] ReadImage(Action<int>? progress = null, CancellationToken cancellationToken = default) =>
        Read(0, profile.ByteCount, progress, cancellationToken);

    public byte[] Read(int byteOffset, int byteCount, Action<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (byteOffset < 0 || byteCount <= 0 || byteOffset % 4 != 0 || byteCount % 4 != 0 ||
            (long)byteOffset + byteCount > profile.ByteCount)
            throw new ArgumentOutOfRangeException(nameof(byteCount), "Requires an aligned range inside the profiled NOR.");

        uint control = bus.ReadWord(profile.CommandControlAddress);
        uint bank = bus.ReadWord(profile.BankAddress);
        uint? pins = ReadPins();

        return WithRestoration(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            PreparePins();
            bus.WriteWord(profile.BankAddress, 0);
            bus.WriteWord(profile.CommandControlAddress, 0);

            var result = new byte[byteCount];

            for (int offset = 0; offset < byteCount; offset += 4096)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int size = Math.Min(4096, byteCount - offset);
                ReadBlock(byteOffset + offset, size).CopyTo(result, offset);
                progress?.Invoke(offset + size);
            }

            return result;
        }, () => RestoreController(control, bank, pins));
    }

    public NorEraseResult Erase(ReadOnlyMemory<byte> originalImage, Action<byte[]> persistBackup,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(persistBackup);

        if (originalImage.Length != profile.ByteCount)
            throw new ArgumentException("Requires a complete original image.", nameof(originalImage));

        byte[] image = originalImage.ToArray();
        uint control = bus.ReadWord(profile.CommandControlAddress);
        uint bank = bus.ReadWord(profile.BankAddress);
        uint? pins = ReadPins();

        if (control != 0 || bank != 0)
            throw new InvalidOperationException("Erase requires normal controller mode and bank zero.");

        bool eraseStarted = false;

        try
        {
            PreparePins();

            uint id = Identify();

            if (id != profile.ExpectedId)
                throw new InvalidOperationException($"Unexpected NOR ID 0x{id:X8}.");

            VerifyImage(image, cancellationToken);
            VerifyImage(image, cancellationToken);

            persistBackup(image.ToArray());

            cancellationToken.ThrowIfCancellationRequested();

            eraseStarted = true;
            EraseAndVerify(cancellationToken);

            return new(id, Convert.ToHexStringLower(SHA256.HashData(image)), image.Length);
        }
        catch (Exception error)
        {
            if (eraseStarted)
                throw new NorRecoveryRequiredException("Erase may be incomplete.", error);

            Finish(error, () => RestoreController(control, bank, pins));

            throw;
        }
    }

    public uint Identify()
    {
        uint control = bus.ReadWord(profile.CommandControlAddress);
        uint bank = bus.ReadWord(profile.BankAddress);
        uint? pins = ReadPins();
        bool enteredCommandMode = false;

        return WithRestoration(() =>
        {
            PreparePins();
            bus.WriteWord(profile.BankAddress, 0);
            enteredCommandMode = true;
            bus.WriteWord(profile.CommandControlAddress, 1);

            Unlock();
            Command(0x5555, 0x90);

            return bus.ReadWord(profile.BaseAddress);
        }, () =>
            {
                if (enteredCommandMode)
                    ResetArray();
            }, () => RestoreController(control, bank, pins));
    }

    public void Reset()
    {
        uint control = bus.ReadWord(profile.CommandControlAddress);
        uint bank = bus.ReadWord(profile.BankAddress);
        uint? pins = ReadPins();
        _ = WithRestoration(() =>
        {
            PreparePins();
            bus.WriteWord(profile.BankAddress, 0);
            bus.WriteWord(profile.CommandControlAddress, 1);

            ResetArray();

            return true;
        }, () => RestoreController(control, bank, pins));
    }

    public NorReprogramResult EraseAndReprogram(ReadOnlyMemory<byte> originalImage,
        Action<byte[]> persistBackup, Action<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(persistBackup);

        if (originalImage.Length != profile.ByteCount)
            throw new ArgumentException("Image must cover the entire profiled device.", nameof(originalImage));

        byte[] image = originalImage.ToArray();
        uint control = bus.ReadWord(profile.CommandControlAddress);
        uint bank = bus.ReadWord(profile.BankAddress);
        uint? pins = ReadPins();

        if (control != 0 || bank != 0)
            throw new InvalidOperationException("Flash operation requires normal read mode and bank zero.");

        bool eraseStarted = false;
        NorReprogramResult result;

        try
        {
            PreparePins();

            uint id = Identify();

            if (id != profile.ExpectedId)
                throw new InvalidOperationException($"NOR ID 0x{id:X8} does not match the profile.");

            for (int pass = 0; pass < 2; pass++)
                VerifyImage(image, cancellationToken);

            persistBackup(image.ToArray());

            cancellationToken.ThrowIfCancellationRequested();

            eraseStarted = true;
            EraseAndVerify(cancellationToken);

            ProgramWords(image, progress, cancellationToken);

            VerifyImage(image, cancellationToken);
            VerifyImage(image, cancellationToken);

            result = new(id, Convert.ToHexStringLower(SHA256.HashData(image)), image.Length);
        }
        catch (Exception error)
        {
            if (eraseStarted)
                throw new NorRecoveryRequiredException(
                    "NOR may be incomplete.", error);

            Finish(error, () => RestoreController(control, bank, pins));

            throw;
        }

        Finish(null, () => RestoreController(control, bank, pins));

        return result;
    }

    public NorReprogramResult ProgramImage(ReadOnlyMemory<byte> desiredImage,
        Action<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (desiredImage.Length != profile.ByteCount)
            throw new ArgumentException("Requires a complete profiled image.", nameof(desiredImage));

        return ProgramPrefix(desiredImage, progress, cancellationToken);
    }

    public NorEraseResult EraseChip(CancellationToken cancellationToken = default,
        Action<int>? verificationProgress = null, Action? eraseCompleted = null)
    {
        uint control = bus.ReadWord(profile.CommandControlAddress);
        uint bank = bus.ReadWord(profile.BankAddress);
        uint? pins = ReadPins();
        bool started = false;

        return WithRestoration(() =>
        {
            try
            {
                uint id = Identify();

                if (id != profile.ExpectedId)
                    throw new InvalidOperationException($"Unexpected NOR ID 0x{id:X8}.");

                cancellationToken.ThrowIfCancellationRequested();

                PreparePins();
                bus.WriteWord(profile.BankAddress, 0);
                started = true;
                EraseAndVerify(cancellationToken, verificationProgress, eraseCompleted);

                byte[] erased = Enumerable.Repeat((byte)0xFF, profile.ByteCount).ToArray();

                return new NorEraseResult(id, Convert.ToHexStringLower(SHA256.HashData(erased)), profile.ByteCount);
            }
            catch (Exception error) when (started)
            {
                throw new NorRecoveryRequiredException("Chip erase failed.", error);
            }
        }, () => RestoreController(control, bank, pins));
    }

    public NorReprogramResult ProgramPrefix(ReadOnlyMemory<byte> desiredImage,
        Action<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (desiredImage.Length <= 0 || desiredImage.Length % 4 != 0 || desiredImage.Length > profile.ByteCount)
            throw new ArgumentException(
                "Requires a nonempty 4-byte-aligned image within the profiled device.", nameof(desiredImage));

        byte[] image = desiredImage.ToArray();
        uint control = bus.ReadWord(profile.CommandControlAddress);
        uint bank = bus.ReadWord(profile.BankAddress);
        uint? pins = ReadPins();

        if (control != 0 || bank != 0)
            throw new InvalidOperationException("Recovery requires normal read mode and bank zero.");

        bool programStarted = false;
        NorReprogramResult result;

        try
        {
            PreparePins();

            uint id = Identify();

            if (id != profile.ExpectedId)
                throw new InvalidOperationException($"Unexpected NOR ID 0x{id:X8}.");

            byte[] current = ReadBlock(0, image.Length);

            for (int i = 0; i < image.Length; i++)
            {
                if ((current[i] & image[i]) != image[i])
                    throw new InvalidOperationException($"Erase required at byte offset 0x{i:X}. Programming was not performed.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            programStarted = true;
            ProgramWords(image, progress, cancellationToken, current);

            VerifyImage(image, cancellationToken);
            VerifyImage(image, cancellationToken);

            result = new(id, Convert.ToHexStringLower(SHA256.HashData(image)), image.Length);
        }
        catch (Exception error)
        {
            if (programStarted)
                throw new NorRecoveryRequiredException("Programming failed.", error);

            Finish(error, () => RestoreController(control, bank, pins));

            throw;
        }

        Finish(null, () => RestoreController(control, bank, pins));

        return result;
    }

    private void EraseAndVerify(CancellationToken token, Action<int>? verificationProgress = null,
        Action? eraseCompleted = null)
    {
        bus.WriteWord(profile.CommandControlAddress, 1);

        Unlock();
        Command(0x5555, 0x80);

        Unlock();
        Command(0x5555, 0x10);

        WaitArray(0, ushort.MaxValue, profile.EraseTimeout, token);

        eraseCompleted?.Invoke();

        for (int offset = 0; offset < profile.ByteCount; offset += 4096)
        {
            token.ThrowIfCancellationRequested();

            byte[] erased = ReadBlock(offset, Math.Min(4096, profile.ByteCount - offset));

            if (erased.Any(value => value != byte.MaxValue))
                throw new IOException($"NOR erase verification failed at 0x{profile.BaseAddress + (uint)offset:X8}.");

            verificationProgress?.Invoke(Math.Min(offset + 4096, profile.ByteCount));
        }
    }

    private void ProgramWords(byte[] image, Action<int>? progress, CancellationToken token,
        byte[]? current = null)
    {
        for (int offset = 0; offset < image.Length; offset += 2)
        {
            token.ThrowIfCancellationRequested();

            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(offset, 2));
            bool matches = current is not null &&
                BinaryPrimitives.ReadUInt16LittleEndian(current.AsSpan(offset, 2)) == value;

            if (value != ushort.MaxValue && !matches)
            {
                bus.WriteWord(profile.CommandControlAddress, 1);
                Unlock();
                Command(0x5555, 0xA0);
                Command((uint)offset / 2, value);

                WaitArray(offset, value, profile.ProgramTimeout, token);
            }

            progress?.Invoke(offset + 2);
        }
    }

    private void WaitArray(int offset, ushort expected, TimeSpan timeout, CancellationToken token)
    {
        long start = Stopwatch.GetTimestamp();
        bus.WriteWord(profile.CommandControlAddress, 0);

        while (true)
        {
            token.ThrowIfCancellationRequested();

            uint address = profile.BaseAddress + (uint)(offset & ~3);
            uint value = bus.ReadWord(address);
            ushort actual = (ushort)(value >> ((offset & 2) * 8));

            if (actual == expected)
                return;

            if (Stopwatch.GetElapsedTime(start) >= timeout)
                throw new TimeoutException(
                    $"NOR polling failed at 0x{profile.BaseAddress + (uint)offset:X8}; expected {expected:X4}, got {actual:X4}.");

            Thread.Sleep(profile.CommandDelay);
        }
    }

    private void VerifyImage(byte[] image, CancellationToken token)
    {
        bus.WriteWord(profile.CommandControlAddress, 0);

        for (int offset = 0; offset < image.Length; offset += 4096)
        {
            token.ThrowIfCancellationRequested();

            int size = Math.Min(4096, image.Length - offset);

            if (!image.AsSpan(offset, size).SequenceEqual(ReadBlock(offset, size)))
                throw new IOException($"NOR full readback mismatch at 0x{profile.BaseAddress + (uint)offset:X8}.");
        }
    }

    private byte[] ReadBlock(int offset, int size)
    {
        byte[] bytes = bus.Read(profile.BaseAddress + (uint)offset, size);

        if (bytes.Length != size)
            throw new IOException("Short NOR read.");

        return bytes;
    }

    private void Unlock()
    {
        Command(0x5555, 0xAA);
        Command(0x2AAA, 0x55);
    }

    private void ResetArray()
    {
        Unlock();

        Command(0x5555, 0xF0);
    }

    private void Command(uint index, uint value)
    {
        bus.WriteHalfword(profile.CommandAddress(index), checked((ushort)value));

        Thread.Sleep(profile.CommandDelay);
    }

    private uint? ReadPins() => profile.PinMuxAddress is uint address ? bus.ReadWord(address) : null;

    private void PreparePins()
    {
        if (profile.PinMuxAddress is uint address)
            bus.WriteWord(address, bus.ReadWord(address) | profile.PinMuxMask);
    }

    private void RestoreController(uint control, uint bank, uint? pins)
    {
        Finish(null,
            () => bus.WriteWord(profile.CommandControlAddress, control),
            () => bus.WriteWord(profile.BankAddress, bank),
            () =>
            {
                if (profile.PinMuxAddress is uint address && pins is uint value)
                    bus.WriteWord(address, value);
            });
    }

    private static T WithRestoration<T>(Func<T> operation, params Action[] cleanup)
    {
        T result;

        try
        {
            result = operation();
        }
        catch (Exception error)
        {
            Finish(error, cleanup);

            throw;
        }

        Finish(null, cleanup);

        return result;
    }

    private static void Finish(Exception? operationError, params Action[] cleanup)
    {
        var failures = new List<Exception>();

        foreach (var action in cleanup)
        {
            try
            {
                action();
            }
            catch (Exception failure)
            {
                failures.Add(failure);
            }
        }

        if (failures.Count == 0)
            return;

        if (operationError is not null)
            failures.Insert(0, operationError);

        throw new AggregateException("NOR operation/restoration failed; retain halted session and backup.", failures);
    }
}
