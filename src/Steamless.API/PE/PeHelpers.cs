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

namespace Steamless.API.PE
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    /// <summary>
    /// PE logic that does not need a parsed image: pattern scanning, range checks, the file
    /// checksum and struct marshalling.
    /// </summary>
    public static class PeHelpers
    {

        /// <summary>
        /// True when [offset, offset + size) lies inside the buffer. Stub headers supply
        /// attacker-controlled sizes, so callers check before they allocate or copy.
        /// </summary>
        public static bool IsValidRange(byte[] data, long offset, long size)
        {
            return data != null && offset >= 0 && size >= 0 &&
                   offset <= data.Length && size <= data.Length - offset;
        }

        /// <summary>
        /// Marshals a struct out of a byte buffer. Returns default when the range does not fit;
        /// callers check the struct's own signature field instead of a bool here.
        /// </summary>
        public static T GetStructure<T>(byte[] data, int offset = 0) where T : struct
        {
            var size = Marshal.SizeOf<T>();

            // A negative offset passes a naive bounds check but makes Marshal.Copy throw.
            if (data == null || offset < 0 || size > data.Length - offset)
                return default;

            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(data, offset, ptr, size);
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        /// Folds a stream into the PE checksum. The caller zeroes the checksum field first; the
        /// save path does that before the file replaces the original.
        /// </summary>
        internal static uint ComputePeChecksum(Stream stream)
        {
            // Even-sized, so every full read starts on a word boundary. Only the final short read
            // can leave an odd byte over.
            var buffer = new byte[0x100000];
            uint checksum = 0;

            while (true)
            {
                var read = stream.ReadAtLeast(buffer, buffer.Length, false);
                if (read <= 0)
                    break;

                var pairs = read & ~1;
                for (var i = 0; i < pairs; i += 2)
                {
                    var word = (uint)(buffer[i] | (buffer[i + 1] << 8));
                    checksum += word;
                    checksum = (checksum & 0xFFFF) + (checksum >> 16);
                }

                // An odd file ends with a single byte. The PE spec pads it with a zero byte,
                // so the byte lands in the low half of the final word.
                if ((read & 1) != 0)
                {
                    checksum += buffer[read - 1];
                    checksum = (checksum & 0xFFFF) + (checksum >> 16);
                }
            }

            checksum = (checksum & 0xFFFF) + (checksum >> 16);
            checksum = (checksum & 0xFFFF) + (checksum >> 16);

            checksum += (uint)stream.Length;

            return checksum;
        }

        public static long FindPattern(byte[] data, string pattern)
        {
            try
            {
                if (data == null || data.Length == 0 || string.IsNullOrEmpty(pattern))
                    return -1;

                var hex = pattern.Replace(" ", string.Empty).Trim();
                if (hex.Length == 0 || (hex.Length & 1) != 0)
                    return -1;

                var length = hex.Length / 2;
                if (length > data.Length)
                    return -1;

                var patternData = new byte[length];
                var patternMask = new bool[length];

                // A '?' in either nibble of a pair makes the whole byte a wildcard. Any other
                // non-hex character makes the pattern malformed.
                for (var i = 0; i < length; i++)
                {
                    if (hex[i * 2] == '?' || hex[i * 2 + 1] == '?')
                        continue;

                    var high = HexValue(hex[i * 2]);
                    var low = HexValue(hex[i * 2 + 1]);
                    if (high < 0 || low < 0)
                        return -1;

                    patternData[i] = (byte)((high << 4) | low);
                    patternMask[i] = true;
                }

                // Jump to occurrences of the first fixed byte; every pattern here starts with one,
                // and IndexOf is vectorized.
                var anchor = Array.FindIndex(patternMask, m => m);
                if (anchor < 0)
                    return 0;

                var lastPossible = data.Length - length;
                var start = 0;

                while (start <= lastPossible)
                {
                    // A window starting at i puts the anchor byte at i + anchor, so the
                    // candidate range is [anchor, lastPossible + anchor].
                    var candidate = Array.IndexOf(data, patternData[anchor], start + anchor, lastPossible - start + 1);
                    if (candidate < 0)
                        return -1;

                    var match = true;
                    for (var y = 0; y < length; y++)
                    {
                        if (patternMask[y] && patternData[y] != data[candidate - anchor + y])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                        return candidate - anchor;

                    start = candidate - anchor + 1;
                }

                return -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private static int HexValue(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'f')
                return c - 'a' + 10;
            if (c >= 'A' && c <= 'F')
                return c - 'A' + 10;

            return -1;
        }
    }
}
