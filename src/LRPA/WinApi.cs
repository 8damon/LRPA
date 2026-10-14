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

    private const uint SupportedFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_LEFTDOWN |
        MOUSEEVENTF_LEFTUP | MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP |
        MOUSEEVENTF_MIDDLEDOWN | MOUSEEVENTF_MIDDLEUP | MOUSEEVENTF_XDOWN |
        MOUSEEVENTF_XUP | MOUSEEVENTF_WHEEL | MOUSEEVENTF_HWHEEL |
        MOUSEEVENTF_MOVE_NOCOALESCE;

    private static readonly object Sync = new();
    private static LrpaController? _controller;
    private static MouseButtons _buttons;

    public static bool IsInitialized { get { lock (Sync) return _controller is not null; } }
    public static DriverProtocol? Protocol { get { lock (Sync) return _controller?.Protocol; } }

    public static bool Initialize(LrpaOptions? options = null)
    {
        lock (Sync)
        {
            if (_controller is not null)
                return _controller.OwnsMouse;

            var controller = new LrpaController(options);
            try
            {
                var created = controller.TryCreateMouse();
                _controller = controller;
                _buttons = MouseButtons.None;
                return created;
            }
            catch
            {
                controller.Dispose();
                throw;
            }
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

        for (var index = 0; index < inputCount; index++)
            Validate(inputs[index], index);

        lock (Sync)
        {
            if (_controller is null)
                Initialize();
            for (var index = 0; index < inputCount; index++)
                SendMouse(inputs[index].U.mi);
            return inputCount;
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
            if (_controller is null)
                return;

            try
            {
                if (_buttons != MouseButtons.None)
                    _controller.SetButtons(MouseButtons.None);
            }
            finally
            {
                _buttons = MouseButtons.None;
                _controller.Dispose();
                _controller = null;
            }
        }
    }

    private static void Validate(INPUT input, int index)
    {
        if (input.type != INPUT_MOUSE)
            throw new NotSupportedException($"inputs[{index}] has type {input.type}; only INPUT_MOUSE is supported.");

        var flags = input.U.mi.dwFlags;
        if ((flags & (MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK)) != 0)
            throw new NotSupportedException("LRPA supports relative mouse input only.");
        if ((flags & ~SupportedFlags) != 0)
            throw new NotSupportedException($"Unsupported mouse flags 0x{flags & ~SupportedFlags:X8}.");
    }

    private static void SendMouse(MOUSEINPUT input)
    {
        var down = MouseButtons.None;
        var up = MouseButtons.None;
        if ((input.dwFlags & MOUSEEVENTF_LEFTDOWN) != 0) down |= MouseButtons.Button1;
        if ((input.dwFlags & MOUSEEVENTF_LEFTUP) != 0) up |= MouseButtons.Button1;
        if ((input.dwFlags & MOUSEEVENTF_RIGHTDOWN) != 0) down |= MouseButtons.Button2;
        if ((input.dwFlags & MOUSEEVENTF_RIGHTUP) != 0) up |= MouseButtons.Button2;
        if ((input.dwFlags & MOUSEEVENTF_MIDDLEDOWN) != 0) down |= MouseButtons.Button3;
        if ((input.dwFlags & MOUSEEVENTF_MIDDLEUP) != 0) up |= MouseButtons.Button3;

        if ((input.dwFlags & (MOUSEEVENTF_XDOWN | MOUSEEVENTF_XUP)) != 0)
        {
            var xButtons = (ushort)(input.mouseData >> 16);
            var translated = MouseButtons.None;
            if ((xButtons & XBUTTON1) != 0) translated |= MouseButtons.Button4;
            if ((xButtons & XBUTTON2) != 0) translated |= MouseButtons.Button5;
            if ((input.dwFlags & MOUSEEVENTF_XDOWN) != 0) down |= translated;
            if ((input.dwFlags & MOUSEEVENTF_XUP) != 0) up |= translated;
        }

        var both = down & up;
        _buttons |= down;
        _buttons &= ~(up & ~both);

        var x = (input.dwFlags & MOUSEEVENTF_MOVE) != 0 ? input.dx : 0;
        var y = (input.dwFlags & MOUSEEVENTF_MOVE) != 0 ? input.dy : 0;
        var wheel = (input.dwFlags & MOUSEEVENTF_WHEEL) != 0
            ? ToHidDetents(unchecked((short)(input.mouseData >> 16)))
            : 0;
        var horizontal = (input.dwFlags & MOUSEEVENTF_HWHEEL) != 0
            ? ToHidDetents(unchecked((short)(input.mouseData >> 16)))
            : 0;

        do
        {
            _controller!.Submit(new MouseReport(
                _buttons,
                TakeAxis(ref x),
                TakeAxis(ref y),
                TakeWheel(ref wheel),
                TakeWheel(ref horizontal)));
        }
        while (x != 0 || y != 0 || wheel != 0 || horizontal != 0);

        if (both == MouseButtons.None)
            return;

        _buttons &= ~both;
        _controller!.SetButtons(_buttons);
    }

    private static int ToHidDetents(short delta)
    {
        if (delta == 0)
            return 0;
        var magnitude = Math.Max(1, (Math.Abs((int)delta) + WHEEL_DELTA / 2) / WHEEL_DELTA);
        return Math.Sign(delta) * magnitude;
    }

    private static short TakeAxis(ref int value)
    {
        var chunk = Math.Clamp(value, -32767, 32767);
        value -= chunk;
        return checked((short)chunk);
    }

    private static sbyte TakeWheel(ref int value)
    {
        var chunk = Math.Clamp(value, -127, 127);
        value -= chunk;
        return checked((sbyte)chunk);
    }
}
