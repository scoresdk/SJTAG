namespace CH347;

internal sealed class LibUsbConnection : IUsbConnection
{
    private const int JtagInterface = 2;
    private nint context;
    private nint device;
    private bool claimed;
    private bool disposed;

    private LibUsbConnection() { }

    public static IUsbConnection Open()
    {
        var connection = new LibUsbConnection();

        try
        {
            Check(LibUsbNative.Initialize(out connection.context), "initialize");

            connection.device = LibUsbNative.Open(connection.context, 0x1A86, 0x55DD);

            if (connection.device == 0)
                throw new IOException("CH347T 1A86:55DD not found or inaccessible.");

            Check(LibUsbNative.Claim(connection.device, JtagInterface), "claim JTAG interface");
            connection.claimed = true;

            return connection;
        }
        catch
        {
            connection.Dispose();

            throw;
        }
    }

    public int BulkTransfer(byte endpoint, byte[] buffer, uint timeoutMilliseconds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        Check(LibUsbNative.BulkTransfer(device, endpoint, buffer, buffer.Length, out int count,
            timeoutMilliseconds), $"bulk endpoint 0x{endpoint:X2}");

        if (count < 0 || count > buffer.Length)
            throw new IOException("libusb returned an invalid transfer length.");

        return count;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        int releaseResult = 0;

        try
        {
            if (claimed)
                releaseResult = LibUsbNative.Release(device, JtagInterface);
        }
        finally
        {
            if (device != 0)
                LibUsbNative.Close(device);

            if (context != 0)
                LibUsbNative.Exit(context);

            device = context = 0;
        }

        Check(releaseResult, "release JTAG interface");
    }

    private static void Check(int code, string operation)
    {
        if (code < 0)
            throw new IOException($"libusb {operation} failed: {code}.");
    }
}
