<div align="center">
    <img width="200" src="assets/steamless.png" alt="steamless">
    </br>
</div>

<div align="center">
    <a href="LICENSE"><img src="https://img.shields.io/badge/license-CCA--NCND%20v4-blue?style=for-the-badge" alt="license" /></a>
    <a href="https://github.com/K0oRui/Steamless/actions/workflows/build.yml"><img src="https://img.shields.io/github/actions/workflow/status/K0oRui/Steamless/build.yml?style=for-the-badge&label=build" alt="build" /></a>
</div>

# Steamless

> **Fork.** This repository contains fixes, improvements, and modernization not yet upstreamed.
>
> ## Modernization
>
> - .NET 9.0. Upgraded from .NET Framework 4.5.2 / .NET 5. SDK pinned via `global.json` (rollForward to latestMajor).
> - MvvmLight replaced with CommunityToolkit.Mvvm. DI now uses Microsoft.Extensions.DependencyInjection.
> - SharpDisasm replaced with Iced in the Variant 2.0 and 2.1 unpackers. SharpDisasm is abandoned and ships a native dependency; Iced is pure C#.
> - Nullable reference types enabled project-wide. Existing files opt out with `#nullable disable`.
> - All 11 legacy `Properties/AssemblyInfo.cs` files deleted. Metadata now comes from SDK-style `.csproj` properties.
> - `AesCryptoServiceProvider` replaced with `Aes.Create()`.
> - `async void` removed. `Thread.Sleep` replaced with `Task.Delay`; tasks take a `CancellationToken`.
> - CLI returns distinct exit codes (1-4) and logs errors to stderr instead of swallowing them.
> - Added `.editorconfig`, `.gitattributes`, `.gitignore`, `Directory.Build.props`, `global.json`.
> - Source moved under `src/`, logo under `assets/`, stale files removed.
>
> ## Fixes
>
> - PE buffer underflow. `GetStructure<T>` checks `offset + size` against the buffer length, not just `size`.
> - MemoryMarshal revert. The DOS header struct has non-blittable `[MarshalAs(ByValArray)] ushort[]` fields, which `MemoryMarshal.Read<T>` can't handle. `GetStructure<T>` uses `Marshal.PtrToStructure` again.
> - PE header offset corruption. `Unsafe.SizeOf` returns the wrong size for the non-blittable DOS header, which undercounted `DosStubSize` and shifted the PE signature. `Marshal.SizeOf` is used consistently now.
> - PlatformTarget. All plugins build as AnyCPU so they load in a 64-bit host process.
> - FindPattern. The LINQ-per-byte scan could read out of bounds. Replaced with a nested loop and a bounds check.
> - PE checksum. The existing `CheckSum` field wasn't zeroed before computing the new value. Fixed in `UpdateFileChecksum`.
> - GetSectionData off-by-one. `>` let `Sections.Count` be used as a valid index; now `>=`.
>
> ## Variant 2.1
>
> - DRMP offset extraction with a fallback chain. If the hardcoded offset patterns fail, it tries dynamic disassembly, then an exhaustive scan (`ScanSteamDrmpOffsets`), validating each candidate.
> - Code section located via the OEP when the stored VA offset is zero. Some SteamDRMP variants don't store that field.
> - Import table reconstruction. When `.bind` is removed, the import directory entry is relocated to the real descriptor table in `.rdata` by matching DLL names.
> - Certificate table fix. The Authenticode security directory file offset is updated to the new overlay position after `.bind` removal.
>
> ## Refactoring
>
> - Import table reconstruction extended to all five x86 variants (Variant 1.0, 2.0, 2.1, 3.0, 3.1).
> - Variant 2.0: `DisassembleFile` had two identical `mov reg, imm` checks; merged into one `if/else if`.
> - Variant 3.0 x64: `RebuildTlsCallbackInformation` uses `Array.Copy` instead of LINQ and bounds-checks the entry data.
> - Variant 3.1 x86: `CodeSectionIndex` guard added with error logging.
> - Variant 2.1: state properties are `private`; `TryGetSteamDrmpOffsets` removed; slicing bounds-checked.
> - Dead code removed across the unpackers: unused parameters, dead properties, stale string scans.
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

  * **SteamStub Variant 1**
    * 32bit version is supported.
  * **SteamStub Variant 2**
    * **v2.0.0**
      * 32bit version is supported.
    * **v2.0.1**
      * 32bit version is supported.
  * **SteamStub Variant 3**
    * **v3.0.0**
      * 32bit version is supported.
      * 64bit version is supported.
    * **v3.0.1**
      * 32bit version is supported.
      * 64bit version is supported.
    * **v3.1.0**
      * 32bit version is supported.
      * 64bit version is supported.
    * **v3.1.2**
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
- .NET 9.0 SDK (pinned via `global.json`, rollForward to latestMajor)
- Any C# IDE or `dotnet` CLI

**Build commands:**
```powershell
# Build solution
dotnet build Steamless.sln -c Release

# Run CLI
dotnet run --project .\src\Steamless.CLI -c Release -- <options>

# Run GUI
dotnet run --project .\src\Steamless -c Release
```

**Notes:**
- Source code is under `src/`. All plugin DLLs are loaded dynamically from `Plugins/` at runtime. After building, copy the plugin DLLs and `Iced.dll` (for Variant 2.x) into the output `Plugins/` folder, or use the provided CI scripts.
- All plugin assemblies are AnyCPU (MSIL), so they load in both 32-bit and 64-bit host processes.
- The solution can be built from Visual Studio or the `dotnet` CLI.

# Contributing

  * Follow the `.editorconfig` conventions (4-space indentation, `m_` prefix for private fields, etc.).
  * Do not introduce additional dependencies without prior discussion.
  * Do not alter or remove any copyrights without prior discussion.
  * Do not hard-code information specific to one target. Steamless should be dynamic for all titles.
  * New code should be nullable-aware (`#nullable enable`). Existing files use `#nullable disable` and can be converted incrementally.
