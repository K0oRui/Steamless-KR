<div align="center">
    <img width="350" src="assets/steamless.svg" alt="steamless">
    </br>
</div>

<div align="center">
    <a href="LICENSE"><img src="https://img.shields.io/badge/license-CCA--NCND%20v4-blue?style=for-the-badge" alt="license" /></a>
    <a href="https://github.com/K0oRui/Steamless-KR/actions/workflows/release.yml"><img src="https://img.shields.io/github/actions/workflow/status/K0oRui/Steamless-KR/release.yml?style=for-the-badge&label=build" alt="build" /></a>
</div>

# Steamless-KR

Steamless-KR is a maintained fork of [atom0s/Steamless](https://github.com/atom0s/Steamless). It removes the SteamStub DRM wrapper from Steam games and applications. Point it at a protected executable and it writes the restored original next to it as `<name>.unpacked.exe`. It handles the SteamStub variants from 1.0 through 3.1, and picks the right unpacker for the file on its own.

The fork builds on the modernization work that first appeared in [TheReaperJay/Steamless](https://github.com/TheReaperJay/Steamless). Releases and plugins live in this repository, not upstream.

## About this fork

Steamless-KR keeps the unpacker design and the stub research of the original project and maintains them here.

- The desktop app is rebuilt on Avalonia and shares one unpacking engine with the CLI, so a run behaves the same in both.
- All five variants ship as one plugin set. The plugin API is 2.0, and a plugin DLL built against a different API version is refused at load with a message naming both versions.
- Releases come from this repository's workflow as x64 and x86 Windows archives plus a Linux CLI archive.
- Problems belong in this repository's issue tracker. Upstream does not track changes made here.

## Features

- Unpacks SteamStub 1.0, 2.0 and 2.1 in their 32-bit form, and 3.0 and 3.1 in both 32-bit and 64-bit builds. Automatic mode tries each unpacker in turn and stops at the first that claims the file.
- Accepts a single executable or a whole folder. Folder input walks every `.exe` it finds, skips anything it already unpacked, and reports a total at the end.
- Lets you pin the layout yourself. The plugin picker has one entry per variant, with 3.0 and 3.1 listed separately.
- Streams progress to the log and lets you cancel a run. Work happens off the UI thread, so the window stays responsive on large files.
- Checks section sizes, alignments and stub-controlled offsets before it allocates or copies anything, so a damaged file fails with a message instead of taking the app down.
- Never leaves a half-written output. The unpacked file is built in a temporary file and moved into place only after the write succeeds.
- Reports without touching anything. `--detect` prints the PE flavour, whether a `.bind` section exists, and which plugins claim the file, which is useful when you just want to identify a stub.

## Supported variants

| Variant | 32-bit | 64-bit |
|---|---|---|
| SteamStub 1.0 | yes | no |
| SteamStub 2.0 | yes | no |
| SteamStub 2.1 | yes | no |
| SteamStub 3.0 | yes | yes |
| SteamStub 3.1 | yes | yes |

Version numbers are not official. They are labels the project assigns to major changes seen in submitted samples.

## Using the desktop app

Start `Steamless.exe`. Browse to a file or a folder, leave the plugin on Automatic unless you know the variant, then press Unpack. The log fills in as the unpacker works and the button turns into Cancel for as long as a run is active. Results land beside their source file.

The options panel exposes the same settings the command line takes, with a tooltip on each one:

- Verbose Output shows debug messages. Turn it on when a file will not unpack.
- Keep Bind Section keeps `.bind`, which holds SteamStub's DRM code. Only useful for studying the stub.
- Dump Payload To Disk writes the stub payload as a `.payload` file.
- Dump SteamDRMP.dll To Disk writes the embedded DLL next to the target.
- Don't Realign Sections leaves section values alone to keep the file small. Clearing it rounds sections up to alignment boundaries, which some loaders need.
- Zero DOS Stub Data overwrites the DOS stub area with zeros. SteamStub hides code there, so leave this on, or clear it to keep the stub.
- Recalculate File Checksum updates the PE checksum after unpacking. Drivers need a valid checksum, most files do not.

Starting the app twice raises the existing window instead of opening a second copy.

## Using the command line

```
Steamless.CLI.exe [options] [file or folder]
```

Unpack one file:

```powershell
Steamless.CLI.exe "C:\Games\MyGame\MyGame.exe"
```

Unpack a whole folder and keep the stub's payload dumps:

```powershell
Steamless.CLI.exe --dumppayload --dumpdrmp "C:\Games\MyGame"
```

| Option | Effect |
|---|---|
| `--detect` | Report the bitness, the `.bind` section and the matching plugins, without unpacking. |
| `--quiet` | Hide debug messages. |
| `--keepbind` | Keep the `.bind` section in the unpacked file. |
| `--keepstub` | Keep the DOS stub in the unpacked file. |
| `--dumppayload` | Write the stub payload to disk. Only 2.1 and 3.x stubs carry one. |
| `--dumpdrmp` | Write `SteamDRMP.dll` to disk. |
| `--realign` | Realign the unpacked file's sections. |
| `--recalcchecksum` | Update the unpacked file's checksum. |

A single file or a folder works as the input. A folder is walked for `.exe` files and every match goes through the same unpacker.

The exit code tells a script what happened. `0` means the file unpacked, `1` bad or missing arguments (running with no input at all also returns 1), `2` an input that does not exist, `3` no plugins loaded, `4` nothing claimed the file, and `5` a plugin claimed it and then failed. `--detect` returns `0` when it printed a report and `4` when a folder held no executables.

## What Steamless-KR won't do

Steamless-KR is not a general purpose Steam bypass tool. It:

- Does not remove the Steamworks API integration (`steam_api.dll` / `steam_api64.dll`).
- Does not include or distribute a Steamworks API emulator.
- Does not handle Valve's CEG (Custom Executable Generation) DRM used on some older games.
- Does not promote or encourage piracy.
- Does not help bypass anti-cheat or other game protections.

Do not ask for help running games without Steam. Requests will be ignored.

## What SteamStub DRM is

SteamStub is the wrapper Steam applies to an executable when a developer turns on DRM for a build. Steam applies it server side, either through the `drm_wrap` command in a SteamPipe upload script or by hand from the App Admin page, and the wrapped file comes back as the build's executable. At runtime the wrapper launches Steam, checks ownership, and refuses to start otherwise.

The wrapper also obfuscates the file. SteamStub adds a `.bind` section, encrypts the original code section, and inserts a stub that runs before the game's entry point and restores the code at runtime. Steamless reverses those steps and drops the stub.

Valve does not claim this stops determined copying. The Steamworks documentation says the wrapper "protects against extremely casual piracy" and is "easily removed by a motivated attacker", and it points developers at Steamworks features instead. SteamStub still appears in a lot of older builds, which is what Steamless is for.

Source: [Steam DRM, Steamworks documentation](https://partner.steamgames.com/doc/features/drm).

## Compiling

You need the .NET 10.0 SDK, which `global.json` pins with rollForward to latestMajor, and either an IDE that builds C# or the `dotnet` CLI.

```powershell
# Build the solution
dotnet build Steamless.sln -c Release

# Run the CLI
dotnet run --project .\src\Steamless.CLI -c Release -- <options>

# Run the desktop app
dotnet run --project .\src\Steamless -c Release

# Publish a 32-bit desktop build
dotnet publish .\src\Steamless\Steamless.csproj -c Release -r win-x86 --self-contained false
```

Released binaries are framework-dependent and need the .NET 10.0 runtime, on Windows or Linux. Windows releases ship as `windows-x64` and `windows-x86` zip archives, each holding the desktop app, `Steamless.CLI.exe` and the `Plugins` folder. Linux ships as a `tar.gz` with the CLI only. The x86 build runs on 32-bit and 64-bit Windows 10 1607 or newer and needs the x86 runtime even on a 64-bit machine. Avalonia lists x86 Windows as tier 3, so that build ships with lower support expectations.

## Contributing

- Follow the `.editorconfig` conventions (4-space indentation, `m_` prefix for private fields, and so on).
- Discuss new dependencies before adding them.
- Do not alter or remove copyright notices without prior discussion.
- Do not hard-code information about one specific target. Steamless-KR should stay dynamic for all titles.
- Write nullable-aware code (`#nullable enable`). Older files still use `#nullable disable` and can be converted as they are touched.
- Plugin DLLs load from `Plugins/` at runtime. A solution build stages them into the output folder, along with `Steamless.Unpacker.Common.dll` and `Iced.dll`.

## Legal

Steamless-KR carries the license of the project it forked from, [Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International](LICENSE). The material comes as-is, with no warranty of any kind, and commercial use is not allowed.

Steamless-KR is not intended for malicious use or for obtaining or playing games illegally. Use it only on games you legally own, and do not distribute unpacked files.

Steamless-KR is not associated with Steam, Valve, or their partners and affiliates, and no code in it comes from Valve. It exists to study and understand DRM.

## Thanks

atom0s wrote the original Steamless and its unpackers, and TheReaperJay did the modernization this fork started from.

Cyanic (aka Golem_x86) supplied notes and help with parts of the stub headers. His write-up is at http://pcgamingwiki.com/wiki/User:Cyanic/Steam_DRM
