#nullable enable

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

namespace Steamless.API.Model
{
    using System.Collections.Generic;

    /// <summary>
    /// What the unpackers see in a file without unpacking it. The CLI prints these values and the
    /// test corpus reads them, so the fields match the printed lines.
    /// </summary>
    public sealed class DetectionResult
    {
        private readonly string[] m_ClaimingPlugins;

        public DetectionResult(bool hasImage, bool is64Bit, bool hasBindSection, string[] claimingPlugins)
        {
            this.HasImage = hasImage;
            this.Is64Bit = is64Bit;
            this.HasBindSection = hasBindSection;
            this.m_ClaimingPlugins = claimingPlugins ?? new string[0];
        }

        /// <summary>False when the file is missing, unreadable, or not a PE image.</summary>
        public bool HasImage { get; }

        /// <summary>Only meaningful when <see cref="HasImage"/> is true.</summary>
        public bool Is64Bit { get; }

        public bool HasBindSection { get; }

        /// <summary>
        /// The plugins that claim the file, each named as specifically as it can. A plugin that
        /// covers several layouts reports the one it found.
        /// </summary>
        public IReadOnlyList<string> ClaimingPlugins => this.m_ClaimingPlugins;
    }
}
