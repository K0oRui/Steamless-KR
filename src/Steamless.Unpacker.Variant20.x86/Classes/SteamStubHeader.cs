#nullable disable

/**
 * Steamless - Copyright (c) 2015 - 2024 atom0s [atom0s@live.com]
 *
 * This work is licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by-nc-nd/4.0/ or send a letter to
 * Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.
 *
 * By using Steamless, you agree to the above license and its terms.
 *
 *      Attribution - You must give appropriate credit, provide a link to the license and indicate if changes were
 *                    made. You must do so in any reasonable manner, but not in any way that suggests the licensor
 *                    endorses you or your use.
 *
 *   Non-Commercial - You may not use the material (Steamless) for commercial purposes.
 *
 *   No-Derivatives - If you remix, transform, or build upon the material (Steamless), you may not distribute the
 *                    modified material. You are, however, allowed to submit the modified works back to the original
 *                    Steamless project in attempt to have it added to the original project.
 *
 * You may not apply legal terms or technological measures that legally restrict others
 * from doing anything the license permits.
 *
 * No warranties are given.
 */

namespace Steamless.Unpacker.Variant20.x86.Classes
{
    using System;
    using System.Runtime.InteropServices;

    // Matches the 856-byte header variant selected during disassembly.
    [StructLayout(LayoutKind.Sequential)]
    public struct SteamStub32Var20_856_Header : ISteamStub32Var20Header
    {
        public uint XorKey1;
        public uint XorKey2;
        public uint GetModuleHandleA_idata;
        public uint GetProcAddress_idata;
        public uint GetModuleHandleW_idata;
        public uint Flags;                     // Protection flags used with the file (see DrmFlags).
        public uint Unknown0000;               // Used as part of a hash check when (Flags & 0x10) is set.
        public uint BindSectionVirtualAddress;
        public uint BindSectionCodeSize;
        public uint BindSectionHash;           // Hash of the .bind code and stub header data. (Only used if (Flags & 1) is set.)
        public uint OEP;
        public uint CodeSectionVirtualAddress;
        public uint CodeSectionSize;
        public uint CodeSectionXorKey;         // Starting key to xor decode against. (Only used if (Flags & 4) is set.)
        public uint SteamAppId;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x08)]
        public byte[] SteamAppIDString;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x314)]
        public byte[] StubData;                 // Misc stub data: strings, error messages, etc.

        uint ISteamStub32Var20Header.Flags => Flags;
        uint ISteamStub32Var20Header.OEP => OEP;
        uint ISteamStub32Var20Header.CodeSectionVirtualAddress => CodeSectionVirtualAddress;
        uint ISteamStub32Var20Header.CodeSectionSize => CodeSectionSize;
        uint ISteamStub32Var20Header.CodeSectionXorKey => CodeSectionXorKey;
    }

    // Matches the 884-byte header variant selected during disassembly.
    [StructLayout(LayoutKind.Sequential)]
    public struct SteamStub32Var20_884_Header : ISteamStub32Var20Header
    {
        public uint XorKey1;
        public uint XorKey2;
        public uint GetModuleHandleA_idata;
        public uint GetProcAddress_idata;
        public uint LoadLibraryA_idata;
        public uint GetProcAddress_custom;
        public uint Flags;                     // Protection flags used with the file (see DrmFlags).
        public uint Unknown0000;               // Used as part of a hash check when (Flags & 0x10) is set.
        public uint BindSectionVirtualAddress;
        public uint BindSectionCodeSize;
        public uint BindSectionHash;           // Hash of the .bind code and stub header data. (Only used if (Flags & 1) is set.)
        public uint OEP;
        public uint CodeSectionVirtualAddress;
        public uint CodeSectionSize;
        public uint CodeSectionXorKey;         // Starting key to xor decode against. (Only used if (Flags & 4) is set.)
        public uint SteamAppId;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x08)]
        public byte[] SteamAppIDString;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x32C)]
        public byte[] StubData;                 // Misc stub data: strings, error messages, etc.

        uint ISteamStub32Var20Header.Flags => Flags;
        uint ISteamStub32Var20Header.OEP => OEP;
        uint ISteamStub32Var20Header.CodeSectionVirtualAddress => CodeSectionVirtualAddress;
        uint ISteamStub32Var20Header.CodeSectionSize => CodeSectionSize;
        uint ISteamStub32Var20Header.CodeSectionXorKey => CodeSectionXorKey;
    }

    // Matches the 952-byte header variant selected during disassembly.
    [StructLayout(LayoutKind.Sequential)]
    public struct SteamStub32Var20_952_Header : ISteamStub32Var20Header
    {
        public uint XorKey1;
        public uint XorKey2;
        public uint GetModuleHandleA_idata;
        public uint GetProcAddress_idata;
        public uint GetModuleHandleW_idata;
        public uint GetProcAddress_bind;
        public uint Flags;                      // Protection flags used with the file (see DrmFlags).
        public uint Unknown0000;                // Used as part of a hash check when (Flags & 0x10) is set.
        public uint BindSectionVirtualAddress;
        public uint BindSectionCodeSize;
        public uint BindSectionHash;            // Hash of the .bind code and stub header data. (Only used if (Flags & 1) is set.)
        public uint OEP;
        public uint CodeSectionVirtualAddress;  // Was 0x0401000 when testing. Possibly original OEP?
        public uint CodeSectionSize;
        public uint CodeSectionXorKey;          // Starting key to xor decode against. (Only used if (Flags & 4) is set.)
        public uint SteamAppID;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x0C)]
        public byte[] SteamAppIDString;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x36C)]
        public byte[] StubData;                 // Misc stub data: strings, error messages, etc.

        uint ISteamStub32Var20Header.Flags => Flags;
        uint ISteamStub32Var20Header.OEP => OEP;
        uint ISteamStub32Var20Header.CodeSectionVirtualAddress => CodeSectionVirtualAddress;
        uint ISteamStub32Var20Header.CodeSectionSize => CodeSectionSize;
        uint ISteamStub32Var20Header.CodeSectionXorKey => CodeSectionXorKey;
    }
}
