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
    using System.Runtime.InteropServices;

    public class NativeApi32
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct ImageDosHeader32
        {
            public ushort e_magic;
            public ushort e_cblp;
            public ushort e_cp;
            public ushort e_crlc;
            public ushort e_cparhdr;
            public ushort e_minalloc;
            public ushort e_maxalloc;
            public ushort e_ss;
            public ushort e_sp;
            public ushort e_csum;
            public ushort e_ip;
            public ushort e_cs;
            public ushort e_lfarlc;
            public ushort e_ovno;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public ushort[] e_res1;

            public ushort e_oemid;
            public ushort e_oeminfo;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public ushort[] e_res2;

            public int e_lfanew;

            public bool IsValid => this.e_magic == 0x5A4D;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct ImageNtHeaders32
        {
            [FieldOffset(0)]
            public uint Signature;

            [FieldOffset(4)]
            public ImageFileHeader32 FileHeader;

            [FieldOffset(24)]
            public ImageOptionalHeader32 OptionalHeader;

            public bool IsValid => this.Signature == 0x00004550;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ImageFileHeader32
        {
            public ushort Machine;
            public ushort NumberOfSections;
            public uint TimeDateStamp;
            public uint PointerToSymbolTable;
            public uint NumberOfSymbols;
            public ushort SizeOfOptionalHeader;
            public ushort Characteristics;
        }

        public enum MachineType : ushort
        {
            Native = 0,
            I386 = 0x014C,
            Itanium = 0x0200,
            X64 = 0x8664
        }

        public enum MagicType : ushort
        {
            ImageNtOptionalHdr32Magic = 0x10B,
            ImageNtOptionalHdr64Magic = 0x20B
        }

        public enum SubSystemType : ushort
        {
            ImageSubsystemUnknown = 0,
            ImageSubsystemNative = 1,
            ImageSubsystemWindowsGui = 2,
            ImageSubsystemWindowsCui = 3,
            ImageSubsystemPosixCui = 7,
            ImageSubsystemWindowsCeGui = 9,
            ImageSubsystemEfiApplication = 10,
            ImageSubsystemEfiBootServiceDriver = 11,
            ImageSubsystemEfiRuntimeDriver = 12,
            ImageSubsystemEfiRom = 13,
            ImageSubsystemXbox = 14
        }

        public enum DllCharacteristicsType : ushort
        {
            Reserved0 = 0x0001,
            Reserved1 = 0x0002,
            Reserved2 = 0x0004,
            Reserved3 = 0x0008,
            ImageDllCharacteristicsDynamicBase = 0x0040,
            ImageDllCharacteristicsForceIntegrity = 0x0080,
            ImageDllCharacteristicsNxCompat = 0x0100,
            ImageDllcharacteristicsNoIsolation = 0x0200,
            ImageDllcharacteristicsNoSeh = 0x0400,
            ImageDllcharacteristicsNoBind = 0x0800,
            Reserved4 = 0x1000,
            ImageDllcharacteristicsWdmDriver = 0x2000,
            ImageDllcharacteristicsTerminalServerAware = 0x8000
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct ImageOptionalHeader32
        {
            [FieldOffset(0)]
            public MagicType Magic;

            [FieldOffset(2)]
            public byte MajorLinkerVersion;

            [FieldOffset(3)]
            public byte MinorLinkerVersion;

            [FieldOffset(4)]
            public uint SizeOfCode;

            [FieldOffset(8)]
            public uint SizeOfInitializedData;

            [FieldOffset(12)]
            public uint SizeOfUninitializedData;

            [FieldOffset(16)]
            public uint AddressOfEntryPoint;

            [FieldOffset(20)]
            public uint BaseOfCode;

            // PE32 contains this additional field
            [FieldOffset(24)]
            public uint BaseOfData;

            [FieldOffset(28)]
            public uint ImageBase;

            [FieldOffset(32)]
            public uint SectionAlignment;

            [FieldOffset(36)]
            public uint FileAlignment;

            [FieldOffset(40)]
            public ushort MajorOperatingSystemVersion;

            [FieldOffset(42)]
            public ushort MinorOperatingSystemVersion;

            [FieldOffset(44)]
            public ushort MajorImageVersion;

            [FieldOffset(46)]
            public ushort MinorImageVersion;

            [FieldOffset(48)]
            public ushort MajorSubsystemVersion;

            [FieldOffset(50)]
            public ushort MinorSubsystemVersion;

            [FieldOffset(52)]
            public uint Win32VersionValue;

            [FieldOffset(56)]
            public uint SizeOfImage;

            [FieldOffset(60)]
            public uint SizeOfHeaders;

            [FieldOffset(64)]
            public uint CheckSum;

            [FieldOffset(68)]
            public SubSystemType Subsystem;

            [FieldOffset(70)]
            public DllCharacteristicsType DllCharacteristics;

            [FieldOffset(72)]
            public uint SizeOfStackReserve;

            [FieldOffset(76)]
            public uint SizeOfStackCommit;

            [FieldOffset(80)]
            public uint SizeOfHeapReserve;

            [FieldOffset(84)]
            public uint SizeOfHeapCommit;

            [FieldOffset(88)]
            public uint LoaderFlags;

            [FieldOffset(92)]
            public uint NumberOfRvaAndSizes;

            [FieldOffset(96)]
            public ImageDataDirectory32 ExportTable;

            [FieldOffset(104)]
            public ImageDataDirectory32 ImportTable;

            [FieldOffset(112)]
            public ImageDataDirectory32 ResourceTable;

            [FieldOffset(120)]
            public ImageDataDirectory32 ExceptionTable;

            [FieldOffset(128)]
            public ImageDataDirectory32 CertificateTable;

            [FieldOffset(136)]
            public ImageDataDirectory32 BaseRelocationTable;

            [FieldOffset(144)]
            public ImageDataDirectory32 Debug;

            [FieldOffset(152)]
            public ImageDataDirectory32 Architecture;

            [FieldOffset(160)]
            public ImageDataDirectory32 GlobalPtr;

            [FieldOffset(168)]
            public ImageDataDirectory32 TLSTable;

            [FieldOffset(176)]
            public ImageDataDirectory32 LoadConfigTable;

            [FieldOffset(184)]
            public ImageDataDirectory32 BoundImport;

            [FieldOffset(192)]
            public ImageDataDirectory32 IAT;

            [FieldOffset(200)]
            public ImageDataDirectory32 DelayImportDescriptor;

            [FieldOffset(208)]
            public ImageDataDirectory32 CLRRuntimeHeader;

            [FieldOffset(216)]
            public ImageDataDirectory32 Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ImageDataDirectory32
        {
            public uint VirtualAddress;
            public uint Size;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct ImageSectionHeader32
        {
            [FieldOffset(0)]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public char[] Name;

            [FieldOffset(8)]
            public uint VirtualSize;

            [FieldOffset(12)]
            public uint VirtualAddress;

            [FieldOffset(16)]
            public uint SizeOfRawData;

            [FieldOffset(20)]
            public uint PointerToRawData;

            [FieldOffset(24)]
            public uint PointerToRelocations;

            [FieldOffset(28)]
            public uint PointerToLinenumbers;

            [FieldOffset(32)]
            public ushort NumberOfRelocations;

            [FieldOffset(34)]
            public ushort NumberOfLinenumbers;

            [FieldOffset(36)]
            public DataSectionFlags Characteristics;

            public string SectionName => new string(this.Name).Trim('\0');

            public bool IsValid => this.SizeOfRawData != 0 && this.PointerToRawData != 0;

            public override string ToString()
            {
                return this.SectionName;
            }
        }

        [Flags]
        public enum DataSectionFlags : uint
        {
            TypeReg = 0x00000000,

            TypeDsect = 0x00000001,

            TypeNoLoad = 0x00000002,

            TypeGroup = 0x00000004,

            TypeNoPadded = 0x00000008,

            TypeCopy = 0x00000010,

            ContentCode = 0x00000020,

            ContentInitializedData = 0x00000040,

            ContentUninitializedData = 0x00000080,

            LinkOther = 0x00000100,

            LinkInfo = 0x00000200,

            TypeOver = 0x00000400,

            LinkRemove = 0x00000800,

            LinkComDat = 0x00001000,

            NoDeferSpecExceptions = 0x00004000,

            RelativeGp = 0x00008000,

            MemPurgeable = 0x00020000,

            Memory16Bit = 0x00020000,

            MemoryLocked = 0x00040000,

            MemoryPreload = 0x00080000,

            Align1Bytes = 0x00100000,

            Align2Bytes = 0x00200000,

            Align4Bytes = 0x00300000,

            Align8Bytes = 0x00400000,

            Align16Bytes = 0x00500000,

            Align32Bytes = 0x00600000,

            Align64Bytes = 0x00700000,

            Align128Bytes = 0x00800000,

            Align256Bytes = 0x00900000,

            Align512Bytes = 0x00A00000,

            Align1024Bytes = 0x00B00000,

            Align2048Bytes = 0x00C00000,

            Align4096Bytes = 0x00D00000,

            Align8192Bytes = 0x00E00000,

            LinkExtendedRelocationOverflow = 0x01000000,

            MemoryDiscardable = 0x02000000,

            MemoryNotCached = 0x04000000,

            MemoryNotPaged = 0x08000000,

            MemoryShared = 0x10000000,

            MemoryExecute = 0x20000000,

            MemoryRead = 0x40000000,

            MemoryWrite = 0x80000000
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ImageTlsDirectory32
        {
            public uint StartAddressOfRawData;
            public uint EndAddressOfRawData;
            public uint AddressOfIndex;
            public uint AddressOfCallBacks;
            public uint SizeOfZeroFill;
            public uint Characteristics;
        }

    }
}
