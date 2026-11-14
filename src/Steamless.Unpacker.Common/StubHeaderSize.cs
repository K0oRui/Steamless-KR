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
    using System;

    /// <summary>
    /// The 3.x stubs write their header length in the stub's prologue. The prologue shape
    /// separates 3.0 from 3.1, so both plugins read it the same way and accept only the sizes they
    /// know.
    /// </summary>
    public static class StubHeaderSize
    {
        private const string PrologueX86 = "E8 00 00 00 00 50 53 51 52 56 57 55 8B 44 24 1C 2D 05 00 00 00 8B CC 83 E4 F0 51 51 51 50";

        private const string PrologueX64 = "E8 00 00 00 00 50 53 51 52 56 57 55 41 50";

        /// <summary>
        /// Offset from the probe match to the 32-bit header length, which counts dwords here.
        /// </summary>
        private static readonly (string Pattern, int ReadAt)[] ProbesX86 =
        {
            ("55 8B EC 81 EC ?? ?? ?? ?? 53 ?? ?? ?? ?? ?? 68", 16),
            ("55 8B EC 81 EC ?? ?? ?? ?? 53 ?? ?? ?? ?? ?? 8D 83", 22),
            ("55 8B EC 81 EC ?? ?? ?? ?? 56 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 8D", 16)
        };

        private static readonly (string Pattern, int ReadAt)[] ProbesX64 =
        {
            ("48 8D 91 ?? ?? ?? ?? 48", 3),
            ("48 8D 91 ?? ?? ?? ?? 41", 3),
            ("48 C7 84 24 ?? ?? ?? ?? ?? ?? ?? ?? 48", 8)
        };

        /// <summary>
        /// Returns the declared header size, or 0 when this is not a 3.x stub.
        /// </summary>
        public static uint Read(PeImage file, BindSection bindSection)
        {
            var prologue = file.Is64Bit ? PrologueX64 : PrologueX86;
            if (PeHelpers.FindPattern(bindSection.Data, prologue) == -1)
                return 0;

            var probes = file.Is64Bit ? ProbesX64 : ProbesX86;

            foreach (var (pattern, readAt) in probes)
            {
                var offset = PeHelpers.FindPattern(bindSection.Data, pattern);
                if (offset == -1)
                    continue;

                var position = offset + readAt;
                if (position + 4 > bindSection.Data.Length)
                    continue;

                // The stub stores the displacement from the prologue to the header, which is
                // negative in some builds, so the magnitude is the size.
                var value = BitConverter.ToInt32(bindSection.Data, (int)position);
                return value == int.MinValue ? 0 : (uint)Math.Abs(value);
            }

            return 0;
        }
    }
}
