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
    using System.Threading.Tasks;

    /// <summary>
    /// The run a host drives over a loaded session: one at a time, with options frozen at the
    /// start, a cancellation and a throw turned into a log line, and one total after a multi-file
    /// run. A host binds its button here and keeps no run rules of its own. The caller still owns
    /// the session.
    /// </summary>
    public sealed class UnpackRunner
    {
        private readonly UnpackSession m_Session;
        private readonly LogFeed m_Feed;
        private readonly object m_Gate = new object();
        private SessionRun? m_Run;

        public UnpackRunner(UnpackSession session, LogFeed feed)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (feed == null)
                throw new ArgumentNullException(nameof(feed));

            this.m_Session = session;
            this.m_Feed = feed;
        }

        /// <summary>True from the moment a run is accepted until it finishes.</summary>
        public bool IsRunning
        {
            get
            {
                lock (this.m_Gate)
                    return this.m_Run != null;
            }
        }

        /// <summary>
        /// Raised when <see cref="IsRunning"/> changes, on the thread that started or finished the
        /// run. A host with a UI thread has to marshal before touching a control.
        /// </summary>
        public event EventHandler? RunningChanged;

        /// <summary>
        /// Starts a run and returns what it did, or null when the run was refused, cancelled or
        /// threw. A second call while one is in flight is refused, because the session shares one
        /// plugin instance and two runs would interleave their writes.
        /// </summary>
        public async Task<UnpackReport?> RunAsync(IReadOnlyList<string> inputs, PluginSelection selection, SteamlessOptions options)
        {
            if (inputs == null)
                throw new ArgumentNullException(nameof(inputs));
            if (selection == null)
                throw new ArgumentNullException(nameof(selection));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            SessionRun run;

            lock (this.m_Gate)
            {
                if (this.m_Run != null)
                    return null;

                // Freeze the options for the whole run so a mid-run toggle cannot mix settings.
                run = new SessionRun(this.m_Session, selection, inputs, options.Snapshot());
                this.m_Run = run;
            }

            this.RunningChanged?.Invoke(this, EventArgs.Empty);

            try
            {
                var report = await run.StartAsync().ConfigureAwait(false);

                // A multi-file run ends with one total; a single file already has its own line.
                if (report.Outcomes.Count > 1)
                    this.m_Feed.Add($"Finished: {report.Summary}.", report.Failed > 0 ? LogMessageType.Warning : LogMessageType.Information);

                return report;
            }
            catch (OperationCanceledException)
            {
                this.m_Feed.Add("Unpack cancelled.", LogMessageType.Warning);
                return null;
            }
            catch (Exception ex)
            {
                // The session guards each plugin; this is the last line of defence so a failed run
                // cannot take the host down with it.
                this.m_Feed.Add($"The unpack stopped: {ex.Message}", LogMessageType.Error);
                return null;
            }
            finally
            {
                lock (this.m_Gate)
                {
                    if (ReferenceEquals(this.m_Run, run))
                        this.m_Run = null;
                }

                this.RunningChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Stops the run between files. A plugin already working on a file finishes first.</summary>
        public void Cancel()
        {
            lock (this.m_Gate)
                this.m_Run?.Cancel();
        }
    }
}
