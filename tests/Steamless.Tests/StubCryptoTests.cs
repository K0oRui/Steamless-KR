namespace Steamless.Tests
{
    using System;
    using Steamless.Unpacker.Common;

    /// <summary>
    /// The rolling XOR both the payload and the 2.0 code section are stored with. The key for each
    /// word is the ciphertext word before it, so the tests build the ciphertext by hand.
    /// </summary>
    internal static class StubCryptoTests
    {
        public static void XorDecryptsWithAGivenKey()
        {
            const uint Key = 0xAAAAAAAA;

            var plain = new[] { 0x11111111u, 0x22222222u, 0x33333333u };
            var cipher = Encrypt(plain, Key);

            var data = ToBytes(cipher);
            var lastKey = StubCrypto.Xor(ref data, (uint)data.Length, Key);

            Assert.SameBytes(ToBytes(plain), data, "decrypted words");
            Assert.Equal(cipher[2], lastKey, "continuation key");
        }

        public static void XorUsesTheFirstWordWhenNoKeyIsGiven()
        {
            const uint Key = 0x12345678;
            const uint Second = 0x22222222;
            const uint Third = 0x33333333;

            // The first word of such a buffer is the key itself and stays in place; each later word
            // is the plaintext word XOR the ciphertext word before it.
            var second = Second ^ Key;
            var data = ToBytes(new[] { Key, second, Third ^ second });

            StubCrypto.Xor(ref data, (uint)data.Length, 0);

            Assert.Equal(Key, BitConverter.ToUInt32(data, 0), "first word");
            Assert.Equal(Second, BitConverter.ToUInt32(data, 4), "second word");
            Assert.Equal(Third, BitConverter.ToUInt32(data, 8), "third word");
        }

        /// <summary>The stub's own chaining, written the way the stub does it.</summary>
        private static uint[] Encrypt(uint[] plain, uint key)
        {
            var cipher = new uint[plain.Length];

            for (var x = 0; x < plain.Length; x++)
            {
                cipher[x] = plain[x] ^ key;
                key = cipher[x];
            }

            return cipher;
        }

        private static byte[] ToBytes(uint[] words)
        {
            var data = new byte[words.Length * 4];

            for (var x = 0; x < words.Length; x++)
                Array.Copy(BitConverter.GetBytes(words[x]), 0, data, x * 4, 4);

            return data;
        }
    }
}
