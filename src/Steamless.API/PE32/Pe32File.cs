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

namespace Steamless.API.PE32
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    public class Pe32File
    {
        public Pe32File()
        {
        }

        public Pe32File(string file)
        {
            this.FilePath = file;
        }

        public bool Parse(string file = null)
        {
            if (file != null)
                this.FilePath = file;

            this.FileData = null;
            this.DosHeader = new NativeApi32.ImageDosHeader32();
            this.NtHeaders = new NativeApi32.ImageNtHeaders32();
            this.DosStubSize = 0;
            this.DosStubOffset = 0;
            this.DosStubData = null;
            this.Sections = new List<NativeApi32.ImageSectionHeader32>();
            this.SectionData = new List<byte[]>();
            this.TlsDirectory = new NativeApi32.ImageTlsDirectory32();
            this.TlsCallbacks = new List<uint>();

            if (string.IsNullOrEmpty(this.FilePath) || !File.Exists(this.FilePath))
                return false;

            this.FileData = File.ReadAllBytes(this.FilePath);

            if (this.FileData.Length < (Marshal.SizeOf<NativeApi32.ImageDosHeader32>() + Unsafe.SizeOf<NativeApi32.ImageNtHeaders32>()))
                return false;

            this.DosHeader = Pe32Helpers.GetStructure<NativeApi32.ImageDosHeader32>(this.FileData);
            if (!this.DosHeader.IsValid)
                return false;

            this.NtHeaders = Pe32Helpers.GetStructure<NativeApi32.ImageNtHeaders32>(this.FileData, this.DosHeader.e_lfanew);
            if (!this.NtHeaders.IsValid)
                return false;

            // Guard against an e_lfanew smaller than the DOS header, which would underflow the stub size.
            if (this.DosHeader.e_lfanew < Marshal.SizeOf<NativeApi32.ImageDosHeader32>())
                return false;

            this.DosStubSize = (uint)(this.DosHeader.e_lfanew - Marshal.SizeOf<NativeApi32.ImageDosHeader32>());
            if (this.DosStubSize > 0)
            {
                this.DosStubOffset = (uint)Marshal.SizeOf<NativeApi32.ImageDosHeader32>();
                this.DosStubData = new byte[this.DosStubSize];
                Array.Copy(this.FileData, this.DosStubOffset, this.DosStubData, 0, this.DosStubSize);
            }

            for (var x = 0; x < this.NtHeaders.FileHeader.NumberOfSections; x++)
            {
                var section = Pe32Helpers.GetSection(this.FileData, x, this.DosHeader, this.NtHeaders);
                this.Sections.Add(section);

                // Reject sections whose raw data extends past the end of the file (corrupt headers).
                if ((ulong)section.PointerToRawData + section.SizeOfRawData > (ulong)this.FileData.Length)
                    return false;

                var sectionData = new byte[this.GetAlignment(section.SizeOfRawData, this.NtHeaders.OptionalHeader.FileAlignment)];
                Array.Copy(this.FileData, section.PointerToRawData, sectionData, 0, section.SizeOfRawData);
                this.SectionData.Add(sectionData);
            }

            try
            {
                var lastSection = this.Sections.Last();
                var fileSize = lastSection.SizeOfRawData + lastSection.PointerToRawData;
                if (fileSize < this.FileData.Length)
                {
                    this.OverlayData = new byte[this.FileData.Length - fileSize];
                    Array.Copy(this.FileData, fileSize, this.OverlayData, 0, this.FileData.Length - fileSize);
                }
            }
            catch (Exception)
            {
                return false;
            }

            if (this.NtHeaders.OptionalHeader.TLSTable.VirtualAddress != 0)
            {
                var tls = this.NtHeaders.OptionalHeader.TLSTable;
                var addr = this.GetFileOffsetFromRva(tls.VirtualAddress);

                this.TlsDirectory = Pe32Helpers.GetStructure<NativeApi32.ImageTlsDirectory32>(this.FileData, (int)addr);

                if (this.TlsDirectory.AddressOfCallBacks == 0)
                    return true;

                addr = this.GetRvaFromVa(this.TlsDirectory.AddressOfCallBacks);
                addr = this.GetFileOffsetFromRva(addr);

                // Callbacks are a null-terminated list; the count bound guards against corrupt addresses.
                var count = 0;
                while (count < 128 && (ulong)addr + ((ulong)count * 4) + 4 <= (ulong)this.FileData.Length)
                {
                    var callback = BitConverter.ToUInt32(this.FileData, (int)addr + (count * 4));
                    if (callback == 0)
                        break;

                    this.TlsCallbacks.Add(callback);
                    count++;
                }
            }

            return true;
        }

        public bool IsFile64Bit()
        {
            return (this.NtHeaders.FileHeader.Machine & (uint)NativeApi32.MachineType.X64) == (uint)NativeApi32.MachineType.X64;
        }

        public bool HasSection(string name)
        {
            return this.Sections.Any(s => string.Compare(s.SectionName, name, StringComparison.InvariantCultureIgnoreCase) == 0);
        }

        public NativeApi32.ImageSectionHeader32 GetSection(string name)
        {
            return this.Sections.FirstOrDefault(s => string.Compare(s.SectionName, name, StringComparison.InvariantCultureIgnoreCase) == 0);
        }

        public NativeApi32.ImageSectionHeader32 GetOwnerSection(uint rva)
        {
            foreach (var s in this.Sections)
            {
                var size = s.VirtualSize;
                if (size == 0)
                    size = s.SizeOfRawData;

                if ((rva >= s.VirtualAddress) && (rva < s.VirtualAddress + size))
                    return s;
            }

            return default(NativeApi32.ImageSectionHeader32);
        }

        public NativeApi32.ImageSectionHeader32 GetOwnerSection(ulong rva)
        {
            foreach (var s in this.Sections)
            {
                var size = s.VirtualSize;
                if (size == 0)
                    size = s.SizeOfRawData;

                if ((rva >= s.VirtualAddress) && (rva < s.VirtualAddress + size))
                    return s;
            }

            return default(NativeApi32.ImageSectionHeader32);
        }

        public byte[] GetSectionData(int index)
        {
            if (index < 0 || index >= this.Sections.Count)
                return null;

            return this.SectionData[index];
        }

        public byte[] GetSectionData(string name)
        {
            for (var x = 0; x < this.Sections.Count; x++)
            {
                if (string.Compare(this.Sections[x].SectionName, name, StringComparison.InvariantCultureIgnoreCase) == 0)
                    return this.SectionData[x];
            }

            return null;
        }

        public int GetSectionIndex(string name)
        {
            for (var x = 0; x < this.Sections.Count; x++)
            {
                if (string.Compare(this.Sections[x].SectionName, name, StringComparison.InvariantCultureIgnoreCase) == 0)
                    return x;
            }

            return -1;
        }

        public int GetSectionIndex(NativeApi32.ImageSectionHeader32 section)
        {
            return this.Sections.IndexOf(section);
        }

        public bool RemoveSection(NativeApi32.ImageSectionHeader32 section)
        {
            var index = this.Sections.IndexOf(section);
            if (index == -1)
                return false;

            this.Sections.RemoveAt(index);
            this.SectionData.RemoveAt(index);

            return true;
        }

        public void RebuildSections(bool realign = true)
        {
            for (var x = 0; x < this.Sections.Count; x++)
            {
                var section = this.Sections[x];

                if (realign)
                {
                    section.VirtualAddress = this.GetAlignment(section.VirtualAddress, this.NtHeaders.OptionalHeader.SectionAlignment);
                    section.VirtualSize = this.GetAlignment(section.VirtualSize, this.NtHeaders.OptionalHeader.SectionAlignment);
                    section.PointerToRawData = this.GetAlignment(section.PointerToRawData, this.NtHeaders.OptionalHeader.FileAlignment);
                    section.SizeOfRawData = this.GetAlignment(section.SizeOfRawData, this.NtHeaders.OptionalHeader.FileAlignment);
                }

                this.Sections[x] = section;
            }

            var ntHeaders = this.NtHeaders;
            ntHeaders.OptionalHeader.SizeOfImage = (uint)this.GetAlignment(this.Sections.Last().VirtualAddress + this.Sections.Last().VirtualSize, this.NtHeaders.OptionalHeader.SectionAlignment);
            this.NtHeaders = ntHeaders;
        }

        public uint GetRvaFromVa(uint va)
        {
            return va - this.NtHeaders.OptionalHeader.ImageBase;
        }

        public uint GetFileOffsetFromRva(uint rva)
        {
            var section = this.GetOwnerSection(rva);
            return (rva - (section.VirtualAddress - section.PointerToRawData));
        }

        public uint GetAlignment(uint val, uint align)
        {
            return (((val + align - 1) / align) * align);
        }

        public string FilePath { get; set; }

        public byte[] FileData { get; set; }

        public NativeApi32.ImageDosHeader32 DosHeader { get; set; }

        public NativeApi32.ImageNtHeaders32 NtHeaders { get; set; }

        public uint DosStubSize { get; set; }

        public uint DosStubOffset { get; set; }

        public byte[] DosStubData { get; set; }

        public List<NativeApi32.ImageSectionHeader32> Sections;

        public List<byte[]> SectionData;

        public byte[] OverlayData;

        public NativeApi32.ImageTlsDirectory32 TlsDirectory { get; set; }

        public List<uint> TlsCallbacks { get; set; }
    }
}
