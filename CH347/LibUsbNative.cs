using System.Reflection;
using System.Runtime.InteropServices;

namespace CH347;

internal static class LibUsbNative
{
    private const string Library = "sjtag-libusb";

    static LibUsbNative() => NativeLibrary.SetDllImportResolver(typeof(LibUsbNative).Assembly, Resolve);

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != Library)
            return 0;

        string[] candidates = OperatingSystem.IsWindows()
            ? ["libusb-1.0.dll"]
            : OperatingSystem.IsMacOS()
                ? [
                    "libusb-1.0.dylib",
                    "/opt/homebrew/lib/libusb-1.0.dylib",
                    "/usr/local/lib/libusb-1.0.dylib"
                ]
                : ["libusb-1.0.so.0", "libusb-1.0.so"];

        foreach (string candidate in candidates)
        {
            if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out nint handle))
                return handle;
        }

        throw new DllNotFoundException("libusb-1.0 is required. Install it and make its native library discoverable.");
    }

    [DllImport(Library, EntryPoint = "libusb_init", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Initialize(out nint context);

    [DllImport(Library, EntryPoint = "libusb_open_device_with_vid_pid", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint Open(nint context, ushort vendorId, ushort productId);

    [DllImport(Library, EntryPoint = "libusb_claim_interface", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Claim(nint device, int interfaceNumber);

    [DllImport(Library, EntryPoint = "libusb_release_interface", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Release(nint device, int interfaceNumber);

    [DllImport(Library, EntryPoint = "libusb_close", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Close(nint device);

    [DllImport(Library, EntryPoint = "libusb_exit", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Exit(nint context);

    [DllImport(Library, EntryPoint = "libusb_bulk_transfer", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int BulkTransfer(nint device, byte endpoint, [In, Out] byte[] buffer,
        int length, out int transferred, uint timeoutMilliseconds);
}
