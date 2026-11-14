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

namespace Steamless.Unpacker.Variant21.x86.Classes
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// The standard 2.1 header, which includes the LoadLibraryW field. The D0 variant drops that
    /// field, making it 4 bytes smaller, and is selected when (structSize / 4) == 0xD0.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SteamStub32Var21Header : ISteamStub32Var21Header
    {
        // XorKey starts the block. The Get*/LoadLibrary* fields below it are import addresses,
        // zero when the stub does not import that API.
        public uint XorKey;
        public uint GetModuleHandleA;
        public uint GetModuleHandleW;
        public uint GetProcAddress;
        public uint LoadLibraryA;
        public uint LoadLibraryW;
        public uint BindSectionVirtualAddress;
        public uint BindStartFunctionSize;
        public uint PayloadKeyMatch; // Matches the first 4 bytes of the payload data.
        public uint PayloadDataVirtualAddress;
        public uint PayloadDataSize;
        public uint SteamAppID;
        public uint Unknown0001;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x08)]
        public byte[] SteamAppIDString;

        // Offsets into the payload data; the stored values are the VA and size of the SteamDRMP.dll data.
        public uint SteamDRMPDllVirtualAddress;
        public uint SteamDRMPDllSize;
        public uint XTeaKeys; // Offset into the payload data where the XTEA key array begins.

        // StubData has no fixed size: it depends on the stub version and the options used, so the
        // caller slices the rest of the header instead of marshalling it.

        uint ISteamStub32Var21Header.PayloadDataVirtualAddress => PayloadDataVirtualAddress;
        uint ISteamStub32Var21Header.PayloadDataSize => PayloadDataSize;
        uint ISteamStub32Var21Header.SteamDRMPDllVirtualAddress => SteamDRMPDllVirtualAddress;
        uint ISteamStub32Var21Header.SteamDRMPDllSize => SteamDRMPDllSize;
        uint ISteamStub32Var21Header.XTeaKeys => XTeaKeys;
    }

    /// <summary>
    /// The 2.1 header without the LoadLibraryW field, 4 bytes smaller than the standard one.
    /// Selected when (structSize / 4) == 0xD0.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SteamStub32Var21Header_D0Variant : ISteamStub32Var21Header
    {
        // XorKey starts the block. The Get*/LoadLibrary* fields below it are import addresses,
        // zero when the stub does not import that API.
        public uint XorKey;
        public uint GetModuleHandleA;
        public uint GetModuleHandleW;
        public uint GetProcAddress;
        public uint LoadLibraryA;
        public uint BindSectionVirtualAddress;
        public uint BindStartFunctionSize;
        public uint PayloadKeyMatch; // Matches the first 4 bytes of the payload data.
        public uint PayloadDataVirtualAddress;
        public uint PayloadDataSize;
        public uint SteamAppID;
        public uint Unknown0000;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x08)]
        public byte[] SteamAppIDString;

        // Offsets into the payload data; the stored values are the VA and size of the SteamDRMP.dll data.
        public uint SteamDRMPDllVirtualAddress;
        public uint SteamDRMPDllSize;
        public uint XTeaKeys; // Offset into the payload data where the XTEA key array begins.

        // StubData has no fixed size: it depends on the stub version and the options used, so the
        // caller slices the rest of the header instead of marshalling it.

        uint ISteamStub32Var21Header.PayloadDataVirtualAddress => PayloadDataVirtualAddress;
        uint ISteamStub32Var21Header.PayloadDataSize => PayloadDataSize;
        uint ISteamStub32Var21Header.SteamDRMPDllVirtualAddress => SteamDRMPDllVirtualAddress;
        uint ISteamStub32Var21Header.SteamDRMPDllSize => SteamDRMPDllSize;
        uint ISteamStub32Var21Header.XTeaKeys => XTeaKeys;
    }
}
