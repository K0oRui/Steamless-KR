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

namespace Steamless.Model
{
    using API;
    using API.Events;
    using API.Model;
    using API.PE32;
    using API.PE64;
    using API.Services;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    [SteamlessApiVersion(1, 0)]
    internal class AutomaticPlugin : SteamlessPlugin
    {
        private LoggingService m_LoggingService;

        public override string Author => "Steamless Development Team";

        public override string Name => "Automatic";

        public override string Description => "Automatically finds which plugin to use for the given file.";

        public override Version Version => new Version(1, 0, 0, 0);

        private void Log(string msg, LogMessageType type)
        {
            this.m_LoggingService.OnAddLogMessage(this, new LogMessageEventArgs(msg, type));
        }

        public override bool Initialize(LoggingService logService)
        {
            this.m_LoggingService = logService;
            return true;
        }

        public override bool CanProcessFile(string file)
        {
            return true;
        }

        // Sibling-aware dispatch: try each sibling plugin, then fall back to a local PE probe.
        public override bool ProcessFile(string file, SteamlessOptions options, IEnumerable<SteamlessPlugin> siblings)
        {
            foreach (var sibling in siblings.Where(p => p != this))
            {
                if (!sibling.CanProcessFile(file))
                    continue;

                if (sibling.ProcessFile(file, options, siblings.Where(x => x != sibling)))
                    return true;
            }

            return this.ProbeFile(file);
        }

        private bool ProbeFile(string file)
        {
            try
            {
                dynamic f = new Pe32File(file);

                if (f.Parse())
                {
                    if (f.IsFile64Bit())
                    {
                        f = new Pe64File(file);
                        if (!f.Parse())
                            return false;
                    }

                    if (!f.HasSection(".bind"))
                    {
                        this.Log("", LogMessageType.Error);
                        this.Log("This file does not appear to be packed with SteamStub!", LogMessageType.Error);
                        this.Log("File missing expected '.bind' section!", LogMessageType.Error);
                        this.Log("", LogMessageType.Error);
                        return false;
                    }
                }
                else
                {
                    this.Log("", LogMessageType.Error);
                    this.Log("This file does not appear to be a valid Win32 PE file. Cannot unpack!", LogMessageType.Error);
                    this.Log("", LogMessageType.Error);
                }
            }
            catch (Exception e)
            {
                this.Log("Failed to parse or unpack the selected file due to an exception:", LogMessageType.Error);
                this.Log("", LogMessageType.Error);
                this.Log(e.Message, LogMessageType.Error);
            }

            return false;
        }
    }
}
