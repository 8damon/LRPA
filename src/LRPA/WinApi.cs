using System.Runtime.InteropServices;
using Logitech.RawPeripheralAccess.ProtocolDiscovery;

namespace Logitech.RawPeripheralAccess;

[StructLayout(LayoutKind.Sequential)]
public struct INPUT
{
    public uint type;
    public INPUTUNION U;
    public static int Size => Marshal.SizeOf<INPUT>();
}

[StructLayout(LayoutKind.Explicit)]
public struct INPUTUNION
{
    [FieldOffset(0)] public MOUSEINPUT mi;
    [FieldOffset(0)] public KEYBDINPUT ki;
    [FieldOffset(0)] public HARDWAREINPUT hi;
}

[StructLayout(LayoutKind.Sequential)]
public struct MOUSEINPUT
{
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public UIntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
public struct KEYBDINPUT
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public UIntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
public struct HARDWAREINPUT
{
    public uint uMsg;
    public ushort wParamL;
    public ushort wParamH;
}

/// <summary>WinAPI-shaped relative mouse interface backed by Logitech's virtual HID bus.</summary>
public static class LRPA
{
    public const uint INPUT_MOUSE = 0;
    public const uint INPUT_KEYBOARD = 1;
    public const uint INPUT_HARDWARE = 2;

    public const uint MOUSEEVENTF_MOVE = 0x0001;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    public const uint MOUSEEVENTF_XDOWN = 0x0080;
    public const uint MOUSEEVENTF_XUP = 0x0100;
    public const uint MOUSEEVENTF_WHEEL = 0x0800;
    public const uint MOUSEEVENTF_HWHEEL = 0x1000;
    public const uint MOUSEEVENTF_MOVE_NOCOALESCE = 0x2000;
    public const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
    public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    public const uint XBUTTON1 = 0x0001;
    public const uint XBUTTON2 = 0x0002;
    public const int WHEEL_DELTA = 120;

    private static readonly object Sync = new();
    private static LrpaInputSession? _session;

    public static bool IsInitialized { get { lock (Sync) return _session is not null; } }
    public static DriverProtocol? Protocol { get { lock (Sync) return _session?.Protocol; } }

    public static bool Initialize(LrpaOptions? options = null)
    {
        lock (Sync)
        {
            if (_session is not null)
                return _session.Created;

            _session = new LrpaInputSession(options);
            return _session.Created;
        }
    }

    public static uint SendInput(uint inputCount, INPUT[] inputs, int cbSize)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputCount > inputs.Length)
            throw new ArgumentOutOfRangeException(nameof(inputCount));
        if (cbSize != INPUT.Size)
            throw new ArgumentException($"cbSize must be {INPUT.Size}.", nameof(cbSize));
        if (inputCount == 0)
            return 0;

        var source = inputs.AsSpan(0, checked((int)inputCount));
        LrpaInputSession.ValidateInputs(source);

        lock (Sync)
        {
            if (_session is null)
                Initialize();
            return _session!.SendInput(source);
        }
    }

    public static uint SendInput(INPUT[] inputs) => SendInput((uint)inputs.Length, inputs, INPUT.Size);

    public static INPUT MouseInput(int dx, int dy, uint flags, uint mouseData = 0)
        => new()
        {
            type = INPUT_MOUSE,
            U = new INPUTUNION
            {
                mi = new MOUSEINPUT { dx = dx, dy = dy, mouseData = mouseData, dwFlags = flags }
            }
        };

    public static void Shutdown()
    {
        lock (Sync)
        {
            if (_session is null)
                return;

            _session.Dispose();
            _session = null;
        }
    }
}
