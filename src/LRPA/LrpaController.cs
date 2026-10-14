using System.ComponentModel;
using System.Runtime.InteropServices;
using Logitech.RawPeripheralAccess.ProtocolDiscovery;
using Microsoft.Win32.SafeHandles;

namespace Logitech.RawPeripheralAccess;

/// <summary>Low-allocation typed access to the validated Logitech virtual-mouse contract.</summary>
public sealed class LrpaController : IDisposable
{
    public static readonly Guid BusInterfaceClass = new("dfbedcdd-2148-416d-9e4d-cecc2424128c");

    private const int ErrorInvalidParameter = 87;
    private readonly object _sync = new();
    private readonly LrpaOptions _options;
    private readonly SafeFileHandle _bus;
    private bool _disposed;
    private bool _ownsMouse;

    public LrpaController(LrpaOptions? options = null)
    {
        _options = options ?? new LrpaOptions();
        if (_options.CreationSettlingDelayMilliseconds is < 0 or > 5000)
            throw new ArgumentOutOfRangeException(nameof(options),
                "Creation settling delay must be between 0 and 5000 ms.");

        Protocol = _options.Protocol ?? ProtocolResolver.Resolve(_options.DriverPath);
        DevicePath = _options.DevicePath ?? ResolveSingleInterface();
        _bus = NativeMethods.OpenBus(DevicePath);
        if (!_bus.IsInvalid)
            return;

        var error = Marshal.GetLastWin32Error();
        _bus.Dispose();
        ThrowNative("CreateFileW(bus read/write)", error);
    }

    public string DevicePath { get; }
    public DriverProtocol Protocol { get; }
    public bool OwnsMouse { get { lock (_sync) return _ownsMouse; } }

    public static IReadOnlyList<string> DiscoverBusInterfaces()
        => NativeMethods.EnumerateDeviceInterfaces(BusInterfaceClass);

    public bool TryCreateMouse()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_ownsMouse)
                return true;

            Span<byte> request = stackalloc byte[8];
            BitConverter.TryWriteBytes(request, 8);
            BitConverter.TryWriteBytes(request[4..], DriverProtocol.MouseDeviceType);
            try
            {
                Invoke("create mouse", Protocol.CreateDeviceIoctl, request, inOut: true);
                _ownsMouse = true;
            }
            catch (LrpaException exception) when (exception.NativeErrorCode == ErrorInvalidParameter)
            {
                return false;
            }

            if (_options.CreationSettlingDelayMilliseconds != 0)
                Thread.Sleep(_options.CreationSettlingDelayMilliseconds);
            return true;
        }
    }

    public void CreateMouse()
    {
        if (!TryCreateMouse())
            throw new InvalidOperationException("The Logitech virtual-mouse slot already exists.");
    }

    public void Submit(MouseReport report)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            Span<byte> bytes = stackalloc byte[MouseReport.ByteLength];
            report.Write(bytes);
            Invoke("submit mouse report", Protocol.SubmitMouseReportIoctl, bytes, inOut: false);
        }
    }

    public void Move(short x, short y, MouseButtons buttons = MouseButtons.None)
        => Submit(new MouseReport(buttons, x, y));

    public void SetButtons(MouseButtons buttons)
        => Submit(new MouseReport(buttons, 0, 0));

    public void Wheel(sbyte vertical, sbyte horizontal = 0, MouseButtons buttons = MouseButtons.None)
        => Submit(new MouseReport(buttons, 0, 0, vertical, horizontal));

    public void Click(MouseButtons button, int holdMilliseconds = 20)
    {
        if (button == MouseButtons.None || (((ushort)button & ((ushort)button - 1)) != 0))
            throw new ArgumentOutOfRangeException(nameof(button), "Click requires one button.");
        if (holdMilliseconds is < 0 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(holdMilliseconds));

        SetButtons(button);
        if (holdMilliseconds != 0)
            Thread.Sleep(holdMilliseconds);
        SetButtons(MouseButtons.None);
    }

    public void RemoveOwnedMouse()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!_ownsMouse)
                return;
            RemoveMouseCore();
            _ownsMouse = false;
        }
    }

    public void ForceRemoveMouse()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            RemoveMouseCore();
            _ownsMouse = false;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            try
            {
                if (_ownsMouse && _options.RemoveOwnedMouseOnDispose)
                    RemoveMouseCore();
            }
            finally
            {
                _ownsMouse = false;
                _disposed = true;
                _bus.Dispose();
            }
        }
    }

    private string ResolveSingleInterface()
    {
        var paths = DiscoverBusInterfaces();
        return paths.Count switch
        {
            0 => throw new InvalidOperationException(
                $"No present Logitech virtual-bus interface {BusInterfaceClass:B} was found."),
            1 => paths[0],
            _ => throw new InvalidOperationException(
                $"Multiple Logitech bus interfaces were found; set {nameof(LrpaOptions.DevicePath)}.")
        };
    }

    private void RemoveMouseCore()
    {
        Span<byte> request = stackalloc byte[12];
        BitConverter.TryWriteBytes(request, 12);
        BitConverter.TryWriteBytes(request[4..], 0);
        BitConverter.TryWriteBytes(request[8..], DriverProtocol.MouseDeviceType);
        Invoke("remove mouse", Protocol.RemoveDeviceIoctl, request, inOut: true);
    }

    private void Invoke(string operation, uint code, Span<byte> buffer, bool inOut)
    {
        if (!NativeMethods.IoControl(_bus, code, buffer, inOut, out _))
            ThrowNative(operation, Marshal.GetLastWin32Error());
    }

    private static void ThrowNative(string operation, int error)
        => throw new LrpaException(operation, error, new Win32Exception(error).Message);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
