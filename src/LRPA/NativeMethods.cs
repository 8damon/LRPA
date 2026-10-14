using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Logitech.RawPeripheralAccess;

internal static class NativeMethods
{
    internal const uint GenericRead = 0x80000000;
    internal const uint GenericWrite = 0x40000000;
    private const uint OpenExisting = 3;
    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfDeviceInterface = 0x00000010;
    private const int ErrorNoMoreItems = 259;
    private const int ErrorInsufficientBuffer = 122;

    internal static IReadOnlyList<string> EnumerateDeviceInterfaces(Guid interfaceClass)
    {
        var paths = new List<string>(1);
        using var set = SetupDiGetClassDevsW(ref interfaceClass, null, IntPtr.Zero,
            DigcfPresent | DigcfDeviceInterface);
        if (set.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetupDiGetClassDevsW failed");

        for (uint index = 0; ; index++)
        {
            var data = new SpDeviceInterfaceData { Size = (uint)Marshal.SizeOf<SpDeviceInterfaceData>() };
            if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref interfaceClass, index, ref data))
            {
                var error = Marshal.GetLastWin32Error();
                if (error == ErrorNoMoreItems)
                    break;
                throw new Win32Exception(error, "SetupDiEnumDeviceInterfaces failed");
            }

            _ = SetupDiGetDeviceInterfaceDetailW(set, ref data, IntPtr.Zero, 0, out var required, IntPtr.Zero);
            var sizeError = Marshal.GetLastWin32Error();
            if (required == 0 || sizeError != ErrorInsufficientBuffer)
                throw new Win32Exception(sizeError, "SetupDiGetDeviceInterfaceDetailW(size) failed");

            var detail = Marshal.AllocHGlobal(checked((int)required));
            try
            {
                Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                if (!SetupDiGetDeviceInterfaceDetailW(set, ref data, detail, required, out _, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(),
                        "SetupDiGetDeviceInterfaceDetailW(data) failed");

                var path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                if (!string.IsNullOrWhiteSpace(path))
                    paths.Add(path);
            }
            finally
            {
                Marshal.FreeHGlobal(detail);
            }
        }

        return paths;
    }

    internal static SafeFileHandle OpenBus(string path)
        => CreateFileW(path, GenericRead | GenericWrite, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);

    internal static unsafe bool IoControl(
        SafeFileHandle device,
        uint controlCode,
        Span<byte> buffer,
        bool inOut,
        out uint returned)
    {
        fixed (byte* pointer = buffer)
        {
            return DeviceIoControl(
                device,
                controlCode,
                pointer,
                (uint)buffer.Length,
                inOut ? pointer : null,
                inOut ? (uint)buffer.Length : 0,
                out returned,
                null);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDeviceInterfaceData
    {
        public uint Size;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public UIntPtr Reserved;
    }

    internal sealed class SafeDeviceInfoSetHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeDeviceInfoSetHandle() : base(true) { }
        protected override bool ReleaseHandle() => SetupDiDestroyDeviceInfoList(handle);
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeDeviceInfoSetHandle SetupDiGetClassDevsW(
        ref Guid classGuid, string? enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        SafeDeviceInfoSetHandle set,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        uint memberIndex,
        ref SpDeviceInterfaceData interfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetailW(
        SafeDeviceInfoSetHandle set,
        ref SpDeviceInterfaceData interfaceData,
        IntPtr detailData,
        uint detailDataSize,
        out uint requiredSize,
        IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern unsafe bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        byte* inBuffer,
        uint inSize,
        byte* outBuffer,
        uint outSize,
        out uint returned,
        NativeOverlapped* overlapped);
}
