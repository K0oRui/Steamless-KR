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

namespace Steamless.Unpacker.Variant31.x64
{
    using API;
    using API.Crypto;
    using API.Events;
    using API.Extensions;
    using API.Model;
    using API.PE64;
    using API.Services;
    using Classes;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;

    [SteamlessApiVersion(1, 0)]
    public class Main : SteamlessPlugin
    {
        private LoggingService m_LoggingService;

        public override string Author => "atom0s";

        public override string Name => "SteamStub Variant 3.1.x Unpacker (x64)";

        public override string Description => "Unpacker for the 64bit SteamStub variant 3.1.x.";

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
                var f = new Pe64File(file);
                if (!f.Parse() || !f.IsFile64Bit() || !f.HasSection(".bind"))
                    return false;

                var bind = f.GetSectionData(".bind");

                var variant = Pe64Helpers.FindPattern(bind, "E8 00 00 00 00 50 53 51 52 56 57 55 41 50");
                if (variant == -1)
                    return false;

                var offset = Pe64Helpers.FindPattern(bind, "48 8D 91 ?? ?? ?? ?? 48"); // 3.0
                if (offset == -1)
                    offset = Pe64Helpers.FindPattern(bind, "48 8D 91 ?? ?? ?? ?? 41"); // 3.1
                if (offset == -1)
                {
                    offset = Pe64Helpers.FindPattern(bind, "48 C7 84 24 ?? ?? ?? ?? ?? ?? ?? ?? 48"); // 3.1.2
                    if (offset > 0)
                        offset += 5;
                }

                if (offset == -1)
                    return false;

                // Read the header size.. (The header size is only 32bit!)
                var headerSize = Math.Abs(BitConverter.ToInt32(bind, (int)offset + 3));

                // Validate against the known v3.1 header size (0xF0).
                return headerSize == 0xF0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override bool ProcessFile(string file, SteamlessOptions options)
        {
            this.TlsAsOep = false;
            this.TlsOepRva = 0;
            this.TlsOepOverride = 0;
            this.Options = options;
            this.CodeSectionData = null;
            this.CodeSectionIndex = -1;
            this.XorKey = 0;

            this.File = new Pe64File(file);
            if (!this.File.Parse())
                return false;

            this.Log("File is packed with SteamStub Variant 3.1 (x64)!", LogMessageType.Information);

            this.Log("Step 1 - Read, decode and validate the SteamStub DRM header.", LogMessageType.Information);
            if (!this.Step1())
                return false;

            this.Log("Step 2 - Read, decode and process the payload data.", LogMessageType.Information);
            if (!this.Step2())
                return false;

            this.Log("Step 3 - Read, decode and dump the SteamDRMP.dll file.", LogMessageType.Information);
            if (!this.Step3())
                return false;

            this.Log("Step 4 - Handle .bind section. Find code section.", LogMessageType.Information);
            if (!this.Step4())
                return false;

            this.Log("Step 5 - Read, decrypt and process code section.", LogMessageType.Information);
            if (!this.Step5())
                return false;

            this.Log("Step 6 - Rebuild and save the unpacked file.", LogMessageType.Information);
            if (!this.Step6())
                return false;

            if (this.Options.RecalculateFileChecksum)
            {
                this.Log("Step 7 - Rebuild unpacked file checksum.", LogMessageType.Information);
                if (!this.Step7())
                    return false;
            }

            return true;
        }

        private bool RebuildTlsCallbackInformation()
        {
            // Ensure the modified main TlsCallback is within the .bind section..
            var section = this.File.GetOwnerSection(this.File.GetRvaFromVa(this.File.TlsCallbacks[0]));
            if (!section.IsValid || string.Compare(section.SectionName, ".bind", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.CompareOptions.IgnoreCase) != 0)
                return false;

            var addr = this.File.GetFileOffsetFromRva(this.File.GetRvaFromVa(this.File.TlsDirectory.AddressOfCallBacks));
            var tlsd = this.File.GetOwnerSection(addr);

            if (!tlsd.IsValid)
                return false;

            addr -= tlsd.PointerToRawData;

            // Restore the true original TlsCallback address..
            var callback = BitConverter.GetBytes(this.File.NtHeaders.OptionalHeader.ImageBase + this.StubHeader.OriginalEntryPoint);
            Array.Copy(callback, 0, this.File.GetSectionData(this.File.GetSectionIndex(tlsd)), (int)addr, callback.Length);

            var entry = (int)this.File.GetFileOffsetFromRva(this.File.NtHeaders.OptionalHeader.AddressOfEntryPoint);
            var data = new byte[Math.Min(0x100, this.File.FileData.Length - entry)];
            Array.Copy(this.File.FileData, entry, data, 0, data.Length);

            // Find the XOR key from within the function..
            var res = Pe64Helpers.FindPattern(data, "48 81 EA ?? ?? ?? ?? 8B 12 81 F2");
            if (res == -1)
                return false;

            // Decrypt and recalculate the true OEP address..
            var key = (ulong)(this.StubHeader.XorKey ^ BitConverter.ToInt32(data, (int)res + 0x0B));
            var off = (ulong)((this.File.NtHeaders.OptionalHeader.ImageBase + this.File.NtHeaders.OptionalHeader.AddressOfEntryPoint) + key);

            this.TlsOepOverride = (uint)(off - this.File.NtHeaders.OptionalHeader.ImageBase);
            return true;
        }

        private bool Step1()
        {
            var headerData = new byte[0xF0];

            // Attempt 1: read the header from EP - 0xF0..
            var fileOffset = this.File.GetFileOffsetFromRva(this.File.NtHeaders.OptionalHeader.AddressOfEntryPoint);
            if (fileOffset >= 0xF0)
            {
                Array.Copy(this.File.FileData, (long)(fileOffset - 0xF0), headerData, 0, 0xF0);
                this.XorKey = SteamStubHelpers.SteamXor(ref headerData, 0xF0);
                this.StubHeader = Pe64Helpers.GetStructure<SteamStub64Var31Header>(headerData);
                if (this.StubHeader.Signature == 0xC0DEC0DF)
                    return true;
            }

            // Attempt 2: read the header from TLS callback - 0xF0..
            if (this.File.TlsCallbacks.Count > 0)
            {
                fileOffset = this.File.GetRvaFromVa(this.File.TlsCallbacks[0]);
                fileOffset = this.File.GetFileOffsetFromRva(fileOffset);
                if (fileOffset >= 0xF0)
                {
                    headerData = new byte[0xF0];
                    Array.Copy(this.File.FileData, (long)(fileOffset - 0xF0), headerData, 0, 0xF0);
                    this.XorKey = SteamStubHelpers.SteamXor(ref headerData, 0xF0);
                    this.StubHeader = Pe64Helpers.GetStructure<SteamStub64Var31Header>(headerData);
                    if (this.StubHeader.Signature == 0xC0DEC0DF)
                    {
                        this.TlsAsOep = true;
                        this.TlsOepRva = this.File.GetRvaFromVa(this.File.TlsCallbacks[0]);

                        return this.RebuildTlsCallbackInformation();
                    }
                }
            }

            // Attempt 3: scan the .bind section for the header.
            // Some packed files have a PE entry point that does not point into
            // the .bind section, so the header is not at EP - 0xF0.
            var bindSection = this.File.GetSection(".bind");
            if (bindSection.IsValid)
            {
                var bindData = this.File.GetSectionData(".bind");
                for (var offset = 0; offset + 0xF0 <= bindData.Length; offset += 4)
                {
                    headerData = new byte[0xF0];
                    Array.Copy(bindData, offset, headerData, 0, 0xF0);
                    this.XorKey = SteamStubHelpers.SteamXor(ref headerData, 0xF0);
                    this.StubHeader = Pe64Helpers.GetStructure<SteamStub64Var31Header>(headerData);
                    if (this.StubHeader.Signature == 0xC0DEC0DF)
                        return true;
                }
            }

            return false;
        }

        private bool Step2()
        {
            var payloadAddr = this.File.GetFileOffsetFromRva(this.TlsAsOep ? this.TlsOepRva - this.StubHeader.BindSectionOffset : this.File.NtHeaders.OptionalHeader.AddressOfEntryPoint - this.StubHeader.BindSectionOffset);
            var payloadSize = (this.StubHeader.PayloadSize + 0x0F) & 0xFFFFFFF0;

            if (payloadSize == 0)
                return true;

            this.Log(" --> File has payload data!", LogMessageType.Debug);

            var payload = new byte[payloadSize];
            Array.Copy(this.File.FileData, (long)payloadAddr, payload, 0, payloadSize);
            this.XorKey = SteamStubHelpers.SteamXor(ref payload, payloadSize, this.XorKey);

            try
            {
                if (this.Options.DumpPayloadToDisk)
                {
                    System.IO.File.WriteAllBytes(this.File.FilePath + ".payload", payload);
                    this.Log(" --> Saved payload to disk!", LogMessageType.Debug);
                }
            }
            catch (Exception)
            {
            }

            return true;
        }

        private bool Step3()
        {
            if (this.StubHeader.DRMPDllSize == 0)
            {
                this.Log(" --> File does not contain a SteamDRMP.dll file.", LogMessageType.Debug);
                return true;
            }

            this.Log(" --> File has SteamDRMP.dll file!", LogMessageType.Debug);

            try
            {
                var drmpAddr = this.File.GetFileOffsetFromRva(this.TlsAsOep ? this.TlsOepRva - this.StubHeader.BindSectionOffset + this.StubHeader.DRMPDllOffset : this.File.NtHeaders.OptionalHeader.AddressOfEntryPoint - this.StubHeader.BindSectionOffset + this.StubHeader.DRMPDllOffset);
                var drmpData = new byte[this.StubHeader.DRMPDllSize];
                Array.Copy(this.File.FileData, (long)drmpAddr, drmpData, 0, drmpData.Length);

                SteamStubHelpers.SteamDrmpDecryptPass1(ref drmpData, this.StubHeader.DRMPDllSize, this.StubHeader.EncryptionKeys);

                try
                {
                    if (this.Options.DumpSteamDrmpToDisk)
                    {
                        var basePath = Path.GetDirectoryName(this.File.FilePath) ?? string.Empty;
                        System.IO.File.WriteAllBytes(Path.Combine(basePath, "SteamDRMP.dll"), drmpData);
                        this.Log(" --> Saved SteamDRMP.dll to disk!", LogMessageType.Debug);
                    }
                }
                catch (Exception)
                {
                }

                return true;
            }
            catch (Exception)
            {
                this.Log(" --> Error trying to decrypt the files SteamDRMP.dll data!", LogMessageType.Error);
                return false;
            }
        }

        private bool Step4()
        {
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

            if ((this.StubHeader.Flags & (uint)SteamStubDrmFlags.NoEncryption) == (uint)SteamStubDrmFlags.NoEncryption)
                return true;

            var codeSection = this.File.GetOwnerSection(this.StubHeader.CodeSectionVirtualAddress);

            this.CodeSectionIndex = this.File.GetSectionIndex(codeSection);

            return true;
        }

        private bool Step5()
        {
            if ((this.StubHeader.Flags & (uint)SteamStubDrmFlags.NoEncryption) == (uint)SteamStubDrmFlags.NoEncryption)
            {
                this.Log(" --> Code section is not encrypted.", LogMessageType.Debug);
                return true;
            }

            try
            {
                var codeSection = this.File.Sections[this.CodeSectionIndex];
                this.Log($" --> {codeSection.SectionName} linked as main code section.", LogMessageType.Debug);
                this.Log($" --> {codeSection.SectionName} section is encrypted.", LogMessageType.Debug);

                if (codeSection.SizeOfRawData == 0)
                {
                    this.Log($" --> {codeSection.SectionName} section is empty; skipping decryption.", LogMessageType.Debug);

                    this.CodeSectionData = new byte[] { };
                    return true;
                }

                var codeSectionData = new byte[(long)this.StubHeader.CodeSectionRawSize + this.StubHeader.CodeSectionStolenData.Length];
                Array.Copy(this.StubHeader.CodeSectionStolenData, 0, codeSectionData, 0, this.StubHeader.CodeSectionStolenData.Length);
                Array.Copy(this.File.FileData, (long)this.File.GetFileOffsetFromRva(codeSection.VirtualAddress), codeSectionData, this.StubHeader.CodeSectionStolenData.Length, (long)this.StubHeader.CodeSectionRawSize);

                var aes = new AesHelper(this.StubHeader.AES_Key, this.StubHeader.AES_IV);
                aes.RebuildIv(this.StubHeader.AES_IV);

                var data = aes.Decrypt(codeSectionData, CipherMode.CBC, PaddingMode.None);
                if (data == null)
                    return false;

                if (this.CodeSectionIndex < 0)
                {
                    this.Log(" --> Error: could not resolve code section index!", LogMessageType.Error);
                    return false;
                }

                var sectionData = this.File.SectionData[this.CodeSectionIndex];
                var copySize = Math.Min((long)this.StubHeader.CodeSectionRawSize, sectionData.Length);
                Array.Copy(data, sectionData, copySize);
                this.CodeSectionData = sectionData;

                return true;
            }
            catch (Exception)
            {
                this.Log(" --> Error trying to decrypt the files code section data!", LogMessageType.Error);
                return false;
            }
        }

        private bool Step6()
        {
            FileStream fStream = null;

            try
            {
                if (this.Options.ZeroDosStubData && this.File.DosStubSize > 0)
                    this.File.DosStubData = Enumerable.Repeat((byte)0, (int)this.File.DosStubSize).ToArray();

                this.File.RebuildSections(this.Options.DontRealignSections == false);

                var unpackedPath = this.File.FilePath + ".unpacked.exe";
                fStream = new FileStream(unpackedPath, FileMode.Create, FileAccess.ReadWrite);

                fStream.WriteBytes(Pe64Helpers.GetStructureBytes(this.File.DosHeader));

                if (this.File.DosStubSize > 0)
                    fStream.WriteBytes(this.File.DosStubData);

                var ntHeaders = this.File.NtHeaders;
                // When the stub redirected the OEP to a TLS callback, use the recovered override.
                if (this.TlsOepOverride > 0)
                    ntHeaders.OptionalHeader.AddressOfEntryPoint = this.TlsOepOverride;
                else
                    ntHeaders.OptionalHeader.AddressOfEntryPoint = (uint)this.StubHeader.OriginalEntryPoint;
                ntHeaders.OptionalHeader.CheckSum = 0;

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
                            var importRva = this.FindImportDescriptorInRdata(rdataData, rdataSection.VirtualAddress);
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

                fStream.WriteBytes(Pe64Helpers.GetStructureBytes(ntHeaders));

                for (var x = 0; x < this.File.Sections.Count; x++)
                {
                    var section = this.File.Sections[x];
                    var sectionData = this.File.SectionData[x];

                    fStream.WriteBytes(Pe64Helpers.GetStructureBytes(section));

                    var sectionOffset = fStream.Position;
                    fStream.Position = section.PointerToRawData;

                    var sectionIndex = this.File.Sections.IndexOf(section);
                    if (sectionIndex == this.CodeSectionIndex)
                        fStream.WriteBytes(this.CodeSectionData ?? sectionData);
                    else
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

        private uint FindImportDescriptorInRdata(byte[] rdataData, uint rdataRva)
        {
            return FindImportByDllNamePattern(rdataData, rdataRva);
        }

        private uint FindImportByDllNamePattern(byte[] rdataData, uint rdataRva)
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

        private bool Step7()
        {
            var unpackedPath = this.File.FilePath + ".unpacked.exe";
            if (!Pe64Helpers.UpdateFileChecksum(unpackedPath))
            {
                this.Log(" --> Error trying to recalculate unpacked file checksum!", LogMessageType.Error);
                return false;
            }

            this.Log(" --> Unpacked file updated with new checksum!", LogMessageType.Success);
            return true;

        }

        private bool TlsAsOep { get; set; }

        private ulong TlsOepRva { get; set; }

        private uint TlsOepOverride { get; set; }

        private SteamlessOptions Options { get; set; }

        private Pe64File File { get; set; }

        private uint XorKey { get; set; }

        private SteamStub64Var31Header StubHeader { get; set; }

        private int CodeSectionIndex { get; set; }

        private byte[] CodeSectionData { get; set; }

        private ulong BindSectionRva { get; set; }

        private ulong BindSectionSize { get; set; }
    }
}
