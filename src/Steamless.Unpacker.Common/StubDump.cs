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

namespace Steamless.Unpacker.Common
{
    using API.Events;
    using API.Services;
    using System;
    using System.IO;

    /// <summary>
    /// Writes the optional stub artifacts next to the input. They exist for stub study, so a failed
    /// write is reported and never fails the unpack.
    /// </summary>
    public static class StubDump
    {
        public static void TryWrite(string path, byte[] data, string label, LoggingService logService)
        {
            try
            {
                File.WriteAllBytes(path, data);
                logService.OnAddLogMessage(null, new LogMessageEventArgs($" --> {label} saved to disk.", LogMessageType.Debug));
            }
            catch (Exception ex)
            {
                logService.OnAddLogMessage(null, new LogMessageEventArgs($" --> Could not save {label}: {ex.Message}", LogMessageType.Warning));
            }
        }
    }
}
