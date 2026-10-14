# Logitech Raw Peripheral Access

LRPA is a small, unofficial SDK for sending relative mouse reports through the virtual HID bus installed by Logitech G HUB.

It provides .NET, C, C++, and Rust interfaces, plus a separate fail-closed module that recovers the private IOCTL family from the installed driver.

> LRPA is independent research software. It is not produced, endorsed, or supported by Logitech.

## Requirements

- Windows x64
- .NET 10
- Logitech G HUB and `logi_joy_bus_enum.x64.sys`

## Build

```powershell
dotnet build .\LRPA.slnx -c Release
dotnet run --project .\src\LRPA.Cli -- self-test
```

Build the self-contained native x64 DLL:

```powershell
dotnet publish .\src\LRPA.Native -c Release -r win-x64 -o .\artifacts\native\win-x64
```

## SendInput migration

```csharp
using Logitech.RawPeripheralAccess;

INPUT[] inputs =
[
    LRPA.MouseInput(64, -16, LRPA.MOUSEEVENTF_MOVE)
];

try
{
    uint sent = LRPA.SendInput((uint)inputs.Length, inputs, INPUT.Size);
}
finally
{
    LRPA.Shutdown();
}
```

The familiar `INPUT`, `INPUTUNION`, `MOUSEINPUT`, and mouse-event constants are included. Relative movement, buttons, vertical wheel, and horizontal wheel are supported. Keyboard and absolute mouse input are intentionally rejected.

## Typed API

```csharp
using Logitech.RawPeripheralAccess;

using var mouse = new LrpaController();
mouse.TryCreateMouse();
mouse.Move(40, -10);
mouse.Click(MouseButtons.Button1);
mouse.Wheel(vertical: 1);
```

## Protocol recovery

IOCTL values are not runtime constants. `LRPA.ProtocolDiscovery` scans the installed PE once, validates the dispatch shape, HID report descriptor, virtual-device identity, and IOCTL encoding, then caches the immutable result.

Unsupported or ambiguous driver images fail before the bus receives an IOCTL. Fingerprinting is version-adaptive, not a guarantee that every future driver rewrite will remain compatible.

The report-submission path performs no scanning, reuses one bus handle, uses stack buffers, and has no third-party dependencies, reflection, middleware, or service framework.

## Native FFI

`LRPA.Native.dll` is a NativeAOT shared library with a stable C ABI. It does not require a separately installed .NET runtime.

```c
#include "lrpa.h"

lrpa_handle mouse;
lrpa_open(NULL, &mouse, NULL);
lrpa_move(mouse, 64, -16);
lrpa_close(mouse);
```

C++ can include `lrpa.hpp` for an RAII wrapper. The Rust binding provides a safe `Controller` whose `Drop` implementation closes the handle.

## Documentation

- [Integration guide](docs/INTEGRATION.md)
- [SDK/API reference](docs/API.md)
- [C/C++/Rust FFI guide](docs/FFI.md)
- [SendInput migration example](examples/SendInputMigration/Program.cs)
- [Typed controller example](examples/TypedController/Program.cs)
- [Read-only protocol inspection](examples/ProtocolInspection/Program.cs)
- [C example](examples/c/main.c)
- [C++ example](examples/cpp/main.cpp)
- [Rust example](examples/rust/src/main.rs)

## Provenance

On the tested G HUB driver `2026.0.0.0`, LRPA mouse reports appeared as Raw Input from virtual device `VID_046D&PID_C231`. The corresponding `WH_MOUSE_LL` event had `LLMHF_INJECTED` clear.

That flag does not establish physical origin. Raw Input still exposes the virtual device, and applications should not use a low-level-hook flag as their sole trust decision.

## Status

Experimental. Validated against one installed G HUB driver family. No package has been published.
