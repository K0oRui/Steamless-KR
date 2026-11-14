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

namespace Steamless.Controls
{
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Media;
    using System;

    public class CheckMark : Control
    {
        public static readonly StyledProperty<double> ProgressProperty =
            AvaloniaProperty.Register<CheckMark, double>(nameof(Progress));

        public static readonly StyledProperty<IBrush> StrokeProperty =
            AvaloniaProperty.Register<CheckMark, IBrush>(nameof(Stroke), Brushes.White);

        public static readonly StyledProperty<double> StrokeThicknessProperty =
            AvaloniaProperty.Register<CheckMark, double>(nameof(StrokeThickness), 1.8);

        static CheckMark()
        {
            AffectsRender<CheckMark>(ProgressProperty, StrokeProperty, StrokeThicknessProperty);
        }

        public double Progress
        {
            get => GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public IBrush Stroke
        {
            get => GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public double StrokeThickness
        {
            get => GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        public override void Render(DrawingContext context)
        {
            var width = this.Bounds.Width;
            var height = this.Bounds.Height;
            if (width <= 0 || height <= 0)
                return;

            var progress = Math.Clamp(this.Progress, 0, 1);
            if (progress <= 0.001)
                return;

            var start = new Point(width * 0.18, height * 0.52);
            var corner = new Point(width * 0.40, height * 0.78);
            var end = new Point(width * 0.82, height * 0.22);

            var firstLength = Distance(start, corner);
            var secondLength = Distance(corner, end);
            var remaining = (firstLength + secondLength) * progress;

            var geometry = new StreamGeometry();
            using (var geometryContext = geometry.Open())
            {
                geometryContext.BeginFigure(start, false);

                if (remaining <= firstLength)
                {
                    geometryContext.LineTo(Lerp(start, corner, remaining / firstLength));
                }
                else
                {
                    geometryContext.LineTo(corner);
                    geometryContext.LineTo(Lerp(corner, end, Math.Min(1, (remaining - firstLength) / secondLength)));
                }

                geometryContext.EndFigure(false);
            }

            var pen = new Pen(this.Stroke, this.StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            context.DrawGeometry(null, pen, geometry);
        }

        private static double Distance(Point a, Point b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        private static Point Lerp(Point a, Point b, double t)
        {
            return new Point(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t));
        }
    }
}
