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
    /// When a 3.x stub starts through a TLS callback instead of the entry point, the callback list
    /// points into the section about to be removed. This rewrites that entry so the unpacked file
    /// starts at the original entry point.
    /// </summary>
    public static class TlsCallbackRepair
    {
        /// <summary>
        /// Returns the entry point the unpacked file should use, or 0 when the callback data leads
        /// nowhere and the caller keeps the header's entry point.
        /// </summary>
        public static uint Repair(PeImage file, ulong originalEntryPoint, uint xorKey)
        {
            if (file.TlsCallbacks.Count == 0)
                return 0;

            var callbackSection = file.FindSectionAtRva(file.RvaFromVa(file.TlsCallbacks[0]));
            if (callbackSection == null || !callbackSection.IsValid ||
                !string.Equals(callbackSection.Name, ".bind", StringComparison.OrdinalIgnoreCase))
                return 0;

            var callbackRva = file.RvaFromVa(file.TlsDirectory.AddressOfCallBacks);
            var callbackOffset = file.FileOffsetFromRva(callbackRva);
            var section = file.FindSectionAtRva(callbackRva);
            if (section == null || !section.IsValid)
                return 0;

            callbackOffset -= section.PointerToRawData;

            var entryValue = file.ImageBase + originalEntryPoint;
            var encoded = file.Is64Bit
                ? BitConverter.GetBytes(entryValue)
                : BitConverter.GetBytes((uint)entryValue);

            var sectionData = file.ReadSection(section);
            if (callbackOffset > (ulong)sectionData.Length ||
                (ulong)sectionData.Length - callbackOffset < (ulong)encoded.Length)
                return 0;

            Array.Copy(encoded, 0, sectionData, (int)callbackOffset, encoded.Length);
            file.ReplaceSection(section, sectionData);

            return DecodeEntryPointOverride(file, xorKey);
        }

        /// <summary>
        /// The stub hides the real entry point behind an XOR key in its prologue; the key sits at a
        /// different offset depending on instruction width.
        /// </summary>
        private static uint DecodeEntryPointOverride(PeImage file, uint xorKey)
        {
            var entryOffset = file.FileOffsetFromRva(file.AddressOfEntryPoint);
            if (entryOffset >= (ulong)file.Length)
                return 0;

            var count = Math.Min(0x100, file.Length - (int)entryOffset);
            if (!file.TryRead(entryOffset, count, out var window))
                return 0;

            var probe = window.ToArray();

            var pattern = file.Is64Bit
                ? "48 81 EA ?? ?? ?? ?? 8B 12 81 F2"
                : "81 EA ?? ?? ?? ?? 8B 12 81 F2";
            var keyOffset = file.Is64Bit ? 0x0B : 0x0A;

            var match = PeHelpers.FindPattern(probe, pattern);
            if (match == -1 || match + keyOffset + 4 > probe.Length)
                return 0;

            var key = (ulong)(uint)(xorKey ^ BitConverter.ToInt32(probe, (int)match + keyOffset));
            var target = file.ImageBase + file.AddressOfEntryPoint + key;

            return (uint)(target - file.ImageBase);
        }
    }
}
