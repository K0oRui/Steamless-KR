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
    using API.Crypto;
    using API.Events;
    using API.PE;
    using API.Services;
    using System;
    using System.Security.Cryptography;

    /// <summary>
    /// The 3.x stubs encrypt the main code section with AES-CBC and move the first cipher block
    /// into the header. Decrypting puts that block back in front of the section, so the two parts
    /// are one stream.
    /// </summary>
    public static class CodeSectionDecryptor
    {
        /// <summary>
        /// Decrypts the section and writes <paramref name="outputSize"/> plaintext bytes into
        /// <paramref name="destination"/>. The sizes differ because the stubs count what they leave
        /// in the file: 3.x keeps every encrypted byte and wants the same count back, while 2.1
        /// keeps one block fewer and expects the stolen block to make up the difference.
        /// </summary>
        public static bool TryDecrypt(
            PeImage file,
            byte[] stolenBlock,
            ulong sourceOffset,
            ulong sourceSize,
            ulong outputSize,
            byte[] destination,
            byte[] aesKey,
            byte[] aesIv,
            LoggingService logService)
        {
            if (sourceSize == 0 || !file.TryRead(sourceOffset, (long)sourceSize, out var source))
            {
                logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> Code section size is outside the file.", LogMessageType.Error));
                return false;
            }

            if (destination == null || outputSize > (ulong)destination.Length)
            {
                logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> Encrypted code section is larger than the section data.", LogMessageType.Error));
                return false;
            }

            try
            {
                using (var aes = new AesHelper(aesKey, aesIv))
                {
                    if (!aes.RebuildIv(aesIv))
                    {
                        logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> Could not rebuild the code section AES IV.", LogMessageType.Error));
                        return false;
                    }

                    if (!aes.Decrypt(stolenBlock, source, (int)sourceSize,
                                     destination, (int)outputSize, CipherMode.CBC, PaddingMode.None))
                    {
                        logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> Could not decrypt the code section.", LogMessageType.Error));
                        return false;
                    }
                }

                return true;
            }
            catch (Exception)
            {
                logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> Could not decrypt the code section.", LogMessageType.Error));
                return false;
            }
        }
    }
}
