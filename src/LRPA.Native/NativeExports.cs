using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Logitech.RawPeripheralAccess.ProtocolDiscovery;

namespace Logitech.RawPeripheralAccess.Native;

internal static unsafe class NativeExports
{
    private const uint AbiVersion = 1;
    private const uint RemoveOwnedOnClose = 0x00000001;
    private const uint KnownOptionFlags = RemoveOwnedOnClose;
    private const uint MaximumInputs = 4096;

    private static readonly ConcurrentDictionary<nuint, NativeSession> Sessions = new();
    private static long _nextHandle;

    [ThreadStatic]
    private static string? _lastError;

    [UnmanagedCallersOnly(EntryPoint = "lrpa_abi_version", CallConvs = [typeof(CallConvCdecl)])]
    public static uint Abi() => AbiVersion;

    [UnmanagedCallersOnly(EntryPoint = "lrpa_options_size", CallConvs = [typeof(CallConvCdecl)])]
    public static nuint OptionsSize() => (nuint)sizeof(NativeOptions);

    [UnmanagedCallersOnly(EntryPoint = "lrpa_input_size", CallConvs = [typeof(CallConvCdecl)])]
    public static nuint InputSize() => (nuint)sizeof(INPUT);

    [UnmanagedCallersOnly(EntryPoint = "lrpa_mouse_report_size", CallConvs = [typeof(CallConvCdecl)])]
    public static nuint MouseReportSize() => (nuint)sizeof(NativeMouseReport);

    [UnmanagedCallersOnly(EntryPoint = "lrpa_open", CallConvs = [typeof(CallConvCdecl)])]
    public static int Open(NativeOptions* options, nuint* outHandle, int* outCreated)
        => Guard(() =>
        {
            if (outHandle is null)
                throw new ArgumentNullException(nameof(outHandle));

            *outHandle = 0;
            if (outCreated is not null)
                *outCreated = 0;

            var managedOptions = ConvertOptions(options);
            var session = new LrpaInputSession(managedOptions);
            NativeSession? state = null;
            try
            {
                state = new NativeSession(session);
                nuint handle;
                do
                {
                    handle = unchecked((nuint)Interlocked.Increment(ref _nextHandle));
                }
                while (handle == 0 || !Sessions.TryAdd(handle, state));

                *outHandle = handle;
                if (outCreated is not null)
                    *outCreated = session.Created ? 1 : 0;
                state = null;
            }
            finally
            {
                state?.Dispose();
            }
        });

    [UnmanagedCallersOnly(EntryPoint = "lrpa_close", CallConvs = [typeof(CallConvCdecl)])]
    public static int Close(nuint handle)
        => Guard(() =>
        {
            if (handle == 0 || !Sessions.TryRemove(handle, out var session))
                throw new InvalidHandleException();
            session.Dispose();
        });

    [UnmanagedCallersOnly(EntryPoint = "lrpa_send_input", CallConvs = [typeof(CallConvCdecl)])]
    public static int SendInput(
        nuint handle,
        uint inputCount,
        INPUT* inputs,
        nuint inputSize,
        uint* outSent)
        => Guard(() =>
        {
            if (outSent is not null)
                *outSent = 0;
            if (inputSize != (nuint)sizeof(INPUT))
                throw new ArgumentException($"input_size must be {sizeof(INPUT)}.");
            if (inputCount > MaximumInputs)
                throw new ArgumentOutOfRangeException(nameof(inputCount),
                    $"A call may contain at most {MaximumInputs} inputs.");
            if (inputCount != 0 && inputs is null)
                throw new ArgumentNullException(nameof(inputs));

            var session = GetSession(handle);
            var source = inputCount == 0
                ? ReadOnlySpan<INPUT>.Empty
                : new ReadOnlySpan<INPUT>(inputs, checked((int)inputCount));
            var sent = session.SendInput(source);
            if (outSent is not null)
                *outSent = sent;
        });

    [UnmanagedCallersOnly(EntryPoint = "lrpa_move", CallConvs = [typeof(CallConvCdecl)])]
    public static int Move(nuint handle, int x, int y)
        => Guard(() =>
        {
            Span<INPUT> input = stackalloc INPUT[1];
            input[0] = LRPA.MouseInput(x, y, LRPA.MOUSEEVENTF_MOVE);
            _ = GetSession(handle).SendInput(input);
        });

    [UnmanagedCallersOnly(EntryPoint = "lrpa_submit_report", CallConvs = [typeof(CallConvCdecl)])]
    public static int SubmitReport(nuint handle, NativeMouseReport* report)
        => Guard(() =>
        {
            if (report is null)
                throw new ArgumentNullException(nameof(report));

            GetSession(handle).Submit(new MouseReport(
                (MouseButtons)report->Buttons,
                report->X,
                report->Y,
                report->Wheel,
                report->HorizontalPan));
        });

    [UnmanagedCallersOnly(EntryPoint = "lrpa_last_error", CallConvs = [typeof(CallConvCdecl)])]
    public static int LastError(byte* buffer, nuint capacity, nuint* outRequired)
    {
        var message = _lastError ?? string.Empty;
        var byteCount = Encoding.UTF8.GetByteCount(message);
        var required = checked((nuint)byteCount + 1);
        if (outRequired is not null)
            *outRequired = required;

        if (buffer is null || capacity < required || capacity > int.MaxValue)
            return (int)LrpaResult.BufferTooSmall;

        var destination = new Span<byte>(buffer, checked((int)capacity));
        var written = Encoding.UTF8.GetBytes(message, destination);
        destination[written] = 0;
        return (int)LrpaResult.Ok;
    }

    private static LrpaOptions? ConvertOptions(NativeOptions* options)
    {
        if (options is null)
            return null;
        if (options->StructSize < sizeof(NativeOptions))
            throw new ArgumentException($"options.struct_size must be at least {sizeof(NativeOptions)}.");
        if ((options->Flags & ~KnownOptionFlags) != 0)
            throw new ArgumentException($"Unknown option flags 0x{options->Flags & ~KnownOptionFlags:X8}.");
        if (options->Reserved != 0)
            throw new ArgumentException("options.reserved must be zero.");

        return new LrpaOptions
        {
            DevicePath = Utf8String(options->DevicePathUtf8),
            DriverPath = Utf8String(options->DriverPathUtf8),
            RemoveOwnedMouseOnDispose = (options->Flags & RemoveOwnedOnClose) != 0,
            CreationSettlingDelayMilliseconds = options->CreationDelayMilliseconds
        };
    }

    private static string? Utf8String(byte* value)
        => value is null ? null : Marshal.PtrToStringUTF8((nint)value);

    private static NativeSession GetSession(nuint handle)
        => handle != 0 && Sessions.TryGetValue(handle, out var session)
            ? session
            : throw new InvalidHandleException();

    private static int Guard(Action action)
    {
        try
        {
            _lastError = null;
            action();
            return (int)LrpaResult.Ok;
        }
        catch (Exception exception)
        {
            _lastError = exception.Message;
            return (int)Map(exception);
        }
    }

    private static LrpaResult Map(Exception exception) => exception switch
    {
        InvalidHandleException => LrpaResult.InvalidHandle,
        ArgumentException or OverflowException => LrpaResult.InvalidArgument,
        NotSupportedException => LrpaResult.NotSupported,
        InvalidDataException or FileNotFoundException => LrpaResult.ProtocolError,
        LrpaException => LrpaResult.NativeError,
        _ => LrpaResult.InternalError
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeOptions
    {
        public uint StructSize;
        public uint Flags;
        public int CreationDelayMilliseconds;
        public uint Reserved;
        public byte* DevicePathUtf8;
        public byte* DriverPathUtf8;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NativeMouseReport
    {
        public ushort Buttons;
        public short X;
        public short Y;
        public sbyte Wheel;
        public sbyte HorizontalPan;
    }

    private enum LrpaResult
    {
        Ok = 0,
        InvalidArgument = 1,
        InvalidHandle = 2,
        NotSupported = 3,
        ProtocolError = 4,
        NativeError = 5,
        InternalError = 6,
        BufferTooSmall = 7
    }

    private sealed class InvalidHandleException : Exception
    {
        public InvalidHandleException() : base("The LRPA handle is invalid or closed.") { }
    }

    private sealed class NativeSession : IDisposable
    {
        private readonly object _sync = new();
        private LrpaInputSession? _session;

        public NativeSession(LrpaInputSession session) => _session = session;

        public uint SendInput(ReadOnlySpan<INPUT> inputs)
        {
            lock (_sync)
                return GetOpen().SendInput(inputs);
        }

        public void Submit(MouseReport report)
        {
            lock (_sync)
                GetOpen().Submit(report);
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_session is null)
                    return;
                try
                {
                    _session.Dispose();
                }
                finally
                {
                    _session = null;
                }
            }
        }

        private LrpaInputSession GetOpen() => _session ?? throw new InvalidHandleException();
    }
}
