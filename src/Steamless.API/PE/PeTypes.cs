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

namespace Steamless.API.PE
{
    /// <summary>
    /// One data directory entry of the optional header. Both fields are 32-bit in PE32 and
    /// PE32+, so one type covers both.
    /// </summary>
    public struct PeDataDirectory
    {
        public uint VirtualAddress;

        public uint Size;

        public PeDataDirectory(uint virtualAddress, uint size)
        {
            this.VirtualAddress = virtualAddress;
            this.Size = size;
        }
    }

    /// <summary>
    /// The TLS directory, normalized: PE32+ stores 64-bit addresses where PE32 stores 32-bit
    /// ones, and callers only care about the callback list.
    /// </summary>
    public struct PeTlsDirectory
    {
        public ulong StartAddressOfRawData;

        public ulong EndAddressOfRawData;

        public ulong AddressOfIndex;

        public ulong AddressOfCallBacks;

        public uint SizeOfZeroFill;

        public uint Characteristics;
    }

    public static class PeConstants
    {
        public const ushort DosSignature = 0x5A4D;

        public const uint NtSignature = 0x00004550;

        public const ushort OptionalHeader32Magic = 0x10B;

        public const ushort OptionalHeader64Magic = 0x20B;

        public const int DosHeaderSize = 0x40;

        public const int SectionHeaderSize = 40;

        public const int OptionalHeader32Size = 224;

        public const int OptionalHeader64Size = 240;

        /// <summary>
        /// Field offsets inside the optional header. Everything up to ImageBase has the same
        /// layout in PE32 and PE32+, but the data directory start and the fields between them
        /// differ, so those go through the bitness switch.
        /// </summary>
        public const int EntryPointOffset = 16;

        public const int BaseOfCodeOffset = 20;

        public const int ImageBaseOffset32 = 28;

        public const int ImageBaseOffset64 = 24;

        public const int SectionAlignmentOffset = 32;

        public const int FileAlignmentOffset = 36;

        public const int SizeOfImageOffset = 56;

        public const int CheckSumOffset = 64;

        public const int DataDirectoryOffset32 = 96;

        public const int DataDirectoryOffset64 = 112;

        /// <summary>
        /// Directory indices, used to reach the import, certificate and TLS entries.
        /// </summary>
        public const int ImportDirectoryIndex = 1;

        public const int CertificateDirectoryIndex = 4;

        public const int TlsDirectoryIndex = 9;

        public static int DataDirectoryOffset(bool is64Bit)
        {
            return is64Bit ? DataDirectoryOffset64 : DataDirectoryOffset32;
        }
    }
}
