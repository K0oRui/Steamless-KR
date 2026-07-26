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

namespace Steamless.Unpacker.Variant21.x86
{
    using API;
    using API.Crypto;
    using API.Events;
    using API.Extensions;
    using API.Model;
    using API.PE32;
    using API.Services;
    using Classes;
    using SharpDisasm;
    using SharpDisasm.Udis86;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;

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
        public override string Name => "SteamStub Variant 2.1 Unpacker (x86)";

        /// <summary>
        /// Gets the description of this plugin.
        /// </summary>
        public override string Description => "Unpacker for the 32bit SteamStub variant 2.1.";

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

                // Attempt to locate the known v2.x signature..
                return Pe32Helpers.FindPattern(bind, "53 51 52 56 57 55 8B EC 81 EC 00 10 00 00 C7") != -1;
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
            this.Log("File is packed with SteamStub Variant 2.1!", LogMessageType.Information);

            this.Log("Step 1 - Read, disassemble and decode the SteamStub DRM header.", LogMessageType.Information);
            if (!this.Step1())
                return false;

            this.Log("Step 2 - Read, decode and process the payload data.", LogMessageType.Information);
            if (!this.Step2())
                return false;

            this.Log("Step 3 - Read, decode and dump the SteamDRMP.dll file.", LogMessageType.Information);
            if (!this.Step3())
                return false;

            this.Log("Step 4 - Scan, dump and pull needed offsets from within the SteamDRMP.dll file.", LogMessageType.Information);
            if (!this.Step4())
                return false;

            this.Log("Step 5 - Read, decrypt and process the main code section.", LogMessageType.Information);
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

        /// <summary>
        /// Step #1
        /// 
        /// Read, disassemble and decode the SteamStub DRM header.
        /// </summary>
        /// <returns></returns>
        private bool Step1()
        {
            /**
             * Note: This version of the stub has a variable length header due to how it builds the 
             * header information. When the stub is generated, the header has additional string data
             * that can be dynamically built based on the various options of the protection being used
             * and other needed API imports. Inside of the stub header, this field is 'StubData'.
             */

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
            this.XorKey = SteamStubHelpers.SteamXor(ref headerData, (uint)headerData.Length, structXorKey);

            // Determine how to handle the header based on the size..
            if ((structSize / 4) == 0xD0)
            {
                this.StubHeader = Pe32Helpers.GetStructure<SteamStub32Var21Header_D0Variant>(headerData);
                this.StubData = headerData.Skip(Marshal.SizeOf(typeof(SteamStub32Var21Header_D0Variant))).ToArray();
            }
            else
            {
                this.StubHeader = Pe32Helpers.GetStructure<SteamStub32Var21Header>(headerData);
                this.StubData = headerData.Skip(Marshal.SizeOf(typeof(SteamStub32Var21Header))).ToArray();
            }

            return true;
        }

        /// <summary>
        /// Step #2
        /// 
        /// Read, decode and process the payload data.
        /// </summary>
        /// <returns></returns>
        private bool Step2()
        {
            // Obtain the payload address and size..
            var payloadAddr = this.File.GetFileOffsetFromRva(this.File.GetRvaFromVa(this.StubHeader.PayloadDataVirtualAddress));
            var payloadData = new byte[this.StubHeader.PayloadDataSize];
            Array.Copy(this.File.FileData, payloadAddr, payloadData, 0, this.StubHeader.PayloadDataSize);

            // Decode the payload data..
            this.XorKey = SteamStubHelpers.SteamXor(ref payloadData, this.StubHeader.PayloadDataSize, this.XorKey);
            this.PayloadData = payloadData;

            try
            {
                if (this.Options.DumpPayloadToDisk)
                {
                    System.IO.File.WriteAllBytes(this.File.FilePath + ".payload", payloadData);
                    this.Log(" --> Saved payload to disk!", LogMessageType.Debug);
                }
            }
            catch
            {
                // Do nothing here since it doesn't matter if this fails..
            }

            return true;
        }

        /// <summary>
        /// Step #3
        /// 
        /// Read, decode and dump the SteamDRMP.dll file.
        /// </summary>
        /// <returns></returns>
        private bool Step3()
        {
            this.Log(" --> File has SteamDRMP.dll file!", LogMessageType.Debug);

            try
            {
                // Obtain the SteamDRMP.dll file address and data..
                var drmpAddr = this.File.GetFileOffsetFromRva(this.File.GetRvaFromVa(BitConverter.ToUInt32(this.PayloadData, (int)this.StubHeader.SteamDRMPDllVirtualAddress)));
                var drmpSize = BitConverter.ToUInt32(this.PayloadData, (int)this.StubHeader.SteamDRMPDllSize);
                var drmpData = new byte[drmpSize];
                Array.Copy(this.File.FileData, drmpAddr, drmpData, 0, drmpSize);

                // Obtain the XTea encryption keys..
                var xteyKeys = new uint[(this.PayloadData.Length - this.StubHeader.XTeaKeys) / 4];
                for (var x = 0; x < (this.PayloadData.Length - this.StubHeader.XTeaKeys) / 4; x++)
                    xteyKeys[x] = BitConverter.ToUInt32(this.PayloadData, (int)this.StubHeader.XTeaKeys + (x * 4));

                // Decrypt the file data..
                SteamStubHelpers.SteamDrmpDecryptPass1(ref drmpData, drmpSize, xteyKeys);
                this.SteamDrmpData = drmpData;

                try
                {
                    if (this.Options.DumpSteamDrmpToDisk)
                    {
                        var basePath = Path.GetDirectoryName(this.File.FilePath) ?? string.Empty;
                        System.IO.File.WriteAllBytes(Path.Combine(basePath, "SteamDRMP.dll"), drmpData);
                        this.Log(" --> Saved SteamDRMP.dll to disk!", LogMessageType.Debug);
                    }
                }
                catch
                {
                    // Do nothing here since it doesn't matter if this fails..
                }

                return true;
            }
            catch
            {
                this.Log(" --> Error trying to decrypt the files SteamDRMP.dll data!", LogMessageType.Error);
                return false;
            }
        }

        /// <summary>
        /// Step #4
        /// 
        /// Scan, dump and pull needed offsets from within the SteamDRMP.dll file.
        /// </summary>
        /// <returns></returns>
        private List<int> TryGetSteamDrmpOffsets(byte[] data, bool fallback)
        {
            // Try dynamic method first if experimental features are enabled..
            if (this.Options.UseExperimentalFeatures)
                return this.GetSteamDrmpOffsetsDynamic(data);

            // Use the hardcoded offset method..
            var useFallback = fallback;
            this.UseFallbackDrmpOffsets = useFallback;
            return this.GetSteamDrmpOffsets(data);
        }

        private bool ValidateSteamDrmpOffsets(List<int> offsets)
        {
            if (offsets.Count != 8)
                return false;

            // Always validate the flags offset since it is used for encryption detection..
            if (offsets[0] < 0 || offsets[0] + 4 > this.PayloadData.Length)
                return false;

            // Check if the file uses encryption by reading the flags..
            var flags = BitConverter.ToUInt32(this.PayloadData.Skip(offsets[0]).Take(4).ToArray(), 0);
            if ((flags & (uint)DrmFlags.NoEncryption) != (uint)DrmFlags.NoEncryption)
            {
                // File is encrypted — validate encryption-related offsets..
                if (offsets[4] < 0 || offsets[4] + 4 > this.PayloadData.Length)
                    return false;
                if (offsets[5] < 0 || offsets[5] + 32 > this.PayloadData.Length)
                    return false;
                if (offsets[6] < 0 || offsets[6] + 16 > this.PayloadData.Length)
                    return false;
                if (offsets[7] < 0 || offsets[7] + 16 > this.PayloadData.Length)
                    return false;
            }

            return true;
        }

        private bool Step4()
        {
            // Define patterns to try, with corresponding fallback flag..
            var patterns = new List<(string Pattern, bool IsFallback)>
                {
                    ("8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8D ?? ?? ?? ?? ?? 05", false),
                    ("8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B", false),
                    ("8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? A3 ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? A3 ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? A3 ?? ?? ?? ?? 8B", true)
                };

            foreach (var (pattern, isFallback) in patterns)
            {
                var drmpOffset = Pe32Helpers.FindPattern(this.SteamDrmpData, pattern);
                if (drmpOffset == -1)
                    continue;

                // Try with the known hardcoded offsets first..
                foreach (var useFallback in new[] { false, true })
                {
                    var drmpOffsetData = new byte[1024];
                    Array.Copy(this.SteamDrmpData, drmpOffset, drmpOffsetData, 0, 1024);

                    this.UseFallbackDrmpOffsets = useFallback;
                    var drmpOffsets = this.GetSteamDrmpOffsets(drmpOffsetData);
                    if (drmpOffsets.Count != 8)
                        continue;

                    if (this.ValidateSteamDrmpOffsets(drmpOffsets))
                    {
                        this.SteamDrmpOffsets = drmpOffsets;
                        return true;
                    }
                }

                // Hardcoded offsets failed — try the dynamic disassembler method on this data block..
                var drmpOffsetData2 = new byte[1024];
                Array.Copy(this.SteamDrmpData, drmpOffset, drmpOffsetData2, 0, 1024);
                var dynOffsets = this.GetSteamDrmpOffsetsDynamic(drmpOffsetData2);
                if (dynOffsets.Count == 8 && this.ValidateSteamDrmpOffsets(dynOffsets))
                {
                    this.Log($" --> Using dynamic offset extraction.", LogMessageType.Debug);
                    this.SteamDrmpOffsets = dynOffsets;
                    return true;
                }

                // Hardcoded and dynamic both failed for this pattern — try scanning for the correct layout..
                this.Log($" --> Scanning for correct offset layout in DRMP data block...", LogMessageType.Debug);
                var foundOffsets = this.ScanSteamDrmpOffsets(this.SteamDrmpData, drmpOffset);
                if (foundOffsets != null)
                {
                    this.Log($" --> Found valid offsets via scan.", LogMessageType.Debug);
                    this.SteamDrmpOffsets = foundOffsets;
                    return true;
                }

                this.Log($" --> Pattern matched but could not find valid offsets, trying next pattern.", LogMessageType.Debug);
            }

            return false;
        }

        private List<int> ScanSteamDrmpOffsets(byte[] steamDrmpData, long scanOffset)
        {
            var data = new byte[1024];
            Array.Copy(steamDrmpData, scanOffset, data, 0, 1024);

            var payloadLimit = this.PayloadData.Length;

            for (int start = 0; start < data.Length - 28; start += 2)
            {
                var vals = new List<int>();
                for (int j = 0; j < 6; j++)
                    vals.Add(BitConverter.ToInt32(data, start + j * 4));

                // vals[6] is the IV offset (used to compute vals[7])
                var ivOffset = BitConverter.ToInt32(data, start + 6 * 4);
                vals.Add(ivOffset);
                vals.Add(ivOffset + 16);

                if (vals[0] < 0 || vals[0] + 4 > payloadLimit)
                    continue;

                var flags = BitConverter.ToUInt32(this.PayloadData, vals[0]);
                var noEnc = (flags & (uint)DrmFlags.NoEncryption) == (uint)DrmFlags.NoEncryption;

                if (vals[3] < 0 || vals[3] + 4 > payloadLimit)
                    continue;

                if (!noEnc)
                {
                    if (vals[4] < 0 || vals[4] + 4 > payloadLimit) continue;
                    if (vals[5] < 0 || vals[5] + 32 > payloadLimit) continue;
                    if (vals[6] < 0 || vals[6] + 16 > payloadLimit) continue;
                    if (vals[7] < 0 || vals[7] + 16 > payloadLimit) continue;

                    if (vals[5] + 32 > vals[6]) continue;
                    if (vals[6] + 16 != vals[7]) continue;
                }

                this.Log($" --> Found valid offset layout at byte offset {start} in data block!", LogMessageType.Debug);
                return vals;
            }

            return null;
        }

        /// <summary>
        /// Step #5
        /// 
        /// Read, decrypt and process the main code section.
        /// </summary>
        /// <returns></returns>
        private bool Step5()
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

            byte[] codeSectionData;

            // Obtain the main code section (typically .text)..
            var mainSection = this.File.GetOwnerSection(this.File.GetRvaFromVa(BitConverter.ToUInt32(this.PayloadData.Skip(this.SteamDrmpOffsets[3]).Take(4).ToArray(), 0)));
            if (this.SteamDrmpOffsets[3] != 0)
            {
                if (mainSection.PointerToRawData == 0 || mainSection.SizeOfRawData == 0)
                    return false;
            }

            this.Log($" --> {mainSection.SectionName} linked as main code section.", LogMessageType.Debug);

            // Save the code section index for later use..
            this.CodeSectionIndex = this.File.GetSectionIndex(mainSection);

            uint encryptedSize = 0;

            // Determine if we are using encryption on the section..
            var flags = BitConverter.ToUInt32(this.PayloadData.Skip(this.SteamDrmpOffsets[0]).Take(4).ToArray(), 0);
            if ((flags & (uint)DrmFlags.NoEncryption) == (uint)DrmFlags.NoEncryption)
            {
                this.Log($" --> {mainSection.SectionName} section is not encrypted.", LogMessageType.Debug);

                // No encryption was used, just read the original data..
                codeSectionData = new byte[mainSection.SizeOfRawData];
                Array.Copy(this.File.FileData, this.File.GetFileOffsetFromRva(mainSection.VirtualAddress), codeSectionData, 0, mainSection.SizeOfRawData);
            }
            else
            {
                this.Log($" --> {mainSection.SectionName} section is encrypted.", LogMessageType.Debug);

                try
                {
                    // Encryption was used, obtain the encryption information..
                    var aesKey = this.PayloadData.Skip(this.SteamDrmpOffsets[5]).Take(32).ToArray();
                    var aesIv = this.PayloadData.Skip(this.SteamDrmpOffsets[6]).Take(16).ToArray();
                    var codeStolen = this.PayloadData.Skip(this.SteamDrmpOffsets[7]).Take(16).ToArray();
                    encryptedSize = BitConverter.ToUInt32(this.PayloadData.Skip(this.SteamDrmpOffsets[4]).Take(4).ToArray(), 0);

                    // Validate offsets before proceeding..
                    if (aesKey.Length != 32 || aesIv.Length != 16 || codeStolen.Length != 16)
                    {
                        this.Log($" --> Invalid encryption offsets (key={aesKey.Length}, iv={aesIv.Length}, stolen={codeStolen.Length}, payloadLen={this.PayloadData.Length}, useFallback={this.UseFallbackDrmpOffsets})", LogMessageType.Warning);
                        return false;
                    }

                    // Read the encrypted section data from the file..
                    var encryptedData = new byte[encryptedSize];
                    Array.Copy(this.File.FileData, this.File.GetFileOffsetFromRva(mainSection.VirtualAddress), encryptedData, 0, encryptedSize);

                    // Decrypt the code section data using AES-CBC..
                    var aes = new AesHelper(aesKey, aesIv);
                    aes.RebuildIv(aesIv);
                    var decryptedData = aes.Decrypt(encryptedData, CipherMode.CBC, PaddingMode.None);

                    // Prepend the stolen bytes to restore the full original section data..
                    codeSectionData = new byte[codeStolen.Length + decryptedData.Length];
                    Array.Copy(codeStolen, 0, codeSectionData, 0, codeStolen.Length);
                    Array.Copy(decryptedData, 0, codeSectionData, codeStolen.Length, decryptedData.Length);
                }
                catch
                {
                    this.Log(" --> Error trying to decrypt the files code section data!", LogMessageType.Error);
                    return false;
                }
            }

            if (this.CodeSectionIndex >= 0)
            {
                var sectionData = this.File.SectionData[this.CodeSectionIndex];
                var copySize = Math.Min(codeSectionData.Length, sectionData.Length);
                Array.Copy(codeSectionData, sectionData, copySize);
                this.CodeSectionData = sectionData;
            }
            else
                this.CodeSectionData = codeSectionData;

            return true;
        }

        /// <summary>
        /// Step #6
        /// 
        /// Rebuild and save the unpacked file.
        /// </summary>
        /// <returns></returns>
        /// <summary>
        /// Scans .rdata section data for the original import descriptor table.
        /// </summary>
        private uint FindImportDescriptorInRdata(byte[] rdataData, uint rdataRva, NativeApi32.ImageDataDirectory32 currentImport)
        {
            return FindImportByDllNamePattern(rdataData, rdataRva);
        }

        /// <summary>
        /// Scans .rdata for import descriptors by searching for DLL name RVA patterns.
        /// Finds the first IMAGE_IMPORT_DESCRIPTOR whose Name field points to a valid DLL name string.
        /// </summary>
        private uint FindImportByDllNamePattern(byte[] rdataData, uint rdataRva)
        {
            // Build a list of known DLL name strings by scanning .rdata..
            var dllStrings = new System.Collections.Generic.List<string>();
            for (int i = 0; i < rdataData.Length - 6; i++)
            {
                if (rdataData[i] >= 0x41 && rdataData[i] <= 0x7A)
                {
                    // Read until null terminator or non-printable
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

            // Scan .rdata for IMAGE_IMPORT_DESCRIPTOR entries..
            for (int offset = 0; offset < rdataData.Length - 20; offset += 4)
            {
                var nameRva = BitConverter.ToUInt32(rdataData, offset + 12);
                if (nameRva < rdataRva || nameRva >= rdataRva + rdataData.Length)
                    continue;

                // Read the DLL name at this RVA..
                var nameFileOff = nameRva - rdataRva;
                if (nameFileOff >= (uint)rdataData.Length)
                    continue;

                var dllName = System.Text.Encoding.ASCII.GetString(rdataData, (int)nameFileOff, Math.Min(64, rdataData.Length - (int)nameFileOff));
                var nullIdx = dllName.IndexOf('\0');
                if (nullIdx >= 0)
                    dllName = dllName.Substring(0, nullIdx);

                if (!dllName.EndsWith(".dll", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                // Check the OriginalFirstThunk and FirstThunk fields are in range..
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

        private bool Step6()
        {
            FileStream fStream = null;

            try
            {
                // Zero the DosStubData if desired..
                if (this.Options.ZeroDosStubData && this.File.DosStubSize > 0)
                    this.File.DosStubData = Enumerable.Repeat((byte)0, (int)this.File.DosStubSize).ToArray();

                // Rebuild the file sections..
                this.File.RebuildSections(this.Options.DontRealignSections == false);

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
                var originalEntry = BitConverter.ToUInt32(this.PayloadData.Skip(this.SteamDrmpOffsets[2]).Take(4).ToArray(), 0);
                ntHeaders.OptionalHeader.AddressOfEntryPoint = this.File.GetRvaFromVa(originalEntry);
                ntHeaders.OptionalHeader.CheckSum = 0;
                ntHeaders.OptionalHeader.SizeOfImage = this.File.GetAlignment(lastSection.VirtualAddress + lastSection.VirtualSize, this.File.NtHeaders.OptionalHeader.SectionAlignment);

                // Fix the import table entry if it points into the removed .bind section..
                if (!this.Options.KeepBindSection && this.BindSectionSize > 0)
                {
                    var importTable = ntHeaders.OptionalHeader.ImportTable;
                    if (importTable.VirtualAddress >= this.BindSectionRva && importTable.VirtualAddress < this.BindSectionRva + this.BindSectionSize)
                    {
                        // Scan .rdata for the original import descriptors..
                        var rdataSection = this.File.GetSection(".rdata");
                        if (rdataSection.IsValid)
                        {
                            var rdataData = this.File.GetSectionData(".rdata");
                            var rdataEnd = rdataSection.VirtualAddress + rdataSection.VirtualSize;
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
                        // The security entry uses a file offset (not RVA). Update it to the current overlay position.
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
        /// Step #7
        /// 
        /// Recalculate the file checksum.
        /// </summary>
        /// <returns></returns>
        private bool Step7()
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
            // Prepare our needed variables..
            Disassembler disasm = null;
            var dataPointer = IntPtr.Zero;
            uint structOffset = 0;
            uint structSize = 0;
            uint structXorKey = 0;

            // Determine the entry offset of the file..
            var entryOffset = this.File.GetFileOffsetFromRva(this.File.NtHeaders.OptionalHeader.AddressOfEntryPoint);

            try
            {
                // Copy the file data to memory for disassembling..
                dataPointer = Marshal.AllocHGlobal(this.File.FileData.Length);
                Marshal.Copy(this.File.FileData, 0, dataPointer, this.File.FileData.Length);

                // Create an offset pointer to our .bind function start..
                var startPointer = IntPtr.Add(dataPointer, (int)entryOffset);

                // Create the disassembler..
                Disassembler.Translator.IncludeAddress = true;
                Disassembler.Translator.IncludeBinary = true;

                disasm = new Disassembler(startPointer, 4096, ArchitectureMode.x86_32, entryOffset);

                // Disassemble our function..
                foreach (var inst in disasm.Disassemble().Where(inst => !inst.Error))
                {
                    // If all values are found, return successfully..
                    if (structOffset > 0 && structSize > 0 && structXorKey > 0)
                    {
                        offset = structOffset;
                        size = structSize;
                        xorKey = structXorKey;
                        return true;
                    }

                    // Looks for: mov dword ptr [value], immediate
                    if (inst.Mnemonic == ud_mnemonic_code.UD_Imov && inst.Operands[0].Type == ud_type.UD_OP_MEM && inst.Operands[1].Type == ud_type.UD_OP_IMM)
                    {
                        if (structOffset == 0)
                            structOffset = inst.Operands[1].LvalUDWord - this.File.NtHeaders.OptionalHeader.ImageBase;
                        else
                            structXorKey = inst.Operands[1].LvalUDWord;
                    }

                    // Looks for: mov reg, immediate
                    if (inst.Mnemonic == ud_mnemonic_code.UD_Imov && inst.Operands[0].Type == ud_type.UD_OP_REG && inst.Operands[1].Type == ud_type.UD_OP_IMM)
                        structSize = inst.Operands[1].LvalUDWord * 4;
                }

                offset = size = xorKey = 0;
                return false;
            }
            catch
            {
                offset = size = xorKey = 0;
                return false;
            }
            finally
            {
                disasm?.Dispose();
                if (dataPointer != IntPtr.Zero)
                    Marshal.FreeHGlobal(dataPointer);
            }
        }

        /// <summary>
        /// Obtains the needed DRM offsets from the SteamDRMP.dll file.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private List<int> GetSteamDrmpOffsets(byte[] data)
        {
            var offset0 = 2; // Flags
            var offset1 = 14; // Steam App Id
            var offset2 = this.UseFallbackDrmpOffsets ? 25 : 26; // OEP
            var offset3 = this.UseFallbackDrmpOffsets ? 36 : 38; // Code Section Virtual Address
            var offset4 = this.UseFallbackDrmpOffsets ? 47 : 50; // Code Section Virtual Size (Encrypted Size)
            var offset5 = this.UseFallbackDrmpOffsets ? 61 : 62; // Code Section AES Key
            var offset6 = this.UseFallbackDrmpOffsets ? 72 : 67; // Code Section AES Iv

            var offsets = new List<int>
                {
                    BitConverter.ToInt32(data, offset0), // ... 0 - Flags
                    BitConverter.ToInt32(data, offset1), // ... 1 - Steam App Id
                    BitConverter.ToInt32(data, offset2), // ... 2 - OEP
                    BitConverter.ToInt32(data, offset3), // ... 3 - Code Section Virtual Address
                    BitConverter.ToInt32(data, offset4), // ... 4 - Code Section Virtual Size (Encrypted Size)
                    BitConverter.ToInt32(data, offset5) // .... 5 - Code Section AES Key
                };

            var aesIvOffset = BitConverter.ToInt32(data, offset6);
            offsets.Add(aesIvOffset); // ................. 6 - Code Section AES Iv
            offsets.Add(aesIvOffset + 16); // ............ 7 - Code Section Stolen Bytes

            return offsets;
        }

        /// <summary>
        /// Obtains the needed DRM offsets from the SteamDRMP.dll file. (Dynamically via disassembling.)
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private List<int> GetSteamDrmpOffsetsDynamic(byte[] data)
        {
            Disassembler disasm = null;
            var offsets = new List<int>();
            var count = 0;

            /**
             * Assumed order of the offset values:
             * - Flags (mov)
             * - SteamAppId (mov)
             * - OEP (mov)
             * - Code Section VA (mov)
             * - Code Section Size (mov)
             * - Code Section AES Key (lea)
             * - Code Section AES IV (offset from above lea)
             * - Stolen Bytes (add)
             */

            try
            {
                var skipMov = false;

                // Disassemble the incoming block of data to look for the needed offsets dynamically..
                disasm = new Disassembler(data, ArchitectureMode.x86_32);
                foreach (var inst in disasm.Disassemble().Where(inst => !inst.Error))
                {
                    if (count >= 8)
                        break;

                    // ex: mov eax, [eax+1234]
                    if (!skipMov && inst.Mnemonic == ud_mnemonic_code.UD_Imov)
                    {
                        if (inst.Operands.Length >= 2
                            && inst.Operands[0].Type == ud_type.UD_OP_REG
                            && inst.Operands[1].Type == ud_type.UD_OP_MEM)
                        {
                            count++;
                            offsets.Add(inst.Operands[1].LvalSDWord);
                        }
                    }

                    // ex: lea eax, [eax+1234]
                    if (inst.Mnemonic == ud_mnemonic_code.UD_Ilea)
                    {
                        if (inst.Operands.Length >= 2
                            && inst.Operands[0].Type == ud_type.UD_OP_REG
                            && inst.Operands[1].Type == ud_type.UD_OP_MEM)
                        {
                            count += 2;
                            offsets.Add(inst.Operands[1].LvalSDWord);
                            offsets.Add(inst.Operands[1].LvalSDWord + 16);

                            /**
                             * Some v2 compiled files have the order of the last offset (add inst) after a mov which loads
                             * GetModuleHandleA's address into a register. In order to skip that from being read as an offset
                             * we need this small workaround..
                             */
                            skipMov = true;
                        }
                    }

                    // ex: add eax, 1234
                    if (inst.Mnemonic == ud_mnemonic_code.UD_Iadd)
                    {
                        if (inst.Operands.Length >= 2
                            && inst.Operands[0].Type == ud_type.UD_OP_REG
                            && inst.Operands[1].Type == ud_type.UD_OP_IMM)
                        {
                            count++;
                            offsets.Add(inst.Operands[1].LvalSDWord);
                        }
                    }
                }

                return offsets;
            }
            catch
            {
                return new List<int>();
            }
            finally
            {
                disasm?.Dispose();
            }
        }

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
        /// Gets or sets the dynamic field 'StubData' from the header.
        /// </summary>
        private byte[] StubData { get; set; }

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
