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

namespace Steamless
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Pipes;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Avalonia.Controls;
    using Avalonia.Logging;
    using Avalonia.Threading;

    /// <summary>
    /// Keeps the app to one process per desktop session. A second launch asks the running
    /// instance to come forward and then exits.
    /// </summary>
    internal static class SingleInstance
    {
        private const string MutexName = "Steamless-KR.Instance";
        private const string ActivateName = "Steamless-KR.Activate";
        private const int SwRestore = 9;

        private static Mutex? s_Mutex;
        private static bool s_Reported;

        /// <summary>
        /// Takes ownership of the instance. Returns false when another process already has it,
        /// after telling that process to raise its window.
        /// </summary>
        public static bool TryAcquire()
        {
            var created = false;

            try
            {
                s_Mutex = new Mutex(true, MutexName, out created);
            }
            catch (UnauthorizedAccessException)
            {
                // A copy started with different rights still owns the name.
            }

            if (!created)
            {
                SignalRunningInstance();
                s_Mutex?.Dispose();
                s_Mutex = null;
                return false;
            }

            return true;
        }

        /// <summary>Starts listening for later launches and binds them to the main window.</summary>
        public static void Listen(Window window)
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await using var server = new NamedPipeServerStream(
                            ActivateName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                        await server.WaitForConnectionAsync().ConfigureAwait(false);
                        Dispatcher.UIThread.Post(() => BringToFront(window));
                    }
                    catch (Exception ex)
                    {
                        // A broken pipe must not end the listener, and a pause keeps a
                        // permanent failure from spinning the thread. Anything unexpected
                        // is reported once, so a dead listener cannot fail silently.
                        if (ex is not (IOException or UnauthorizedAccessException))
                            SingleInstance.ReportOnce(ex.Message);

                        await Task.Delay(250).ConfigureAwait(false);
                    }
                }
            });
        }

        private static void ReportOnce(string detail)
        {
            if (s_Reported)
                return;

            s_Reported = true;
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)
                ?.Log(typeof(SingleInstance), "The activation listener hit an unexpected error and keeps retrying. {Detail}", detail);
        }

        private static void SignalRunningInstance()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                    AllowForeground();

                using var client = new NamedPipeClientStream(".", ActivateName, PipeDirection.Out);
                client.Connect(400);
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
            {
                // The running instance cannot be told, but it still keeps the single slot.
            }
        }

        /// <summary>
        /// Hands this process's foreground rights to the running instance, otherwise Windows
        /// refuses the window that asks to come forward.
        /// </summary>
        private static void AllowForeground()
        {
            using var current = Process.GetCurrentProcess();

            foreach (var process in Process.GetProcessesByName(current.ProcessName))
            {
                using (process)
                {
                    if (process.Id != current.Id)
                        _ = AllowSetForegroundWindow(process.Id);
                }
            }
        }

        private static void BringToFront(Window window)
        {
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            window.Show();
            window.Activate();

            if (!OperatingSystem.IsWindows())
                return;

            var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle == IntPtr.Zero)
                return;

            _ = ShowWindow(handle, SwRestore);
            _ = SetForegroundWindow(handle);
        }

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int processId);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);
    }
}
