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
    using Avalonia.Controls;
    using Avalonia.Media;
    using Avalonia.Threading;
    using Presentation;
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Fades the top and bottom edges of a scroll viewer while there is more to scroll in that
    /// direction. Any scroll viewer gets the behaviour by setting the attached property; the
    /// gradient rebuild and the tween stay here.
    /// </summary>
    public static class ScrollFade
    {
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("IsEnabled", typeof(ScrollFade));

        private static readonly ConditionalWeakTable<ScrollViewer, FadeState> States = new();

        public static void SetIsEnabled(ScrollViewer viewer, bool value)
        {
            viewer.SetValue(IsEnabledProperty, value);
        }

        public static bool GetIsEnabled(ScrollViewer viewer)
        {
            return viewer.GetValue(IsEnabledProperty);
        }

        static ScrollFade()
        {
            IsEnabledProperty.Changed.AddClassHandler<ScrollViewer>((viewer, args) =>
            {
                if (args.GetNewValue<bool>())
                    Attach(viewer);
                else
                    Detach(viewer);
            });
        }

        private static void Attach(ScrollViewer viewer)
        {
            if (States.TryGetValue(viewer, out _))
                return;

            var state = new FadeState(viewer);
            States.Add(viewer, state);

            viewer.Loaded += state.OnLoaded;
            viewer.SizeChanged += state.OnSizeChanged;
            viewer.ScrollChanged += state.OnScrollChanged;
        }

        private static void Detach(ScrollViewer viewer)
        {
            if (!States.TryGetValue(viewer, out var state))
                return;

            viewer.Loaded -= state.OnLoaded;
            viewer.SizeChanged -= state.OnSizeChanged;
            viewer.ScrollChanged -= state.OnScrollChanged;
            state.Cancel();

            States.Remove(viewer);
        }

        /// <summary>
        /// One viewer's fade. The two alphas are tweened towards their targets rather than set, so
        /// a fast scroll does not step the mask.
        /// </summary>
        private sealed class FadeState
        {
            private const double Band = 0.07;
            private const double Ramp = 32;

            private readonly ScrollViewer m_Viewer;
            private readonly DispatcherTimer m_Timer = new() { Interval = ShellMotion.Frame };
            private readonly Stopwatch m_Clock = Stopwatch.StartNew();

            private double m_Top;
            private double m_Bottom;
            private double m_FromTop;
            private double m_FromBottom;
            private double m_ToTop;
            private double m_ToBottom;
            private TimeSpan m_StartedAt;

            public FadeState(ScrollViewer viewer)
            {
                this.m_Viewer = viewer;
                this.m_Timer.Tick += this.OnTick;
            }

            public void OnLoaded(object? sender, EventArgs e)
            {
                this.Update();
            }

            public void OnSizeChanged(object? sender, SizeChangedEventArgs e)
            {
                this.Update();
            }

            public void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
            {
                this.Update();
            }

            public void Cancel()
            {
                this.m_Timer.Stop();
            }

            private void Update()
            {
                var maxOffset = this.m_Viewer.Extent.Height - this.m_Viewer.Viewport.Height;
                if (maxOffset <= 1)
                {
                    this.AnimateTo(0, 0);
                    return;
                }

                var offset = this.m_Viewer.Offset.Y;
                this.AnimateTo(Math.Clamp(offset / Ramp, 0, 1), Math.Clamp((maxOffset - offset) / Ramp, 0, 1));
            }

            private void AnimateTo(double top, double bottom)
            {
                top = Math.Clamp(top, 0, 1);
                bottom = Math.Clamp(bottom, 0, 1);

                if (Math.Abs(top - this.m_ToTop) < 0.005 && Math.Abs(bottom - this.m_ToBottom) < 0.005)
                    return;

                this.m_FromTop = this.m_Top;
                this.m_FromBottom = this.m_Bottom;
                this.m_ToTop = top;
                this.m_ToBottom = bottom;
                this.m_StartedAt = this.m_Clock.Elapsed;

                if (!this.m_Timer.IsEnabled)
                    this.m_Timer.Start();
            }

            private void OnTick(object? sender, EventArgs e)
            {
                var elapsed = (this.m_Clock.Elapsed - this.m_StartedAt).TotalMilliseconds;
                var progress = Math.Clamp(elapsed / ShellMotion.Fade.TotalMilliseconds, 0, 1);
                var eased = ShellMotion.EaseOutCubic(progress);

                this.m_Top = this.m_FromTop + ((this.m_ToTop - this.m_FromTop) * eased);
                this.m_Bottom = this.m_FromBottom + ((this.m_ToBottom - this.m_FromBottom) * eased);
                this.Apply();

                if (progress >= 1)
                {
                    this.m_Top = this.m_ToTop;
                    this.m_Bottom = this.m_ToBottom;
                    this.Apply();
                    this.m_Timer.Stop();
                }
            }

            private void Apply()
            {
                if (this.m_Top <= 0.001 && this.m_Bottom <= 0.001)
                {
                    this.m_Viewer.OpacityMask = null;
                    return;
                }

                var brush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative)
                };

                brush.GradientStops.Add(new GradientStop(Edge(1 - this.m_Top), 0));
                brush.GradientStops.Add(new GradientStop(Colors.White, Band));
                brush.GradientStops.Add(new GradientStop(Colors.White, 1 - Band));
                brush.GradientStops.Add(new GradientStop(Edge(1 - this.m_Bottom), 1));

                this.m_Viewer.OpacityMask = brush;
            }

            private static Color Edge(double alpha)
            {
                var value = (byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255);
                return Color.FromArgb(value, 255, 255, 255);
            }
        }
    }
}
