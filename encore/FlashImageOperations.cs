using SJTag;

namespace Encore;

public sealed class FlashEraseRequiredException : InvalidOperationException
{
    public FlashEraseRequiredException(string message) : base(message)
    {
    }
}

public static class FlashImageOperations
{
    public static void ValidateSize(long actualSize, int flashSize)
    {
        if (actualSize <= 0 || actualSize > flashSize || actualSize % 4 != 0)
            throw new ArgumentException($"Input must be nonempty, 4-byte aligned, and at most {flashSize} bytes; got {actualSize} bytes.");
    }

    public static int CountChanges(ReadOnlySpan<byte> current, ReadOnlySpan<byte> desired)
    {
        ValidateSize(desired.Length, current.Length);
        if (desired.Length != current.Length)
            throw new ArgumentException("Comparison buffers must have equal lengths.");
        if (current.Length == 0 || current.Length % 2 != 0)
            throw new ArgumentException("Flash images must contain complete halfwords.");
        int changed = 0;
        for (int offset = 0; offset < current.Length; offset += 2)
        {
            for (int i = offset; i < offset + 2; i++)
                if ((current[i] & desired[i]) != desired[i])
                    throw new FlashEraseRequiredException(
                        $"Erase required at byte offset 0x{i:X}: current 0x{current[i]:X2}, desired 0x{desired[i]:X2}. No programming performed.");
            if (current[offset] != desired[offset] || current[offset + 1] != desired[offset + 1])
                changed++;
        }
        return changed;
    }

    public static void Verify(ReadOnlySpan<byte> desired, ReadOnlySpan<byte> actual)
    {
        ValidateSize(actual.Length, desired.Length);
        if (actual.Length != desired.Length)
            throw new ArgumentException("Verification buffers must have equal lengths.");
        for (int i = 0; i < desired.Length; i++)
            if (desired[i] != actual[i])
                throw new IOException($"Verification failed at byte offset 0x{i:X}: expected 0x{desired[i]:X2}, got 0x{actual[i]:X2}.");
    }

    public static int Program(NorFlash flash, byte[] desired,
        Action<int>? readProgress = null, Action<int>? writeProgress = null)
    {
        ArgumentNullException.ThrowIfNull(flash);
        ArgumentNullException.ThrowIfNull(desired);
        byte[] current = flash.Read(0, desired.Length, readProgress);
        int changes = CountChanges(current, desired);
        if (changes == 0)
        {
            writeProgress?.Invoke(desired.Length);
            return 0;
        }
        flash.ProgramPrefix(desired, writeProgress);
        return changes;
    }

    public static int ProgramStaged(NorFlash flash, byte[] desired,
        Action<ReadOnlyMemory<byte>, Action<int>?, Action<int>?> execute,
        Action<int>? readProgress = null, Action<int>? writeProgress = null,
        Action<int>? verifyProgress = null)
    {
        ArgumentNullException.ThrowIfNull(flash);
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(execute);
        ValidateSize(desired.Length, flash.ByteCount);

        byte[] current = flash.Read(0, desired.Length, readProgress);
        int changes = CountChanges(current, desired);
        if (changes == 0)
        {
            writeProgress?.Invoke(desired.Length);
            return 0;
        }

        execute(desired, writeProgress, verifyProgress);
        return changes;
    }
}
