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
    using System;
    using System.IO;
    using System.Text;

    /// <summary>
    /// One entry of the PE section table: the 40 header bytes plus the body, which stays in the
    /// file image until a caller reads it. Keeping the header as bytes makes PE32 and PE32+
    /// sections identical, since their headers are. A replaced body is held here, so a save
    /// writes what the section holds.
    /// </summary>
    public sealed class PeSection
    {
        private static readonly byte[] ZeroPad = new byte[0x1000];

        private readonly byte[] m_Header;
        private readonly byte[] m_FileData;
        private readonly long m_FileOffset;
        private byte[]? m_Replacement;

        internal PeSection(byte[] header, byte[] fileData, long fileOffset)
        {
            this.m_Header = header;
            this.m_FileData = fileData;
            this.m_FileOffset = fileOffset;
        }

        public string Name => Encoding.ASCII.GetString(this.m_Header, 0, 8).Trim('\0');

        public uint VirtualSize
        {
            get => BitConverter.ToUInt32(this.m_Header, 8);
            set => WriteUInt32(8, value);
        }

        public uint VirtualAddress
        {
            get => BitConverter.ToUInt32(this.m_Header, 12);
            set => WriteUInt32(12, value);
        }

        public uint SizeOfRawData
        {
            get => BitConverter.ToUInt32(this.m_Header, 16);
            set => WriteUInt32(16, value);
        }

        public uint PointerToRawData
        {
            get => BitConverter.ToUInt32(this.m_Header, 20);
            set => WriteUInt32(20, value);
        }

        /// <summary>
        /// A section without a body is not one the unpackers can work with. The stub's own
        /// `.bind` and code sections always have one.
        /// </summary>
        public bool IsValid => this.SizeOfRawData != 0 && this.PointerToRawData != 0;

        public uint GetAlignedSize(uint fileAlignment)
        {
            return (uint)PeImage.Align(this.SizeOfRawData, fileAlignment);
        }

        /// <summary>
        /// Copies the body, padded to the file alignment. The caller owns the copy, and changes
        /// to it reach the saved file only through <see cref="PeImage.ReplaceSection"/>.
        /// </summary>
        internal byte[] Read(uint fileAlignment)
        {
            var data = new byte[this.GetAlignedSize(fileAlignment)];
            var size = this.Available();

            if (size > 0)
                Array.Copy(this.m_FileData, this.m_FileOffset, data, 0, size);

            return data;
        }

        /// <summary>
        /// Keeps the body the next save writes. The section takes ownership of the array.
        /// </summary>
        internal void Replace(byte[] data)
        {
            this.m_Replacement = data;
        }

        internal void WriteTo(Stream stream, uint fileAlignment)
        {
            if (this.m_Replacement != null)
            {
                stream.Write(this.m_Replacement, 0, this.m_Replacement.Length);
                WritePadding(stream, this.GetAlignedSize(fileAlignment) - (uint)this.m_Replacement.Length);
                return;
            }

            var size = this.Available();

            if (size > 0)
                stream.Write(this.m_FileData, (int)this.m_FileOffset, (int)size);

            WritePadding(stream, this.GetAlignedSize(fileAlignment) - size);
        }

        /// <summary>
        /// The bytes the file actually holds for this section. Realigning rounds the declared size
        /// up, and a section at the end of the file has no padding behind it, so the declared size
        /// can point past the buffer. The rest is written as zeros.
        /// </summary>
        private uint Available()
        {
            if (this.SizeOfRawData == 0 || this.m_FileOffset >= this.m_FileData.Length)
                return 0;

            var available = this.m_FileData.Length - this.m_FileOffset;
            return (uint)Math.Min(this.SizeOfRawData, available);
        }

        private static void WritePadding(Stream stream, uint padding)
        {
            while (padding > 0)
            {
                var chunk = (int)Math.Min(padding, (uint)ZeroPad.Length);

                stream.Write(ZeroPad, 0, chunk);
                padding -= (uint)chunk;
            }
        }

        internal byte[] HeaderBytes => this.m_Header;

        private void WriteUInt32(int offset, uint value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, this.m_Header, offset, bytes.Length);
        }
    }
}
