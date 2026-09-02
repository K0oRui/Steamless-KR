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
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;

    public class AesHelper : IDisposable
    {
        private readonly byte[] m_OriginalKey;
        private readonly byte[] m_OriginalIv;
        private Aes m_AesCryptoProvider;

        // codeql[cs/ecb-encryption] ECB mode required for SteamStub variant compatibility
        public AesHelper(byte[] key, byte[] iv, CipherMode mode = CipherMode.ECB, PaddingMode padding = PaddingMode.None)
        {
            this.m_OriginalKey = key;
            this.m_OriginalIv = iv;

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

        public bool RebuildIv(byte[] iv = null)
        {
            if (iv == null)
                iv = this.m_OriginalIv;

            try
            {
                using (var decryptor = this.m_AesCryptoProvider.CreateDecryptor())
                {
                    return decryptor.TransformBlock(iv, 0, iv.Length, this.m_OriginalIv, 0) > 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public byte[] Decrypt(byte[] data, CipherMode mode, PaddingMode padding)
        {
            ICryptoTransform decryptor = null;
            MemoryStream mStream = null;
            CryptoStream cStream = null;

            try
            {
                this.m_AesCryptoProvider.Mode = mode;
                this.m_AesCryptoProvider.Padding = padding;

                decryptor = this.m_AesCryptoProvider.CreateDecryptor(this.m_OriginalKey, this.m_OriginalIv);

                mStream = new MemoryStream(data);

                cStream = new CryptoStream(mStream, decryptor, CryptoStreamMode.Read);

                var totalBuffer = new List<byte>();
                var buffer = new byte[16];
                int read;
                // Read can return fewer than 16 bytes; append only the bytes actually read.
                while ((read = cStream.Read(buffer, 0, 16)) > 0)
                    totalBuffer.AddRange(buffer.AsSpan(0, read));

                return totalBuffer.ToArray();
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                cStream?.Dispose();
                mStream?.Dispose();
                decryptor?.Dispose();
            }
        }
    }
}
