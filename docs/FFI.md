# Native FFI integration

LRPA exposes a versioned C ABI from a self-contained NativeAOT Windows x64 DLL. C++, Rust, and other languages use the same exports.

## Build the native library

NativeAOT compilation requires the .NET 10 SDK and an MSVC x64 build toolchain:

```powershell
dotnet publish .\src\LRPA.Native\LRPA.Native.csproj `
  -c Release -r win-x64 `
  -o .\artifacts\native\win-x64
```

The integration artifacts are:

- `artifacts/native/win-x64/LRPA.Native.dll`
- `artifacts/native/win-x64/LRPA.Native.lib`
- `native/include/lrpa.h`
- `native/include/lrpa.hpp`

Consumers do not need a separately installed .NET runtime.

## ABI guarantees

- Windows x64, C calling convention
- ABI version queried with `lrpa_abi_version()`
- Fixed-width fields and compile-time structure-size checks
- Opaque integer handles; no managed pointer crosses the boundary
- No exception crosses the boundary
- Per-thread UTF-8 error messages
- A maximum of 4096 `lrpa_input` records per call

Every binding should validate:

| Query | ABI 1 value |
|---|---:|
| `lrpa_abi_version()` | 1 |
| `lrpa_options_size()` | 32 |
| `lrpa_input_size()` | 40 |
| `lrpa_mouse_report_size()` | 8 |

## C

```c
#include "lrpa.h"

lrpa_handle handle = 0;
int32_t created = 0;

lrpa_result result = lrpa_open(NULL, &handle, &created);
if (result != LRPA_OK) {
    /* call lrpa_last_error */
}

lrpa_input input = {0};
input.type = LRPA_INPUT_MOUSE;
input.data.mouse.dx = 64;
input.data.mouse.dy = -16;
input.data.mouse.flags = LRPA_MOUSEEVENTF_MOVE;

uint32_t sent = 0;
result = lrpa_send_input(handle, 1, &input, sizeof(input), &sent);
lrpa_close(handle);
```

Build both native examples with CMake:

```powershell
cmake -S .\examples\native -B .\out\native-examples `
  -DLRPA_NATIVE_DIR="$PWD\artifacts\native\win-x64"
cmake --build .\out\native-examples --config Release
```

## C++

`lrpa.hpp` provides a movable, non-copyable RAII controller:

```cpp
#include "lrpa.hpp"

lrpa::controller mouse;
mouse.move(64, -16);
```

Its destructor closes the handle without throwing. Other failures become `std::runtime_error` with the native thread-local diagnostic.

## Rust

The repository includes raw declarations and a safe wrapper without external crates:

```rust
use lrpa::{Controller, Input};

let mouse = Controller::open()?;
let sent = mouse.send_input(&[Input::move_relative(64, -16)])?;
```

Build or run with the import-library directory supplied to the crate:

```powershell
$env:LRPA_NATIVE_DIR = (Resolve-Path .\artifacts\native\win-x64)
$env:PATH = "$env:LRPA_NATIVE_DIR;$env:PATH"
cargo run --manifest-path .\examples\rust\Cargo.toml
```

`Controller` validates the ABI version and all structure sizes before opening a device. It is safe to move between threads; native operations on one handle are serialized. `Drop` closes the handle.

## C exports

| Export | Purpose |
|---|---|
| `lrpa_open` | Create an opaque session and create/attach the virtual mouse |
| `lrpa_close` | Close a handle and perform ownership-aware cleanup |
| `lrpa_send_input` | Submit WinAPI-shaped relative mouse records |
| `lrpa_move` | Relative movement convenience call |
| `lrpa_submit_report` | Submit the exact eight-byte typed HID report |
| `lrpa_last_error` | Copy the current thread's UTF-8 diagnostic |
| `lrpa_*_size` | Runtime ABI layout verification |

`lrpa_last_error(NULL, 0, &required)` returns `LRPA_BUFFER_TOO_SMALL` and the required NUL-terminated byte count. Allocate that many bytes and call it again.

## Result codes

| Code | Name | Meaning |
|---:|---|---|
| 0 | `LRPA_OK` | Success |
| 1 | `LRPA_INVALID_ARGUMENT` | Pointer, count, size, flags, or option invalid |
| 2 | `LRPA_INVALID_HANDLE` | Zero, unknown, or closed handle |
| 3 | `LRPA_NOT_SUPPORTED` | Unsupported input type or mode |
| 4 | `LRPA_PROTOCOL_ERROR` | Driver missing, malformed, ambiguous, or unsupported |
| 5 | `LRPA_NATIVE_ERROR` | Bus open or IOCTL failure |
| 6 | `LRPA_INTERNAL_ERROR` | Unexpected implementation failure |
| 7 | `LRPA_BUFFER_TOO_SMALL` | Error buffer is absent or too small |

## Options

Pass `NULL` to `lrpa_open` for safe defaults: remove a process-owned child on close and wait 750 ms after creation.

When passing `lrpa_options`:

- initialize `struct_size = sizeof(lrpa_options)`;
- set `LRPA_OPTION_REMOVE_OWNED_ON_CLOSE` when cleanup is desired;
- set UTF-8 paths to `NULL` for automatic discovery;
- keep `reserved` zero; and
- bound `creation_delay_ms` to `0..5000`.

## Threading and ownership

Different handles may be called concurrently. Calls using the same handle are serialized. Closing a handle waits for an active call on that handle and prevents new calls.

The Logitech virtual mouse is driver-global. A handle records whether it created that child and removes it on close only when the option is enabled. Applications must still coordinate ownership across processes.
