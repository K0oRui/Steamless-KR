namespace Steamless.Tests
{
    using System;
    using System.Security.Cryptography;

    /// <summary>
    /// Encrypts a buffer the way a SteamStub stub does, so a check can ask a decryptor for the
    /// plaintext back. The working IV is the stub IV run through one ECB block, then the plaintext
    /// is encrypted in CBC with it.
    /// </summary>
    internal static class AesFixture
    {
        public const int BlockSize = 16;

        public static byte[] Key => Block(0x20, 32);

        public static byte[] Iv => Block(0x40, BlockSize);

        public static byte[] Encrypt(byte[] plain, byte[] key, byte[] iv)
        {
            var workingIv = new byte[BlockSize];

            using (var ecb = Aes.Create())
            {
                ecb.Key = key;
                ecb.Mode = CipherMode.ECB;
                ecb.Padding = PaddingMode.None;

                using var decryptor = ecb.CreateDecryptor(key, iv);
                decryptor.TransformBlock(iv, 0, iv.Length, workingIv, 0);
            }

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = workingIv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;

            using var encryptor = aes.CreateEncryptor();
            return encryptor.TransformFinalBlock(plain, 0, plain.Length);
        }

        /// <summary>A buffer of one repeated byte, for keys, IVs and plaintext.</summary>
        public static byte[] Block(byte value, int length)
        {
            var data = new byte[length];
            for (var x = 0; x < length; x++)
                data[x] = value;

            return data;
        }
    }
}
