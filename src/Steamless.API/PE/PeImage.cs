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
    using Events;
    using Services;
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// A parsed PE32 or PE32+ image. One type covers both architectures because only field
    /// widths and two offset tables differ. Header bytes stay as arrays and are patched in
    /// place, so a save rewrites only the fields that changed.
    /// </summary>
    public sealed class PeImage
    {
        private static readonly byte[] ZeroChecksum = new byte[4];

        private readonly byte[] m_FileHeader;
        private readonly byte[] m_OptionalHeader;
        private readonly byte[] m_FileData;
        private readonly List<PeSection> m_Sections = new List<PeSection>();
        private long m_OverlayOffset = -1;

        private PeImage(string filePath, byte[] fileData, bool is64Bit, byte[] fileHeader, byte[] optionalHeader)
        {
            this.FilePath = filePath;
            this.m_FileData = fileData;
            this.Is64Bit = is64Bit;
            this.m_FileHeader = fileHeader;
            this.m_OptionalHeader = optionalHeader;
        }

        /// <summary>
        /// Reads and validates a PE image. A rejected file keeps the reason, including corrupt
        /// alignments that would otherwise drive multi-gigabyte allocations. This is the only
        /// place a file is read from disk.
        /// </summary>
        public static PeLoadResult Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return PeLoadResult.Failed(PeLoadFailure.FileMissing);

            byte[] data;
            try
            {
                data = File.ReadAllBytes(path);
            }
            catch (Exception)
            {
                return PeLoadResult.Failed(PeLoadFailure.Unreadable);
            }

            if (data.Length < PeConstants.DosHeaderSize + 24)
                return PeLoadResult.Failed(PeLoadFailure.TooSmall);

            if (BitConverter.ToUInt16(data, 0) != PeConstants.DosSignature)
                return PeLoadResult.Failed(PeLoadFailure.NotDosExecutable);

            var ntOffset = BitConverter.ToInt32(data, 0x3C);

            // e_lfanew must leave room for the DOS header and the NT signature + file header.
            if (ntOffset < PeConstants.DosHeaderSize || ntOffset > data.Length - 24)
                return PeLoadResult.Failed(PeLoadFailure.BadNtOffset);

            if (BitConverter.ToUInt32(data, ntOffset) != PeConstants.NtSignature)
                return PeLoadResult.Failed(PeLoadFailure.NotPortableExecutable);

            var fileHeader = new byte[20];
            Array.Copy(data, ntOffset + 4, fileHeader, 0, fileHeader.Length);

            if (BitConverter.ToUInt16(fileHeader, 2) == 0)
                return PeLoadResult.Failed(PeLoadFailure.NoSections);

            var optionalSize = BitConverter.ToUInt16(fileHeader, 16);
            var optionalOffset = ntOffset + 24;

            // The optional header must keep at least its magic word inside the file. A zero size
            // at the end of the file passes a bare offset check and throws here.
            if (optionalSize < 2 || optionalOffset > data.Length - optionalSize)
                return PeLoadResult.Failed(PeLoadFailure.OptionalHeaderOutsideFile);

            var magic = BitConverter.ToUInt16(data, optionalOffset);
            var is64Bit = magic == PeConstants.OptionalHeader64Magic;
            if (!is64Bit && magic != PeConstants.OptionalHeader32Magic)
                return PeLoadResult.Failed(PeLoadFailure.UnsupportedOptionalHeader);

            if (optionalSize < (is64Bit ? PeConstants.OptionalHeader64Size : PeConstants.OptionalHeader32Size))
                return PeLoadResult.Failed(PeLoadFailure.OptionalHeaderTruncated);

            var optionalHeader = new byte[optionalSize];
            Array.Copy(data, optionalOffset, optionalHeader, 0, optionalSize);

            var alignmentOffset = optionalOffset + PeConstants.SectionAlignmentOffset;
            var fileAlignment = BitConverter.ToUInt32(data, optionalOffset + PeConstants.FileAlignmentOffset);
            var sectionAlignment = BitConverter.ToUInt32(data, alignmentOffset);

            if (!IsPowerOfTwo(fileAlignment) || fileAlignment > 0x10000 ||
                !IsPowerOfTwo(sectionAlignment) || sectionAlignment > 0x100000)
                return PeLoadResult.Failed(PeLoadFailure.BadAlignment);

            var image = new PeImage(path, data, is64Bit, fileHeader, optionalHeader);

            var sectionCount = BitConverter.ToUInt16(fileHeader, 2);
            var sectionOffset = optionalOffset + optionalSize;
            if (sectionOffset + (sectionCount * PeConstants.SectionHeaderSize) > data.Length)
                return PeLoadResult.Failed(PeLoadFailure.SectionTableOutsideFile);

            var headerEnd = sectionOffset + (sectionCount * PeConstants.SectionHeaderSize);

            for (var x = 0; x < sectionCount; x++)
            {
                var header = new byte[PeConstants.SectionHeaderSize];
                Array.Copy(data, sectionOffset + (x * PeConstants.SectionHeaderSize), header, 0, header.Length);

                var section = new PeSection(header, data, BitConverter.ToUInt32(header, 20));

                if ((ulong)section.PointerToRawData + section.SizeOfRawData > (ulong)data.Length)
                    return PeLoadResult.Failed(PeLoadFailure.SectionOutsideFile);

                // A body that starts inside the header block would overwrite the DOS header, the NT
                // headers and the section table on write. Sections without a body may point
                // anywhere, including zero.
                if (section.SizeOfRawData != 0 && section.PointerToRawData < headerEnd)
                    return PeLoadResult.Failed(PeLoadFailure.SectionOverlapsHeaders);

                image.m_Sections.Add(section);
            }

            // The overlay starts after the last section with a body. A bodyless section may point
            // anywhere, including zero, so using its pointer would make the whole file look like
            // the overlay and the save would append the input to itself.
            var endOfSections = (long)headerEnd;
            foreach (var section in image.m_Sections)
            {
                if (section.SizeOfRawData == 0)
                    continue;

                endOfSections = Math.Max(endOfSections, (long)section.PointerToRawData + section.SizeOfRawData);
            }

            if (endOfSections < data.Length)
                image.m_OverlayOffset = endOfSections;

            image.DosStub = BuildDosStub(data, ntOffset);
            image.ReadTlsDirectory(data);

            return PeLoadResult.Loaded(image);
        }

        /// <summary>
        /// The bytes between the DOS header and the NT headers. In a packed game this is
        /// executable junk, and the user may have asked to zero it.
        /// </summary>
        private static byte[] BuildDosStub(byte[] data, int ntOffset)
        {
            var size = ntOffset - PeConstants.DosHeaderSize;
            if (size <= 0)
                return Array.Empty<byte>();

            var stub = new byte[size];
            Array.Copy(data, PeConstants.DosHeaderSize, stub, 0, size);
            return stub;
        }

        private void ReadTlsDirectory(byte[] data)
        {
            var tls = this.TlsTable;
            if (tls.VirtualAddress == 0)
                return;

            var offset = this.FileOffsetFromRva(tls.VirtualAddress);
            var entrySize = this.Is64Bit ? 40 : 24;
            if (!PeHelpers.IsValidRange(data, (long)offset, entrySize))
                return;

            var cursor = (int)offset;
            var pointerSize = this.Is64Bit ? 8 : 4;

            this.TlsDirectory = new PeTlsDirectory
            {
                StartAddressOfRawData = ReadPointer(data, cursor, pointerSize),
                EndAddressOfRawData = ReadPointer(data, cursor + pointerSize, pointerSize),
                AddressOfIndex = ReadPointer(data, cursor + (pointerSize * 2), pointerSize),
                AddressOfCallBacks = ReadPointer(data, cursor + (pointerSize * 3), pointerSize),
                SizeOfZeroFill = BitConverter.ToUInt32(data, cursor + (pointerSize * 4)),
                Characteristics = BitConverter.ToUInt32(data, cursor + (pointerSize * 4) + 4)
            };

            if (this.TlsDirectory.AddressOfCallBacks == 0)
                return;

            var callbacks = this.FileOffsetFromRva(this.RvaFromVa(this.TlsDirectory.AddressOfCallBacks));

            // Callbacks are a null-terminated list; the count cap guards against corrupt addresses.
            for (var x = 0; x < 128; x++)
            {
                var entry = (long)callbacks + (x * pointerSize);
                if (!PeHelpers.IsValidRange(data, entry, pointerSize))
                    break;

                var callback = ReadPointer(data, (int)entry, pointerSize);
                if (callback == 0)
                    break;

                this.TlsCallbacks.Add(callback);
            }
        }

        private static ulong ReadPointer(byte[] data, int offset, int pointerSize)
        {
            return pointerSize == 8
                ? BitConverter.ToUInt64(data, offset)
                : BitConverter.ToUInt32(data, offset);
        }

        public bool HasSection(string name)
        {
            return this.FindSection(name) != null;
        }

        public PeSection FindSection(string name)
        {
            foreach (var section in this.m_Sections)
            {
                if (string.Equals(section.Name, name, StringComparison.OrdinalIgnoreCase))
                    return section;
            }

            return null;
        }

        public PeSection FindSectionAtRva(ulong rva)
        {
            foreach (var section in this.m_Sections)
            {
                var size = section.VirtualSize;
                if (size == 0)
                    size = section.SizeOfRawData;

                if (rva >= section.VirtualAddress && rva < section.VirtualAddress + (ulong)size)
                    return section;
            }

            return null;
        }

        public bool RemoveSection(PeSection section)
        {
            if (!this.m_Sections.Remove(section))
                return false;

            this.NumberOfSections--;
            return true;
        }

        /// <summary>
        /// Aligns every section and recomputes SizeOfImage. A save runs this first so the section
        /// table, the bodies and the image size agree.
        /// </summary>
        public void RebuildSections(bool realign = true)
        {
            var sectionAlignment = this.SectionAlignment;
            var fileAlignment = this.FileAlignment;

            for (var x = 0; x < this.m_Sections.Count; x++)
            {
                var section = this.m_Sections[x];

                if (realign)
                {
                    section.VirtualAddress = (uint)Align(section.VirtualAddress, sectionAlignment);
                    section.VirtualSize = (uint)Align(section.VirtualSize, sectionAlignment);
                    section.PointerToRawData = (uint)Align(section.PointerToRawData, fileAlignment);
                    section.SizeOfRawData = (uint)Align(section.SizeOfRawData, fileAlignment);
                }
            }

            var last = this.m_Sections[this.m_Sections.Count - 1];
            this.SizeOfImage = (uint)Align(last.VirtualAddress + (ulong)last.VirtualSize, sectionAlignment);
        }

        public ulong RvaFromVa(ulong va)
        {
            return va - this.ImageBase;
        }

        public ulong FileOffsetFromRva(ulong rva)
        {
            var section = this.FindSectionAtRva(rva);
            if (section == null)
                return rva;

            return rva - section.VirtualAddress + section.PointerToRawData;
        }

        public static ulong Align(ulong value, ulong alignment)
        {
            if (alignment == 0)
                return value;

            var remainder = value % alignment;
            return remainder == 0 ? value : value + alignment - remainder;
        }

        /// <summary>The size of the file the image was read from.</summary>
        public int Length => this.m_FileData.Length;

        /// <summary>
        /// A window onto the file at a file offset, with no copy. Returns false when the range
        /// falls outside the file; a zero size at the end of the file is valid.
        /// </summary>
        public bool TryRead(ulong offset, long count, out ReadOnlyMemory<byte> bytes)
        {
            bytes = default;

            if (count < 0 || count > int.MaxValue)
                return false;

            if (!PeHelpers.IsValidRange(this.m_FileData, (long)offset, count))
                return false;

            bytes = new ReadOnlyMemory<byte>(this.m_FileData, (int)offset, (int)count);
            return true;
        }

        /// <summary>
        /// A copy of a section's body, padded to the file alignment. Changes to the copy reach the
        /// file only through <see cref="ReplaceSection"/>.
        /// </summary>
        public byte[] ReadSection(PeSection section)
        {
            this.RequireOwn(section);
            return section.Read(this.FileAlignment);
        }

        /// <summary>
        /// Stores a body for the next save and takes ownership of the array. A body larger than
        /// the section's aligned size would run into the next section, so it is rejected here
        /// rather than written as a file that parses but will not load.
        /// </summary>
        public void ReplaceSection(PeSection section, byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            this.RequireOwn(section);

            var limit = section.GetAlignedSize(this.FileAlignment);
            if ((uint)data.Length > limit)
                throw new ArgumentException($"The body is {data.Length} bytes, larger than the section's {limit} bytes.", nameof(data));

            section.Replace(data);
        }

        private void RequireOwn(PeSection section)
        {
            if (section == null)
                throw new ArgumentNullException(nameof(section));

            if (!this.m_Sections.Contains(section))
                throw new ArgumentException("The section does not belong to this image.", nameof(section));
        }

        /// <summary>
        /// Writes the image to <paramref name="path"/>, including any body stored with
        /// <see cref="ReplaceSection"/>, and optionally recomputes the checksum the loader checks.
        /// The write lands in a temp file and replaces the path only when it is complete, so a
        /// failed run cannot leave a truncated result or a half-updated checksum behind.
        /// </summary>
        /// <remarks>
        /// This is the only place that writes bytes. Everything a caller wants changed has to
        /// change on the image first: the entry point, header fields, tables or a body.
        /// </remarks>
        public bool Save(string path, bool recalculateChecksum)
        {
            var tempPath = path + ".tmp";
            FileStream stream = null;

            try
            {
                stream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite);

                stream.Write(this.m_FileData, 0, PeConstants.DosHeaderSize);

                if (this.DosStub.Length > 0)
                    stream.Write(this.DosStub, 0, this.DosStub.Length);

                stream.Write(BitConverter.GetBytes(PeConstants.NtSignature), 0, 4);
                stream.Write(this.m_FileHeader, 0, this.m_FileHeader.Length);

                var optionalHeaderOffset = stream.Position;
                stream.Write(this.m_OptionalHeader, 0, this.m_OptionalHeader.Length);

                foreach (var section in this.m_Sections)
                {
                    stream.Write(section.HeaderBytes, 0, PeConstants.SectionHeaderSize);

                    // Bodies are not in header order once a section is removed or the file realigned,
                    // so each one is placed at its own raw offset.
                    var nextHeaderOffset = stream.Position;
                    stream.Position = section.PointerToRawData;

                    section.WriteTo(stream, this.FileAlignment);

                    stream.Position = nextHeaderOffset;
                }

                stream.Position = stream.Length;
                this.WriteOverlay(stream);

                // The checksum covers the whole file with its own field read as zero. Zeroing and
                // patching here keeps a stale value from the source out of the saved file.
                var checksumOffset = optionalHeaderOffset + PeConstants.CheckSumOffset;
                stream.Position = checksumOffset;
                stream.Write(PeImage.ZeroChecksum, 0, 4);

                if (recalculateChecksum)
                {
                    stream.Position = 0;
                    var checksum = PeHelpers.ComputePeChecksum(stream);

                    stream.Position = checksumOffset;
                    stream.Write(BitConverter.GetBytes(checksum), 0, 4);
                }

                stream.Dispose();
                stream = null;
                File.Move(tempPath, path, true);

                return true;
            }
            catch (Exception)
            {
                stream?.Dispose();
                stream = null;

                // Never leave a half-written file behind under the name of a successful result.
                PeImage.TryDelete(tempPath);
                return false;
            }
            finally
            {
                stream?.Dispose();
            }
        }

        /// <summary>
        /// Repoints the header entries that referenced a removed `.bind` section: the import table
        /// when it lived inside, and the certificate table, which is a file offset and moves with
        /// the overlay. Runs after the realign, because the certificate entry moves with the new
        /// overlay start.
        /// </summary>
        public void RepairHeaderPointers(ulong removedVa, ulong removedSize, LoggingService logService)
        {
            if (removedSize == 0)
                return;

            var importTable = this.ImportTable;
            if (importTable.VirtualAddress >= removedVa &&
                importTable.VirtualAddress < removedVa + removedSize)
            {
                var rdata = this.FindSection(".rdata");
                if (rdata != null && rdata.IsValid)
                {
                    var rdataData = this.ReadSection(rdata);
                    var importRva = PeImage.FindImportDescriptorInRdata(rdataData, rdata.VirtualAddress);

                    if (importRva > 0)
                    {
                        importTable.VirtualAddress = importRva;
                        this.ImportTable = importTable;

                        logService.OnAddLogMessage(null, new LogMessageEventArgs($" --> Import table now points to RVA 0x{importRva:X8}", LogMessageType.Debug));
                    }
                }
            }

            // The security entry is a file offset, not an RVA, so it moves with the overlay.
            var certificateTable = this.CertificateTable;
            if (certificateTable.VirtualAddress > 0 && certificateTable.Size > 0)
            {
                var lastSection = this.m_Sections[this.m_Sections.Count - 1];
                var overlayStart = lastSection.PointerToRawData + lastSection.SizeOfRawData;

                certificateTable.VirtualAddress = overlayStart;
                this.CertificateTable = certificateTable;

                logService.OnAddLogMessage(null, new LogMessageEventArgs($" --> Certificate table now points to file offset 0x{overlayStart:X8}", LogMessageType.Debug));
            }
        }

        private void WriteOverlay(Stream stream)
        {
            if (this.m_OverlayOffset < 0)
                return;

            stream.Write(this.m_FileData, (int)this.m_OverlayOffset, (int)(this.m_FileData.Length - this.m_OverlayOffset));
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Finds an import descriptor in `.rdata` whose name ends in `.dll` and whose IAT and
        /// lookup RVAs also land inside the section. Returns 0 when none matches.
        /// </summary>
        private static uint FindImportDescriptorInRdata(byte[] rdataData, uint rdataRva)
        {
            for (int offset = 0; offset < rdataData.Length - 20; offset += 4)
            {
                var nameRva = BitConverter.ToUInt32(rdataData, offset + 12);
                if (nameRva < rdataRva || nameRva >= rdataRva + rdataData.Length)
                    continue;

                var nameFileOff = nameRva - rdataRva;
                if (nameFileOff >= (uint)rdataData.Length)
                    continue;

                var dllName = System.Text.Encoding.ASCII.GetString(rdataData, (int)nameFileOff, Math.Min(64, rdataData.Length - (int)nameFileOff));
                var nullIdx = dllName.IndexOf('\0');
                if (nullIdx >= 0)
                    dllName = dllName.Substring(0, nullIdx);

                if (!dllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    continue;

                var origRva = BitConverter.ToUInt32(rdataData, offset);
                var iatRva = BitConverter.ToUInt32(rdataData, offset + 16);
                if (origRva < rdataRva || origRva >= rdataRva + rdataData.Length)
                    continue;
                if (iatRva < rdataRva || iatRva >= rdataRva + rdataData.Length)
                    continue;

                return rdataRva + (uint)offset;
            }

            return 0;
        }

        private static bool IsPowerOfTwo(uint value)
        {
            return value != 0 && (value & (value - 1)) == 0;
        }

        private uint ReadOptionalUInt32(int offset)
        {
            return BitConverter.ToUInt32(this.m_OptionalHeader, offset);
        }

        private void WriteOptionalUInt32(int offset, uint value)
        {
            Array.Copy(BitConverter.GetBytes(value), 0, this.m_OptionalHeader, offset, 4);
        }

        public byte[] DosStub { get; set; }

        public string FilePath { get; }

        public bool Is64Bit { get; }

        public ushort NumberOfSections
        {
            get => BitConverter.ToUInt16(this.m_FileHeader, 2);
            set => Array.Copy(BitConverter.GetBytes(value), 0, this.m_FileHeader, 2, 2);
        }

        public uint AddressOfEntryPoint
        {
            get => this.ReadOptionalUInt32(PeConstants.EntryPointOffset);
            set => this.WriteOptionalUInt32(PeConstants.EntryPointOffset, value);
        }

        public uint BaseOfCode => this.ReadOptionalUInt32(PeConstants.BaseOfCodeOffset);

        public ulong ImageBase
        {
            get => this.Is64Bit
                ? BitConverter.ToUInt64(this.m_OptionalHeader, PeConstants.ImageBaseOffset64)
                : this.ReadOptionalUInt32(PeConstants.ImageBaseOffset32);
        }

        public uint SectionAlignment => this.ReadOptionalUInt32(PeConstants.SectionAlignmentOffset);

        public uint FileAlignment => this.ReadOptionalUInt32(PeConstants.FileAlignmentOffset);

        public uint SizeOfImage
        {
            get => this.ReadOptionalUInt32(PeConstants.SizeOfImageOffset);
            set => this.WriteOptionalUInt32(PeConstants.SizeOfImageOffset, value);
        }

        public PeDataDirectory ImportTable
        {
            get => this.ReadDataDirectory(PeConstants.ImportDirectoryIndex);
            set => this.WriteDataDirectory(PeConstants.ImportDirectoryIndex, value);
        }

        public PeDataDirectory CertificateTable
        {
            get => this.ReadDataDirectory(PeConstants.CertificateDirectoryIndex);
            set => this.WriteDataDirectory(PeConstants.CertificateDirectoryIndex, value);
        }

        public PeDataDirectory TlsTable => this.ReadDataDirectory(PeConstants.TlsDirectoryIndex);

        public PeTlsDirectory TlsDirectory { get; private set; }

        public List<ulong> TlsCallbacks { get; } = new List<ulong>();

        private PeDataDirectory ReadDataDirectory(int index)
        {
            var offset = PeConstants.DataDirectoryOffset(this.Is64Bit) + (index * 8);
            return new PeDataDirectory(this.ReadOptionalUInt32(offset), this.ReadOptionalUInt32(offset + 4));
        }

        private void WriteDataDirectory(int index, PeDataDirectory directory)
        {
            var offset = PeConstants.DataDirectoryOffset(this.Is64Bit) + (index * 8);
            this.WriteOptionalUInt32(offset, directory.VirtualAddress);
            this.WriteOptionalUInt32(offset + 4, directory.Size);
        }
    }
}
