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

[assembly: Avalonia.Metadata.XmlnsDefinition("https://github.com/avaloniaui", "Steamless.Controls")]

namespace Steamless.Controls
{
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.Documents;
    using Avalonia.Media;
    using System.Collections.Generic;

    /// <summary>
    /// One Lucide icon, drawn from the path data in <see cref="IconGeometry"/>. This replaces
    /// Lucide.Avalonia's control, which materialises every icon on first use and costs about
    /// 7 MB of managed strings for the dozen icons the app actually draws.
    /// </summary>
    public class LucideIcon : Control
    {
        private const float RawIconSize = 24;

        private static readonly Dictionary<IconKind, Geometry> GeometryCache = new Dictionary<IconKind, Geometry>();

        public static readonly StyledProperty<double> SizeProperty =
            AvaloniaProperty.Register<LucideIcon, double>(nameof(Size), 24);

        public static readonly StyledProperty<IBrush?> ForegroundProperty =
            TextElement.ForegroundProperty.AddOwner<LucideIcon>();

        public static readonly StyledProperty<double> StrokeWidthProperty =
            AvaloniaProperty.Register<LucideIcon, double>(nameof(StrokeWidth), 1.8);

        public static readonly StyledProperty<IconKind> KindProperty =
            AvaloniaProperty.Register<LucideIcon, IconKind>(nameof(Kind));

        private Pen? m_Stroke;
        private Geometry? m_Geometry;

        public double Size
        {
            get => this.GetValue(SizeProperty);
            set => this.SetValue(SizeProperty, value);
        }

        public IBrush? Foreground
        {
            get => this.GetValue(ForegroundProperty);
            set => this.SetValue(ForegroundProperty, value);
        }

        public double StrokeWidth
        {
            get => this.GetValue(StrokeWidthProperty);
            set => this.SetValue(StrokeWidthProperty, value);
        }

        public IconKind Kind
        {
            get => this.GetValue(KindProperty);
            set => this.SetValue(KindProperty, value);
        }

        static LucideIcon()
        {
            AffectsRender<LucideIcon>(
                KindProperty,
                SizeProperty,
                ForegroundProperty,
                StrokeWidthProperty);

            AffectsMeasure<LucideIcon>(SizeProperty);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == KindProperty)
            {
                this.m_Geometry = LucideIcon.GeometryFor(this.Kind);
            }
            else if (change.Property == ForegroundProperty || change.Property == StrokeWidthProperty)
            {
                var brush = this.Foreground;
                var width = this.StrokeWidth;

                if (this.m_Stroke == null)
                {
                    this.m_Stroke = new Pen(brush, width, null, PenLineCap.Round, PenLineJoin.Round);
                }
                else
                {
                    this.m_Stroke.Brush = brush;
                    this.m_Stroke.Thickness = width;
                }
            }

            base.OnPropertyChanged(change);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(this.Size, this.Size);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            return new Size(this.Size, this.Size);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (this.m_Stroke == null && this.Foreground != null)
                this.m_Stroke = new Pen(this.Foreground, this.StrokeWidth, null, PenLineCap.Round, PenLineJoin.Round);

            this.m_Geometry ??= LucideIcon.GeometryFor(this.Kind);
        }

        public override void Render(DrawingContext context)
        {
            if (this.m_Geometry == null)
                return;

            // Draw a transparent background so the icon is hit-testable, like the original control.
            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, this.Bounds.Width, this.Bounds.Height));

            var scale = this.Size / RawIconSize;

            using (context.PushTransform(Matrix.CreateScale(scale, scale)))
                context.DrawGeometry(null, this.m_Stroke, this.m_Geometry);
        }

        /// <summary>Parsed once per kind and shared by every instance, including log rows.</summary>
        private static Geometry GeometryFor(IconKind kind)
        {
            if (LucideIcon.GeometryCache.TryGetValue(kind, out var geometry))
                return geometry;

            geometry = Geometry.Parse(IconGeometry.Get(kind));
            LucideIcon.GeometryCache[kind] = geometry;
            return geometry;
        }
    }

    /// <summary>The icons the app draws, and only those.</summary>
    public enum IconKind
    {
        Info,
        Minus,
        Square,
        Copy,
        X,
        File,
        Folder,
        PackageOpen,
        Users,
        Gavel,
        CircleCheck,
        TriangleExclamationPoint,
        CircleX,
        Bug,
    }
}
