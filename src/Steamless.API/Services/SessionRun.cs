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
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// One run of a session: the inputs, the plugin choice and the options the host froze, plus
    /// cancellation. The work goes to the thread pool, so a host with a UI thread can await it
    /// without blocking. A run starts once and reports what it did; to go again, create another.
    /// </summary>
    public sealed class SessionRun
    {
        private readonly UnpackSession m_Session;
        private readonly PluginSelection m_Selection;
        private readonly string[] m_Inputs;
        private readonly SteamlessOptions m_Options;
        private readonly object m_Gate = new object();
        private CancellationTokenSource? m_Cts;
        private bool m_Started;
        private bool m_Running;

        public SessionRun(UnpackSession session, PluginSelection selection, IReadOnlyList<string> inputs, SteamlessOptions options)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (selection == null)
                throw new ArgumentNullException(nameof(selection));
            if (inputs == null)
                throw new ArgumentNullException(nameof(inputs));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            this.m_Session = session;
            this.m_Selection = selection;
            this.m_Options = options;
            this.m_Inputs = new string[inputs.Count];

            for (var x = 0; x < inputs.Count; x++)
                this.m_Inputs[x] = inputs[x];
        }

        /// <summary>True from the moment the run starts until it returns or throws.</summary>
        public bool IsRunning
        {
            get
            {
                lock (this.m_Gate)
                    return this.m_Running;
            }
        }

        /// <summary>
        /// Stops the run between files. A plugin already working on a file is not interrupted; that
        /// file finishes or fails before the run checks the token again.
        /// </summary>
        public void Cancel()
        {
            lock (this.m_Gate)
                this.m_Cts?.Cancel();
        }

        /// <summary>
        /// Runs on the thread pool and returns the report. A second start is a host bug, not a
        /// retry, so it throws instead of unpacking the same inputs twice.
        /// </summary>
        public async Task<UnpackReport> StartAsync()
        {
            CancellationTokenSource cts;

            lock (this.m_Gate)
            {
                if (this.m_Started)
                    throw new InvalidOperationException("This run has already started.");

                this.m_Started = true;
                this.m_Running = true;
                cts = new CancellationTokenSource();
                this.m_Cts = cts;
            }

            try
            {
                return await Task.Run(() => this.m_Session.Run(this.m_Inputs, this.m_Selection, this.m_Options, cts.Token), cts.Token).ConfigureAwait(false);
            }
            finally
            {
                lock (this.m_Gate)
                {
                    this.m_Running = false;

                    if (ReferenceEquals(this.m_Cts, cts))
                        this.m_Cts = null;
                }

                cts.Dispose();
            }
        }
    }
}
