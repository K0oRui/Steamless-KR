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

namespace Steamless.API.Model
{
    /// <summary>
    /// The switches a run uses. A run reads them once at the start and nothing observes later
    /// changes, so a plain value object is enough.
    /// </summary>
    public class SteamlessOptions
    {
        public bool VerboseOutput { get; set; } = true;

        public bool KeepBindSection { get; set; }

        public bool DumpPayloadToDisk { get; set; }

        public bool DumpSteamDrmpToDisk { get; set; }

        public bool DontRealignSections { get; set; } = true;

        public bool ZeroDosStubData { get; set; } = true;

        public bool RecalculateFileChecksum { get; set; }

        /// <summary>
        /// Copies the current values so a long run keeps the options it started with.
        /// </summary>
        public SteamlessOptions Snapshot()
        {
            return (SteamlessOptions)this.MemberwiseClone();
        }
    }
}
