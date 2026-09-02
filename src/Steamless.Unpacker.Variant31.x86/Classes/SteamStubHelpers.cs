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

namespace Steamless.Unpacker.Variant31.x86.Classes
{
    using System;

    public static class SteamStubHelpers
    {
        /// <summary>
        /// Xor decrypts the given data using a rolling key; the key is replaced by
        /// the previous plaintext word for the next block.
        /// @note If no key is given (0) then the first key is read from the first
        ///       4 bytes inside of the data given.
        /// </summary>
        public static uint SteamXor(ref byte[] data, uint size, uint key = 0)
        {
            var offset = (uint)0;

            if (key == 0)
            {
                offset += 4;
                key = BitConverter.ToUInt32(data, 0);
            }

            for (var x = offset; x < size; x += 4)
            {
                var val = BitConverter.ToUInt32(data, (int)x);
                Array.Copy(BitConverter.GetBytes(val ^ key), 0, data, x, 4);

                key = val;
            }

            return key;
        }

        /// <summary>
        /// The second pass of decryption for the SteamDRMP.dll file.
        /// @note The encryption method here is known as XTEA.
        /// </summary>
        public static void SteamDrmpDecryptPass2(ref uint[] res, uint[] keys, uint v1, uint v2, uint n = 32)
        {
            const uint delta = 0x9E3779B9;
            const uint mask = 0xFFFFFFFF;
            var sum = (delta * n) & mask;

            for (var x = 0; x < n; x++)
            {
                v2 = (v2 - (((v1 << 4 ^ v1 >> 5) + v1) ^ (sum + keys[sum >> 11 & 3]))) & mask;
                sum = (sum - delta) & mask;
                v1 = (v1 - (((v2 << 4 ^ v2 >> 5) + v2) ^ (sum + keys[sum & 3]))) & mask;
            }

            res[0] = v1;
            res[1] = v2;
        }

        /// <summary>
        /// The first pass of the decryption for the SteamDRMP.dll file.
        /// @note The encryption method here is known as XTEA. It is modded to include
        ///       some basic xor'ing.
        /// </summary>
        public static void SteamDrmpDecryptPass1(ref byte[] data, uint size, uint[] keys)
        {
            var v1 = (uint)0x55555555;
            var v2 = (uint)0x55555555;
            var res = new uint[2];

            for (var x = 0; x < size; x += 8)
            {
                var d1 = BitConverter.ToUInt32(data, x + 0);
                var d2 = BitConverter.ToUInt32(data, x + 4);

                SteamDrmpDecryptPass2(ref res, keys, d1, d2);

                Array.Copy(BitConverter.GetBytes(res[0] ^ v1), 0, data, x + 0, 4);
                Array.Copy(BitConverter.GetBytes(res[1] ^ v2), 0, data, x + 4, 4);

                v1 = d1;
                v2 = d2;
            }
        }
    }
}
