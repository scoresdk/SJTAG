namespace SJTag;

public sealed partial class SjtagClient
{
    public void Release()
    {
        if (IsReleased)
            return;

        IsReleased = true;

        if (transport is IDisposable disposable)
            disposable.Dispose();
    }

    public void Dispose() => Release();
}
