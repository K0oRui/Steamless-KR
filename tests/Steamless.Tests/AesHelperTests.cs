namespace Steamless.Tests
{
    using System;
    using System.Security.Cryptography;
    using Steamless.API.Crypto;

    /// <summary>
    /// The code-section cipher. The stubs keep the first ciphertext block in their header, encrypt
    /// the rest in place, and expect a fixed length back, so the helper joins the stolen block in
    /// front of the stream, transforms it in chunks and stops at the destination. The check
    /// encrypts a known plaintext the way the stub does and asks for it back.
    /// </summary>
    internal static class AesHelperTests
    {
        public static void DecryptRestoresTheStolenBlockAndChunkedInput()
        {
            var key = AesFixture.Key;
            var iv = AesFixture.Iv;

            // Longer than the helper's 0x10000 read, so the transform has to resume on a boundary
            // instead of rewriting from the start.
            var plain = new byte[0x10000 + 32];
            for (var x = 0; x < plain.Length; x++)
                plain[x] = (byte)((x * 31) + 7);

            var cipher = AesFixture.Encrypt(plain, key, iv);
            var prefix = cipher[..AesFixture.BlockSize];
            var source = cipher[AesFixture.BlockSize..];

            using var helper = new AesHelper(key, iv);
            Assert.True(helper.RebuildIv(iv), "the IV did not rebuild");

            var destination = new byte[plain.Length];
            Assert.True(
                helper.Decrypt(prefix, source, source.Length, destination, destination.Length, CipherMode.CBC, PaddingMode.None),
                "the decrypt failed");

            Assert.SameBytes(plain, destination, "decrypted code section");
        }

        public static void DecryptRejectsAMisalignedSource()
        {
            var key = AesFixture.Key;
            var iv = AesFixture.Iv;

            using var helper = new AesHelper(key, iv);
            Assert.True(helper.RebuildIv(iv), "the IV did not rebuild");

            var destination = new byte[64];

            // CBC decrypts in whole blocks; a partial block would silently shift every later byte.
            Assert.False(
                helper.Decrypt(new byte[AesFixture.BlockSize], new byte[AesFixture.BlockSize + 1], AesFixture.BlockSize + 1, destination, destination.Length, CipherMode.CBC, PaddingMode.None),
                "a misaligned source");
        }
    }
}
