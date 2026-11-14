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
    using API.Model;
    using API.PE;
    using API.Services;

    /// <summary>
    /// The decisions an unpacker makes about its output: which switches to apply to the image, and
    /// where the result goes. Everything that touches bytes belongs to <see cref="PeImage.Save"/>.
    /// </summary>
    public static class UnpackedOutput
    {
        public static string PathFor(PeImage file)
        {
            return file.FilePath + ".unpacked.exe";
        }

        public static bool Write(
            PeImage file,
            BindSection bindSection,
            uint entryPoint,
            SteamlessOptions options,
            LoggingService logService)
        {
            if (options.ZeroDosStubData && file.DosStub.Length > 0)
                file.DosStub = new byte[file.DosStub.Length];

            file.RebuildSections(options.DontRealignSections == false);
            file.AddressOfEntryPoint = entryPoint;

            // The certificate entry is a file offset, so it needs the realigned overlay start,
            // which is why this runs after RebuildSections.
            if (bindSection != null && bindSection.Removed)
                file.RepairHeaderPointers(bindSection.VirtualAddress, bindSection.VirtualSize, logService);

            var path = UnpackedOutput.PathFor(file);
            if (!file.Save(path, options.RecalculateFileChecksum))
            {
                logService.OnAddLogMessage(null, new LogMessageEventArgs(" --> Could not write the unpacked file.", LogMessageType.Error));
                return false;
            }

            logService.OnAddLogMessage(null, new LogMessageEventArgs($" --> Saved unpacked file: {path}", LogMessageType.Success));
            return true;
        }
    }
}
