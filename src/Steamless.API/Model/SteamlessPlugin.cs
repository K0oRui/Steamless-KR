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

namespace Steamless.API.Model
{
    using Events;
    using PE;
    using Services;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Base class for an unpacker. The host calls <see cref="Initialize"/> once, parses a file
    /// once, then asks every plugin <see cref="CanProcessFile"/> and runs the one that claims it.
    /// A session shares one plugin instance across files, so a run keeps its state on the stack,
    /// not in fields here.
    /// </summary>
    public abstract class SteamlessPlugin : IDisposable
    {
        protected LoggingService LoggingService { get; private set; }

        public virtual string Author => "Steamless Development Team";

        public virtual string Name => "Steamless Plugin";

        public virtual string Description => "The Steamless base plugin class.";

        /// <summary>
        /// The variants this plugin can unpack, one picker entry each. A plugin that covers more
        /// than one layout lists them all here. Defaults to the plugin's own name.
        /// </summary>
        public virtual IReadOnlyList<string> Variants => new[] { this.Name };

        ~SteamlessPlugin()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public virtual bool Initialize(LoggingService logService)
        {
            this.LoggingService = logService;
            return true;
        }

        /// <summary>
        /// Whether this plugin claims the image. The host has already parsed and validated it, so
        /// a claim rule only has to recognise its own variant.
        /// </summary>
        public virtual bool CanProcessFile(PeImage file)
        {
            return false;
        }

        /// <summary>
        /// The most specific variant this plugin recognises in an image it claimed. Detection
        /// prints it, so a plugin covering several layouts can say which one the file uses. The
        /// host calls this only after <see cref="CanProcessFile"/> returns true.
        /// </summary>
        public virtual string DetectVariant(PeImage file)
        {
            return this.Name;
        }

        /// <summary>
        /// Unpacks the image the host parsed. Returns false when the file turns out not to be this
        /// variant, or when a step failed and logged why.
        /// </summary>
        public virtual bool ProcessFile(PeImage file, SteamlessOptions options)
        {
            return false;
        }

        protected void Log(string message, LogMessageType type)
        {
            this.LoggingService?.OnAddLogMessage(this, new LogMessageEventArgs(message, type));
        }
    }
}
