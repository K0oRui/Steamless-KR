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

namespace Steamless.Unpacker.Variant10.x86
{
    using API;
    using API.Events;
    using API.Extensions;
    using API.Model;
    using API.PE32;
    using API.Services;
    using Classes;
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    [SteamlessApiVersion(1, 0)]
    public class Main : SteamlessPlugin
    {
        private LoggingService m_LoggingService;

        public override string Author => "atom0s";

        public override string Name => "SteamStub Variant 1.0 Unpacker (x86)";

        public override string Description => "Unpacker for the 32bit SteamStub variant 1.0.";

        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        private void Log(string msg, LogMessageType type)
        {
            this.m_LoggingService.OnAddLogMessage(this, new LogMessageEventArgs(msg, type));
        }

        public override bool Initialize(LoggingService logService)
        {
            this.m_LoggingService = logService;
            return true;
        }

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

                // Attempt to locate the known v1.x signature..
                return Pe32Helpers.FindPattern(bind, "60 81 EC 00 10 00 00 BE ?? ?? ?? ?? B9 6A") != -1;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override bool ProcessFile(string file, SteamlessOptions options)
        {
            this.Options = options;
            this.OriginalEntryPoint = 0;

            this.File = new Pe32File(file);
            if (!this.File.Parse())
                return false;

            this.Log("File is packed with SteamStub Variant 1.0!", LogMessageType.Information);

            this.Log("Step 1 - Read, decode and validate the SteamStub DRM header.", LogMessageType.Information);
            if (!this.Step1())
                return false;

            this.Log("Step 2 - Handle .bind section.", LogMessageType.Information);
            if (!this.Step2())
                return false;

            this.Log("Step 3 - Rebuild and save the unpacked file.", LogMessageType.Information);
            if (!this.Step3())
                return false;

            if (this.Options.RecalculateFileChecksum)
            {
                this.Log("Step 4 - Rebuild unpacked file checksum.", LogMessageType.Information);
                if (!this.Step4())
                    return false;
            }

            return true;
        }

        private bool Step1()
        {
            var section = this.File.GetSection(".bind");
            if (!section.IsValid)
                return false;

            var bind = this.File.GetSectionData(".bind");
            var offset = Pe32Helpers.FindPattern(bind, "60 81 EC 00 10 00 00 BE ?? ?? ?? ?? B9 6A");
            if (offset == -1)
                return false;

            var headerPointer = BitConverter.ToUInt32(bind, (int)offset + 8);
            var headerSizeRaw = BitConverter.ToUInt32(bind, (int)offset + 13);
            if (headerSizeRaw > 0x3FFFFFFF)
                return false;

            var headerSize = headerSizeRaw * 4;

            var fileOffset = this.File.GetFileOffsetFromRva(headerPointer - this.File.NtHeaders.OptionalHeader.ImageBase);

            var headerData = new byte[headerSize];
            Array.Copy(this.File.FileData, fileOffset, headerData, 0, headerSize);

            // The v1.0 header is obfuscated by XOR-ing each byte with the square of its index.
            for (var x = 0; x < headerSize; x++)
                headerData[x] ^= (byte)(x * x);

            this.StubHeader = Pe32Helpers.GetStructure<SteamStub32Var10Header>(headerData);

            // Validates the header: the unpacker function must resolve to the file's entry point.
            if (this.StubHeader.BindFunction - this.File.NtHeaders.OptionalHeader.ImageBase != this.File.NtHeaders.OptionalHeader.AddressOfEntryPoint)
                return false;

            offset = Pe32Helpers.FindPattern(bind, "61 B8 ?? ?? ?? ?? FF E0");
            if (offset == -1)
                return false;

            this.OriginalEntryPoint = BitConverter.ToUInt32(bind, (int)offset + 2) - this.File.NtHeaders.OptionalHeader.ImageBase;

            return true;
        }

        private bool Step2()
        {
            // Stash the .bind bounds first; they are needed later to repair pointers into the removed section.
            {
                var bindSection = this.File.GetSection(".bind");
                if (bindSection.IsValid)
                {
                    this.BindSectionRva = bindSection.VirtualAddress;
                    this.BindSectionSize = bindSection.VirtualSize;
                }
            }

            if (!this.Options.KeepBindSection)
            {
                var bindSection = this.File.GetSection(".bind");
                if (!bindSection.IsValid)
                    return false;

                this.File.RemoveSection(bindSection);

                var ntHeaders = this.File.NtHeaders;
                ntHeaders.FileHeader.NumberOfSections--;
                this.File.NtHeaders = ntHeaders;

                this.Log(" --> .bind section was removed from the file.", LogMessageType.Debug);
            }
            else
                this.Log(" --> .bind section was kept in the file.", LogMessageType.Debug);

            return true;
        }

        private bool Step3()
        {
            FileStream fStream = null;

            try
            {
                if (this.Options.ZeroDosStubData && this.File.DosStubSize > 0)
                    this.File.DosStubData = Enumerable.Repeat((byte)0, (int)this.File.DosStubSize).ToArray();

                this.File.RebuildSections(this.Options.DontRealignSections == false);

                var unpackedPath = this.File.FilePath + ".unpacked.exe";
                fStream = new FileStream(unpackedPath, FileMode.Create, FileAccess.ReadWrite);

                fStream.WriteBytes(Pe32Helpers.GetStructureBytes(this.File.DosHeader));

                if (this.File.DosStubSize > 0)
                    fStream.WriteBytes(this.File.DosStubData);

                var ntHeaders = this.File.NtHeaders;
                ntHeaders.OptionalHeader.AddressOfEntryPoint = this.OriginalEntryPoint;
                ntHeaders.OptionalHeader.CheckSum = 0;

                // If the import table lived in the removed .bind section, repoint it to the real descriptor in .rdata.
                if (!this.Options.KeepBindSection && this.BindSectionSize > 0)
                {
                    var importTable = ntHeaders.OptionalHeader.ImportTable;
                    if (importTable.VirtualAddress >= this.BindSectionRva && importTable.VirtualAddress < this.BindSectionRva + this.BindSectionSize)
                    {
                        var rdataSection = this.File.GetSection(".rdata");
                        if (rdataSection.IsValid)
                        {
                            var rdataData = this.File.GetSectionData(".rdata");
                            var importRva = Pe32Helpers.FindImportDescriptorInRdata(rdataData, rdataSection.VirtualAddress);
                            if (importRva > 0)
                            {
                                importTable.VirtualAddress = importRva;
                                ntHeaders.OptionalHeader.ImportTable = importTable;
                                this.Log($" --> Fixed import table pointer to RVA 0x{importRva:X8}", LogMessageType.Debug);
                            }
                        }
                    }
                }

                // Repoint the certificate table to the new overlay start; its offset shifts when .bind is removed.
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

                fStream.WriteBytes(Pe32Helpers.GetStructureBytes(ntHeaders));

                for (var x = 0; x < this.File.Sections.Count; x++)
                {
                    var section = this.File.Sections[x];
                    var sectionData = this.File.SectionData[x];

                    fStream.WriteBytes(Pe32Helpers.GetStructureBytes(section));

                    var sectionOffset = fStream.Position;
                    fStream.Position = section.PointerToRawData;

                    fStream.WriteBytes(sectionData);

                    fStream.Position = sectionOffset;
                }

                fStream.Position = fStream.Length;

                if (this.File.OverlayData != null)
                    fStream.WriteBytes(this.File.OverlayData);

                this.Log(" --> Unpacked file saved to disk!", LogMessageType.Success);
                this.Log($" --> File Saved As: {unpackedPath}", LogMessageType.Success);

                return true;
            }
            catch (Exception)
            {
                this.Log(" --> Error trying to save unpacked file!", LogMessageType.Error);
                return false;
            }
            finally
            {
                fStream?.Dispose();
            }
        }

        private bool Step4()
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

        private SteamlessOptions Options { get; set; }

        private Pe32File File { get; set; }

        private SteamStub32Var10Header StubHeader { get; set; }

        private uint OriginalEntryPoint { get; set; }

        private uint BindSectionRva { get; set; }

        private uint BindSectionSize { get; set; }
    }
}
