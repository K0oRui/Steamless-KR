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

namespace Steamless.Unpacker.Variant3x.Classes
{
    using Common;
    using System.Runtime.InteropServices;

    /// <summary>
    /// The SteamStub 3.1 header. The 32-bit and 64-bit stubs lay this struct out identically, so
    /// one type covers both; the header is always 0xF0 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SteamStubVar31Header
    {
        public uint XorKey;
        public uint Signature; // 0xC0DEC0DF signature; validates the xor decoding was successful.
        public ulong ImageBase;
        public ulong AddressOfEntryPoint;
        public uint BindSectionOffset; // Offset relative to AddressOfEntryPoint, e.g. RVA(AddressOfEntryPoint - BindSectionOffset).
        public uint Unknown0000; // [Cyanic] Likely the .bind code size.
        public ulong OriginalEntryPoint;
        public uint Unknown0001; // [Cyanic] Likely an offset to a string table.
        public uint PayloadSize;
        public uint DRMPDllOffset;
        public uint DRMPDllSize;
        public uint SteamAppId;
        public uint Flags;
        public uint BindSectionVirtualSize;
        public uint Unknown0002; // [Cyanic] Likely a hash of some sort.
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

        /// <summary>
        /// Projects this layout into the width-independent shape the shared 3.x steps read. This
        /// header has no TLS flag, so a repaired callback always applies.
        /// </summary>
        public Stub3xHeader ToStub3xHeader()
        {
            return new Stub3xHeader
            {
                Signature = this.Signature,
                Flags = this.Flags,
                OriginalEntryPoint = this.OriginalEntryPoint,
                BindSectionOffset = this.BindSectionOffset,
                PayloadSize = this.PayloadSize,
                DRMPDllOffset = this.DRMPDllOffset,
                DRMPDllSize = this.DRMPDllSize,
                XorKey = this.XorKey,
                CodeSectionVirtualAddress = this.CodeSectionVirtualAddress,
                CodeSectionRawSize = this.CodeSectionRawSize,
                AES_Key = this.AES_Key,
                AES_IV = this.AES_IV,
                CodeSectionStolenData = this.CodeSectionStolenData,
                EncryptionKeys = this.EncryptionKeys,
                HasTlsCallback = 1,
            };
        }
    }
}
