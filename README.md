# net-bootloader

A .NET/WPF client for flashing firmware to Microchip 16-bit MCUs/DSCs (PIC24,
dsPIC33) running the MPLAB Code Configurator UART bootloader, over a serial
connection.

This is a C# port of the wire protocol and flashing workflow implemented by
[mcbootflash](https://github.com/bessman/mcbootflash) (Python). Class and
method names were kept close to the original so the two can be cross-referenced.

## Structure

- **`src/NetBootloader.Core`** - platform-agnostic class library with no WPF
  dependency:
  - `Protocol/` - wire-format packet types (`BootloaderCommand`,
    `VersionResponse`, `Response`, `MemoryRangeResponse`, `ChecksumResponse`),
    each with a `Pack`/`Unpack` that matches mcbootflash's `protocol.py`
    layout exactly (little-endian, no padding).
  - `Exceptions/` - `BootloaderException` and its subclasses, one per
    bootloader error response code.
  - `Communication/` - `IBootloaderConnection` (mirrors mcbootflash's
    `Connection` protocol) and `SerialBootloaderConnection`, a
    `System.IO.Ports.SerialPort`-backed implementation.
  - `HexFile/` - a small Intel HEX parser and a chunker that splits a parsed
    image into write-size-aligned, max-packet-length-sized pieces.
  - `BootloaderClient` - one method per bootloader command
    (`GetBootAttributes`, `EraseFlash`, `WriteFlash`, `SelfVerify`,
    `VerifyChecksum`, `Reset`, `ReadFlash`), a direct translation of
    `flash.py`.
  - `FirmwareFlasher` - the high-level handshake/erase/write/verify/reset
    workflow from mcbootflash's CLI (`__main__.py`), as an awaitable
    operation with `IProgress<FlashProgressReport>` support and
    cancellation, for driving a UI.

- **`src/NetBootloader.App`** - WPF shell: pick a COM port and baud rate,
  browse for a `.hex` file, flash it with a progress bar and a live packet
  log (`MainWindow.xaml` / `MainViewModel`).

- **`tests/NetBootloader.Core.Tests`** - xUnit tests covering packet
  pack/unpack byte layouts, response-code-to-exception mapping, the Intel HEX
  parser, and chunk alignment/cropping.

## Building

```
dotnet build NetBootloader.sln   # Core + tests build on any OS
dotnet test                      # runs the Core test suite
```

`NetBootloader.App` targets `net8.0-windows` with `UseWPF`, so it only builds
on Windows (or with the Windows desktop workload installed) - this sandbox
could build and test `NetBootloader.Core` but not the WPF project itself.
Review `MainWindow.xaml`/`MainViewModel.cs` on a Windows machine before
relying on them.

## Known gaps / things to verify against real hardware

- The Intel HEX parser is a generic byte-addressed implementation (record
  types `00`/`01`/`02`/`04`; `03`/`05` are recognized and ignored). It was
  not validated against mcbootflash's `bincopy`-based Microchip-specific
  handling - for standard MPLAB X/XC16-generated `.hex` output this should
  produce identical byte layout (including the "phantom" 4th byte per
  24-bit instruction word), but it's worth diffing chunk output against a
  known-good flash for your specific device family before trusting it in
  production.
- No real device or serial logs were available while writing this - the
  protocol layout, command codes, and workflow come from mcbootflash's
  source and its checked-in test fixtures, not from hardware captures.
  Validate `BootloaderClient`/`FirmwareFlasher` against your target before
  shipping.
- `SerialBootloaderConnection`'s erase-timeout handling is basic: erasing a
  large memory area can take several seconds, so size the connection's
  `timeoutMilliseconds` accordingly before calling `EraseFlash`.
