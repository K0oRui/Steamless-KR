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

namespace Steamless.Presentation
{
    using Avalonia.Animation.Easings;
    using System;

    /// <summary>
    /// The shell's motion timings: every duration, the one curve, and the chevron's angle. The
    /// AXAML transitions read these fields with x:Static, so a timing change is one edit here
    /// instead of a search through the views.
    /// </summary>
    public static class ShellMotion
    {
        /// <summary>A brush or a border settling into a hover state.</summary>
        public static readonly TimeSpan Hover = TimeSpan.FromMilliseconds(140);

        /// <summary>A flyout sliding, a scroll edge fading, or a border changing state.</summary>
        public static readonly TimeSpan Fade = TimeSpan.FromMilliseconds(160);

        /// <summary>A chevron turning over, or one view cross-fading into another.</summary>
        public static readonly TimeSpan Slow = TimeSpan.FromMilliseconds(220);

        /// <summary>The check mark drawing itself.</summary>
        public static readonly TimeSpan Check = TimeSpan.FromMilliseconds(240);

        /// <summary>A collapsing section growing to its measured height.</summary>
        public static readonly TimeSpan Collapse = TimeSpan.FromMilliseconds(260);

        /// <summary>The body fading in behind a collapse.</summary>
        public static readonly TimeSpan CollapseFade = TimeSpan.FromMilliseconds(180);

        /// <summary>The frame interval the scroll fade tweens at.</summary>
        public static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(16);

        /// <summary>
        /// The one curve. The AXAML transitions name it through x:Static, and
        /// <see cref="EaseOutCubic"/> is the same curve for the tween that runs each frame.
        /// </summary>
        public static readonly Easing EaseOut = new CubicEaseOut();

        /// <summary>
        /// The angle a chevron shows. It is absolute rather than accumulated, so a popup that opens
        /// and closes in any order still ends pointing the right way.
        /// </summary>
        public static double ChevronAngle(bool open)
        {
            return open ? 180 : 0;
        }

        /// <summary>The cubic ease-out the transitions use, for code that tweens frame by frame.</summary>
        public static double EaseOutCubic(double progress)
        {
            var clamped = Math.Clamp(progress, 0, 1);
            return 1 - Math.Pow(1 - clamped, 3);
        }
    }
}
