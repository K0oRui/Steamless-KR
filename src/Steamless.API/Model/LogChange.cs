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
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One ordered change to the log feed. A host that keeps its own copy applies these in the
    /// order they arrive, so it never has to guess which entries a trim removed.
    /// </summary>
    public sealed class LogChange
    {
        private static readonly LogEntry[] NoEntries = new LogEntry[0];

        private LogChange(LogChangeKind kind, IReadOnlyList<LogEntry> entries)
        {
            this.Kind = kind;
            this.Entries = entries;
        }

        public LogChangeKind Kind { get; }

        /// <summary>The appended entry, the dropped entries, or none.</summary>
        public IReadOnlyList<LogEntry> Entries { get; }

        public static LogChange Added(LogEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            return new LogChange(LogChangeKind.Added, new[] { entry });
        }

        public static LogChange Removed(IReadOnlyList<LogEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            return new LogChange(LogChangeKind.Removed, entries);
        }

        public static LogChange Cleared()
        {
            return new LogChange(LogChangeKind.Cleared, LogChange.NoEntries);
        }
    }
}
