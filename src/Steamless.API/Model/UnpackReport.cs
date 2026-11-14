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

    public enum UnpackStatus
    {
        /// <summary>A plugin claimed the file and unpacked it.</summary>
        Unpacked,

        /// <summary>A plugin claimed the file and could not unpack it.</summary>
        Failed,

        /// <summary>No plugin claimed the file.</summary>
        Skipped,
    }

    public sealed class UnpackOutcome
    {
        public UnpackOutcome(string path, UnpackStatus status)
        {
            this.Path = path;
            this.Status = status;
        }

        public string Path { get; }

        public UnpackStatus Status { get; }
    }

    /// <summary>
    /// What a run did, per file and in total. The CLI turns it into an exit code and the GUI into
    /// a status line.
    /// </summary>
    public sealed class UnpackReport
    {
        private readonly UnpackOutcome[] m_Outcomes;

        public UnpackReport(UnpackOutcome[] outcomes)
        {
            this.m_Outcomes = outcomes ?? new UnpackOutcome[0];
        }

        public IReadOnlyList<UnpackOutcome> Outcomes => this.m_Outcomes;

        /// <summary>The counts as one line, for a host that shows a run's total.</summary>
        public string Summary => $"{this.Unpacked} unpacked, {this.Failed} failed, {this.Skipped} skipped";

        public int Unpacked => this.Count(UnpackStatus.Unpacked);

        public int Failed => this.Count(UnpackStatus.Failed);

        public int Skipped => this.Count(UnpackStatus.Skipped);

        private int Count(UnpackStatus status)
        {
            var count = 0;

            foreach (var outcome in this.m_Outcomes)
            {
                if (outcome.Status == status)
                    count++;
            }

            return count;
        }
    }
}
