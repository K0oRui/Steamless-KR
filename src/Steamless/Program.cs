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

using System;
using Avalonia;

namespace Steamless
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (!SingleInstance.TryAcquire())
                return;

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            var builder = AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

            if (OperatingSystem.IsWindows())
                builder = builder.With(Program.Win32Options());

            return builder;
        }

        /// <summary>
        /// The Windows renderer and compositor choices. The app renders entirely on the CPU, so
        /// no GPU device or GL context is created and the UI behaves the same on every machine.
        /// </summary>
        private static Win32PlatformOptions Win32Options()
        {
            return new Win32PlatformOptions
            {
                RenderingMode = new[] { Win32RenderingMode.Software },
                CompositionMode = new[] { Win32CompositionMode.RedirectionSurface },
            };
        }
    }
}
