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

namespace Steamless.CLI
{
    using Steamless.API.Events;
    using Steamless.API.Model;
    using Steamless.API.Services;
    using System;
    using System.IO;
    using System.Threading;

    internal class Program
    {
        private const int ExitOk = 0;
        private const int ExitBadArguments = 1;
        private const int ExitFileMissing = 2;
        private const int ExitNoPlugins = 3;
        private const int ExitNoMatch = 4;
        private const int ExitPluginFailed = 5;

        private static void PrintHeader()
        {
            Console.WriteLine("  _________ __                        .__                        ");
            Console.WriteLine(" /   _____//  |_  ____ _____    _____ |  |   ____   ______ ______");
            Console.WriteLine(" \\_____  \\\\   __\\/ __ \\\\__  \\  /     \\|  | _/ __ \\ /  ___//  ___/");
            Console.WriteLine(" /        \\|  | \\  ___/ / __ \\|  Y Y  \\  |_\\  ___/ \\___ \\ \\___ \\ ");
            Console.WriteLine("/_______  /|__|  \\___  >____  /__|_|  /____/\\___  >____  >____  >");
            Console.WriteLine("        \\/           \\/     \\/      \\/          \\/     \\/     \\/ \n");
            Console.WriteLine("Steamless - SteamStub DRM Remover");
            Console.WriteLine("by atom0s\n");
            Console.WriteLine("GitHub    : https://github.com/atom0s/Steamless");
            Console.WriteLine("Homepage  : https://atom0s.com");
            Console.WriteLine("Donations : https://paypal.me/atom0s");
            Console.WriteLine("Donations : https://github.com/sponsors/atom0s");
            Console.WriteLine("Donations : https://patreon.com/atom0s\n");
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("    Steamless.CLI.exe [options] [file or folder]\n");
            Console.WriteLine("Options:");
            Console.WriteLine("    --detect         - Report the bitness, .bind section and matching plugin.");
            Console.WriteLine("    --quiet          - Hide debug messages.");
            Console.WriteLine("    --keepbind       - Keep the .bind section in the unpacked file.");
            Console.WriteLine("    --keepstub       - Keep the DOS stub in the unpacked file.");
            Console.WriteLine("    --dumppayload    - Write the stub payload to disk.");
            Console.WriteLine("    --dumpdrmp       - Write SteamDRMP.dll to disk.");
            Console.WriteLine("    --realign        - Realign the unpacked file sections.");
            Console.WriteLine("    --recalcchecksum - Update the unpacked file checksum.\n");
            Console.WriteLine("Exit codes:");
            Console.WriteLine("    0 unpacked, 1 bad arguments, 2 file not found, 3 no plugins,");
            Console.WriteLine("    4 nothing claimed the file, 5 a plugin claimed it and failed.");
        }

        /// <summary>
        /// The console adapter: one line per entry the feed accepts. The console feed is unbounded,
        /// so a trim never reaches here, and a clear has nothing to redraw.
        /// </summary>
        private static void WriteLogChange(object sender, LogChange change)
        {
            if (change.Kind != LogChangeKind.Added)
                return;

            foreach (var entry in change.Entries)
                Console.WriteLine(entry.Line);
        }

        private static int Main(string[] args)
        {
            PluginLoader.AttachAssemblyResolver(message => Console.Error.WriteLine($"Steamless.CLI: {message}"));
            return Program.Run(args);
        }

        private static int Run(string[] args)
        {
            var logService = new LoggingService();
            var options = new SteamlessOptions();
            var detect = false;
            var file = string.Empty;
            var fileSpecified = false;
            var unknownOption = string.Empty;

            using var feed = new LogFeed(logService, () => options.VerboseOutput);
            feed.Changed += Program.WriteLogChange;

            Program.PrintHeader();

            foreach (var arg in args)
            {
                if (string.Equals(arg, "--detect", StringComparison.OrdinalIgnoreCase))
                    detect = true;
                else if (string.Equals(arg, "--quiet", StringComparison.OrdinalIgnoreCase))
                    options.VerboseOutput = false;
                else if (string.Equals(arg, "--keepbind", StringComparison.OrdinalIgnoreCase))
                    options.KeepBindSection = true;
                else if (string.Equals(arg, "--keepstub", StringComparison.OrdinalIgnoreCase))
                    options.ZeroDosStubData = false;
                else if (string.Equals(arg, "--dumppayload", StringComparison.OrdinalIgnoreCase))
                    options.DumpPayloadToDisk = true;
                else if (string.Equals(arg, "--dumpdrmp", StringComparison.OrdinalIgnoreCase))
                    options.DumpSteamDrmpToDisk = true;
                else if (string.Equals(arg, "--realign", StringComparison.OrdinalIgnoreCase))
                    options.DontRealignSections = false;
                else if (string.Equals(arg, "--recalcchecksum", StringComparison.OrdinalIgnoreCase))
                    options.RecalculateFileChecksum = true;
                else if (!arg.StartsWith("--"))
                {
                    if (fileSpecified)
                    {
                        Console.Error.WriteLine("Steamless.CLI: multiple input files specified; only one is allowed.");
                        return ExitBadArguments;
                    }

                    file = arg;
                    fileSpecified = true;
                }
                else if (string.IsNullOrEmpty(unknownOption))
                {
                    // A typo must not silently fall back to the defaults.
                    unknownOption = arg;
                }
            }

            if (!string.IsNullOrEmpty(unknownOption))
            {
                Console.Error.WriteLine($"Steamless.CLI: unknown option '{unknownOption}'.");
                Program.PrintHelp();
                return ExitBadArguments;
            }

            if (string.IsNullOrEmpty(file))
            {
                Program.PrintHelp();
                return ExitBadArguments;
            }

            if (!File.Exists(file) && !Directory.Exists(file))
            {
                logService.OnAddLogMessage(null, new LogMessageEventArgs($"Cannot find the input path: {file}", LogMessageType.Error));
                return ExitFileMissing;
            }

            var pluginDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");

            using var session = UnpackSession.Load(pluginDirectory, logService);

            if (session.PluginCount == 0)
                return ExitNoPlugins;

            if (detect)
                return Program.RunDetection(session, file);

            // The first entry is automatic: try each plugin in turn and stop at the first that
            // claims the file.
            var report = session.Run(new[] { file }, session.Selections[0], options, CancellationToken.None);

            // A folder expands to many files; the session reports each one, and the host adds the
            // total.
            if (report.Outcomes.Count > 1)
                feed.Add($"Finished: {report.Summary}.", report.Failed > 0 ? LogMessageType.Warning : LogMessageType.Information);

            if (report.Failed > 0)
                return ExitPluginFailed;

            return report.Unpacked > 0 ? ExitOk : ExitNoMatch;
        }

        /// <summary>
        /// Reports what the unpackers see, for tooling that needs detection without unpacking: the
        /// PE flavour, whether the file has a `.bind` section, and the plugins that claim it. The
        /// keys are stable, and the test corpus sorts files by them. A folder is walked with the
        /// same rules a run uses, and each file gets a `File:` line so the blocks can be told apart;
        /// a single file keeps the three lines the corpus scripts parse.
        /// </summary>
        private static int RunDetection(UnpackSession session, string file)
        {
            var files = session.FindInputs(new[] { file });

            if (files.Count == 0)
            {
                Console.Error.WriteLine($"Steamless.CLI: no executables found in {file}.");
                return ExitNoMatch;
            }

            for (var x = 0; x < files.Count; x++)
            {
                if (files.Count > 1)
                {
                    if (x > 0)
                        Console.WriteLine();

                    Console.WriteLine($"File: {files[x]}");
                }

                var result = session.Detect(files[x]);

                Console.WriteLine($"PE: {(result.HasImage ? result.Is64Bit ? "PE32+" : "PE32" : "invalid")}");
                Console.WriteLine($"Bind: {(result.HasBindSection ? "yes" : "no")}");
                Console.WriteLine($"Plugin: {(result.ClaimingPlugins.Count == 0 ? "none" : string.Join("; ", result.ClaimingPlugins))}");
            }

            return ExitOk;
        }
    }
}
