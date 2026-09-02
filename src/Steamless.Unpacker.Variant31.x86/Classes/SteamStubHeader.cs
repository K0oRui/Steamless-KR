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

namespace Steamless.Unpacker.Variant31.x86.Classes
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    public struct SteamStub32Var31Header
    {
        public uint XorKey;
        public uint Signature; // 0xC0DEC0DF signature; validates the xor decoding was successful.
        public ulong ImageBase;
        public ulong AddressOfEntryPoint;
        public uint BindSectionOffset; // Offset relative to AddressOfEntryPoint, e.g. RVA(AddressOfEntryPoint - BindSectionOffset).
        public uint Unknown0000; // [Cyanic: This field is most likely the .bind code size.]
        public ulong OriginalEntryPoint;
        public uint Unknown0001; // [Cyanic: This field is most likely an offset to a string table.]
        public uint PayloadSize;
        public uint DRMPDllOffset;
        public uint DRMPDllSize;
        public uint SteamAppId;
        public uint Flags;
        public uint BindSectionVirtualSize;
        public uint Unknown0002; // [Cyanic: This field is most likely a hash of some sort.]
        public ulong CodeSectionVirtualAddress;
        public ulong CodeSectionRawSize;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x20)]
        public byte[] AES_Key;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x10)]
        public byte[] AES_IV;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x10)]
        public byte[] CodeSectionStolenData; // The first 16 bytes of the code section, moved here before encryption.

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x04)]
        public uint[] EncryptionKeys; // XTEA keys used for decrypting the SteamDRMP.dll file.

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x08)]
        public uint[] Unknown0003;

        public ulong GetModuleHandleA_Rva;
        public ulong GetModuleHandleW_Rva;
        public ulong LoadLibraryA_Rva;
        public ulong LoadLibraryW_Rva;
        public ulong GetProcAddress_Rva;
    }
}
