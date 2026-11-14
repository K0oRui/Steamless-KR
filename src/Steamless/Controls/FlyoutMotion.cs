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

namespace Steamless.Controls
{
    using Avalonia;
    using Avalonia.Animation;
    using Avalonia.Controls;
    using Avalonia.Controls.Primitives;
    using Avalonia.Logging;
    using Avalonia.Media;
    using Avalonia.Media.Transformation;
    using Avalonia.Threading;
    using Avalonia.VisualTree;
    using Presentation;
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;

    /// <summary>
    /// The fade and lift a flyout plays when it opens and closes, including the chevron in the
    /// button that opened it. A page attaches it to the button and keeps no flyout state in its
    /// code-behind.
    /// </summary>
    /// <remarks>
    /// A popup hides without animating, so every close is cancelled once, played as a fade, then
    /// hidden for real. That covers the button, an outside click and a pick, so all three end the
    /// same way. The fade runs on the popup presenter so the panel background and its contents
    /// move as one piece. On Windows the popup window is also clipped to the panel's rounded shape,
    /// because a software-rendered popup has no per-pixel alpha for the corners.
    /// </remarks>
    public static class FlyoutMotion
    {
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(FlyoutMotion));

        /// <summary>The chevron to turn over when the flyout opens and closes.</summary>
        public static readonly AttachedProperty<ChevronIcon?> ChevronProperty =
            AvaloniaProperty.RegisterAttached<Control, ChevronIcon?>("Chevron", typeof(FlyoutMotion));

        private static readonly AttachedProperty<FlyoutState?> StateProperty =
            AvaloniaProperty.RegisterAttached<Control, FlyoutState?>("State", typeof(FlyoutMotion));

        private static readonly IBrush PopupBackground = new SolidColorBrush(Color.Parse("#1E1E1E"));

        private static bool s_OverlayWarningSent;

        /// <summary>
        /// ShadUI's window template puts an unnamed VisualLayerManager in the tree, and Avalonia's
        /// TopLevel only enables the popup overlay layer for its own template part. Turn it on so
        /// flyouts can render inside the window, where per-pixel alpha and antialiasing work.
        /// </summary>
        /// <remarks>
        /// The property is not public, so a future Avalonia release can rename or remove it. That
        /// case is reported once instead of silently changing every flyout to a native popup.
        /// </remarks>
        private static bool EnableOverlay(Control target)
        {
            var top = TopLevel.GetTopLevel(target);
            var layers = top?.GetVisualDescendants().OfType<VisualLayerManager>().FirstOrDefault();
            if (layers == null)
                return false;

            var property = typeof(VisualLayerManager).GetProperty(
                "EnablePopupOverlayLayer",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (property == null || property.PropertyType != typeof(bool))
            {
                WarnOverlayUnavailable("VisualLayerManager.EnablePopupOverlayLayer is missing or is not a bool.");
                return false;
            }

            try
            {
                property.SetValue(layers, true);
                return true;
            }
            catch (Exception ex)
            {
                WarnOverlayUnavailable(ex.Message);
                return false;
            }
        }

        private static void WarnOverlayUnavailable(string detail)
        {
            if (s_OverlayWarningSent)
                return;

            s_OverlayWarningSent = true;
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)
                ?.Log(typeof(FlyoutMotion), "Popup overlay layer unavailable, flyouts fall back to native popups. {Detail}", detail);
        }

        static FlyoutMotion()
        {
            IsEnabledProperty.Changed.AddClassHandler<Control>((control, args) =>
            {
                if (args.GetNewValue<bool>())
                    control.AttachedToVisualTree += OnAttachedToVisualTree;
                else
                    control.AttachedToVisualTree -= OnAttachedToVisualTree;
            });
        }

        public static void SetIsEnabled(Control control, bool value)
        {
            control.SetValue(IsEnabledProperty, value);
        }

        public static bool GetIsEnabled(Control control)
        {
            return control.GetValue(IsEnabledProperty);
        }

        public static void SetChevron(Control control, ChevronIcon? value)
        {
            control.SetValue(ChevronProperty, value);
        }

        public static ChevronIcon? GetChevron(Control control)
        {
            return control.GetValue(ChevronProperty);
        }

        /// <summary>
        /// Asks the flyout to close. The close is animated, then hidden once the fade finishes.
        /// </summary>
        public static void Close(Control control)
        {
            var state = control.GetValue(StateProperty);
            state?.Flyout.Hide();
        }

        private static void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is not Control control || control.GetValue(StateProperty) != null)
                return;

            if (control is not Button button || button.Flyout is not Flyout flyout)
                return;

            var state = new FlyoutState(button, flyout);
            control.SetValue(StateProperty, state);

            flyout.Opening += state.OnOpening;
            flyout.Opened += state.OnOpened;
            flyout.Closing += state.OnClosing;
            flyout.Closed += state.OnClosed;

            control.DetachedFromVisualTree += (_, _) => Detach(control, state);
        }

        private static void Detach(Control control, FlyoutState state)
        {
            state.CancelTimer();
            state.Flyout.Opening -= state.OnOpening;
            state.Flyout.Opened -= state.OnOpened;
            state.Flyout.Closing -= state.OnClosing;
            state.Flyout.Closed -= state.OnClosed;
            control.SetValue(StateProperty, null);
        }

        private static TransformOperations Transform(double y, double scale)
        {
            return TransformOperations.Parse($"translate(0px, {y}px) scale({scale})");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr window, out NativeRect rect);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr obj);

        /// <summary>
        /// Per-button flyout state. The popup's content is kept so the close can play before the
        /// popup goes away.
        /// </summary>
        private sealed class FlyoutState
        {
            private readonly Button m_Button;
            private Control? m_Presenter;

            public FlyoutState(Button button, Flyout flyout)
            {
                this.m_Button = button;
                this.Flyout = flyout;

                if (flyout.Content is Control surface)
                    this.Surface = surface;
            }

            public Flyout Flyout { get; }

            public Control? Surface { get; }

            public bool Closing { get; set; }

            public bool AllowClose { get; set; }

            public IDisposable? HideTimer { get; set; }

            public void CancelTimer()
            {
                this.HideTimer?.Dispose();
                this.HideTimer = null;
                this.Closing = false;
                this.AllowClose = false;
            }

            /// <summary>
            /// Points the button's chevron the way the flyout is going. The angle is set, not
            /// advanced, so an interrupted close cannot leave it upside down.
            /// </summary>
            public void SetChevron(bool open)
            {
                var chevron = this.m_Button.GetValue(ChevronProperty);
                if (chevron != null)
                    chevron.Angle = ShellMotion.ChevronAngle(open);
            }

            public void OnOpening(object? sender, EventArgs e)
            {
                this.CancelTimer();
                this.SetChevron(true);

                // Render inside the window when its popup overlay layer is available. That
                // keeps the rounded corners antialiased and lets the panel fade as one surface.
                this.Flyout.Popup.ShouldUseOverlayLayer = FlyoutMotion.EnableOverlay(this.m_Button);

                if (this.Surface == null)
                    return;

                // The popup has no width of its own; the button is the only thing to match.
                this.Surface.Width = this.m_Button.Bounds.Width;

                // The presenter owns the panel background, so it has to fade as one piece.
                this.EnsurePresenter();

                if (this.m_Presenter != null)
                {
                    this.m_Presenter.Opacity = 0;
                    this.m_Presenter.RenderTransform = Transform(-6, 0.97);
                }
            }

            public void OnOpened(object? sender, EventArgs e)
            {
                this.CancelTimer();
                this.EnsurePresenter();

                if (this.m_Presenter == null)
                    return;

                // The first open creates the presenter during ShowAt, so its starting state can
                // only be set here. Rendering happens after this, so the fade still starts at 0.
                this.m_Presenter.Opacity = 0;
                this.m_Presenter.RenderTransform = Transform(-6, 0.97);

                Dispatcher.UIThread.Post(() =>
                {
                    if (this.m_Presenter == null)
                        return;

                    this.m_Presenter.Opacity = 1;
                    this.m_Presenter.RenderTransform = Transform(0, 1);
                }, DispatcherPriority.Render);

                this.ApplyRoundedRegion();
            }

            public void OnClosing(object? sender, CancelEventArgs e)
            {
                // A window that is going away must not wait for a fade.
                var topLevel = TopLevel.GetTopLevel(this.m_Button);
                if (this.AllowClose || this.Surface == null || (topLevel is Window window && !window.IsActive))
                    return;

                e.Cancel = true;

                if (this.Closing)
                    return;

                this.Closing = true;
                this.SetChevron(false);
                this.EnsurePresenter();

                if (this.m_Presenter != null)
                {
                    this.m_Presenter.Opacity = 0;
                    this.m_Presenter.RenderTransform = Transform(-4, 0.98);
                }

                this.HideTimer = DispatcherTimer.RunOnce(() =>
                {
                    this.HideTimer = null;
                    this.AllowClose = true;
                    this.Flyout.Hide();
                    this.AllowClose = false;
                }, ShellMotion.Fade);
            }

            public void OnClosed(object? sender, EventArgs e)
            {
                this.CancelTimer();
                this.SetChevron(false);

                if (this.m_Presenter == null)
                    return;

                this.m_Presenter.Opacity = 0;
                this.m_Presenter.RenderTransform = Transform(-6, 0.97);
            }

            private void EnsurePresenter()
            {
                this.m_Presenter ??= this.Flyout.Popup.Child;

                if (this.m_Presenter == null || this.m_Presenter.Transitions != null)
                    return;

                this.m_Presenter.Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = Visual.OpacityProperty,
                        Duration = ShellMotion.Fade,
                        Easing = ShellMotion.EaseOut,
                    },
                    new TransformOperationsTransition
                    {
                        Property = Visual.RenderTransformProperty,
                        Duration = ShellMotion.Fade,
                        Easing = ShellMotion.EaseOut,
                    },
                };

                this.m_Presenter.RenderTransformOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative);
            }

            /// <summary>
            /// A native popup window has no per-pixel alpha under software rendering, so the
            /// rounded panel corners would show the window's black background. Clip the window
            /// itself to the same rounded shape.
            /// </summary>
            private void ApplyRoundedRegion()
            {
                if (!OperatingSystem.IsWindows())
                    return;

                var owner = TopLevel.GetTopLevel(this.m_Button);
                if (this.m_Presenter == null || TopLevel.GetTopLevel(this.m_Presenter) is not TopLevel top || ReferenceEquals(top, owner))
                    return;

                // The popup root's default background is white, and a transparent software surface
                // shows black. Use the shell's base colour so a fade starts from neither.
                if (top is PopupRoot popupRoot)
                    popupRoot.Background = PopupBackground;

                var handle = top.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (handle == IntPtr.Zero || !GetClientRect(handle, out var rect))
                    return;

                var radius = (int)Math.Round(10 * top.RenderScaling);
                var region = CreateRoundRectRgn(0, 0, rect.Right + 1, rect.Bottom + 1, radius * 2, radius * 2);
                if (region == IntPtr.Zero)
                    return;

                if (SetWindowRgn(handle, region, true) == 0)
                    DeleteObject(region);
            }
        }
    }
}
