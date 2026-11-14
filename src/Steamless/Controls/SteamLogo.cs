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

    /// <summary>
    /// The Steamless mark, drawn from the path data that used to live in steam.svg. Shipping the
    /// geometry directly keeps Svg.Controls.Avalonia and its parser out of the release, which is
    /// about 5 MB of managed assemblies for one logo.
    /// </summary>
    public class SteamLogo : Control
    {
        private const double RawWidth = 250;
        private const double RawHeight = 150;

        private static readonly Geometry Logo = Geometry.Parse(
            "F0 M 48.70000 39.20000 A 27.5 27.5 0 0 1 72.02132 52.12722 C 79.44019 63.99989 91.5 81.5 " +
            "113.5 81.5 C 140.5 81.5 147.3 73.5 147.3 52 A 41.5 41.5 0 1 1 188.8 93.5 C 173.5 93.5 " +
            "169.07185 100.16016 163.94155 114.25555 A 27.5 27.5 0 0 1 113.38316 116.90521 C 101.54714 " +
            "92.63777 78.99974 87.05130 55.81752 93.26296 A 27.5 27.5 0 1 1 48.70000 39.20000 Z " +
            "M 37.900000000000006 66.7 a 10.8 10.8 0 1 0 21.6 0 a 10.8 10.8 0 1 0 -21.6 0 Z " +
            "M 169.10000000000002 52 a 19.7 19.7 0 1 0 39.4 0 a 19.7 19.7 0 1 0 -39.4 0 Z " +
            "M 126.6 104.85 a 11.5 11.5 0 1 0 23.0 0 a 11.5 11.5 0 1 0 -23.0 0 Z");

        public static readonly StyledProperty<IBrush?> ForegroundProperty =
            AvaloniaProperty.Register<SteamLogo, IBrush?>(nameof(Foreground), Brushes.White);

        public IBrush? Foreground
        {
            get => this.GetValue(ForegroundProperty);
            set => this.SetValue(ForegroundProperty, value);
        }

        static SteamLogo()
        {
            AffectsRender<SteamLogo>(ForegroundProperty);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsInfinity(availableSize.Width) ? RawWidth : availableSize.Width;
            return new Size(width, width * (RawHeight / RawWidth));
        }

        public override void Render(DrawingContext context)
        {
            var brush = this.Foreground;
            if (brush == null || this.Bounds.Width <= 0 || this.Bounds.Height <= 0)
                return;

            var scale = Math.Min(this.Bounds.Width / RawWidth, this.Bounds.Height / RawHeight);
            var offsetX = (this.Bounds.Width - (RawWidth * scale)) / 2;
            var offsetY = (this.Bounds.Height - (RawHeight * scale)) / 2;

            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
                context.DrawGeometry(brush, null, SteamLogo.Logo);
        }
    }
}
