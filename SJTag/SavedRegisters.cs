namespace SJTag;

public static class SavedRegisters
{
    public static IReadOnlyDictionary<string, uint> Decode(IReadOnlyDictionary<uint, uint> words)
    {
        ArgumentNullException.ThrowIfNull(words);

        if (words[Mailbox.TlbSaveBytes] != 0 || words[Mailbox.FramePointer] != Mailbox.Buffer - 276)
            throw new InvalidOperationException("Unverified TLB frame or unexpected frame pointer.");

        uint frame = words[Mailbox.FramePointer];
        var names = new List<string> { "sr0", "sr1", "sr2", "ceh", "cel" };
        names.AddRange(Enumerable.Range(0, 32).Select(i => $"cr{i}"));
        names.AddRange(Enumerable.Range(0, 32).Select(i => $"r{i}"));

        var result = new Dictionary<string, uint>(69);

        for (int i = 0; i < names.Count; i++)
            result.Add(names[i], words[frame + (uint)i * 4]);

        return result;
    }
}
