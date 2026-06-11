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
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Win32;
    using Model;
    using Model.Tasks;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Documents;
    using System.Windows.Input;

    public partial class MainWindowViewModel : ObservableObject
    {
        /// <summary>
        /// Internal data service instance.
        /// </summary>
        private readonly IDataService m_DataService;

        /// <summary>
        /// Internal logging service instance.
        /// </summary>
        private readonly LoggingService m_LoggingService;

        /// <summary>
        /// Internal thread used to process tasks.
        /// </summary>
        private Thread m_TaskThread;

        [ObservableProperty] private ApplicationState _state;
        [ObservableProperty] private Version _steamlessVersion;
        [ObservableProperty] private BaseTask _currentTask;
        [ObservableProperty] private ConcurrentBag<BaseTask> _tasks;
        [ObservableProperty] private bool _showAboutView;
        [ObservableProperty] private ObservableCollection<SteamlessPlugin> _plugins;
        [ObservableProperty] private int _selectedPluginIndex;
        [ObservableProperty] private string _inputFilePath;
        [ObservableProperty] private SteamlessOptions _options;
        [ObservableProperty] private ObservableCollection<LogMessageEventArgs> _log;

        /// <summary>
        /// Default Constructor
        /// </summary>
        /// <param name="dataService"></param>
        /// <param name="logService"></param>
        public MainWindowViewModel(IDataService dataService, LoggingService logService)
        {
            // Store the data service instance..
            this.m_DataService = dataService;
            this.m_LoggingService = logService;

            // Initialize the model..
            this.State = ApplicationState.Initializing;
            this.Tasks = new ConcurrentBag<BaseTask>();
            this.Options = new SteamlessOptions();
            this.Log = new ObservableCollection<LogMessageEventArgs>();
            this.ShowAboutView = false;
            this.InputFilePath = string.Empty;

            // Attach logging service events..
            logService.AddLogMessage += this.AddLogMessage;
            logService.ClearLogMessages += this.ClearLogMessages;

            this.AddLogMessage(this, new LogMessageEventArgs("Steamless (c) 2015 - 2024 atom0s [atom0s@live.com]", LogMessageType.Debug));
            this.AddLogMessage(this, new LogMessageEventArgs("Website: http://atom0s.com/", LogMessageType.Debug));

            // Initialize this model..
            this.Initialize();
        }

        /// <summary>
        /// Internal async call to load the main view model.
        /// </summary>
        private async void Initialize()
        {
            // Obtain the Steamless version..
            this.CurrentTask = new StatusTask("Initializing..");
            this.SteamlessVersion = await this.m_DataService.GetSteamlessVersion();

            // Load the Steamless plugins..
            this.Tasks.Add(new LoadPluginsTask(this.m_DataService, this.m_LoggingService));

            // Start the application..
            this.Tasks.Add(new StartSteamlessTask());

            // Start the tasks thread..
            if (this.m_TaskThread != null)
                return;
            this.m_TaskThread = new Thread(this.ProcessTasksThread) { IsBackground = true };
            this.m_TaskThread.Start();
        }

        /// <summary>
        /// Thread callback to process application tasks.
        /// </summary>
        private async void ProcessTasksThread()
        {
            while (Interlocked.CompareExchange(ref this.m_TaskThread, null, null) != null && this.State != ApplicationState.Closing)
            {
                // Obtain a task from the task list..
                if (this.Tasks.TryTake(out var task))
                {
                    this.CurrentTask = task;
                    await this.CurrentTask.StartTask();

                    // Consume LoadPluginsTask result on the UI thread..
                    if (task is LoadPluginsTask lpt && lpt.LoadedPlugins != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            this.Plugins = lpt.LoadedPlugins;
                            this.SelectedPluginIndex = 0;
                        });
                    }
                }
                else
                {
                    // No tasks left, set application to a running state..
                    if (this.State == ApplicationState.Initializing)
                        this.State = ApplicationState.Running;
                }

                Thread.Sleep(200);
            }
        }

        /// <summary>
        /// Adds a message to the message log.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddLogMessage(object sender, LogMessageEventArgs e)
        {
            // Do not log debug messages if verbose output is disabled..
            if (!this.Options.VerboseOutput && e.MessageType == LogMessageType.Debug)
                return;

            // Check if we need to invoke from the dispatcher thread..
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => this.AddLogMessage(sender, e));
                return;
            }

            // Prefix the parent to the message..
            try
            {
                if (sender != null)
                {
                    var baseName = sender.GetType().Assembly.GetName().Name;
                    e.Message = $"[{baseName}] {e.Message}";
                }
                else
                    e.Message = "[Unknown] " + e.Message;
            }
            catch
            {
                // Do nothing with this exception..
            }

            this.Log.Add(e);
        }

        /// <summary>
        /// Clears the message log.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearLogMessages(object sender, EventArgs e)
        {
            // Check if we need to invoke from the dispatcher thread..
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => this.ClearLogMessages(sender, e));
                return;
            }

            this.Log.Clear();
        }

        [RelayCommand]
        private void WindowClose()
        {
            // Set the launcher state to closing..
            this.State = ApplicationState.Closing;

            // Shutdown the application..
            Application.Current.Shutdown(0);
        }

        [RelayCommand]
        private static void WindowMinimize()
        {
            // Minimize the window..
            if (Application.Current.MainWindow != null)
                Application.Current.MainWindow.WindowState = WindowState.Minimized;
        }

        [RelayCommand]
        private void WindowMouseDown(MouseButtonEventArgs args)
        {
            if (Application.Current.MainWindow != null)
                Application.Current.MainWindow.DragMove();
        }

        [RelayCommand]
        private void ToggleAboutView()
        {
            this.ShowAboutView = !this.ShowAboutView;
        }

        [RelayCommand]
        private void OpenHyperlink(object parameter)
        {
            if (parameter is Hyperlink link)
                Process.Start(new ProcessStartInfo(link.NavigateUri.AbsoluteUri) { UseShellExecute = true });
        }

        [RelayCommand]
        private void DragDrop(DragEventArgs args)
        {
            args.Handled = true;

            // Check for files being dragged..
            if (args.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Ensure only 1 file is being dropped..
                var files = (string[])args.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length >= 1)
                    this.InputFilePath = files[0];
            }
        }

        [RelayCommand]
        private void PreviewDragEnter(DragEventArgs args)
        {
            args.Handled = true;

            // Check for files being dragged..
            if (args.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Ensure only 1 file is being dropped..
                var files = (string[])args.Data.GetData(DataFormats.FileDrop);
                args.Effects = files != null && files.Length == 1 ? DragDropEffects.Move : DragDropEffects.None;
            }
            else
                args.Effects = DragDropEffects.None;
        }

        [RelayCommand]
        private void BrowseForInputFile()
        {
            // Display the find file dialog..
            var ofd = new OpenFileDialog
            {
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = "*.exe",
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                FilterIndex = 0,
                InitialDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),
                Multiselect = false,
                RestoreDirectory = true
            };

            // Update the input file path..
            var showDialog = ofd.ShowDialog();
            if (showDialog != null && (bool)showDialog)
                this.InputFilePath = ofd.FileName;
        }

        [RelayCommand]
        private async Task UnpackFileAsync()
        {
            await Task.Run(() =>
            {
                // Validation checks..
                if (this.SelectedPluginIndex == -1)
                    return;
                if (this.SelectedPluginIndex > this.Plugins.Count)
                    return;
                if (string.IsNullOrEmpty(this.InputFilePath))
                    return;

                try
                {
                    // Select the plugin..
                    var plugin = this.Plugins[this.SelectedPluginIndex];
                    if (plugin == null)
                        throw new Exception("Invalid plugin selected.");

                    // Allow the plugin to process the file..
                    var siblings = this.Plugins.Where(p => p != plugin);
                    if (plugin.CanProcessFile(this.InputFilePath))
                        this.AddLogMessage(this, !plugin.ProcessFile(this.InputFilePath, this.Options, siblings) ? new LogMessageEventArgs("Failed to unpack file.", LogMessageType.Error) : new LogMessageEventArgs("Successfully unpacked file!", LogMessageType.Success));
                    else
                        this.AddLogMessage(this, new LogMessageEventArgs("Failed to unpack file.", LogMessageType.Error));
                }
                catch (Exception ex)
                {
                    this.AddLogMessage(this, new LogMessageEventArgs("Caught unhandled exception trying to unpack file.", LogMessageType.Error));
                    this.AddLogMessage(this, new LogMessageEventArgs("Exception:", LogMessageType.Error));
                    this.AddLogMessage(this, new LogMessageEventArgs(ex.Message, LogMessageType.Error));
                }
            });
        }

        [RelayCommand]
        private void ClearLog()
        {
            this.ClearLogMessages(this, EventArgs.Empty);
        }
    }
}
