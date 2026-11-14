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
    using Model;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    /// <summary>
    /// A feed as a list a host can bind. The feed owns the rules; this only replays its changes
    /// into an observable list, on the thread the host supplies. A host with a single-threaded
    /// list passes its dispatcher; a console or a test passes nothing and takes the logging thread.
    /// </summary>
    public sealed class LogLines : IDisposable
    {
        private readonly LogFeed m_Feed;
        private readonly Action<Action> m_Marshal;
        private readonly ObservableCollection<LogEntry> m_Items;
        private readonly List<LogChange> m_Pending = new List<LogChange>();
        private readonly object m_Gate = new object();
        private bool m_Scheduled;
        private bool m_Disposed;

        public LogLines(LogFeed feed, Action<Action>? marshal = null)
        {
            if (feed == null)
                throw new ArgumentNullException(nameof(feed));

            this.m_Feed = feed;
            this.m_Marshal = marshal ?? (action => action());
            this.m_Items = new ObservableCollection<LogEntry>(feed.Entries);
            this.Items = new ReadOnlyObservableCollection<LogEntry>(this.m_Items);

            this.m_Feed.Changed += this.OnChanged;
        }

        /// <summary>The lines so far, in the order the feed announced them. A host binds this.</summary>
        public ReadOnlyObservableCollection<LogEntry> Items { get; }

        public void Dispose()
        {
            if (this.m_Disposed)
                return;

            this.m_Disposed = true;
            this.m_Feed.Changed -= this.OnChanged;
        }

        /// <summary>
        /// Queues the change and asks the host to drain the queue once. A burst of lines arrives on
        /// one producer thread, so this turns thousands of dispatcher posts into one.
        /// </summary>
        private void OnChanged(object? sender, LogChange change)
        {
            lock (this.m_Gate)
            {
                this.m_Pending.Add(change);

                if (this.m_Scheduled)
                    return;

                this.m_Scheduled = true;
            }

            this.m_Marshal(this.Drain);
        }

        private void Drain()
        {
            LogChange[] changes;

            lock (this.m_Gate)
            {
                changes = this.m_Pending.ToArray();
                this.m_Pending.Clear();
                this.m_Scheduled = false;
            }

            foreach (var change in changes)
                this.Apply(change);
        }

        private void Apply(LogChange change)
        {
            if (this.m_Disposed)
                return;

            switch (change.Kind)
            {
                case LogChangeKind.Added:
                    foreach (var entry in change.Entries)
                        this.m_Items.Add(entry);
                    break;

                case LogChangeKind.Removed:
                    foreach (var entry in change.Entries)
                        this.m_Items.Remove(entry);
                    break;

                case LogChangeKind.Cleared:
                    this.m_Items.Clear();
                    break;
            }
        }
    }
}
