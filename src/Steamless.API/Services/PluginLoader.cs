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

namespace Steamless.API.Services
{
    using Events;
    using Model;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Loads plugin assemblies from a folder. The CLI and the GUI share it, so both see the same
    /// plugins.
    /// </summary>
    public static class PluginLoader
    {
        private static readonly Version ApiVersion = SteamlessApiVersionAttribute.ApiVersion;
        private static bool s_ResolverAttached;

        /// <summary>
        /// Lets plugin dependencies load from the Plugins folder instead of next to the host
        /// executable. Both hosts need the same rule.
        /// </summary>
        public static void AttachAssemblyResolver(Action<string> onError = null)
        {
            if (s_ResolverAttached)
                return;

            s_ResolverAttached = true;

            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                try
                {
                    var name = e.Name;
                    var comma = name.IndexOf(',');
                    if (comma >= 0)
                        name = name.Substring(0, comma);
                    name = name.Replace(".dll", string.Empty);

                    // Satellite resource assemblies are not plugin dependencies.
                    if (name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                        return null;

                    var file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", name + ".dll");

                    // Map the image from disk. Loading from a byte[] keeps a copy of the whole
                    // assembly on the managed heap for the life of the process.
                    return File.Exists(file) ? Assembly.LoadFrom(file) : null;
                }
                catch (Exception ex)
                {
                    onError?.Invoke($"failed to resolve assembly '{e.Name}': {ex.Message}");
                    return null;
                }
            };
        }

        public static List<SteamlessPlugin> LoadPlugins(string directory, LoggingService logService)
        {
            var plugins = new List<SteamlessPlugin>();

            try
            {
                if (!Directory.Exists(directory))
                    return plugins;

                foreach (var dll in Directory.GetFiles(directory, "*.dll"))
                {
                    // The API assembly is not a plugin; skip it.
                    if (dll.EndsWith("steamless.api.dll", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        // Map the image instead of copying it into a byte[] the runtime keeps.
                        // This locks the plugin DLL for the life of the process, which is fine
                        // because a second instance exits and plugins need a restart anyway.
                        var asm = Assembly.LoadFrom(Path.GetFullPath(dll));

                        // Dependency assemblies such as iced.dll are not plugins; skip them silently.
                        if (!asm.GetReferencedAssemblies().Any(a => string.Equals(a.Name, "Steamless.API", StringComparison.OrdinalIgnoreCase)))
                            continue;

                        var baseClass = asm.GetTypes().FirstOrDefault(t => !t.IsAbstract && typeof(SteamlessPlugin).IsAssignableFrom(t));
                        if (baseClass == null)
                            continue;

                        var baseAttr = baseClass.GetCustomAttributes(typeof(SteamlessApiVersionAttribute), false);
                        if (baseAttr.Length == 0)
                        {
                            Warn(logService, $"Cannot load {Path.GetFileName(dll)}: no SteamlessApiVersion attribute.");
                            continue;
                        }

                        var apiVersion = (SteamlessApiVersionAttribute)baseAttr[0];
                        if (apiVersion.Version != ApiVersion)
                        {
                            Warn(logService, $"Cannot load {Path.GetFileName(dll)}: plugin targets API {apiVersion.Version}, this build uses {ApiVersion}.");
                            continue;
                        }

                        var plugin = Activator.CreateInstance(baseClass) as SteamlessPlugin;
                        if (plugin == null)
                            continue;

                        if (!plugin.Initialize(logService))
                        {
                            Warn(logService, $"Cannot load {Path.GetFileName(dll)}: the plugin did not initialize.");
                            continue;
                        }

                        plugins.Add(plugin);
                    }
                    catch (Exception ex)
                    {
                        logService.OnAddLogMessage(null, new LogMessageEventArgs($"Cannot load {Path.GetFileName(dll)} as a plugin: {ex.Message}", LogMessageType.Error));
                    }
                }
            }
            catch (Exception ex)
            {
                logService.OnAddLogMessage(null, new LogMessageEventArgs($"Cannot list plugins in {directory}: {ex.Message}", LogMessageType.Error));
            }

            return plugins.OrderBy(p => p.Name).ToList();
        }

        private static void Warn(LoggingService logService, string message)
        {
            logService.OnAddLogMessage(null, new LogMessageEventArgs(message, LogMessageType.Warning));
        }
    }
}
