<div align="center">
    <img width="200" src="assets/steamless.png" alt="steamless">
    </br>
</div>

<div align="center">
    <a href="https://discord.gg/UmXNvjq"><img src="https://img.shields.io/discord/704822642846466096.svg?style=for-the-badge" alt="Discord server" /></a>
    <a href="LICENSE"><img src="https://img.shields.io/badge/license-CCA--NCND%20v4-blue?style=for-the-badge" alt="license" /></a>
    <br/>
    <a href="https://github.com/sponsors/atom0s/"><img src="https://img.shields.io/github/sponsors/atom0s?style=for-the-badge" alt="sponsors" /></a>
    <a href="https://paypal.me/atom0s"><img src="https://img.shields.io/badge/donate-PayPal-blue?style=for-the-badge" alt="paypal" /></a>
    <a href="https://patreon.com/atom0s"><img src="https://img.shields.io/badge/sponsor-Patreon-blue?style=for-the-badge" alt="paypal" /></a>
</div>

# Steamless

> **Fork** — this repository contains fixes, improvements, and modernization not yet upstreamed.
>
> ## Modernization
> - **.NET 9.0 migration** — upgraded from .NET Framework 4.5.2 / .NET 5 to .NET 9.0. SDK pinned via `global.json` (rollForward to latestMajor).
> - **Nullable reference types** — enabled project-wide; existing code opted out with `#nullable disable` for gradual migration.
> - **AssemblyInfo cleanup** — deleted all 11 legacy `Properties/AssemblyInfo.cs` files; metadata now auto-generated from SDK-style `.csproj` properties.
> - **SharpDisasm → Iced** — replaced the abandoned SharpDisasm (native dependency, unmaintained) with Iced v1.21.0 (100% C#, MIT, by 0xd4d/dnlib author) in Variant 2.0 and 2.1 unpackers.
> - **AesHelper modernization** — `AesCryptoServiceProvider` → `Aes.Create()`.
> - **Async pattern cleanup** — removed `async void` methods; added `CancellationToken` support; `Thread.Sleep` → `Task.Delay`.
> - **CLI improvements** — distinct exit codes (1-4); bare `catch {}` replaced with logged stderr output.
> - **DataService sync-refactored** — interface methods are synchronous; UI thread offload handled at ViewModel level.
> - **SplashView fixed** — removed binding to non-existent `FileName` property; shows `Text` and percentage progress.
> - **GUI cleanup** — removed redundant `DataContext` from child views; deleted stale `App.config`.
> - **Infrastructure added** — `.editorconfig` (code style), `.gitattributes` (line endings), `.gitignore` updates.
>
> ## Fixes
> - **PE buffer underflow** — `GetStructure<T>` now checks `offset + size` instead of just `size`.
> - **MemoryMarshal revert** — PE structs contain non-blittable `[MarshalAs(ByValArray)] ushort[]` fields that `MemoryMarshal.Read<T>` cannot handle. Reverted to `Marshal.PtrToStructure` (with `OffsetOf` caching retained).
> - **PlatformTarget fix** — all plugins now build as AnyCPU (MSIL) so they load in a 64-bit host process.
>
> ## Variant 2.1
> - **DRMP offset extraction with fallback chain** — if the hardcoded offset pattern fails, falls back to dynamic disassembly or exhaustive scan (`ScanSteamDrmpOffsets`) to locate valid DRMP offsets, fixing games where the original pattern doesn't match.
> - **Code section decryption fix** — previously the stolen bytes were included in the AES-CBC decryption input, corrupting the output. Now only the encrypted payload is decrypted; stolen bytes are prepended afterward.
> - **Import table reconstruction** — when `.bind` is removed, the import directory entry is relocated to the real descriptor table in `.rdata` via DLL name matching (`FindImportByDllNamePattern`). Fixes HUNTED, TCC, xrrengine.
> - **Certificate table fix** — Authenticode Security directory file offset is updated to the new overlay position after `.bind` removal.
>
> ## Variant 3.0/3.1 — DRMP bounds clamping
> `DRMPDllSize` clamped to file boundary with 8-byte alignment to prevent buffer over-read (fixes nw.exe).
>
> ## Variant 3.1 — Code section decryption corrected
> Stolen bytes and encrypted payload sized were mishandled. Now decrypts only the encrypted portion, then prepends stolen bytes at the correct offset.
>
> Original upstream: [atom0s/Steamless](https://github.com/atom0s/Steamless)

Steamless is a DRM remover of the various SteamStub variants applied to applications and games released on Steam via the DRM tool in the Steamworks SDK.

Steamless aims to be a single solution for unpacking all variants of the SteamStub DRM, ranging from the very first version to the most recently released.

_However, due to personal limited funds, I cannot test every game myself._

# Donations

Want to say thanks for my work on Steamless? Feel free to donate or sponsor me:

  * **GitHub:** https://github.com/users/atom0s/sponsorship
  * **Paypal:** https://www.paypal.me/atom0s
  * **Patreon:** https://www.patreon.com/atom0s

# What Steamless Will Do

Steamless will remove the SteamStub DRM protection layer that is applied via the DRM tool from the Steamworks SDK.

# What Steamless Wont Do

Steamless **WILL NEVER** do any of the following:

  * Steamless will never remove the Steamworks API integration. (via steam_api.dll/steam_api64.dll)
  * Steamless will never include or distribute any emulator for the Steamworks API integration.
  * Steamless will never handle Valve's CEG (Custom Executable Generation) DRM that is used on some older games.
  * Steamless will never promote, encourage, or assist with piracy.
  * Steamless will never assist with bypassing anti-cheats or other protections in place by games.

Do not ask for help with running games without Steam. Your requests will be ignored/blocked.

That is not the scope or goal of this project.

# What is SteamStub DRM?

From the Steamworks documentation:

> Steamworks Digital Rights Management wraps your game's compiled executable and checks to make sure that it is running under an authenticated instance of Steam. This DRM solution is the same as the one used to protect games like Half-Life 2 and Counter-Strike: Source. Steamworks DRM has been heavily road-tested and is customer-friendly.
> In addition to DRM solutions, Steamworks also offers protection for game through day one release by shipping encrypted media to stores worldwide. There's no worry that your game will leak early from the manufacturing path, because your game stays encrypted until the moment you decide to release it. This protection can be added to your game simply by handing us finished bits or a gold master. <br><br>
> ref: hxxps://partner.steamgames.com/documentation/api

# Supported Versions

Steamless currently supports the following SteamStub DRM variants:

  * **SteamStub Variant 1**
    * 32bit version is supported. _(Support for this is only tested with 1 file so far.)_
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

_**Note:** Version numbers are not 'real'. They are superficial and are simply assumed versions based on major changes to the DRM and what has been observed in the various submitted file samples. A better versioning system may come at a later date._

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

Steamless is not intended for malicious use or for the use of obtaining or playing games illegally.
Steamless should only be used on games that you legally purchased and own.

Steamless is not associated with Steam or any of its partners / affiliates.
No code used within Steamless is taken from Valve or any of its partners / affiliates.

Steamless is released for educational purposes in the hopes to learn and understand DRM technologies. 

Use Steamless at your own risk. I, atom0s, am not responsible for what happens while using Steamless. You take full reponsibility for any outcome that happens to you while using this application. Do not distribute unpacked files.
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
- All plugin assemblies are AnyCPU (MSIL) — they load correctly in both 32-bit and 64-bit host processes.
- The solution can be built from Visual Studio or the `dotnet` CLI.

# Contributing To Steamless (Guidelines)

I welcome and encourage contributions to the Steamless project. However, I do have some guidelines I wish for people to follow when doing so.

  * Follow the `.editorconfig` conventions (4-space indentation, `m_` prefix for private fields, etc.).
  * Please do not introduce additional dependencies without a discussion before hand.
  * Please do not alter or remove any copyrights without a discussion prior.
  * Please do not hard code information specific to any one target. Steamless should be dynamic for all titles.
  * New code should be nullable-aware (`#nullable enable`). Existing files use `#nullable disable` and can be converted incrementally.

Discussions can be opened within the Issue tracker here:
  * https://github.com/atom0s/Steamless/issues
