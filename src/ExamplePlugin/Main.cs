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

namespace ExamplePlugin
{
    using Steamless.API;
    using Steamless.API.Events;
    using Steamless.API.Model;
    using Steamless.API.PE;
    using Steamless.API.Services;
    using System;

    [SteamlessApiVersion(2, 0)]
    public class Main : SteamlessPlugin
    {
        public override string Author => "Steamless Development Team";

        public override string Name => "Example Plugin";

        public override string Description => "A simple plugin example.";

        public override bool Initialize(LoggingService logService)
        {
            base.Initialize(logService);
            this.Log("ExamplePlugin initialized.", LogMessageType.Debug);

            return true;
        }

        public override bool CanProcessFile(PeImage file)
        {
            this.Log("ExamplePlugin was asked whether it can process a file.", LogMessageType.Debug);

            return false;
        }

        public override bool ProcessFile(PeImage file, SteamlessOptions options)
        {
            this.Log("ExamplePlugin is processing this file.", LogMessageType.Debug);

            return false;
        }
    }
}
