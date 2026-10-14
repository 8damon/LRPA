using Logitech.RawPeripheralAccess.ProtocolDiscovery;

namespace Logitech.RawPeripheralAccess;

public sealed record LrpaOptions
{
    public string? DevicePath { get; init; }
    public string? DriverPath { get; init; }
    public DriverProtocol? Protocol { get; init; }
    public bool RemoveOwnedMouseOnDispose { get; init; } = true;
    public int CreationSettlingDelayMilliseconds { get; init; } = 750;
}

public sealed class LrpaException : Exception
{
    public LrpaException(string operation, int nativeErrorCode, string message)
        : base($"{operation} failed with Win32 {nativeErrorCode} (0x{nativeErrorCode:X8}): {message}")
    {
        Operation = operation;
        NativeErrorCode = nativeErrorCode;
    }

    public string Operation { get; }
    public int NativeErrorCode { get; }
}
