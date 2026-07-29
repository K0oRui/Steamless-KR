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

namespace Steamless.API.PE32
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    public class Pe32Helpers
    {
        private static readonly int OptionalHeaderFieldOffset = Marshal.OffsetOf(typeof(NativeApi32.ImageNtHeaders32), "OptionalHeader").ToInt32();
        private static readonly int CheckSumFieldOffset = Marshal.OffsetOf(typeof(NativeApi32.ImageOptionalHeader32), "CheckSum").ToInt32();

        public static T GetStructure<T>(byte[] data, int offset = 0) where T : struct
        {
            var size = Marshal.SizeOf<T>();
            if (offset + size > data.Length)
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

        public static byte[] GetStructureBytes<T>(T obj) where T : struct
        {
            var size = Marshal.SizeOf<T>();
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(obj, ptr, false);
                var bytes = new byte[size];
                Marshal.Copy(ptr, bytes, 0, size);
                return bytes;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public static NativeApi32.ImageSectionHeader32 GetSection(byte[] rawData, int index, NativeApi32.ImageDosHeader32 dosHeader, NativeApi32.ImageNtHeaders32 ntHeaders)
        {
            var sectionSize = Unsafe.SizeOf<NativeApi32.ImageSectionHeader32>();
            var dataOffset = dosHeader.e_lfanew + OptionalHeaderFieldOffset + ntHeaders.FileHeader.SizeOfOptionalHeader;

            return GetStructure<NativeApi32.ImageSectionHeader32>(rawData, dataOffset + (index * sectionSize));
        }

        private static uint ComputePeChecksum(byte[] data)
        {
            uint checksum = 0;

            for (var i = 0; i < data.Length - 1; i += 2)
            {
                var word = (uint)(data[i] | (data[i + 1] << 8));
                checksum += word;
                checksum = (checksum & 0xFFFF) + (checksum >> 16);
            }

            if ((data.Length & 1) != 0)
            {
                checksum += (uint)(data[data.Length - 1] << 8);
                checksum = (checksum & 0xFFFF) + (checksum >> 16);
            }

            checksum = (checksum & 0xFFFF) + (checksum >> 16);
            checksum = (checksum & 0xFFFF) + (checksum >> 16);

            checksum += (uint)data.Length;

            return checksum;
        }

        public static bool UpdateFileChecksum(string path)
        {
            try
            {
                var data = File.ReadAllBytes(path);

                var dosHeader = GetStructure<NativeApi32.ImageDosHeader32>(data, 0);
                var sigOffset = dosHeader.e_lfanew;

                var checksumOffset = sigOffset + 4 +
                    (uint)Unsafe.SizeOf<NativeApi32.ImageFileHeader32>() +
                    (uint)CheckSumFieldOffset;

                // Zero the existing checksum field per the PE checksum algorithm spec.
                Buffer.BlockCopy(new byte[4], 0, data, (int)checksumOffset, 4);

                var checksum = ComputePeChecksum(data);
                var checksumBytes = BitConverter.GetBytes(checksum);
                Buffer.BlockCopy(checksumBytes, 0, data, (int)checksumOffset, 4);

                File.WriteAllBytes(path, data);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static long FindPattern(byte[] data, string pattern)
        {
            try
            {
                var trimPattern = pattern.Replace(" ", "").Trim();

                var patternData = new List<byte>();
                var patternMask = new List<bool>();
                for (var i = 0; i < trimPattern.Length; i += 2)
                {
                    var bt = trimPattern.Substring(i, 2);
                    patternMask.Add(!bt.Contains('?'));
                    patternData.Add(bt.Contains('?') ? (byte)0 : Convert.ToByte(bt, 16));
                }

                var pd = patternData.ToArray();
                var pm = patternMask.ToArray();
                var lastPossible = data.Length - pd.Length;

                for (var x = 0; x <= lastPossible; x++)
                {
                    var found = true;
                    for (var y = 0; y < pd.Length; y++)
                    {
                        if (pm[y] && pd[y] != data[x + y])
                        {
                            found = false;
                            break;
                        }
                    }
                    if (found)
                        return (uint)x;
                }

                return -1;
            }
            catch
            {
                return -1;
            }
        }
    }
}
