# SDK/API reference

Namespaces:

- `Logitech.RawPeripheralAccess`
- `Logitech.RawPeripheralAccess.ProtocolDiscovery`

## `LRPA`

### `Initialize(LrpaOptions? options = null)`

Creates the process-global controller and creates or attaches to the Logitech virtual mouse. Returns `true` if this process created the child.

### `SendInput(uint count, INPUT[] inputs, int cbSize)`

### `SendInput(INPUT[] inputs)`

Submits validated relative mouse input. The API mirrors the WinAPI function and structures but throws on error instead of setting a thread last-error value.

### `MouseInput(int dx, int dy, uint flags, uint mouseData = 0)`

Constructs one `INPUT_MOUSE` record.

### `Shutdown()`

Releases held buttons, disposes the controller, and removes a process-owned child. Safe to call repeatedly.

## WinAPI-compatible types

- `INPUT`
- `INPUTUNION`
- `MOUSEINPUT`
- `KEYBDINPUT`
- `HARDWAREINPUT`

`INPUT.Size` is 40 in the supported x64 process.

The `LRPA` class exports the standard relative mouse flags, X-button identifiers, input-type values, and `WHEEL_DELTA`.

## `LrpaController`

| Member | Purpose |
|---|---|
| `TryCreateMouse()` | Create the global mouse or return false if it exists |
| `CreateMouse()` | Create the mouse or throw if it exists |
| `Submit(MouseReport)` | Submit one eight-byte report |
| `Move(short, short, MouseButtons)` | Relative movement |
| `SetButtons(MouseButtons)` | Replace the 16-bit button state |
| `Wheel(sbyte, sbyte, MouseButtons)` | Wheel/pan report |
| `Click(MouseButtons, int)` | Bounded press and release |
| `RemoveOwnedMouse()` | Remove only this controller's child |
| `ForceRemoveMouse()` | Explicitly remove the global child |

## `MouseReport`

| Offset | Width | Value |
|---:|---:|---|
| 0 | 2 | 16 button bits |
| 2 | 2 | signed relative X |
| 4 | 2 | signed relative Y |
| 6 | 1 | signed vertical wheel |
| 7 | 1 | signed horizontal pan |

`Write(Span<byte>)` is the allocation-free encoder. `Encode()` is available for diagnostics and tests.

## `LrpaOptions`

| Property | Default |
|---|---|
| `DevicePath` | SetupAPI auto-discovery |
| `DriverPath` | Installed Logitech bus-driver path |
| `Protocol` | Auto-resolved and validated |
| `RemoveOwnedMouseOnDispose` | `true` |
| `CreationSettlingDelayMilliseconds` | `750` |

## Protocol discovery

### `ProtocolResolver.Resolve(string? driverPath = null)`

Returns a process-cached `DriverProtocol` keyed by normalized full path. Restart the process after updating G HUB. The first resolution:

1. reads the small driver image once;
2. parses PE sections;
3. runs registered fingerprints;
4. requires exactly one semantic match; and
5. calculates the image SHA-256 for evidence.

`DriverProtocol` includes the fingerprint ID, driver identity, evidence offsets/RVAs, and recovered create/report/remove IOCTLs.

New recovery strategies belong in `src/LRPA.ProtocolDiscovery/Fingerprints`. They must validate semantics and fail on missing or ambiguous matches.

## Exceptions

| Exception | Meaning |
|---|---|
| `LrpaException` | Native open or IOCTL failure; includes the Win32 code |
| `InvalidDataException` | Malformed, unsupported, or ambiguous driver image |
| `InvalidOperationException` | Interface or device lifecycle conflict |
| `NotSupportedException` | Unsupported WinAPI input type or mode |
| Argument exception | Invalid size, count, flag, or bounded value |
