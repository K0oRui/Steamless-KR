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

    /// <summary>
    /// State for one unpack. A session shares one plugin instance, so a run's data lives here
    /// instead of on the plugin. A variant subclasses this to add its header and any intermediate
    /// data its steps produce.
    /// </summary>
    public class UnpackRun
    {
        public PeImage File { get; set; }

        public BindSection Bind { get; set; }

        /// <summary>The entry point to write, which each variant decides differently.</summary>
        public uint EntryPoint { get; set; }

        /// <summary>
        /// The key the header decode passed on. The rolling XOR uses the last word it read as the
        /// next key, so this is the cipher's continuation state, not just a value.
        /// </summary>
        public uint XorKey { get; set; }

        /// <summary>
        /// Counts the steps this run has logged, so a variant can add a step without renumbering
        /// the ones the template owns.
        /// </summary>
        public int Step { get; set; }
    }
}
