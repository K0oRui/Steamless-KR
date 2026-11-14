<div align="center">
    <img width="350" src="assets/steamless.svg" alt="steamless">
    </br>
</div>

<div align="center">
    <a href="LICENSE"><img src="https://img.shields.io/badge/license-CCA--NCND%20v4-blue?style=for-the-badge" alt="license" /></a>
    <a href="https://github.com/K0oRui/Steamless-KR/actions/workflows/release.yml"><img src="https://img.shields.io/github/actions/workflow/status/K0oRui/Steamless-KR/release.yml?style=for-the-badge&label=build" alt="build" /></a>
</div>

# Steamless

> This fork starts from [atom0s/Steamless](https://github.com/atom0s/Steamless). The changes below are the delta against that upstream.
>
> ### GUI
>
> - The WPF shell is now Avalonia 12, with a custom title bar, splash screen, animated About page, ShadUI controls, Lucide icons, and a vector logo.
> - Input accepts a file or a folder. Folder input is walked for `.exe` files, skipping `.unpacked.exe` and reparse points, and every executable found is offered to the selected plugin.
> - The plugin picker lists one entry per SteamStub variant, with 3.0 and 3.1 as separate entries backed by one module. That module reads the layout from the stub's declared header size and the bitness from each file, so both entries are labelled x86/x64 and the separate x86 and x64 copies are gone. Picking a variant pins the run to that layout.
> - Unpacking runs off the UI thread and can be cancelled. Options are snapshotted per run, log messages are marshaled to the UI thread in batches, and the log is capped at 5,000 entries.
> - MvvmLight and the custom task queue are gone, along with the `IDataService` indirection. The view model uses async/await and CommunityToolkit.Mvvm source generators, with services composed in `App`.
> - A second launch raises the existing window instead of starting another instance.
> - On Windows the GUI uses Avalonia's software renderer, so it does not need a GPU or GL context.
> - Icons come from a small built-in control with path data for only the icons the app uses, replacing the `Lucide.Avalonia` package.
>
> ### Unpacking
>
> - Variant 2.1 resolves SteamDRMP offsets in stages. It tries alternate hardcoded layouts, then dynamic disassembly, then a validated exhaustive scan. If the stored code-section VA is zero, it finds the section from the OEP.
> - Variants 3.0 and 3.1 locate the stub header at the entry point, then at the TLS callback, then by scanning `.bind`.
> - Variant 2.0 uses the stub header's code section only when `BaseOfCode` does not map to a valid section, instead of overriding a valid `BaseOfCode`.
> - One `PeImage` reads and writes both PE32 and PE32+, and one unpacker template owns the flow every SteamStub variant shares: load the image, open `.bind`, read the header, run the variant steps, save. A plugin supplies only its claim rule, its header layout and its own steps.
> - One shared library holds the pieces more than one variant needs: the rolling XOR and XTEA ciphers, the DRM flags, the header-size probes, `.bind` removal with its header repairs, the Iced disassembly walk, TLS callback repair, the payload and SteamDRMP.dll decrypts, the code-section AES, and the offsets variant 2.1 reads out of its payload. The 3.0 and 3.1 stubs are one plugin with two header layouts, and 2.1 no longer carries its own copies of the payload, cipher and code-section code.
> - Output is written to a temporary file and moved into place only after the write succeeds, so a failed run cannot leave a truncated `.unpacked.exe`.
>
> ### Memory and I/O
>
> - Parsing keeps the file in one buffer. `PeImage` holds each section as its 40 header bytes plus the original file offset, and materialises a section body only when a caller asks for it. Saving streams untouched sections straight from that buffer and zero-pads them to the file alignment.
> - The host parses each file once and hands the image to every plugin, so a packed executable is read once rather than once per plugin. A plugin that claims a file and then gives up can leave that image changed, so the next plugin gets a fresh read of the file. `PeImage.Load` returns a result that names the check which rejected a file, and the log prints that reason instead of guessing from a null.
> - Plugins load with `Assembly.LoadFrom`, so the runtime maps each DLL instead of holding a copy of its image on the managed heap.
> - `PeImage.Save` is the only place that writes the unpacked PE. It lays the file out into a temp stream, folds in the checksum when asked, and moves the result into place, so the caller cannot hand it a section from another image or leave a half-written file behind.
> - The checksum is computed over that stream with its own field zeroed and patched before the move, so a large executable is not opened a second time and a failed save cannot leave a stale checksum behind.
> - AES decrypts into the destination buffer. The helper writes with chunked `TransformBlock` calls, and the 2.1 and 3.x unpackers pass the stolen block and ciphertext to `Decrypt(prefix, source, ...)` so the two are never joined into a second full-size copy.
>
> ### Safety
>
> - PE parsing rejects non-power-of-two or oversized alignments, sections past EOF, and an `e_lfanew` below the DOS header, and stops reading a TLS callback list at the end of the file. Stub-controlled sizes and offsets are checked before allocating or copying in every unpacker.
> - The Windows-only `MapFileAndCheckSum` call is replaced by a managed checksum that zeroes the field before computing and pads odd-length files correctly. `FindPattern` no longer reads past the buffer, section reads clamp to the bytes the file actually holds, zero alignment is treated as no alignment, and `GetStructure` validates the whole `[offset, offset+size)` range. The overlay starts after the last section that has a body, so a bodyless section cannot make a save append the whole input to itself.
> - AES uses `Aes.Create()`, and `RebuildIv` no longer writes over its own input.
>
> ### Build and tooling
>
> - .NET Framework 4.5.2 is gone. The projects are SDK-style, target .NET 10, enable nullable at the project level, and pin the SDK in `global.json`. Source lives under `src/`, project metadata replaces the legacy `AssemblyInfo.cs` files, and plugins are AnyCPU.
> - Variants 2.0 and 2.1 use Iced 1.21 instead of the embedded SharpDisasm DLL.
> - `PluginLoader` is shared by the CLI and GUI. It skips non-plugin and dependency DLLs and enforces the API version attribute. Both hosts resolve plugin dependencies from `Plugins/` and report failures per plugin. The plugin API is 1.2: an unpacker receives a parsed `PeImage` rather than a path, so a DLL built against an older API version is refused at load with a message naming both versions.
> - The CLI and GUI drive the same `UnpackSession`. Folder expansion, automatic dispatch, plugin selection, cancellation and report totals live in the API, so each host only translates its own input and output.
> - The CLI takes `--detect`, which prints the PE flavour, `.bind` presence and the claiming plugins, naming the 3.x layout when the merged plugin claims a file. The test corpus scripts sort and validate files from that output instead of keeping their own copy of the signatures. It also accepts a folder, the same as the GUI, and `--detect` prints one block per executable it finds there.
> - The CLI returns 0 on success, 1 for a usage error, 2 for a missing input, 3 when no plugins load, 4 when nothing claims the file, and 5 when a plugin claimed it and failed.
> - The release workflow builds Windows (GUI and CLI) for x64 and x86, plus Linux (CLI), publishes to GitHub and Codeberg, and the lint workflow runs `dotnet format`, the PE checks in `tests/Steamless.Tests` and CodeQL.
>
> Original upstream: [atom0s/Steamless](https://github.com/atom0s/Steamless)
> Modernization originally developed in: [TheReaperJay/Steamless](https://github.com/TheReaperJay/Steamless)

Steamless removes the SteamStub DRM protection from Steam games and applications. It handles every known variant, from the first version to the most recent.

# What Steamless does

Steamless removes the SteamStub DRM protection layer applied via the DRM tool from the Steamworks SDK.

# What Steamless won't do

Steamless is not a general-purpose Steam bypass tool. It:

  * Does not remove the Steamworks API integration (`steam_api.dll` / `steam_api64.dll`).
  * Does not include or distribute a Steamworks API emulator.
  * Does not handle Valve's CEG (Custom Executable Generation) DRM used on some older games.
  * Does not promote, encourage, or assist with piracy.
  * Does not help bypass anti-cheats or other game protections.

Do not ask for help running games without Steam. Requests will be ignored.

# What is SteamStub DRM

From the Steamworks documentation:

> Steamworks Digital Rights Management wraps your game's compiled executable and checks to make sure that it is running under an authenticated instance of Steam. This DRM solution is the same as the one used to protect games like Half-Life 2 and Counter-Strike: Source. Steamworks DRM has been heavily road-tested and is customer-friendly.
> In addition to DRM solutions, Steamworks also offers protection for game through day one release by shipping encrypted media to stores worldwide. There's no worry that your game will leak early from the manufacturing path, because your game stays encrypted until the moment you decide to release it. This protection can be added to your game simply by handing us finished bits or a gold master. <br><br>
> ref: hxxps://partner.steamgames.com/documentation/api

# Supported versions

Steamless currently supports the following SteamStub DRM variants:

  * **SteamStub Variant 1.0**
    * 32bit version is supported.
  * **SteamStub Variant 2.0**
    * 32bit version is supported.
  * **SteamStub Variant 2.1**
    * 32bit version is supported.
  * **SteamStub Variant 3.0**
    * 32bit version is supported.
    * 64bit version is supported.
  * **SteamStub Variant 3.1**
    * 32bit version is supported.
    * 64bit version is supported.

_**Note:** Version numbers are not 'real'. They are superficial and are simply assumed versions based on major changes to the DRM and what has been observed in the various submitted file samples._

# Legal

```
Steamless is released under the following license:
Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International

Unless otherwise separately undertaken by the Licensor, to the extent possible, the Licensor offers the Licensed Material 
as-is and as-available, and makes no representations or warranties of any kind concerning the Licensed Material, whether 
express, implied, statutory, or other. This includes, without limitation, warranties of title, merchantability, fitness 
for a particular purpose, non-infringement, absence of latent or other defects, accuracy, or the presence or absence of 
errors, whether or not known or discoverable. Where disclaimers of warranties are not allowed in full or in part, this 
disclaimer may not apply to You.

Steamless is not intended for malicious use or for obtaining or playing games illegally.
Use Steamless only on games you legally purchased and own.

Steamless is not associated with Steam or any of its partners / affiliates.
No code used within Steamless is taken from Valve or any of its partners / affiliates.

Steamless is released for educational purposes in the hopes to learn and understand DRM technologies. 

Use Steamless at your own risk. I, atom0s, am not responsible for what happens while using Steamless. You take full responsibility for any outcome while using this application. Do not distribute unpacked files.
```

# Thanks

Thanks to Cyanic (aka Golem_x86) for his notes and help with parts of the stub headers and such.<br>
You can find his information here: http://pcgamingwiki.com/wiki/User:Cyanic/Steam_DRM

# Compiling Steamless

**Requirements:**
- .NET 10.0 SDK (pinned via `global.json`, rollForward to latestMajor)
- Any C# IDE or `dotnet` CLI

**Build commands:**
```powershell
# Build solution
dotnet build Steamless.sln -c Release

# Run CLI
dotnet run --project .\src\Steamless.CLI -c Release -- <options>

# Run GUI
dotnet run --project .\src\Steamless -c Release

# Run the PE reader, writer and stub checks
dotnet run --project .\tests\Steamless.Tests -c Release

# Publish a 32-bit GUI
dotnet publish .\src\Steamless\Steamless.csproj -c Release -r win-x86 --self-contained false
```

**Notes:**
- The checks in `tests/Steamless.Tests` build their own PE fixtures, so they need no game binaries and run in CI. The unpacker corpus harness in `tests/` is separate and stays local.
- The released binaries are framework-dependent. They need the .NET 10.0 runtime installed on Windows and Linux.
- Windows releases ship as two `.zip` archives, `windows-x64` and `windows-x86`. The x86 build runs on 32-bit and 64-bit Windows 10 1607 or newer and needs the x86 .NET runtime even on a 64-bit machine. Avalonia lists x86 Windows as tier 3, so it is built but not part of its test matrix.
- Source code is under `src/`. All plugin DLLs are loaded dynamically from `Plugins/` at runtime. A solution build stages the plugin DLLs, `Steamless.Unpacker.Common.dll` and `Iced.dll` into the output `Plugins/` folder. The release workflow stages them into the release archive too.
- All plugin assemblies are AnyCPU (MSIL), so they load in both 32-bit and 64-bit host processes.
- The solution can be built from Visual Studio or the `dotnet` CLI.

# Contributing

  * Follow the `.editorconfig` conventions (4-space indentation, `m_` prefix for private fields, etc.).
  * Do not introduce additional dependencies without prior discussion.
  * Do not alter or remove any copyrights without prior discussion.
  * Do not hard-code information specific to one target. Steamless should be dynamic for all titles.
  * New code should be nullable-aware (`#nullable enable`). Existing files use `#nullable disable` and can be converted incrementally.
