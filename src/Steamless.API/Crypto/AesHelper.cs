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

namespace Steamless.API.Crypto
{
    using System;
    using System.Security.Cryptography;

    public class AesHelper : IDisposable
    {
        private readonly byte[] m_OriginalKey;
        private readonly byte[] m_OriginalIv;
        private byte[] m_WorkingIv;
        private Aes m_AesCryptoProvider;

        // codeql[cs/ecb-encryption] ECB mode required for SteamStub variant compatibility
        public AesHelper(byte[] key, byte[] iv, CipherMode mode = CipherMode.ECB, PaddingMode padding = PaddingMode.None)
        {
            this.m_OriginalKey = key;
            this.m_OriginalIv = iv;
            this.m_WorkingIv = iv;

            this.m_AesCryptoProvider = Aes.Create();
            this.m_AesCryptoProvider.Key = key;
            this.m_AesCryptoProvider.IV = iv;
            this.m_AesCryptoProvider.Mode = mode;
            this.m_AesCryptoProvider.Padding = padding;
        }

        ~AesHelper()
        {
            this.Dispose(false);
        }

        private byte[] WorkingIv => this.m_WorkingIv ?? this.m_OriginalIv;

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            this.m_AesCryptoProvider?.Dispose();
            this.m_AesCryptoProvider = null;
        }
        /// <summary>
        /// Derives the working IV from the stub's IV. The input array stays untouched, so a second
        /// call starts from the same place.
        /// </summary>
        public bool RebuildIv(byte[] iv = null)
        {
            var source = iv ?? this.m_OriginalIv;

            var blockSize = this.m_AesCryptoProvider.BlockSize / 8;
            if (source == null || source.Length != blockSize)
                return false;

            try
            {
                // Transform into a separate buffer so input and output never alias.
                var transformed = new byte[source.Length];
                using (var decryptor = this.m_AesCryptoProvider.CreateDecryptor())
                {
                    if (decryptor.TransformBlock(source, 0, source.Length, transformed, 0) <= 0)
                        return false;
                }

                this.m_WorkingIv = transformed;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Decrypts prefix followed by source as one stream, writing up to destinationLength
        /// bytes to destination. The 3.x stubs keep the first ciphertext block in the header, and
        /// joining the two into one array would cost a full copy of the code section. The source
        /// is a window onto the file, so the caller never hands over the image's bytes.
        /// </summary>
        public bool Decrypt(byte[] prefix, ReadOnlyMemory<byte> source, int sourceLength, byte[] destination, int destinationLength, CipherMode mode, PaddingMode padding)
        {
            const int ChunkSize = 0x10000;

            ICryptoTransform decryptor = null;

            try
            {
                var blockSize = this.m_AesCryptoProvider.BlockSize / 8;
                var prefixLength = prefix?.Length ?? 0;

                if (destination == null || destinationLength <= 0)
                    return false;
                if (sourceLength <= 0 || sourceLength > source.Length || prefixLength % blockSize != 0 || sourceLength % blockSize != 0)
                    return false;

                this.m_AesCryptoProvider.Mode = mode;
                this.m_AesCryptoProvider.Padding = padding;

                decryptor = this.m_AesCryptoProvider.CreateDecryptor(this.m_OriginalKey, this.WorkingIv);

                var scratch = new byte[Math.Max(ChunkSize, prefixLength)];
                var input = new byte[ChunkSize];
                var written = 0;

                if (prefixLength > 0)
                {
                    var produced = decryptor.TransformBlock(prefix, 0, prefixLength, scratch, 0);
                    written += CopyResult(scratch, produced, destination, written, destinationLength);
                }

                for (var offset = 0; offset < sourceLength && written < destinationLength; offset += ChunkSize)
                {
                    var count = Math.Min(ChunkSize, sourceLength - offset);
                    source.Slice(offset, count).CopyTo(input);

                    var produced = decryptor.TransformBlock(input, 0, count, scratch, 0);
                    written += CopyResult(scratch, produced, destination, written, destinationLength);
                }

                return written == destinationLength;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                decryptor?.Dispose();
            }
        }

        private static int CopyResult(byte[] source, int produced, byte[] destination, int written, int destinationLength)
        {
            var copy = Math.Min(produced, destinationLength - written);
            if (copy <= 0)
                return 0;

            Buffer.BlockCopy(source, 0, destination, written, copy);
            return copy;
        }
    }
}
