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
    /// <summary>
    /// One entry in a host's unpacker picker: either automatic mode, which runs the first plugin
    /// that claims the file, or one variant of a specific plugin.
    /// </summary>
    public sealed class PluginSelection
    {
        private PluginSelection(string name, string description, SteamlessPlugin? plugin, string? variant)
        {
            this.Name = name;
            this.Description = description;
            this.Plugin = plugin;
            this.Variant = variant;
        }

        public string Name { get; }

        public string Description { get; }

        /// <summary>The unpacker to run, or null when this entry is the automatic mode.</summary>
        public SteamlessPlugin? Plugin { get; }

        /// <summary>
        /// The layout this entry picked, or null when the entry is not pinned to one. A pinned run
        /// only unpacks a file whose detected variant matches.
        /// </summary>
        public string? Variant { get; }

        public bool IsAutomatic => this.Plugin == null;

        public static PluginSelection Automatic()
        {
            return new PluginSelection("Automatic", "Automatically finds which plugin to use for the given file.", null, null);
        }

        public static PluginSelection For(SteamlessPlugin plugin)
        {
            return new PluginSelection(plugin.Name, plugin.Description, plugin, null);
        }

        public static PluginSelection For(SteamlessPlugin plugin, string variant)
        {
            return new PluginSelection(variant, plugin.Description, plugin, variant);
        }
    }
}
