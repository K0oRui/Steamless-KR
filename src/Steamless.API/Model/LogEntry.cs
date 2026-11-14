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
    using Events;

    /// <summary>
    /// One accepted log line: the severity a renderer colours by, the source, and the message. The
    /// feed decides which messages become entries.
    /// </summary>
    public sealed class LogEntry
    {
        private readonly string m_Line;

        public LogEntry(LogMessageType messageType, string source, string message)
        {
            this.MessageType = messageType;
            this.Source = source ?? string.Empty;
            this.Message = message ?? string.Empty;

            // An empty message is spacing, not a line with a dangling source prefix.
            this.m_Line = this.Message.Length == 0 ? string.Empty : $"[{this.Source}] {this.Message}";
        }

        public LogMessageType MessageType { get; }

        /// <summary>The plugin that wrote the line, or the host's name for a host message.</summary>
        public string Source { get; }

        public string Message { get; }

        /// <summary>The message with its source in front, ready to render.</summary>
        public string Line => this.m_Line;
    }
}
