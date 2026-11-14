#nullable enable

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
    using PE;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;

    /// <summary>
    /// A run of the unpackers. The session owns the plugin set for the life of a host, picks the
    /// plugin for a file, expands folders into files, and reports what happened. The CLI adapts it
    /// to arguments and an exit code; the GUI to a button and a log panel.
    /// </summary>
    public sealed class UnpackSession : IDisposable
    {
        /// <summary>
        /// Paths differ by case only on Windows, so the duplicate check and the sort follow the OS.
        /// On Linux, GAME.EXE and game.exe are two files and both get unpacked.
        /// </summary>
        private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        private readonly List<SteamlessPlugin> m_Plugins;
        private readonly LoggingService m_LogService;
        private bool m_Disposed;

        /// <summary>
        /// A session over a plugin set the caller already holds. The session takes ownership and
        /// disposes the plugins. Hosts use <see cref="Load"/>; this constructor is for a caller
        /// with its own set, such as a test.
        /// </summary>
        public UnpackSession(IReadOnlyList<SteamlessPlugin> plugins, LoggingService logService)
        {
            if (plugins == null)
                throw new ArgumentNullException(nameof(plugins));
            if (logService == null)
                throw new ArgumentNullException(nameof(logService));

            this.m_Plugins = new List<SteamlessPlugin>(plugins);
            this.m_LogService = logService;
            this.Selections = UnpackSession.BuildSelections(this.m_Plugins);
        }

        /// <summary>The entries a host shows in its picker. Automatic is always first.</summary>
        public IReadOnlyList<PluginSelection> Selections { get; }

        public int PluginCount => this.m_Plugins.Count;

        /// <summary>
        /// The files a run would offer to the plugins, in the order it would offer them. A host
        /// that reports on inputs without unpacking gets the same expansion <see cref="Run"/> uses.
        /// </summary>
        public IReadOnlyList<string> FindInputs(IReadOnlyList<string> inputs)
        {
            if (inputs == null)
                throw new ArgumentNullException(nameof(inputs));

            this.ThrowIfDisposed();
            return UnpackSession.ExpandInputs(inputs, CancellationToken.None);
        }

        /// <summary>
        /// Loads every plugin in a folder and keeps them for the life of the session. The assembly
        /// resolver has to be in place before the first load, so it is attached here instead of in
        /// each host.
        /// </summary>
        public static UnpackSession Load(string pluginDirectory, LoggingService logService)
        {
            if (logService == null)
                throw new ArgumentNullException(nameof(logService));

            PluginLoader.AttachAssemblyResolver();

            var plugins = PluginLoader.LoadPlugins(pluginDirectory, logService);

            foreach (var plugin in plugins)
                logService.OnAddLogMessage(null, new LogMessageEventArgs($"Loaded plugin: {plugin.Name} (by {plugin.Author})", LogMessageType.Success));

            if (plugins.Count == 0)
                logService.OnAddLogMessage(null, new LogMessageEventArgs("No unpacker plugins found. Put the plugin DLLs in the Plugins folder next to the host executable.", LogMessageType.Warning));

            return new UnpackSession(plugins, logService);
        }

        /// <summary>
        /// Expands the inputs into files and unpacks each one. A folder is walked for executables;
        /// a file is taken as given. Options are frozen for the run, so a host that toggles a
        /// switch mid-run cannot mix settings.
        /// </summary>
        public UnpackReport Run(IReadOnlyList<string> inputs, PluginSelection selection, SteamlessOptions options, CancellationToken token)
        {
            if (inputs == null)
                throw new ArgumentNullException(nameof(inputs));
            if (selection == null)
                throw new ArgumentNullException(nameof(selection));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            this.ThrowIfDisposed();

            var settings = options.Snapshot();
            var files = UnpackSession.ExpandInputs(inputs, token);

            if (files.Count == 0)
            {
                this.Log("No executables found in the selection.", LogMessageType.Warning);
                return new UnpackReport(new UnpackOutcome[0]);
            }

            if (files.Count > 1)
                this.Log($"Found {files.Count} executables to unpack.", LogMessageType.Information);

            var outcomes = new UnpackOutcome[files.Count];

            for (var x = 0; x < files.Count; x++)
            {
                token.ThrowIfCancellationRequested();

                outcomes[x] = new UnpackOutcome(files[x], this.Unpack(files[x], selection, settings, token));
            }

            return new UnpackReport(outcomes);
        }

        /// <summary>
        /// Reports what the unpackers see in a file without unpacking it. The file is parsed once
        /// and claimed through the same step <see cref="Run"/> uses, so the two cannot drift apart.
        /// A plugin that throws while deciding does not claim here, while a run reports the error
        /// and moves on, which is why the caller decides what an error means.
        /// </summary>
        public DetectionResult Detect(string file)
        {
            this.ThrowIfDisposed();

            var loaded = PeImage.Load(file);
            var image = loaded.Image;
            var claimants = new List<string>();

            if (image != null)
            {
                foreach (var plugin in this.m_Plugins)
                {
                    if (UnpackSession.TryClaim(plugin, image, out _) != ClaimOutcome.Claimed)
                        continue;

                    try
                    {
                        claimants.Add(plugin.DetectVariant(image));
                    }
                    catch (Exception)
                    {
                        // A plugin that cannot name its variant is left out, the same way a plugin
                        // that cannot decide does not claim.
                    }
                }
            }

            var hasImage = image != null;
            return new DetectionResult(hasImage, image != null && image.Is64Bit, image != null && image.HasSection(".bind"), claimants.ToArray());
        }

        public void Dispose()
        {
            if (this.m_Disposed)
                return;

            this.m_Disposed = true;

            foreach (var plugin in this.m_Plugins)
                plugin.Dispose();
        }

        private static IReadOnlyList<PluginSelection> BuildSelections(List<SteamlessPlugin> plugins)
        {
            var selections = new List<PluginSelection>(plugins.Count + 1) { PluginSelection.Automatic() };

            foreach (var plugin in plugins)
            {
                var variants = plugin.Variants;

                // A plugin with one layout keeps its name and is not pinned, so an explicit pick
                // does not have to name a variant.
                if (variants == null || variants.Count < 2)
                {
                    selections.Add(PluginSelection.For(plugin));
                    continue;
                }

                foreach (var variant in variants)
                    selections.Add(PluginSelection.For(plugin, variant));
            }

            return selections;
        }

        private static List<string> ExpandInputs(IReadOnlyList<string> inputs, CancellationToken token)
        {
            var files = new List<string>();

            foreach (var input in inputs)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    if (Directory.Exists(input))
                        UnpackSession.CollectExecutables(input, files, token);
                    else if (File.Exists(input))
                        files.Add(input);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // Ignore inputs that disappear or cannot be read.
                }
            }

            var unique = new List<string>();
            var seen = new HashSet<string>(UnpackSession.PathComparer);

            foreach (var file in files)
            {
                if (seen.Add(file))
                    unique.Add(file);
            }

            unique.Sort(UnpackSession.PathComparer);
            return unique;
        }

        private static void CollectExecutables(string directory, List<string> files, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    token.ThrowIfCancellationRequested();

                    // The extension match has to be case insensitive: the OS decides for "*.exe",
                    // and on Linux an upper-case .EXE would never be offered.
                    if (!file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Skip the output of an earlier run; it is not an input.
                    if (file.EndsWith(".unpacked.exe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    files.Add(file);
                }

                foreach (var subDirectory in new DirectoryInfo(directory).EnumerateDirectories())
                {
                    token.ThrowIfCancellationRequested();

                    // Skip junctions and symlinks so a folder tree cannot loop back on itself.
                    if ((subDirectory.Attributes & FileAttributes.ReparsePoint) != 0)
                        continue;

                    UnpackSession.CollectExecutables(subDirectory.FullName, files, token);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip folders the current user cannot read.
            }
            catch (IOException)
            {
                // Skip folders that disappear or are otherwise inaccessible.
            }
        }

        /// <summary>
        /// Reads the file once, then hands the parsed image to the plugins. A file that cannot be
        /// parsed is reported with the parser's reason and counts as unclaimed.
        /// </summary>
        private UnpackStatus Unpack(string path, PluginSelection selection, SteamlessOptions options, CancellationToken token)
        {
            var loaded = PeImage.Load(path);
            if (!loaded.Success)
            {
                this.Log($"{path}: {loaded.Message}.", LogMessageType.Error);
                return UnpackStatus.Skipped;
            }

            var image = loaded.Image!;

            return selection.IsAutomatic
                ? this.UnpackAutomatic(image, options, token)
                : this.UnpackWith(image, selection, options, token);
        }

        /// <summary>
        /// Tries every plugin in picker order and stops at the first one that claims the file and
        /// unpacks it. A plugin that claims and fails is reported, then the next one gets a turn.
        /// </summary>
        private UnpackStatus UnpackAutomatic(PeImage image, SteamlessOptions options, CancellationToken token)
        {
            var claimed = false;

            foreach (var plugin in this.m_Plugins)
            {
                token.ThrowIfCancellationRequested();

                var claim = UnpackSession.TryClaim(plugin, image, out var error);

                if (claim == ClaimOutcome.Rejected)
                    continue;

                if (claim == ClaimOutcome.Errored)
                {
                    // A plugin that threw while deciding engaged the file: the next plugin still
                    // gets a turn, but the file is reported as failed rather than unmatched.
                    claimed = true;
                    this.Log($"{image.FilePath}: {error!.Message}", LogMessageType.Error);
                    image = UnpackSession.Reload(image);
                    continue;
                }

                claimed = true;

                try
                {
                    if (plugin.ProcessFile(image, options))
                        return this.ReportUnpacked(image.FilePath);

                    this.ReportFailed(image.FilePath);
                }
                catch (Exception ex)
                {
                    this.Log($"{image.FilePath}: {ex.Message}", LogMessageType.Error);
                }

                // A plugin that claimed the file can change the image before it gives up, so the
                // next plugin gets the file as it is on disk. A plugin that did not claim never
                // touched the image.
                image = UnpackSession.Reload(image);
            }

            if (!claimed)
                this.ExplainNoClaim(image);

            return claimed ? UnpackStatus.Failed : UnpackStatus.Skipped;
        }

        /// <summary>
        /// Re-reads the file after a failed attempt. A file that is gone or unreadable keeps the
        /// image the run already has, which the next plugin will not claim.
        /// </summary>
        private static PeImage Reload(PeImage image)
        {
            var loaded = PeImage.Load(image.FilePath);
            return loaded.Success ? loaded.Image! : image;
        }

        /// <summary>
        /// Asks one plugin whether it claims the image and contains anything it throws. What an
        /// errored attempt means is the caller's call: detection lists claims, while a run treats
        /// the error as engaged and keeps going.
        /// </summary>
        private static ClaimOutcome TryClaim(SteamlessPlugin plugin, PeImage image, out Exception? error)
        {
            error = null;

            try
            {
                return plugin.CanProcessFile(image) ? ClaimOutcome.Claimed : ClaimOutcome.Rejected;
            }
            catch (Exception ex)
            {
                error = ex;
                return ClaimOutcome.Errored;
            }
        }

        private UnpackStatus UnpackWith(PeImage image, PluginSelection selection, SteamlessOptions options, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var plugin = selection.Plugin!;
            var claim = UnpackSession.TryClaim(plugin, image, out var error);

            if (claim == ClaimOutcome.Errored)
            {
                this.Log($"{image.FilePath}: {error!.Message}", LogMessageType.Error);
                return UnpackStatus.Failed;
            }

            if (claim == ClaimOutcome.Rejected)
            {
                this.ExplainNoClaim(image);
                return UnpackStatus.Skipped;
            }

            if (selection.Variant != null)
            {
                string detected;

                try
                {
                    detected = plugin.DetectVariant(image);
                }
                catch (Exception ex)
                {
                    this.Log($"{image.FilePath}: {ex.Message}", LogMessageType.Error);
                    return UnpackStatus.Failed;
                }

                if (!string.Equals(detected, selection.Variant, StringComparison.Ordinal))
                {
                    this.Log($"{image.FilePath}: this file is {detected}, not the selected {selection.Variant}.", LogMessageType.Warning);
                    return UnpackStatus.Skipped;
                }
            }

            try
            {
                return plugin.ProcessFile(image, options) ? this.ReportUnpacked(image.FilePath) : this.ReportFailed(image.FilePath);
            }
            catch (Exception ex)
            {
                this.Log($"{image.FilePath}: {ex.Message}", LogMessageType.Error);
                return UnpackStatus.Failed;
            }
        }

        private UnpackStatus ReportUnpacked(string file)
        {
            this.Log($"Unpacked: {file}", LogMessageType.Success);
            return UnpackStatus.Unpacked;
        }

        private UnpackStatus ReportFailed(string file)
        {
            this.Log($"Could not unpack: {file}", LogMessageType.Error);
            return UnpackStatus.Failed;
        }

        /// <summary>
        /// Says why nothing claimed a valid image. A file that could not be parsed never gets
        /// here; its reason was logged where it failed.
        /// </summary>
        private void ExplainNoClaim(PeImage image)
        {
            this.Log($"No matching plugin for: {image.FilePath}", LogMessageType.Warning);

            if (!image.HasSection(".bind"))
            {
                this.Log(string.Empty, LogMessageType.Error);
                this.Log("This file is not packed with SteamStub.", LogMessageType.Error);
                this.Log("The .bind section is missing.", LogMessageType.Error);
                this.Log(string.Empty, LogMessageType.Error);
                return;
            }

            this.Log("The SteamStub variant may not be supported by any installed plugin.", LogMessageType.Error);
        }

        private void Log(string message, LogMessageType type)
        {
            this.m_LogService.OnAddLogMessage(null, new LogMessageEventArgs(message, type));
        }

        private void ThrowIfDisposed()
        {
            if (this.m_Disposed)
                throw new ObjectDisposedException(nameof(UnpackSession));
        }

        /// <summary>What one plugin said about the image it was shown.</summary>
        private enum ClaimOutcome
        {
            Rejected,
            Claimed,
            Errored
        }
    }
}
