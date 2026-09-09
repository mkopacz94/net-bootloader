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

- **`src/NetBootloader.App`** - WPF shell, MVVM via
  [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
  (`[ObservableProperty]`/`[RelayCommand]` source generators instead of
  hand-rolled base classes). Split by concern rather than one
  view/viewmodel:
  - `ViewModels/ConnectionViewModel` + `Views/ConnectionView` - COM port,
    baud rate, timeout.
  - `ViewModels/FirmwareViewModel` + `Views/FirmwareView` - `.hex` file
    picker, checksum/reset options.
  - `ViewModels/FlashLogViewModel` + `Views/FlashLogView` - progress bar,
    status text, live packet log.
  - `ViewModels/MainViewModel` - composition root: owns the three
    sub-viewmodels and the `Flash`/`Cancel` commands, since those are the
    only things that need data from more than one of them.
  - `MainWindow.xaml` just lays the three views out and binds the
    action buttons.
  - `Themes/Colors.xaml` + `Themes/Controls.xaml` - the app's visual theme:
    a light palette (blue `Primary`/pink `Secondary` accents, both with
    `.MouseOver`/`.Pressed` variants, plus neutrals for background/surface/
    border/text) and rounded control styles (`Button`, `TextBox`,
    `ComboBox`, `CheckBox`, `ProgressBar`, and a card-style `GroupBox`)
    built on it. Merged into `App.xaml`, so they apply everywhere without
    per-view changes. The `ComboBox` restyle is a full `ControlTemplate`
    override (needed for rounded corners) and is the one piece here that
    couldn't be checked any way short of a real Windows build - flag it if
    the dropdown behaves oddly.

- **`tests/NetBootloader.Core.Tests`** - xUnit tests covering packet
  pack/unpack byte layouts, response-code-to-exception mapping, the Intel HEX
  parser, and chunk alignment/cropping. `BootloaderClientRealCaptureTests`
  replays byte sequences copied verbatim from real serial captures (see
  below) through `BootloaderClient` as regression tests.

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

## Validated against real captures

Three real serial logs were used to check this port against actual bootloader
traffic: two from mcbootflash's own CLI (one full flash, one that hit a real
`Checksum mismatch` failure) and one from Microchip's official Unified
Bootloader Host Application (UBHA) - an independent implementation talking to
the same firmware. All three agree with this port's framing:

- `READ_VERSION` / `GET_MEMORY_ADDRESS_RANGE` field offsets and the `+2`
  half-open range adjustment (checked against the captured
  `0x003800:0x0153FE` -> `MemoryRangeEnd = 0x00015400`).
- `ERASE_FLASH` command encoding (`data_length` = page count, the
  `0x00AA0055` unlock key, little-endian address).
- `CALC_CHECKSUM` response decoding, including the real
  `Checksum mismatch: 3120 != 2100` failure from the CRC_ERR log, replayed
  byte-for-byte as a regression test.
- `READ_FLASH`'s two-part response (an ack, then a separate raw-data read
  sized from the response's echoed `data_length`) - confirmed via UBHA's
  read-back verification step. One real difference surfaced here: UBHA sends
  the flash unlock key even on `READ_FLASH`, while mcbootflash (and this
  port) correctly omits it, since the protocol only documents
  `unlock_sequence` as mattering for `WRITE_FLASH`/`ERASE_FLASH` - see
  `BootloaderClientRealCaptureTests.ReadFlash_MatchesRealCapturedUbhaReadback`.
- The UBHA log also confirmed the bootloader accepts an `ERASE_FLASH` command
  spanning many pages at once (`data_length = 71`) rather than mcbootflash's
  page-by-page approach; `FirmwareFlasher` sticks with page-by-page since it
  drives incremental progress reporting, but a single large erase is also
  valid if you'd rather trade that off for fewer round trips.

## Remaining gaps

- The Intel HEX parser is a generic byte-addressed implementation (record
  types `00`/`01`/`02`/`04`; `03`/`05` are recognized and ignored) and wasn't
  checked against an actual `.hex` file - the captures above cover the wire
  protocol, not hex parsing/chunking. For standard MPLAB X/XC16-generated
  output this should produce identical byte layout (including the "phantom"
  4th byte per 24-bit instruction word), but it's worth diffing chunk output
  against a known-good flash for your specific device family before trusting
  it in production.
- No capture of a `BAD_ADDRESS`-during-erase response was available, so
  `FirmwareFlasher`'s workaround for that (re-erasing the remainder in one
  command, per mcbootflash issue #86) is untested against real traffic -
  only the general response-code-to-exception mapping is unit tested.
- `SerialBootloaderConnection`'s erase-timeout handling is basic: erasing a
  large memory area can take several seconds, so size the connection's
  `timeoutMilliseconds` accordingly before calling `EraseFlash`.
