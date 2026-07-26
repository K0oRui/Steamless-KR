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

namespace Steamless.Unpacker.Variant20.x86
{
    using API;
    using API.Events;
    using API.Extensions;
    using API.Model;
    using API.PE32;
    using API.Services;
    using Classes;
    using Iced.Intel;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    [SteamlessApiVersion(1, 0)]
    public class Main : SteamlessPlugin
    {
        /// <summary>
        /// Internal logging service instance.
        /// </summary>
        private LoggingService m_LoggingService;

        /// <summary>
        /// Gets the author of this plugin.
        /// </summary>
        public override string Author => "atom0s";

        /// <summary>
        /// Gets the name of this plugin.
        /// </summary>
        public override string Name => "SteamStub Variant 2.0 Unpacker (x86)";

        /// <summary>
        /// Gets the description of this plugin.
        /// </summary>
        public override string Description => "Unpacker for the 32bit SteamStub variant 2.0.";

        /// <summary>
        /// Gets the version of this plugin.
        /// </summary>
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        /// <summary>
        /// Internal wrapper to log a message.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="type"></param>
        private void Log(string msg, LogMessageType type)
        {
            this.m_LoggingService.OnAddLogMessage(this, new LogMessageEventArgs(msg, type));
        }

        /// <summary>
        /// Initialize function called when this plugin is first loaded.
        /// </summary>
        /// <param name="logService"></param>
        /// <returns></returns>
        public override bool Initialize(LoggingService logService)
        {
            this.m_LoggingService = logService;
            return true;
        }

        /// <summary>
        /// Processing function called when a file is being unpacked. Allows plugins to check the file
        /// and see if it can handle the file for its intended purpose.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public override bool CanProcessFile(string file)
        {
            try
            {
                // Load the file..
                var f = new Pe32File(file);
                if (!f.Parse() || f.IsFile64Bit() || !f.HasSection(".bind"))
                    return false;

                // Obtain the bind section data..
                var bind = f.GetSectionData(".bind");

                // Attempt to locate the known v2.0 signature..
                return Pe32Helpers.FindPattern(bind, "53 51 52 56 57 55 8B EC 81 EC 00 10 00 00 BE") != -1;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Processing function called to allow the plugin to process the file.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public override bool ProcessFile(string file, SteamlessOptions options)
        {
            // Initialize the class members..
            this.Options = options;
            this.CodeSectionData = null;
            this.CodeSectionIndex = -1;
            this.PayloadData = null;
            this.SteamDrmpData = null;
            this.SteamDrmpOffsets = new List<int>();
            this.UseFallbackDrmpOffsets = false;
            this.XorKey = 0;

            // Parse the file..
            this.File = new Pe32File(file);
            if (!this.File.Parse())
                return false;

            // Announce we are being unpacked with this packer..
            this.Log("File is packed with SteamStub Variant 2.0!", LogMessageType.Information);

            this.Log("Step 1 - Read, disassemble and decode the SteamStub DRM header.", LogMessageType.Information);
            if (!this.Step1())
                return false;

            this.Log("Step 2 - Read, decrypt and process the main code section.", LogMessageType.Information);
            if (!this.Step2())
                return false;

            this.Log("Step 3 - Prepare the file sections.", LogMessageType.Information);
            if (!this.Step3())
                return false;

            this.Log("Step 4 - Rebuild and save the unpacked file.", LogMessageType.Information);
            if (!this.Step4())
                return false;

            if (this.Options.RecalculateFileChecksum)
            {
                this.Log("Step 5 - Rebuild unpacked file checksum.", LogMessageType.Information);
                if (!this.Step5())
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Step #1
        /// 
        /// Read, disassemble and decode the SteamStub DRM header.
        /// </summary>
        /// <returns></returns>
        private bool Step1()
        {
            // Obtain the file entry offset..
            var fileOffset = this.File.GetFileOffsetFromRva(this.File.NtHeaders.OptionalHeader.AddressOfEntryPoint);

            // Validate the DRM header..
            if (BitConverter.ToUInt32(this.File.FileData, (int)fileOffset - 4) != 0xC0DEC0DE)
                return false;

            // Disassemble the file to locate the needed DRM information..
            if (!this.DisassembleFile(out var structOffset, out var structSize, out var structXorKey))
                return false;

            // Obtain the DRM header data..
            var headerData = new byte[structSize];
            Array.Copy(this.File.FileData, this.File.GetFileOffsetFromRva(structOffset), headerData, 0, structSize);

            // Xor decode the header data..
            this.XorKey = SteamStubHelpers.SteamXor(ref headerData, (uint)headerData.Length, 0);

            // Create the stub header..
            switch (structSize)
            {
                case 856:
                    this.StubHeader = Pe32Helpers.GetStructure<SteamStub32Var20_856_Header>(headerData);
                    break;
                case 884:
                    this.StubHeader = Pe32Helpers.GetStructure<SteamStub32Var20_884_Header>(headerData);
                    break;
                case 952:
                    this.StubHeader = Pe32Helpers.GetStructure<SteamStub32Var20_952_Header>(headerData);
                    break;
                default:
                    {
                        this.Log("", LogMessageType.Error);
                        this.Log($"Invalid/unknown variant header size: {structSize}", LogMessageType.Error);
                        this.Log("Please report this issue on Steamless' GitHub issue tracker!", LogMessageType.Error);
                        this.Log("Be sure to include a copy of this games .exe file you are trying to unpack!", LogMessageType.Error);
                        this.Log("", LogMessageType.Error);
                        return false;
                    }
            }

            return true;
        }

        /// <summary>
        /// Step #2
        /// 
        /// Read, decrypt and process the main code section.
        /// </summary>
        /// <returns></returns>
        private bool Step2()
        {
            /**
             * TODO:
             * 
             * Should we add custom checks here that mimic the validations of the stub based on the header flags?
             * 
             *      0x01 - Hash check validation of the .bind code and stub header.
             *      0x02 - WinTrustVerify validation of the file.
             *      
             * These would just be for warnings to let users know if the file was broken/tampered, but unpacking should
             * still complete if it can.
             */

            // Determine the code section RVA..
            var codeSectionRVA = this.File.NtHeaders.OptionalHeader.BaseOfCode;

            // TODO: This is not really ideal to do but for now this breaks support for other variants of this version..
            if (this.Options.UseExperimentalFeatures)
            {
                if (this.StubHeader.CodeSectionVirtualAddress != 0)
                    codeSectionRVA = this.File.GetRvaFromVa(this.StubHeader.CodeSectionVirtualAddress);
            }

            // Get the code section..
            var codeSection = this.File.GetOwnerSection(codeSectionRVA);
            if (codeSection.PointerToRawData == 0 || codeSection.SizeOfRawData == 0)
                return false;

            this.CodeSectionIndex = this.File.GetSectionIndex(codeSection);

            // Get the code section data..
            var codeSectionData = new byte[codeSection.SizeOfRawData];
            Array.Copy(this.File.FileData, this.File.GetFileOffsetFromRva(codeSection.VirtualAddress), codeSectionData, 0, codeSection.SizeOfRawData);

            // Skip the code section encoding if we do not need to process it..
            if ((this.StubHeader.Flags & (uint)DrmFlags.UseEncodedCodeSection) == 0)
                return true;

            // Decode the code section data..
            var key = this.StubHeader.CodeSectionXorKey;
            var offset = 0;
            for (var x = this.StubHeader.CodeSectionSize >> 2; x > 0; --x)
            {
                var val1 = BitConverter.ToUInt32(codeSectionData, offset);
                var val2 = val1 ^ key;
                key = val1;

                Array.Copy(BitConverter.GetBytes(val2), 0, codeSectionData, offset, 4);

                offset += 4;
            }

            // Store the section data..
            this.CodeSectionData = codeSectionData;

            return true;
        }

        /// <summary>
        /// Step #3
        /// 
        /// Prepare the file sections.
        /// </summary>
        /// <returns></returns>
        private bool Step3()
        {
            // Save the .bind section info before removal for later use..
            {
                var bindSection = this.File.GetSection(".bind");
                if (bindSection.IsValid)
                {
                    this.BindSectionRva = bindSection.VirtualAddress;
                    this.BindSectionSize = bindSection.VirtualSize;
                }
            }

            // Remove the bind section if its not requested to be saved..
            if (!this.Options.KeepBindSection)
            {
                // Obtain the .bind section..
                var bindSection = this.File.GetSection(".bind");
                if (!bindSection.IsValid)
                    return false;

                // Remove the section..
                this.File.RemoveSection(bindSection);

                // Decrease the header section count..
                var ntHeaders = this.File.NtHeaders;
                ntHeaders.FileHeader.NumberOfSections--;
                this.File.NtHeaders = ntHeaders;

                this.Log(" --> .bind section was removed from the file.", LogMessageType.Debug);
            }
            else
                this.Log(" --> .bind section was kept in the file.", LogMessageType.Debug);

            try
            {
                // Rebuild the file sections..
                this.File.RebuildSections(this.Options.DontRealignSections == false);
            }
            catch
            {
                return false;
            }


            return true;
        }

        /// <summary>
        /// Scans .rdata section data for the original import descriptor table.
        /// </summary>
        private uint FindImportDescriptorInRdata(byte[] rdataData, uint rdataRva, NativeApi32.ImageDataDirectory32 currentImport)
        {
            return FindImportByDllNamePattern(rdataData, rdataRva);
        }

        /// <summary>
        /// Scans .rdata for import descriptors by searching for DLL name RVA patterns.
        /// </summary>
        private uint FindImportByDllNamePattern(byte[] rdataData, uint rdataRva)
        {
            var dllStrings = new System.Collections.Generic.List<string>();
            for (int i = 0; i < rdataData.Length - 6; i++)
            {
                if (rdataData[i] >= 0x41 && rdataData[i] <= 0x7A)
                {
                    var end = i;
                    while (end < rdataData.Length && rdataData[end] >= 0x20 && rdataData[end] <= 0x7E)
                        end++;
                    var len = end - i;
                    if (len > 5 && len < 260)
                    {
                        var str = System.Text.Encoding.ASCII.GetString(rdataData, i, len);
                        if (str.EndsWith(".dll", System.StringComparison.OrdinalIgnoreCase))
                            dllStrings.Add(str);
                    }
                }
            }

            if (dllStrings.Count == 0)
                return 0;

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

                if (!dllName.EndsWith(".dll", System.StringComparison.OrdinalIgnoreCase))
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

        /// <summary>
        /// Step #4
        /// 
        /// Rebuild and save the unpacked file.
        /// </summary>
        /// <returns></returns>
        private bool Step4()
        {
            FileStream fStream = null;

            try
            {
                // Zero the DosStubData if desired..
                if (this.Options.ZeroDosStubData && this.File.DosStubSize > 0)
                    this.File.DosStubData = Enumerable.Repeat((byte)0, (int)this.File.DosStubSize).ToArray();

                // Open the unpacked file for writing..
                var unpackedPath = this.File.FilePath + ".unpacked.exe";
                fStream = new FileStream(unpackedPath, FileMode.Create, FileAccess.ReadWrite);

                // Write the DOS header to the file..
                fStream.WriteBytes(Pe32Helpers.GetStructureBytes(this.File.DosHeader));

                // Write the DOS stub to the file..
                if (this.File.DosStubSize > 0)
                    fStream.WriteBytes(this.File.DosStubData);

                // Update the NT headers..
                var ntHeaders = this.File.NtHeaders;
                var lastSection = this.File.Sections[this.File.Sections.Count - 1];
                ntHeaders.OptionalHeader.AddressOfEntryPoint = this.File.GetRvaFromVa(this.StubHeader.OEP);
                ntHeaders.OptionalHeader.CheckSum = 0;
                ntHeaders.OptionalHeader.SizeOfImage = this.File.GetAlignment(lastSection.VirtualAddress + lastSection.VirtualSize, this.File.NtHeaders.OptionalHeader.SectionAlignment);

                // Fix the import table entry if it points into the removed .bind section..
                if (!this.Options.KeepBindSection && this.BindSectionSize > 0)
                {
                    var importTable = ntHeaders.OptionalHeader.ImportTable;
                    if (importTable.VirtualAddress >= this.BindSectionRva && importTable.VirtualAddress < this.BindSectionRva + this.BindSectionSize)
                    {
                        var rdataSection = this.File.GetSection(".rdata");
                        if (rdataSection.IsValid)
                        {
                            var rdataData = this.File.GetSectionData(".rdata");
                            var importRva = this.FindImportDescriptorInRdata(rdataData, rdataSection.VirtualAddress, importTable);
                            if (importRva > 0)
                            {
                                importTable.VirtualAddress = importRva;
                                ntHeaders.OptionalHeader.ImportTable = importTable;
                                this.Log($" --> Fixed import table pointer to RVA 0x{importRva:X8}", LogMessageType.Debug);
                            }
                        }
                    }
                }

                // Fix the certificate table entry if a certificate exists and the file layout has changed..
                if (!this.Options.KeepBindSection && this.BindSectionSize > 0)
                {
                    var certTable = ntHeaders.OptionalHeader.CertificateTable;
                    if (certTable.VirtualAddress > 0 && certTable.Size > 0)
                    {
                        var lastSectionRaw = this.File.Sections[this.File.Sections.Count - 1];
                        var overlayStart = lastSectionRaw.PointerToRawData + lastSectionRaw.SizeOfRawData;
                        certTable.VirtualAddress = overlayStart;
                        ntHeaders.OptionalHeader.CertificateTable = certTable;
                        this.Log($" --> Fixed certificate table pointer to file offset 0x{overlayStart:X8}", LogMessageType.Debug);
                    }
                }

                this.File.NtHeaders = ntHeaders;

                // Write the NT headers to the file..
                fStream.WriteBytes(Pe32Helpers.GetStructureBytes(ntHeaders));

                // Write the sections to the file..
                for (var x = 0; x < this.File.Sections.Count; x++)
                {
                    var section = this.File.Sections[x];
                    var sectionData = this.File.SectionData[x];

                    // Write the section header to the file..
                    fStream.WriteBytes(Pe32Helpers.GetStructureBytes(section));

                    // Set the file pointer to the sections raw data..
                    var sectionOffset = fStream.Position;
                    fStream.Position = section.PointerToRawData;

                    // Write the sections raw data..
                    var sectionIndex = this.File.Sections.IndexOf(section);
                    if (sectionIndex == this.CodeSectionIndex)
                        fStream.WriteBytes(this.CodeSectionData ?? sectionData);
                    else
                        fStream.WriteBytes(sectionData);

                    // Reset the file offset..
                    fStream.Position = sectionOffset;
                }

                // Set the stream to the end of the file..
                fStream.Position = fStream.Length;

                // Write the overlay data if it exists..
                if (this.File.OverlayData != null)
                    fStream.WriteBytes(this.File.OverlayData);

                this.Log(" --> Unpacked file saved to disk!", LogMessageType.Success);
                this.Log($" --> File Saved As: {unpackedPath}", LogMessageType.Success);

                return true;
            }
            catch
            {
                this.Log(" --> Error trying to save unpacked file!", LogMessageType.Error);
                return false;
            }
            finally
            {
                fStream?.Dispose();
            }
        }

        /// <summary>
        /// Step #5
        /// 
        /// Recalculate the file checksum.
        /// </summary>
        /// <returns></returns>
        private bool Step5()
        {
            var unpackedPath = this.File.FilePath + ".unpacked.exe";
            if (!Pe32Helpers.UpdateFileChecksum(unpackedPath))
            {
                this.Log(" --> Error trying to recalculate unpacked file checksum!", LogMessageType.Error);
                return false;
            }

            this.Log(" --> Unpacked file updated with new checksum!", LogMessageType.Success);
            return true;

        }

        /// <summary>
        /// Disassembles the file to locate the needed DRM header information.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <param name="xorKey"></param>
        /// <returns></returns>
        private bool DisassembleFile(out uint offset, out uint size, out uint xorKey)
        {
            uint structOffset = 0;
            uint structSize = 0;
            uint structXorKey = 0;

            var entryOffset = this.File.GetFileOffsetFromRva(this.File.NtHeaders.OptionalHeader.AddressOfEntryPoint);

            try
            {
                var reader = new ByteArrayCodeReader(this.File.FileData, (int)entryOffset, Math.Min(4096, this.File.FileData.Length - (int)entryOffset));
                var decoder = Decoder.Create(32, reader);
                decoder.IP = (ulong)entryOffset;
                var endRip = decoder.IP + 4096;

                while (decoder.IP < endRip && reader.CanReadByte)
                {
                    var inst = decoder.Decode();

                    if (structOffset > 0 && structSize > 0 && structXorKey > 0)
                    {
                        offset = structOffset;
                        size = structSize;
                        xorKey = structXorKey;
                        return true;
                    }

                    // Looks for: mov reg, immediate
                    if (inst.Op0Kind == OpKind.Register && IsImmediate32(inst.Op1Kind))
                    {
                        if (structOffset == 0)
                        {
                            structOffset = inst.Immediate32 - this.File.NtHeaders.OptionalHeader.ImageBase;
                            continue;
                        }
                    }

                    // Looks for: mov reg, immediate
                    if (inst.Op0Kind == OpKind.Register && IsImmediate32(inst.Op1Kind))
                    {
                        structSize = inst.Immediate32 * 4;
                        structXorKey = 1;
                    }
                }

                offset = size = xorKey = 0;
                return false;
            }
            catch
            {
                offset = size = xorKey = 0;
                return false;
            }
        }

        private static bool IsImmediate32(OpKind kind) =>
            kind == OpKind.Immediate32 || kind == OpKind.Immediate8to32;

        /// <summary>
        /// Gets or sets the Steamless options this file was requested to process with.
        /// </summary>
        private SteamlessOptions Options { get; set; }

        /// <summary>
        /// Gets or sets the file being processed.
        /// </summary>
        private Pe32File File { get; set; }

        /// <summary>
        /// Gets or sets the current xor key being used against the file data.
        /// </summary>
        private uint XorKey { get; set; }

        /// <summary>
        /// Gets or sets the DRM stub header.
        /// </summary>
        private dynamic StubHeader { get; set; }

        /// <summary>
        /// Gets or sets the payload data.
        /// </summary>
        public byte[] PayloadData { get; set; }

        /// <summary>
        /// Gets or sets the SteamDRMP.dll data.
        /// </summary>
        public byte[] SteamDrmpData { get; set; }

        /// <summary>
        /// Gets or sets the list of SteamDRMP.dll offsets.
        /// </summary>
        public List<int> SteamDrmpOffsets { get; set; }

        /// <summary>
        /// Gets or sets if the offsets should be read using fallback values.
        /// </summary>
        private bool UseFallbackDrmpOffsets { get; set; }

        /// <summary>
        /// Gets or sets the index of the code section.
        /// </summary>
        private int CodeSectionIndex { get; set; }

        /// <summary>
        /// Gets or sets the decrypted code section data.
        /// </summary>
        private byte[] CodeSectionData { get; set; }

        /// <summary>
        /// Gets or sets the .bind section virtual address.
        /// </summary>
        private uint BindSectionRva { get; set; }

        /// <summary>
        /// Gets or sets the .bind section virtual size.
        /// </summary>
        private uint BindSectionSize { get; set; }
    }
}