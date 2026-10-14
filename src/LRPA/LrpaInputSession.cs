using Logitech.RawPeripheralAccess.ProtocolDiscovery;

namespace Logitech.RawPeripheralAccess;

/// <summary>Instance-based WinAPI input translation and virtual-mouse lifecycle.</summary>
public sealed class LrpaInputSession : IDisposable
{
    private const uint SupportedFlags = LRPA.MOUSEEVENTF_MOVE | LRPA.MOUSEEVENTF_LEFTDOWN |
        LRPA.MOUSEEVENTF_LEFTUP | LRPA.MOUSEEVENTF_RIGHTDOWN | LRPA.MOUSEEVENTF_RIGHTUP |
        LRPA.MOUSEEVENTF_MIDDLEDOWN | LRPA.MOUSEEVENTF_MIDDLEUP | LRPA.MOUSEEVENTF_XDOWN |
        LRPA.MOUSEEVENTF_XUP | LRPA.MOUSEEVENTF_WHEEL | LRPA.MOUSEEVENTF_HWHEEL |
        LRPA.MOUSEEVENTF_MOVE_NOCOALESCE;

    private readonly object _sync = new();
    private readonly LrpaController _controller;
    private MouseButtons _buttons;
    private bool _disposed;

    public LrpaInputSession(LrpaOptions? options = null)
    {
        _controller = new LrpaController(options);
        try
        {
            Created = _controller.TryCreateMouse();
        }
        catch
        {
            _controller.Dispose();
            throw;
        }
    }

    public bool Created { get; }
    public string DevicePath => _controller.DevicePath;
    public DriverProtocol Protocol => _controller.Protocol;

    public uint SendInput(ReadOnlySpan<INPUT> inputs)
    {
        ValidateInputs(inputs);
        lock (_sync)
        {
            ThrowIfDisposed();
            foreach (ref readonly var input in inputs)
                SendMouse(input.U.mi);
            return checked((uint)inputs.Length);
        }
    }

    public void Submit(MouseReport report)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            _controller.Submit(report);
            _buttons = report.Buttons;
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
                if (_buttons != MouseButtons.None)
                    _controller.SetButtons(MouseButtons.None);
            }
            finally
            {
                _buttons = MouseButtons.None;
                _disposed = true;
                _controller.Dispose();
            }
        }
    }

    internal static void ValidateInputs(ReadOnlySpan<INPUT> inputs)
    {
        for (var index = 0; index < inputs.Length; index++)
        {
            var input = inputs[index];
            if (input.type != LRPA.INPUT_MOUSE)
                throw new NotSupportedException(
                    $"inputs[{index}] has type {input.type}; only INPUT_MOUSE is supported.");

            var flags = input.U.mi.dwFlags;
            if ((flags & (LRPA.MOUSEEVENTF_ABSOLUTE | LRPA.MOUSEEVENTF_VIRTUALDESK)) != 0)
                throw new NotSupportedException("LRPA supports relative mouse input only.");
            if ((flags & ~SupportedFlags) != 0)
                throw new NotSupportedException($"Unsupported mouse flags 0x{flags & ~SupportedFlags:X8}.");
        }
    }

    private void SendMouse(MOUSEINPUT input)
    {
        var down = MouseButtons.None;
        var up = MouseButtons.None;
        if ((input.dwFlags & LRPA.MOUSEEVENTF_LEFTDOWN) != 0) down |= MouseButtons.Button1;
        if ((input.dwFlags & LRPA.MOUSEEVENTF_LEFTUP) != 0) up |= MouseButtons.Button1;
        if ((input.dwFlags & LRPA.MOUSEEVENTF_RIGHTDOWN) != 0) down |= MouseButtons.Button2;
        if ((input.dwFlags & LRPA.MOUSEEVENTF_RIGHTUP) != 0) up |= MouseButtons.Button2;
        if ((input.dwFlags & LRPA.MOUSEEVENTF_MIDDLEDOWN) != 0) down |= MouseButtons.Button3;
        if ((input.dwFlags & LRPA.MOUSEEVENTF_MIDDLEUP) != 0) up |= MouseButtons.Button3;

        if ((input.dwFlags & (LRPA.MOUSEEVENTF_XDOWN | LRPA.MOUSEEVENTF_XUP)) != 0)
        {
            var xButtons = (ushort)(input.mouseData >> 16);
            var translated = MouseButtons.None;
            if ((xButtons & LRPA.XBUTTON1) != 0) translated |= MouseButtons.Button4;
            if ((xButtons & LRPA.XBUTTON2) != 0) translated |= MouseButtons.Button5;
            if ((input.dwFlags & LRPA.MOUSEEVENTF_XDOWN) != 0) down |= translated;
            if ((input.dwFlags & LRPA.MOUSEEVENTF_XUP) != 0) up |= translated;
        }

        var both = down & up;
        _buttons |= down;
        _buttons &= ~(up & ~both);

        var x = (input.dwFlags & LRPA.MOUSEEVENTF_MOVE) != 0 ? input.dx : 0;
        var y = (input.dwFlags & LRPA.MOUSEEVENTF_MOVE) != 0 ? input.dy : 0;
        var wheel = (input.dwFlags & LRPA.MOUSEEVENTF_WHEEL) != 0
            ? ToHidDetents(unchecked((short)(input.mouseData >> 16)))
            : 0;
        var horizontal = (input.dwFlags & LRPA.MOUSEEVENTF_HWHEEL) != 0
            ? ToHidDetents(unchecked((short)(input.mouseData >> 16)))
            : 0;

        do
        {
            _controller.Submit(new MouseReport(
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
        _controller.SetButtons(_buttons);
    }

    private static int ToHidDetents(short delta)
    {
        if (delta == 0)
            return 0;
        var magnitude = Math.Max(1, (Math.Abs((int)delta) + LRPA.WHEEL_DELTA / 2) / LRPA.WHEEL_DELTA);
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

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
