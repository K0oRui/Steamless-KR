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
    using API.Events;
    using API.Model;
    using API.PE;
    using API.Services;

    /// <summary>
    /// The `.bind` section every SteamStub file carries. Its body holds the stub code and header,
    /// and its bounds decide which header pointers to rewrite when the section goes away.
    /// </summary>
    public sealed class BindSection
    {
        private BindSection(PeImage file, PeSection section)
        {
            this.Section = section;
            this.Data = file.ReadSection(section);
            this.VirtualAddress = section.VirtualAddress;
            this.VirtualSize = section.VirtualSize;
        }

        public PeSection Section { get; }

        public byte[] Data { get; }

        public ulong VirtualAddress { get; }

        public ulong VirtualSize { get; }

        public bool Removed { get; private set; }

        /// <summary>
        /// Returns the section, or null when the file has none. Every unpacker asks first, because
        /// a file without it is not a SteamStub file.
        /// </summary>
        public static BindSection Open(PeImage file)
        {
            var section = file.FindSection(".bind");
            return section == null ? null : new BindSection(file, section);
        }

        /// <summary>
        /// Removes the section unless the user asked to keep it. Returns false only when a present
        /// section cannot be removed, which the caller reports as a failed unpack.
        /// </summary>
        public bool Remove(PeImage file, SteamlessOptions options, LoggingService logService)
        {
            if (options.KeepBindSection)
            {
                logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> .bind section kept.", LogMessageType.Debug));
                return true;
            }

            if (!this.Section.IsValid)
                return false;

            file.RemoveSection(this.Section);
            this.Removed = true;

            logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> .bind section removed.", LogMessageType.Debug));
            return true;
        }
    }
}
