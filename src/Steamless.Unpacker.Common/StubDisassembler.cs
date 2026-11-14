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
    using API.PE;
    using Iced.Intel;
    using System;

    /// <summary>
    /// Locates a SteamStub header by walking the stub's prologue. Versions 2.0 and 2.1 build the
    /// header pointer with different instruction shapes, so both walks live here instead of in
    /// each plugin.
    /// </summary>
    public static class StubDisassembler
    {
        private const int ScanLength = 4096;

        /// <summary>
        /// Version 2.0 keeps the header offset and size in two `mov reg, imm32` instructions.
        /// </summary>
        public static bool TryFindV20Header(PeImage file, out uint headerOffset, out uint headerSize)
        {
            headerOffset = 0;
            headerSize = 0;

            if (!TryDecode(file, out var decoder, out var reader))
                return false;

            try
            {
                var endRip = decoder.IP + ScanLength;

                while (decoder.IP < endRip && reader.CanReadByte)
                {
                    var instruction = decoder.Decode();

                    if (instruction.Mnemonic == Mnemonic.Mov &&
                        instruction.Op0Kind == OpKind.Register && IsImmediate32(instruction.Op1Kind))
                    {
                        if (headerOffset == 0)
                            headerOffset = instruction.Immediate32 - (uint)file.ImageBase;
                        else if (headerSize == 0)
                            headerSize = instruction.Immediate32 * 4;
                    }

                    if (headerOffset > 0 && headerSize > 0)
                        return true;
                }
            }
            catch (Exception)
            {
            }

            headerOffset = 0;
            headerSize = 0;
            return false;
        }

        /// <summary>
        /// Version 2.1 keeps the offset and XOR key in `mov [mem], imm32` and the size in
        /// `mov reg, imm32`.
        /// </summary>
        public static bool TryFindV21Header(PeImage file, out uint headerOffset, out uint headerSize, out uint xorKey)
        {
            headerOffset = 0;
            headerSize = 0;
            xorKey = 0;

            if (!TryDecode(file, out var decoder, out var reader))
                return false;

            try
            {
                var endRip = decoder.IP + ScanLength;

                while (decoder.IP < endRip && reader.CanReadByte)
                {
                    var instruction = decoder.Decode();

                    if (instruction.Mnemonic == Mnemonic.Mov &&
                        instruction.Op0Kind == OpKind.Memory && IsImmediate32(instruction.Op1Kind))
                    {
                        if (headerOffset == 0)
                            headerOffset = instruction.Immediate32 - (uint)file.ImageBase;
                        else
                            xorKey = instruction.Immediate32;
                    }

                    if (instruction.Mnemonic == Mnemonic.Mov &&
                        instruction.Op0Kind == OpKind.Register && IsImmediate32(instruction.Op1Kind))
                    {
                        headerSize = instruction.Immediate32 * 4;
                    }

                    if (headerOffset > 0 && headerSize > 0 && xorKey > 0)
                        return true;
                }
            }
            catch (Exception)
            {
            }

            headerOffset = 0;
            headerSize = 0;
            xorKey = 0;
            return false;
        }

        private static bool TryDecode(PeImage file, out Decoder decoder, out ByteArrayCodeReader reader)
        {
            decoder = default;
            reader = null;

            var entryOffset = file.FileOffsetFromRva(file.AddressOfEntryPoint);
            if (entryOffset >= (ulong)file.Length)
                return false;

            var available = Math.Min(ScanLength, file.Length - (int)entryOffset);
            if (!file.TryRead(entryOffset, available, out var window))
                return false;

            var code = window.ToArray();
            reader = new ByteArrayCodeReader(code, 0, code.Length);
            decoder = Decoder.Create(32, reader);
            decoder.IP = entryOffset;

            return true;
        }

        /// <summary>The decoder treats an 8-bit sign-extended immediate as a 32-bit value.</summary>
        internal static bool IsImmediate32(OpKind kind)
        {
            return kind == OpKind.Immediate32 || kind == OpKind.Immediate8to32;
        }
    }
}
