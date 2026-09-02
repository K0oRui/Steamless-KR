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
    using Steamless.API;
    using Steamless.API.Events;
    using Steamless.API.Model;
    using Steamless.API.Services;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    internal class Program
    {
        // Must match the API version defined in DataService.cs.
        private static readonly Version SteamlessApiVersion = new Version(1, 0);

        static void PrintHeader()
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

        static void PrintHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("    Steamless.CLI.exe [options] [file]\n\n");
            Console.WriteLine("Options:");
            Console.WriteLine("    --quiet          - Disables output of debug log messages.");
            Console.WriteLine("    --keepbind       - Keeps the .bind section in the unpacked file.");
            Console.WriteLine("    --keepstub       - Keeps the DOS stub in the unpacked file.");
            Console.WriteLine("    --dumppayload    - Dumps the stub payload to disk.");
            Console.WriteLine("    --dumpdrmp       - Dumps the SteamDRMP.dll to disk.");
            Console.WriteLine("    --realign        - Realigns the unpacked file sections.");
            Console.WriteLine("    --recalcchecksum - Recalculates the unpacked file checksum.");
            Console.WriteLine("    --exp            - Use experimental features.");
        }

        static List<SteamlessPlugin> GetSteamlessPlugins(LoggingService logService)
        {
            try
            {
                var plugins = new List<SteamlessPlugin>();

                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                foreach (var dll in Directory.GetFiles(path, "*.dll"))
                {
                    // The API assembly is not a plugin; skip it.
                    if (dll.EndsWith("steamless.api.dll", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        var asm = Assembly.Load(File.ReadAllBytes(dll));

                        var baseClass = asm.GetTypes().FirstOrDefault(t => t.BaseType == typeof(SteamlessPlugin));
                        if (baseClass == null)
                            continue;

                        var baseAttr = baseClass.GetCustomAttributes(typeof(SteamlessApiVersionAttribute), false);
                        if (baseAttr.Length == 0)
                            continue;

                        var apiVersion = (SteamlessApiVersionAttribute)baseAttr[0];
                        if (apiVersion.Version != SteamlessApiVersion)
                            continue;

                        var plugin = (SteamlessPlugin)Activator.CreateInstance(baseClass);
                        if (!plugin.Initialize(logService))
                            continue;

                        plugins.Add(plugin);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Steamless.CLI: failed to load plugin '{dll}': {ex.Message}");
                    }
                }

                return plugins.OrderBy(p => p.Name).ToList();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Steamless.CLI: failed to enumerate plugins: {ex.Message}");
                return new List<SteamlessPlugin>();
            }
        }

        static int Main(string[] args)
        {
            // AssemblyResolve override so plugin dependencies can be loaded from the Plugins folder.
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                try
                {
                    var name = e.Name.Contains(",") ? e.Name.Substring(0, e.Name.IndexOf(",", StringComparison.InvariantCultureIgnoreCase)) : e.Name.Replace(".dll", "");

                    // Satellite resource assemblies are not plugin dependencies.
                    if (name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                        return null;

                    var fullName = $"{Assembly.GetExecutingAssembly().EntryPoint.DeclaringType?.Namespace}.Embedded.{new AssemblyName(e.Name).Name}.dll";
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fullName))
                    {
                        if (stream == null)
                        {
                            var f = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", name + ".dll");
                            if (!File.Exists(f))
                                return null;
                            return Assembly.Load(File.ReadAllBytes(f));
                        }

                        var data = new byte[stream.Length];
                        stream.ReadExactly(data, 0, (int)stream.Length);
                        return Assembly.Load(data);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Steamless.CLI: failed to resolve assembly '{e.Name}': {ex.Message}");
                    return null;
                }
            };

            return Program.Run(args);
        }

        static int Run(string[] args)
        {
            var logService = new LoggingService();
            var opts = new SteamlessOptions();
            var file = string.Empty;
            var fileSpecified = false;

            logService.AddLogMessage += (sender, e) =>
            {
                if (!opts.VerboseOutput && e.MessageType == LogMessageType.Debug)
                    return;

                try
                {
                    if (sender != null)
                        e.Message = $"[{sender.GetType().Assembly.GetName().Name}] {e.Message}";
                    else
                        e.Message = $"[Steamless] {e.Message}";
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Steamless.CLI: failed to format log message: {ex.Message}");
                }

                Console.WriteLine(e.Message);
            };

            Program.PrintHeader();

            foreach (var arg in args)
            {
                if (string.Equals(arg, "--quiet", StringComparison.OrdinalIgnoreCase))
                    opts.VerboseOutput = false;
                else if (string.Equals(arg, "--keepbind", StringComparison.OrdinalIgnoreCase))
                    opts.KeepBindSection = true;
                else if (string.Equals(arg, "--keepstub", StringComparison.OrdinalIgnoreCase))
                    opts.ZeroDosStubData = false;
                else if (string.Equals(arg, "--dumppayload", StringComparison.OrdinalIgnoreCase))
                    opts.DumpPayloadToDisk = true;
                else if (string.Equals(arg, "--dumpdrmp", StringComparison.OrdinalIgnoreCase))
                    opts.DumpSteamDrmpToDisk = true;
                else if (string.Equals(arg, "--realign", StringComparison.OrdinalIgnoreCase))
                    opts.DontRealignSections = false;
                else if (string.Equals(arg, "--recalcchecksum", StringComparison.OrdinalIgnoreCase))
                    opts.RecalculateFileChecksum = true;
                else if (string.Equals(arg, "--exp", StringComparison.OrdinalIgnoreCase))
                    opts.UseExperimentalFeatures = true;
                else if (!arg.StartsWith("--"))
                {
                    if (fileSpecified)
                    {
                        Console.Error.WriteLine("Steamless.CLI: multiple input files specified; only one is allowed.");
                        return 1;
                    }

                    file = arg;
                    fileSpecified = true;
                }
            }

            if (string.IsNullOrEmpty(file))
            {
                Program.PrintHelp();
                return 1; // No input file
            }

            if (!File.Exists(file))
            {
                logService.OnAddLogMessage(null, new LogMessageEventArgs("Invalid input file given; cannot continue.", LogMessageType.Error));
                return 2; // File not found
            }

            var plugins = GetSteamlessPlugins(logService);
            plugins.ForEach(p => logService.OnAddLogMessage(null, new LogMessageEventArgs($"Loaded plugin: {p.Name} - by {p.Author} (v.{p.Version})", LogMessageType.Success)));

            if (plugins.Count == 0)
            {
                logService.OnAddLogMessage(null, new LogMessageEventArgs("No plugins were loaded; be sure to fully extract Steamless before running!", LogMessageType.Error));
                return 3; // No plugins loaded
            }

            foreach (var p in plugins)
            {
                try
                {
                    if (p.CanProcessFile(file))
                    {
                        var ret = p.ProcessFile(file, opts);

                        logService.OnAddLogMessage(null, !ret
                            ? new LogMessageEventArgs("Failed to unpack file.", LogMessageType.Error)
                            : new LogMessageEventArgs("Successfully unpacked file!", LogMessageType.Success));

                        if (ret) return 0;
                    }
                }
                catch (Exception ex)
                {
                    logService.OnAddLogMessage(null, new LogMessageEventArgs($"Plugin {p.Name} threw an unexpected exception: {ex.Message}", LogMessageType.Error));
                }
                finally
                {
                    p.Dispose();
                }
            }

            logService.OnAddLogMessage(null, new LogMessageEventArgs("All unpackers failed to unpack file.", LogMessageType.Error));
            return 4; // All unpackers failed
        }
    }
}
