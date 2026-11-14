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
    using System.Collections.Generic;

    /// <summary>
    /// The 2.1 stub keeps its SteamDRMP.dll pointer, sizes and offset table inside the decrypted
    /// payload rather than in the stub header. Its steps read from the payload, then reuse the 3.x
    /// decrypt and code-section code.
    /// </summary>
    public static class Stub21Flow
    {
        private const int AesIvSize = 16;

        /// <summary>
        /// Reads the SteamDRMP.dll pointer and size out of the payload, then decrypts the blob.
        /// Both values are attacker controlled, so they are range checked against the payload
        /// before use.
        /// </summary>
        public static bool TryDecryptDrmp(
            PeImage file,
            byte[] payload,
            uint drmpOffsetField,
            uint drmpSizeField,
            uint xteaKeysField,
            SteamlessOptions options,
            LoggingService logService,
            out byte[] drmp)
        {
            drmp = null;

            try
            {
                // These are offsets into the payload, not file offsets. They are attacker controlled.
                if (!PeHelpers.IsValidRange(payload, drmpOffsetField, 4) ||
                    !PeHelpers.IsValidRange(payload, drmpSizeField, 4) ||
                    xteaKeysField > payload.Length)
                {
                    logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> SteamDRMP offsets point outside the payload.", LogMessageType.Error));
                    return false;
                }

                var drmpVa = BitConverter.ToUInt32(payload, (int)drmpOffsetField);
                var drmpSize = BitConverter.ToUInt32(payload, (int)drmpSizeField);

                // XTEA consumes 8 bytes per block; drop any partial block so the decrypt stays in bounds.
                drmpSize &= 0xFFFFFFF8;

                var drmpOffset = file.FileOffsetFromRva(file.RvaFromVa(drmpVa));
                if (drmpSize == 0 || !file.TryRead(drmpOffset, drmpSize, out _))
                {
                    logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> SteamDRMP.dll size is outside the file.", LogMessageType.Error));
                    return false;
                }

                logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> SteamDRMP.dll found.", LogMessageType.Debug));

                var keyCount = (payload.Length - (int)xteaKeysField) / 4;
                var xteaKeys = new uint[keyCount];
                for (var x = 0; x < keyCount; x++)
                    xteaKeys[x] = BitConverter.ToUInt32(payload, (int)xteaKeysField + (x * 4));

                return Stub3xFlow.TryDecryptDrmp(file, drmpOffset, drmpSize, xteaKeys, options, logService, out drmp);
            }
            catch (Exception)
            {
                logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> Could not decrypt the SteamDRMP.dll data.", LogMessageType.Error));
                return false;
            }
        }

        /// <summary>
        /// Removes `.bind`, picks the code section the offsets point at, and decrypts it. A stored
        /// section address of zero means the stub does not know it, and the original entry point
        /// always sits inside the code section, so that becomes the fallback.
        /// </summary>
        public static bool TryPrepareCodeSection(
            PeImage file,
            byte[] payload,
            IReadOnlyList<int> offsets,
            BindSection bind,
            SteamlessOptions options,
            LoggingService logService)
        {
            if (!bind.Remove(file, options, logService))
                return false;

            PeSection codeSection;

            if (offsets[3] != 0)
            {
                var sectionRva = file.RvaFromVa(BitConverter.ToUInt32(payload, offsets[3]));
                codeSection = file.FindSectionAtRva(sectionRva);
                if (codeSection == null || !codeSection.IsValid)
                    return false;
            }
            else
            {
                var originalEntry = BitConverter.ToUInt32(payload, offsets[2]);
                codeSection = file.FindSectionAtRva(file.RvaFromVa(originalEntry));
                if (codeSection == null || !codeSection.IsValid)
                    return false;

                logService.OnAddLogMessage(null, new LogMessageEventArgs($" --> Code section VA offset was 0. Used OEP 0x{originalEntry:X8} instead.", LogMessageType.Debug));
            }

            logService.OnAddLogMessage(null, new LogMessageEventArgs($" --> {codeSection.Name} is the main code section.", LogMessageType.Debug));

            var flags = BitConverter.ToUInt32(payload, offsets[0]);
            var sectionData = file.ReadSection(codeSection);

            if (!StubDrmFlags.EncryptsCodeSectionV21(flags))
            {
                logService.OnAddLogMessage(null, new LogMessageEventArgs($" --> {codeSection.Name} section is not encrypted.", LogMessageType.Debug));
                return true;
            }

            logService.OnAddLogMessage(null, new LogMessageEventArgs($" --> {codeSection.Name} section is encrypted.", LogMessageType.Debug));

            var aesKey = new byte[32];
            Buffer.BlockCopy(payload, offsets[5], aesKey, 0, 32);
            var aesIv = new byte[AesIvSize];
            Buffer.BlockCopy(payload, offsets[6], aesIv, 0, AesIvSize);
            var stolenBlock = new byte[AesIvSize];
            Buffer.BlockCopy(payload, offsets[7], stolenBlock, 0, AesIvSize);
            var encryptedSize = BitConverter.ToUInt32(payload, offsets[4]);

            var sectionOffset = file.FileOffsetFromRva(codeSection.VirtualAddress);

            // 2.1 counts only the bytes it left in the section; the stolen block completes the
            // plaintext, so the output is one block longer than what the file holds.
            var outputSize = encryptedSize + (ulong)stolenBlock.Length;
            if (!CodeSectionDecryptor.TryDecrypt(file, stolenBlock, sectionOffset, encryptedSize, outputSize, sectionData, aesKey, aesIv, logService))
                return false;

            file.ReplaceSection(codeSection, sectionData);
            return true;
        }
    }
}
