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

namespace Steamless.API.PE64
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    public class Pe64File
    {
        public Pe64File()
        {
        }

        public Pe64File(string file)
        {
            this.FilePath = file;
        }

        public bool Parse(string file = null)
        {
            if (file != null)
                this.FilePath = file;

            this.FileData = null;
            this.DosHeader = new NativeApi64.ImageDosHeader64();
            this.NtHeaders = new NativeApi64.ImageNtHeaders64();
            this.DosStubSize = 0;
            this.DosStubOffset = 0;
            this.DosStubData = null;
            this.Sections = new List<NativeApi64.ImageSectionHeader64>();
            this.SectionData = new List<byte[]>();
            this.TlsDirectory = new NativeApi64.ImageTlsDirectory64();
            this.TlsCallbacks = new List<ulong>();

            if (string.IsNullOrEmpty(this.FilePath) || !File.Exists(this.FilePath))
                return false;

            this.FileData = File.ReadAllBytes(this.FilePath);

            if (this.FileData.Length < (Marshal.SizeOf<NativeApi64.ImageDosHeader64>() + Unsafe.SizeOf<NativeApi64.ImageNtHeaders64>()))
                return false;

            this.DosHeader = Pe64Helpers.GetStructure<NativeApi64.ImageDosHeader64>(this.FileData);
            if (!this.DosHeader.IsValid)
                return false;

            this.NtHeaders = Pe64Helpers.GetStructure<NativeApi64.ImageNtHeaders64>(this.FileData, this.DosHeader.e_lfanew);
            if (!this.NtHeaders.IsValid)
                return false;

            // Guard against an e_lfanew smaller than the DOS header, which would underflow the stub size.
            if (this.DosHeader.e_lfanew < Marshal.SizeOf<NativeApi64.ImageDosHeader64>())
                return false;

            this.DosStubSize = (ulong)(this.DosHeader.e_lfanew - Marshal.SizeOf<NativeApi64.ImageDosHeader64>());
            if (this.DosStubSize > 0)
            {
                this.DosStubOffset = (ulong)Marshal.SizeOf<NativeApi64.ImageDosHeader64>();
                this.DosStubData = new byte[this.DosStubSize];
                Array.Copy(this.FileData, (int)this.DosStubOffset, this.DosStubData, 0, (int)this.DosStubSize);
            }

            for (var x = 0; x < this.NtHeaders.FileHeader.NumberOfSections; x++)
            {
                var section = Pe64Helpers.GetSection(this.FileData, x, this.DosHeader, this.NtHeaders);
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

                this.TlsDirectory = Pe64Helpers.GetStructure<NativeApi64.ImageTlsDirectory64>(this.FileData, (int)addr);

                if (this.TlsDirectory.AddressOfCallBacks == 0)
                    return true;

                addr = this.GetRvaFromVa(this.TlsDirectory.AddressOfCallBacks);
                addr = this.GetFileOffsetFromRva(addr);

                // Callbacks are a null-terminated list; the count bound guards against corrupt addresses.
                var count = 0;
                while (count < 128 && (ulong)addr + ((ulong)count * 8) + 8 <= (ulong)this.FileData.Length)
                {
                    var callback = BitConverter.ToUInt64(this.FileData, (int)addr + (count * 8));
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
            return (this.NtHeaders.FileHeader.Machine & (uint)NativeApi64.MachineType.X64) == (uint)NativeApi64.MachineType.X64;
        }

        public bool HasSection(string name)
        {
            return this.Sections.Any(s => string.Compare(s.SectionName, name, StringComparison.InvariantCultureIgnoreCase) == 0);
        }

        public NativeApi64.ImageSectionHeader64 GetSection(string name)
        {
            return this.Sections.FirstOrDefault(s => string.Compare(s.SectionName, name, StringComparison.InvariantCultureIgnoreCase) == 0);
        }

        public NativeApi64.ImageSectionHeader64 GetOwnerSection(uint rva)
        {
            foreach (var s in this.Sections)
            {
                var size = s.VirtualSize;
                if (size == 0)
                    size = s.SizeOfRawData;

                if ((rva >= s.VirtualAddress) && (rva < s.VirtualAddress + size))
                    return s;
            }

            return default(NativeApi64.ImageSectionHeader64);
        }

        public NativeApi64.ImageSectionHeader64 GetOwnerSection(ulong rva)
        {
            foreach (var s in this.Sections)
            {
                var size = s.VirtualSize;
                if (size == 0)
                    size = s.SizeOfRawData;

                if ((rva >= s.VirtualAddress) && (rva < s.VirtualAddress + size))
                    return s;
            }

            return default(NativeApi64.ImageSectionHeader64);
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

        public int GetSectionIndex(NativeApi64.ImageSectionHeader64 section)
        {
            return this.Sections.IndexOf(section);
        }

        public bool RemoveSection(NativeApi64.ImageSectionHeader64 section)
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
                    section.VirtualAddress = (uint)this.GetAlignment(section.VirtualAddress, this.NtHeaders.OptionalHeader.SectionAlignment);
                    section.VirtualSize = (uint)this.GetAlignment(section.VirtualSize, this.NtHeaders.OptionalHeader.SectionAlignment);
                    section.PointerToRawData = (uint)this.GetAlignment(section.PointerToRawData, this.NtHeaders.OptionalHeader.FileAlignment);
                    section.SizeOfRawData = (uint)this.GetAlignment(section.SizeOfRawData, this.NtHeaders.OptionalHeader.FileAlignment);
                }

                this.Sections[x] = section;
            }

            var ntHeaders = this.NtHeaders;
            ntHeaders.OptionalHeader.SizeOfImage = (uint)this.GetAlignment(this.Sections.Last().VirtualAddress + this.Sections.Last().VirtualSize, this.NtHeaders.OptionalHeader.SectionAlignment);
            this.NtHeaders = ntHeaders;
        }

        public ulong GetRvaFromVa(ulong va)
        {
            return va - this.NtHeaders.OptionalHeader.ImageBase;
        }

        public ulong GetFileOffsetFromRva(ulong rva)
        {
            var section = this.GetOwnerSection(rva);
            return (rva - (section.VirtualAddress - section.PointerToRawData));
        }

        public ulong GetAlignment(ulong val, ulong align)
        {
            return (((val + align - 1) / align) * align);
        }

        public string FilePath { get; set; }

        public byte[] FileData { get; set; }

        public NativeApi64.ImageDosHeader64 DosHeader { get; set; }

        public NativeApi64.ImageNtHeaders64 NtHeaders { get; set; }

        public ulong DosStubSize { get; set; }

        public ulong DosStubOffset { get; set; }

        public byte[] DosStubData { get; set; }

        public List<NativeApi64.ImageSectionHeader64> Sections;

        public List<byte[]> SectionData;

        public byte[] OverlayData;

        public NativeApi64.ImageTlsDirectory64 TlsDirectory { get; set; }

        public List<ulong> TlsCallbacks { get; set; }
    }
}