namespace CH347;

public interface IUsbConnection : IDisposable
{
    int BulkTransfer(byte endpoint, byte[] buffer, uint timeoutMilliseconds);
}
