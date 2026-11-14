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
    using System;

    /// <summary>
    /// The two ciphers SteamStub uses: a rolling XOR over 32-bit words, and the XTEA variant that
    /// stores the SteamDRMP.dll blob.
    /// </summary>
    public static class StubCrypto
    {
        /// <summary>
        /// XOR decrypts with a rolling key: each word becomes the key for the next. A zero key
        /// takes the first data word as the starting key.
        /// </summary>
        public static uint Xor(ref byte[] data, uint size, uint key = 0)
        {
            uint offset = 0;

            if (key == 0)
            {
                offset = 4;
                key = BitConverter.ToUInt32(data, 0);
            }

            for (var x = offset; x < size; x += 4)
            {
                var value = BitConverter.ToUInt32(data, (int)x);
                Array.Copy(BitConverter.GetBytes(value ^ key), 0, data, x, 4);

                key = value;
            }

            return key;
        }

        /// <summary>
        /// The first pass over the SteamDRMP.dll blob: XTEA with the previous ciphertext word
        /// chained in.
        /// </summary>
        public static void DecryptDrmp(ref byte[] data, uint size, uint[] keys)
        {
            var v1 = 0x55555555u;
            var v2 = 0x55555555u;
            var result = new uint[2];

            for (var x = 0; x < size; x += 8)
            {
                var d1 = BitConverter.ToUInt32(data, x);
                var d2 = BitConverter.ToUInt32(data, x + 4);

                DecryptXteaBlock(ref result, keys, d1, d2);

                Array.Copy(BitConverter.GetBytes(result[0] ^ v1), 0, data, x, 4);
                Array.Copy(BitConverter.GetBytes(result[1] ^ v2), 0, data, x + 4, 4);

                v1 = d1;
                v2 = d2;
            }
        }

        /// <summary>
        /// One XTEA block, decrypted with the standard 32 rounds.
        /// </summary>
        private static void DecryptXteaBlock(ref uint[] result, uint[] keys, uint v1, uint v2, uint rounds = 32)
        {
            const uint delta = 0x9E3779B9;
            const uint mask = 0xFFFFFFFF;
            var sum = (delta * rounds) & mask;

            for (var x = 0; x < rounds; x++)
            {
                v2 = (v2 - (((v1 << 4 ^ v1 >> 5) + v1) ^ (sum + keys[sum >> 11 & 3]))) & mask;
                sum = (sum - delta) & mask;
                v1 = (v1 - (((v2 << 4 ^ v2 >> 5) + v2) ^ (sum + keys[sum & 3]))) & mask;
            }

            result[0] = v1;
            result[1] = v2;
        }
    }
}
