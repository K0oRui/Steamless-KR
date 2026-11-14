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
    using Avalonia.Controls.Presenters;
    using Avalonia.Controls.Primitives;
    using System;

    /// <summary>
    /// A section card whose body expands and collapses. The header, chevron, border and height
    /// measurement live in one template, so a page supplies a title, a subtitle, an icon and the
    /// content.
    /// </summary>
    public class CollapsibleSection : ContentControl
    {
        public static readonly StyledProperty<string?> HeaderProperty =
            AvaloniaProperty.Register<CollapsibleSection, string?>(nameof(Header));

        public static readonly StyledProperty<string?> DescriptionProperty =
            AvaloniaProperty.Register<CollapsibleSection, string?>(nameof(Description));

        /// <summary>The badge icon. Any control; the template places it inside a rounded square.</summary>
        public static readonly StyledProperty<object?> IconProperty =
            AvaloniaProperty.Register<CollapsibleSection, object?>(nameof(Icon));

        public static readonly StyledProperty<bool> IsExpandedProperty =
            AvaloniaProperty.Register<CollapsibleSection, bool>(nameof(IsExpanded));

        private Border? m_Body;

        private ContentPresenter? m_Presenter;

        static CollapsibleSection()
        {
            IsExpandedProperty.Changed.AddClassHandler<CollapsibleSection>((section, _) => section.ApplyExpanded());
        }

        public string? Header
        {
            get => this.GetValue(HeaderProperty);
            set => this.SetValue(HeaderProperty, value);
        }

        public string? Description
        {
            get => this.GetValue(DescriptionProperty);
            set => this.SetValue(DescriptionProperty, value);
        }

        public object? Icon
        {
            get => this.GetValue(IconProperty);
            set => this.SetValue(IconProperty, value);
        }

        public bool IsExpanded
        {
            get => this.GetValue(IsExpandedProperty);
            set => this.SetValue(IsExpandedProperty, value);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            this.m_Body = e.NameScope.Find<Border>("PART_Body");
            this.m_Presenter = e.NameScope.Find<ContentPresenter>("PART_Presenter");

            this.ApplyExpanded();
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);

            // Text rewraps when the card changes width, so an open section has to be measured again.
            if (this.IsExpanded)
                this.SetBodyHeight();
        }

        private void ApplyExpanded()
        {
            if (this.m_Body == null)
                return;

            if (!this.IsExpanded)
            {
                this.m_Body.MaxHeight = 0;
                this.m_Body.Opacity = 0;
                return;
            }

            this.m_Body.Opacity = 1;
            this.SetBodyHeight();
        }

        private void SetBodyHeight()
        {
            if (this.m_Body == null || this.m_Presenter == null)
                return;

            // Measure the presenter against the width it will actually get, so wrapped text is not
            // clipped by an underestimated height. Before the first layout the body has no width,
            // and the section's own width is the best available answer.
            var width = this.m_Body.Bounds.Width > 0 ? this.m_Body.Bounds.Width : this.Bounds.Width;
            this.m_Presenter.Measure(new Size(width, double.PositiveInfinity));

            this.m_Body.MaxHeight = Math.Ceiling(this.m_Presenter.DesiredSize.Height);
        }
    }
}
