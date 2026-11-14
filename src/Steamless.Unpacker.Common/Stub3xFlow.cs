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

namespace Steamless.Unpacker.Common
{
    using API.Events;
    using API.Model;
    using API.PE;
    using API.Services;
    using System;
    using System.IO;

    /// <summary>
    /// How a stub declares its payload size. The 3.x headers round the size up to the AES block
    /// size; the 2.1 header declares the exact byte count and expects it word aligned.
    /// </summary>
    public enum PayloadSizing
    {
        Block,
        Declared,
    }

    /// <summary>
    /// The payload, SteamDRMP.dll and code-section steps shared by the 3.0 and 3.1 stubs. Their
    /// headers differ, but everything a header points at has the same layout.
    /// </summary>
    public static class Stub3xFlow
    {
        /// <summary>
        /// Decrypts the stub payload and returns the plaintext. Callers that only needed the range
        /// checked still get the bytes, which is what lets 2.1 read its offsets out of the payload
        /// without decrypting it twice.
        /// </summary>
        public static bool TryDecryptPayload(
            PeImage file,
            ulong payloadRva,
            uint payloadSizeValue,
            uint xorKey,
            PayloadSizing sizing,
            SteamlessOptions options,
            LoggingService logService,
            out byte[] payload)
        {
            payload = new byte[0];

            var payloadSize = sizing == PayloadSizing.Block
                ? (payloadSizeValue + 0x0F) & 0xFFFFFFF0
                : payloadSizeValue;

            if (payloadSize == 0)
                return true;

            // The rolling XOR decodes 4-byte words; a partial trailing word cannot be decoded.
            if ((payloadSize & 3) != 0)
            {
                logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> Payload size is not a multiple of 4, so it cannot be decoded.", LogMessageType.Error));
                return false;
            }

            var payloadOffset = file.FileOffsetFromRva(payloadRva);

            // The payload size is attacker controlled. Reject anything outside the file before
            // copying, instead of letting the copy or the allocator throw.
            if (!file.TryRead(payloadOffset, payloadSize, out var window))
                return false;

            logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> Payload found.", LogMessageType.Debug));

            var data = window.ToArray();
            StubCrypto.Xor(ref data, payloadSize, xorKey);

            if (options.DumpPayloadToDisk)
                StubDump.TryWrite(file.FilePath + ".payload", data, "payload", logService);

            payload = data;
            return true;
        }

        /// <summary>
        /// Decrypts and optionally dumps the SteamDRMP.dll blob at a known file offset. Callers
        /// find the blob their own way and keep their own found-logging; they share the decrypt and
        /// the dump.
        /// </summary>
        public static bool TryDecryptDrmp(
            PeImage file,
            ulong drmpOffset,
            uint drmpSizeDeclared,
            uint[] encryptionKeys,
            SteamlessOptions options,
            LoggingService logService,
            out byte[] drmp)
        {
            drmp = null;

            try
            {
                if (drmpOffset >= (ulong)file.Length)
                {
                    logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> SteamDRMP.dll offset is outside the file.", LogMessageType.Error));
                    return false;
                }

                // The declared size can exceed the file and is not always a multiple of 8, which is
                // what the XTEA pass reads. Clamp and align it before copying.
                var size = Math.Min((long)drmpSizeDeclared, (long)file.Length - (long)drmpOffset);
                size = (size / 8) * 8;

                if (!file.TryRead(drmpOffset, size, out var window))
                {
                    logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> SteamDRMP.dll offset is outside the file.", LogMessageType.Error));
                    return false;
                }

                var data = window.ToArray();

                StubCrypto.DecryptDrmp(ref data, (uint)size, encryptionKeys);

                if (options.DumpSteamDrmpToDisk)
                {
                    var basePath = Path.GetDirectoryName(file.FilePath) ?? string.Empty;
                    StubDump.TryWrite(Path.Combine(basePath, "SteamDRMP.dll"), data, "SteamDRMP.dll", logService);
                }

                drmp = data;
                return true;
            }
            catch (Exception)
            {
                logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> Could not decrypt the SteamDRMP.dll data.", LogMessageType.Error));
                return false;
            }
        }

        /// <summary>
        /// Removes `.bind` and picks the encrypted code section out of what is left. Returns true
        /// with a null section when the caller says the section is not encrypted.
        /// </summary>
        public static bool TryPrepareCodeSection(
            PeImage file,
            BindSection bindSection,
            SteamlessOptions options,
            bool encrypted,
            ulong codeSectionRva,
            LoggingService logService,
            out PeSection codeSection)
        {
            codeSection = null;

            if (!bindSection.Remove(file, options, logService))
                return false;

            if (!encrypted)
                return true;

            codeSection = file.FindSectionAtRva(codeSectionRva);
            return codeSection != null && codeSection.IsValid;
        }

        public static bool TryDecryptCodeSection(
            PeImage file,
            PeSection codeSection,
            bool encrypted,
            ulong encryptedSize,
            byte[] aesKey,
            byte[] aesIv,
            byte[] stolenBlock,
            LoggingService logService)
        {
            if (!encrypted)
            {
                logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> Code section is not encrypted.", LogMessageType.Debug));
                return true;
            }

            logService.OnAddLogMessage(null, new LogMessageEventArgs($" --> {codeSection.Name} is the main code section.", LogMessageType.Debug));
            logService.OnAddLogMessage(null, new LogMessageEventArgs($" --> {codeSection.Name} section is encrypted.", LogMessageType.Debug));

            var sectionData = file.ReadSection(codeSection);

            if (codeSection.SizeOfRawData == 0)
            {
                logService.OnAddLogMessage(null, new LogMessageEventArgs($" --> {codeSection.Name} section is empty, so there is nothing to decrypt.", LogMessageType.Debug));
                return true;
            }

            var sectionOffset = file.FileOffsetFromRva(codeSection.VirtualAddress);

            // 3.x keeps every encrypted byte in the section and wants the same count back.
            if (!CodeSectionDecryptor.TryDecrypt(file, stolenBlock, sectionOffset, encryptedSize, encryptedSize, sectionData, aesKey, aesIv, logService))
                return false;

            file.ReplaceSection(codeSection, sectionData);
            return true;
        }
    }
}
