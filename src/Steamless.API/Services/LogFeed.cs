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

namespace Steamless.API.Services
{
    using Events;
    using Model;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The log both hosts show and the only place its rules live. It drops debug lines while the
    /// host is not verbose, names the source of each line, bounds the session, and publishes every
    /// change in order. The console writes added lines; the GUI mirrors the changes into a list it
    /// can bind.
    /// </summary>
    public sealed class LogFeed : IDisposable
    {
        private readonly List<LogEntry> m_Entries = new List<LogEntry>();
        private readonly LoggingService m_LogService;
        private readonly Func<bool> m_Verbose;
        private readonly int m_Capacity;
        private readonly object m_Gate = new object();
        private bool m_Disposed;

        /// <summary>
        /// Subscribes to the service and starts the feed. The verbose switch is asked for every
        /// message, so a host can flip it mid-session. A capacity of zero keeps every entry; a full
        /// feed drops its oldest fifth, so trims are rare and cheap.
        /// </summary>
        public LogFeed(LoggingService logService, Func<bool> verbose, int capacity = 0)
        {
            if (logService == null)
                throw new ArgumentNullException(nameof(logService));
            if (verbose == null)
                throw new ArgumentNullException(nameof(verbose));

            this.m_LogService = logService;
            this.m_Verbose = verbose;
            this.m_Capacity = capacity < 0 ? 0 : capacity;

            this.m_LogService.AddLogMessage += this.OnLogMessage;
        }

        /// <summary>
        /// The entries accepted so far, oldest first. This is a copy, so a reader cannot race the
        /// producer.
        /// </summary>
        public IReadOnlyList<LogEntry> Entries
        {
            get
            {
                lock (this.m_Gate)
                    return this.m_Entries.ToArray();
            }
        }

        /// <summary>
        /// Raised for every change the feed makes, in the order it makes them. Handlers run on the
        /// thread that logged, under the feed's gate, so a host that draws lines has to marshal the
        /// change to its own thread first.
        /// </summary>
        public event EventHandler<LogChange>? Changed;

        /// <summary>
        /// Adds a message from the host itself. Plugin messages arrive on the logging service and
        /// follow the same rules.
        /// </summary>
        public void Add(string message, LogMessageType messageType)
        {
            this.Add(null, new LogMessageEventArgs(message, messageType));
        }

        public void Add(object? sender, LogMessageEventArgs e)
        {
            if (e == null)
                return;

            lock (this.m_Gate)
            {
                if (this.m_Disposed)
                    return;

                if (!this.m_Verbose() && e.MessageType == LogMessageType.Debug)
                    return;

                var entry = new LogEntry(e.MessageType, LogFeed.SourceOf(sender), e.Message);

                this.m_Entries.Add(entry);
                this.Changed?.Invoke(this, LogChange.Added(entry));

                if (this.m_Capacity <= 0 || this.m_Entries.Count <= this.m_Capacity)
                    return;

                var drop = Math.Min(this.m_Entries.Count, Math.Max(1, this.m_Capacity / 5));
                var removed = this.m_Entries.GetRange(0, drop);

                this.m_Entries.RemoveRange(0, drop);
                this.Changed?.Invoke(this, LogChange.Removed(removed));
            }
        }

        public void Clear()
        {
            lock (this.m_Gate)
            {
                if (this.m_Disposed)
                    return;

                this.m_Entries.Clear();
                this.Changed?.Invoke(this, LogChange.Cleared());
            }
        }

        public void Dispose()
        {
            lock (this.m_Gate)
            {
                if (this.m_Disposed)
                    return;

                this.m_Disposed = true;
            }

            this.m_LogService.AddLogMessage -= this.OnLogMessage;
        }

        /// <summary>
        /// The sender is the plugin, or null for a host message. A plugin is named by its assembly,
        /// so a fork shipping under another name reads correctly without a setting.
        /// </summary>
        private static string SourceOf(object? sender)
        {
            if (sender == null)
                return "Steamless";

            try
            {
                return sender.GetType().Assembly.GetName().Name ?? "Steamless";
            }
            catch (Exception)
            {
                return "Steamless";
            }
        }

        private void OnLogMessage(object? sender, LogMessageEventArgs e)
        {
            this.Add(sender, e);
        }
    }
}
