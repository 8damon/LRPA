# Integration guide

## Add LRPA to an application

Until a package is published, reference the projects directly:

```xml
<ItemGroup>
  <ProjectReference Include="path\to\LRPA\src\LRPA\LRPA.csproj" />
</ItemGroup>
```

The core project brings in `LRPA.ProtocolDiscovery` automatically.

## Migrating a SendInput call

Existing relative-mouse code can retain the WinAPI structure shape. Replace the P/Invoke call:

```csharp
uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
```

with:

```csharp
using Logitech.RawPeripheralAccess;

uint sent = LRPA.SendInput((uint)inputs.Length, inputs, INPUT.Size);
```

Call `LRPA.Shutdown()` during normal application shutdown. The façade initializes lazily, creates or attaches to the single Logitech virtual-mouse slot, and removes only a child it created.

For explicit configuration:

```csharp
LRPA.Initialize(new LrpaOptions
{
    DriverPath = @"C:\Windows\System32\drivers\logi_joy_bus_enum.x64.sys",
    CreationSettlingDelayMilliseconds = 750,
    RemoveOwnedMouseOnDispose = true
});
```

## Supported mappings

| WinAPI input | LRPA |
|---|---|
| Relative X/Y movement | Supported |
| Left/right/middle/X1/X2 | Supported |
| Vertical/horizontal wheel | Supported |
| Large relative delta | Split into bounded HID reports |
| Absolute or virtual-desktop coordinates | Rejected |
| Keyboard/hardware input | Rejected |
| `time` and `dwExtraInfo` | Ignored; no HID-report equivalent |

`LRPA.SendInput` validates the complete source array before emitting its first report. It returns the number of source `INPUT` records and throws on validation or driver errors.

## Typed integration

Use `LrpaController` when WinAPI source compatibility is unnecessary:

```csharp
using var mouse = new LrpaController();
bool created = mouse.TryCreateMouse();

mouse.Submit(new MouseReport(MouseButtons.None, 20, -5));
mouse.SetButtons(MouseButtons.Button1);
mouse.SetButtons(MouseButtons.None);
```

`Dispose` removes only a device created by that controller. `ForceRemoveMouse` removes the driver-global virtual mouse regardless of ownership and should be used only as an explicit recovery action.

## Read-only compatibility check

```powershell
dotnet run --project .\src\LRPA.Cli -- protocol
dotnet run --project .\src\LRPA.Cli -- inspect
dotnet run --project .\src\LRPA.Cli -- self-test
```

`protocol` reads and fingerprints the driver. `inspect` additionally verifies that the bus interface can be opened. Neither command creates a virtual mouse.

## Lifecycle and concurrency

- One `LrpaController` owns one open bus handle.
- Controller operations are serialized and report submission allocates no managed report buffer.
- The static façade serializes its controller and button state.
- Independent processes are not coordinated; the mouse child is driver-global.
- A process crash can prevent normal ownership cleanup.

## Deployment

Deploy both assemblies:

- `Logitech.RawPeripheralAccess.dll`
- `Logitech.RawPeripheralAccess.ProtocolDiscovery.dll`

Do not copy or redistribute Logitech drivers. LRPA uses the locally installed G HUB component.

For native C, C++, or Rust integration, publish `LRPA.Native` and follow [FFI.md](FFI.md). Native consumers deploy only `LRPA.Native.dll`; its managed dependencies are compiled into the NativeAOT image.
