/**
 * Steamless - Copyright (c) 2015 - 2024 atom0s [atom0s@live.com]
 *
 * This work is licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by-nc-nd/4.0/
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
namespace Steamless.View
{
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.Primitives;
    using Avalonia.Input;
    using Avalonia.Interactivity;
    using Avalonia.Threading;
    using Avalonia.VisualTree;
    using Steamless.Presentation;
    using Steamless.ViewModel;
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;

    public partial class MainWindow : ShadUI.Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.DataContext = App.ViewModel;
            App.ViewModel.PropertyChanged += this.OnViewModelPropertyChanged;

            this.UpdateWindowStateVisuals();
        }

        /// <summary>
        /// The About page is built on first use. Most runs never open it, so parsing and laying out
        /// that view at startup would be wasted work. The host fades in either way.
        /// </summary>
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.ShowSplashView))
            {
                if (!App.ViewModel.ShowSplashView)
                    this.RemoveSplashWhenFaded();
                return;
            }

            if (e.PropertyName != nameof(MainWindowViewModel.ShowAboutViewPanel))
                return;

            if (App.ViewModel.ShowAboutViewPanel && this.AboutHost.Content == null)
                this.AboutHost.Content = new AboutView();
        }

        /// <summary>
        /// The splash holds an indeterminate progress bar, which keeps animating at 60 fps even
        /// after the view fades out. Removing it once the fade ends stops that work and frees its
        /// visual tree. The splash is never needed again.
        /// </summary>
        private void RemoveSplashWhenFaded()
        {
            DispatcherTimer.RunOnce(() =>
            {
                if (this.SplashHost.Parent is Panel parent)
                    parent.Children.Remove(this.SplashHost);
            }, ShellMotion.Slow);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            // ShadUI insets the window by 8px when maximized; that leaves a transparent
            // frame around the window because this app uses a transparent root.
            if (e.NameScope.Find<Border>("PART_WindowRoot") is { } windowRoot)
                windowRoot.Margin = default;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == WindowStateProperty)
                this.UpdateWindowStateVisuals();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            // The software renderer presents black for the first frame. Keep the window
            // transparent until that frame has been painted.
            DispatcherTimer.RunOnce(() => this.Opacity = 1, TimeSpan.FromMilliseconds(60));

            // These window attributes are Windows-only; the GUI also builds and runs on Linux.
            if (!OperatingSystem.IsWindows())
                return;

            var handle = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle == IntPtr.Zero)
                return;

            // Windows 11 draws a 1px frame around every window. Remove it and let the
            // app-drawn rounded corners define the shape.
            var noBorder = unchecked((int)0xFFFFFFFE);
            _ = DwmSetWindowAttribute(handle, 34, ref noBorder, sizeof(int));

            var round = 2;
            _ = DwmSetWindowAttribute(handle, 33, ref round, sizeof(int));
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private void UpdateWindowStateVisuals()
        {
            var maximized = this.WindowState is WindowState.Maximized or WindowState.FullScreen;

            if (this.ContentRoot != null)
                this.ContentRoot.CornerRadius = maximized ? default : new CornerRadius(8);

            if (this.TitleBar != null)
                this.TitleBar.Margin = maximized ? new Thickness(16, 0, 16, 0) : default;

            if (this.MaximizeIcon != null)
                this.MaximizeIcon.IsVisible = !maximized;

            if (this.RestoreIcon != null)
                this.RestoreIcon.IsVisible = maximized;
        }

        private void ContentRoot_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            if ((e.Source as Visual)?.FindAncestorOfType<TextBox>(true) != null)
                return;

            this.FocusManager?.Focus(null, NavigationMethod.Unspecified, KeyModifiers.None);
        }

        private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            if ((e.Source as Visual)?.FindAncestorOfType<Button>(true) != null)
                return;

            if (e.ClickCount == 2)
            {
                this.ToggleMaximized();
                return;
            }

            if (this.WindowState == WindowState.Normal)
                this.BeginMoveDrag(e);
        }

        private void Minimize_Click(object? sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object? sender, RoutedEventArgs e)
        {
            this.ToggleMaximized();
        }

        private void Close_Click(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ToggleMaximized()
        {
            if (!this.CanResize)
                return;

            this.WindowState = this.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (this.DataContext is MainWindowViewModel vm)
                vm.Shutdown();

            base.OnClosing(e);
        }
    }
}
