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

namespace Steamless.ViewModel
{
    using API.Events;
    using API.Model;
    using API.Services;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.ApplicationLifetimes;
    using Avalonia.Platform.Storage;
    using Avalonia.Threading;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Model;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public partial class MainWindowViewModel : ObservableObject
    {
        private const int StartupDelayMilliseconds = 400;
        private const int MaxLogEntries = 5000;

        private readonly LoggingService m_LoggingService;
        private readonly LogFeed m_LogFeed;
        private readonly LogLines m_LogLines;

        private UnpackSession m_Session;

        private UnpackRunner m_Runner;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowSplashView))]
        [NotifyPropertyChangedFor(nameof(ShowMainView))]
        [NotifyPropertyChangedFor(nameof(ShowAboutViewPanel))]
        private ApplicationState m_State;
        [ObservableProperty] private Version m_SteamlessVersion;
        [ObservableProperty] private string m_StatusText;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowMainView))]
        [NotifyPropertyChangedFor(nameof(ShowAboutViewPanel))]
        private bool m_ShowAboutView;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedPlugin))]
        private ObservableCollection<PluginSelection> m_Plugins;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedPlugin))]
        private int m_SelectedPluginIndex;
        [ObservableProperty] private string m_InputPath;
        [ObservableProperty] private SteamlessOptions m_Options;

        /// <summary>
        /// The bindable lines the panel shows. The feed owns which lines exist; this list only
        /// replays its changes on the UI thread, so the view never guesses at a trim.
        /// </summary>
        public ReadOnlyObservableCollection<LogEntry> Log => this.m_LogLines.Items;

        public PluginSelection SelectedPlugin =>
            this.Plugins != null && this.SelectedPluginIndex >= 0 && this.SelectedPluginIndex < this.Plugins.Count
                ? this.Plugins[this.SelectedPluginIndex]
                : null;

        /// <summary>
        /// The runner owns whether a run is in flight; the view needs to hear when that changes.
        /// </summary>
        public bool IsUnpacking => this.m_Runner != null && this.m_Runner.IsRunning;

        public bool ShowSplashView => this.State == ApplicationState.Initializing;

        public bool ShowMainView => this.State == ApplicationState.Running && !this.ShowAboutView;

        public bool ShowAboutViewPanel => this.State == ApplicationState.Running && this.ShowAboutView;

        public MainWindowViewModel(LoggingService logService)
        {
            this.m_LoggingService = logService;

            this.State = ApplicationState.Initializing;
            this.Options = new SteamlessOptions();
            this.InputPath = string.Empty;
            this.ShowAboutView = false;

            this.m_LogFeed = new LogFeed(logService, () => this.Options.VerboseOutput, MaxLogEntries);
            this.m_LogLines = new LogLines(this.m_LogFeed, MainWindowViewModel.OnUiThread);

            this.m_LogFeed.Add("Steamless-KR (c) 2015 - 2024 atom0s, fork by K0oRui", LogMessageType.Information);
            this.m_LogFeed.Add("Upstream: atom0s/Steamless - This fork: K0oRui/Steamless-KR", LogMessageType.Debug);

            _ = this.InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            this.StatusText = "Initializing...";
            this.SteamlessVersion = ReadVersion();

            try
            {
                var plugins = await Task.Run(this.LoadPlugins);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    this.Plugins = plugins;
                    this.SelectedPluginIndex = 0;
                });
            }
            catch (Exception ex)
            {
                this.m_LogFeed.Add($"Could not load plugins: {ex.Message}", LogMessageType.Error);
            }

            // Keep the splash up long enough to read instead of flashing past on fast machines.
            await Task.Delay(StartupDelayMilliseconds);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (this.State == ApplicationState.Initializing)
                    this.State = ApplicationState.Running;

                this.StatusText = string.Empty;
            });
        }

        private static Version ReadVersion()
        {
            var version = typeof(MainWindowViewModel).Assembly.GetName().Version;
            return version == null ? new Version(0, 0, 0) : new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
        }

        private ObservableCollection<PluginSelection> LoadPlugins()
        {
            // Plugin DLLs pull their dependencies from the same folder, so the resolver has to be
            // in place before the first load. The session attaches it and owns the plugins for the
            // life of the app.
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            this.m_Session = UnpackSession.Load(path, this.m_LoggingService);
            this.m_Runner = new UnpackRunner(this.m_Session, this.m_LogFeed);
            this.m_Runner.RunningChanged += this.OnRunStateChanged;

            return new ObservableCollection<PluginSelection>(this.m_Session.Selections);
        }

        /// <summary>
        /// The feed announces on whichever thread logged; the bound list has to change on the UI one.
        /// </summary>
        private static void OnUiThread(Action action)
        {
            if (Dispatcher.UIThread.CheckAccess())
                action();
            else
                Dispatcher.UIThread.Post(action);
        }

        private void OnRunStateChanged(object sender, EventArgs e)
        {
            MainWindowViewModel.OnUiThread(() => this.OnPropertyChanged(nameof(IsUnpacking)));
        }

        private static Window GetMainWindow()
        {
            return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        }

        public void Shutdown()
        {
            this.m_Runner?.Cancel();

            // A run in flight is cancelled above; disposing its plugins underneath it would
            // race with the worker, and the process is exiting anyway.
            if (!this.IsUnpacking)
                this.m_Session?.Dispose();

            // Stop taking changes before the feed stops making them, so a late plugin message
            // cannot reach a list the window has already let go of.
            this.m_LogLines.Dispose();
            this.m_LogFeed.Dispose();

            this.State = ApplicationState.Closing;
        }

        [RelayCommand]
        private void ToggleAboutView()
        {
            this.ShowAboutView = !this.ShowAboutView;
        }

        [RelayCommand]
        private void OpenHyperlink(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        [RelayCommand]
        private async Task SelectFileAsync()
        {
            var window = GetMainWindow();
            if (window == null)
                return;

            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Executable Files (*.exe)") { Patterns = new[] { "*.exe" } },
                    new FilePickerFileType("All Files (*.*)") { Patterns = new[] { "*.*" } }
                },
                SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(AppDomain.CurrentDomain.BaseDirectory)
            });

            if (files.Count > 0)
                this.InputPath = files[0].TryGetLocalPath();
        }

        [RelayCommand]
        private async Task SelectFolderAsync()
        {
            var window = GetMainWindow();
            if (window == null)
                return;

            var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(AppDomain.CurrentDomain.BaseDirectory)
            });

            if (folders.Count > 0)
                this.InputPath = folders[0].TryGetLocalPath();
        }

        [RelayCommand]
        private async Task UnpackFileAsync()
        {
            if (string.IsNullOrWhiteSpace(this.InputPath))
            {
                this.m_LogFeed.Add("Select a file or folder first.", LogMessageType.Warning);
                return;
            }

            var selection = this.SelectedPlugin;
            if (this.m_Runner == null || selection == null)
            {
                this.m_LogFeed.Add("Plugins are still loading.", LogMessageType.Warning);
                return;
            }

            await this.m_Runner.RunAsync(new[] { this.InputPath }, selection, this.Options);
        }

        [RelayCommand]
        private void CancelUnpack()
        {
            this.m_Runner?.Cancel();
        }

        [RelayCommand]
        private void ClearLog()
        {
            this.m_LogFeed.Clear();
        }
    }
}
