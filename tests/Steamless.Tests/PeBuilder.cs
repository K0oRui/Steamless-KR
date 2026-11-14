namespace Steamless.Tests
{
    using System;

    /// <summary>
    /// Builds a canonical PE32 or PE32+ image from real section bodies, with a DOS stub, an optional
    /// overlay and no data directories. It is the smallest input the reader accepts, so a test can
    /// state the exact bytes the writer has to produce without carrying a game binary.
    /// </summary>
    internal static class PeBuilder
    {
        public const int DosHeaderSize = 0x40;

        public const int NtOffset = 0x80;

        public const int FileAlignment = 0x200;

        public const int SectionAlignment = 0x1000;

        public const int HeadersSize = 0x400;

        public const int CheckSumOffset = NtOffset + 24 + 64;

        /// <summary>Two aligned sections with recognisable bodies.</summary>
        public static (string Name, uint VirtualAddress, byte[] Body)[] Sections => new[]
        {
            (".text", 0x1000u, Pattern(0x11, 0x200)),
            (".data", 0x2000u, Pattern(0x22, 0x200)),
        };

        public static byte[] Pattern(byte value, int length)
        {
            var data = new byte[length];
            for (var x = 0; x < length; x++)
                data[x] = value;

            return data;
        }

        public static byte[] Build(bool is64Bit, byte[]? overlay, params (string Name, uint VirtualAddress, byte[] Body)[] sections)
        {
            var optionalSize = is64Bit ? 240 : 224;
            var sectionTable = NtOffset + 24 + optionalSize;

            var rawOffsets = new int[sections.Length];
            var rawSizes = new int[sections.Length];
            var endOfSections = HeadersSize;

            for (var x = 0; x < sections.Length; x++)
            {
                rawOffsets[x] = endOfSections;
                rawSizes[x] = Align(sections[x].Body.Length, FileAlignment);
                endOfSections += rawSizes[x];
            }

            var data = new byte[endOfSections + (overlay?.Length ?? 0)];

            // DOS header: MZ plus the pointer to the NT headers.
            data[0] = 0x4D;
            data[1] = 0x5A;
            WriteUInt32(data, 0x3C, NtOffset);

            // NT signature and file header.
            WriteUInt32(data, NtOffset, 0x00004550);

            var fileHeader = NtOffset + 4;
            WriteUInt16(data, fileHeader, is64Bit ? (ushort)0x8664 : (ushort)0x014C);
            WriteUInt16(data, fileHeader + 2, (ushort)sections.Length);
            WriteUInt16(data, fileHeader + 16, (ushort)optionalSize);
            WriteUInt16(data, fileHeader + 18, 0x0102);

            // Optional header: the fields the reader reads, and nothing else.
            var optional = fileHeader + 20;
            var last = sections[sections.Length - 1];

            WriteUInt16(data, optional, is64Bit ? (ushort)0x20B : (ushort)0x10B);
            WriteUInt32(data, optional + 16, sections[0].VirtualAddress + 0x10);
            WriteUInt32(data, optional + 20, sections[0].VirtualAddress);

            if (is64Bit)
                WriteUInt64(data, optional + 24, 0x0000000140000000);
            else
                WriteUInt32(data, optional + 28, 0x00400000);

            WriteUInt32(data, optional + 32, SectionAlignment);
            WriteUInt32(data, optional + 36, FileAlignment);
            WriteUInt32(data, optional + 56, (uint)Align((int)(last.VirtualAddress + (uint)last.Body.Length), SectionAlignment));
            WriteUInt32(data, optional + 60, HeadersSize);
            WriteUInt32(data, optional + (is64Bit ? 108 : 92), 16);

            for (var x = 0; x < sections.Length; x++)
            {
                var header = sectionTable + (x * 40);
                var name = System.Text.Encoding.ASCII.GetBytes(sections[x].Name);
                Array.Copy(name, 0, data, header, name.Length);

                WriteUInt32(data, header + 8, (uint)sections[x].Body.Length);
                WriteUInt32(data, header + 12, sections[x].VirtualAddress);
                WriteUInt32(data, header + 16, (uint)rawSizes[x]);
                WriteUInt32(data, header + 20, (uint)rawOffsets[x]);
                WriteUInt32(data, header + 36, 0x60000020);
                Array.Copy(sections[x].Body, 0, data, rawOffsets[x], sections[x].Body.Length);
            }

            if (overlay != null)
                Array.Copy(overlay, 0, data, endOfSections, overlay.Length);

            return data;
        }

        public static int Align(int value, int alignment)
        {
            var remainder = value % alignment;
            return remainder == 0 ? value : value + alignment - remainder;
        }

        public static void WriteUInt16(byte[] data, int offset, ushort value)
        {
            Array.Copy(BitConverter.GetBytes(value), 0, data, offset, 2);
        }

        public static void WriteUInt32(byte[] data, int offset, uint value)
        {
            Array.Copy(BitConverter.GetBytes(value), 0, data, offset, 4);
        }

        public static void WriteUInt64(byte[] data, int offset, ulong value)
        {
            Array.Copy(BitConverter.GetBytes(value), 0, data, offset, 8);
        }
    }
}
