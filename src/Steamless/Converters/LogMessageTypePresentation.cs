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

namespace Steamless.Converters
{
    using Avalonia.Media;
    using Steamless.API.Events;
    using Steamless.Controls;

    /// <summary>
    /// How one log level is drawn. Colour, icon and tooltip bindings all read this table, so a new
    /// level is one edit.
    /// </summary>
    internal static class LogMessageTypePresentation
    {
        internal sealed class Entry
        {
            public Entry(IconKind icon, IBrush brush, string tooltip)
            {
                this.Icon = icon;
                this.Brush = brush;
                this.Tooltip = tooltip;
            }

            public IconKind Icon { get; }

            public IBrush Brush { get; }

            public string Tooltip { get; }
        }

        private static readonly Entry Information = new Entry(IconKind.Info, new SolidColorBrush(Color.Parse("#C6CBD4")), "Information");

        private static readonly Entry Success = new Entry(IconKind.CircleCheck, new SolidColorBrush(Color.Parse("#9FD36A")), "Success");

        private static readonly Entry Warning = new Entry(IconKind.TriangleExclamationPoint, new SolidColorBrush(Color.Parse("#E0AE5A")), "Warning");

        private static readonly Entry Error = new Entry(IconKind.CircleX, new SolidColorBrush(Color.Parse("#E06C75")), "Error");

        private static readonly Entry Debug = new Entry(IconKind.Bug, new SolidColorBrush(Color.Parse("#7E899B")), "Debug");

        public static Entry For(object value)
        {
            return value is LogMessageType type
                ? type switch
                {
                    LogMessageType.Success => Success,
                    LogMessageType.Warning => Warning,
                    LogMessageType.Error => Error,
                    LogMessageType.Debug => Debug,
                    _ => Information
                }
                : Information;
        }
    }
}
