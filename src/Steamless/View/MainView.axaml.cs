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

namespace Steamless.View
{
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Input;
    using Avalonia.Interactivity;
    using Avalonia.Threading;
    using Avalonia.VisualTree;
    using Controls;
    using Steamless.ViewModel;
    using System;
    using System.Collections.Specialized;

    /// <summary>
    /// The main page. Both of its flyouts animate through <see cref="FlyoutMotion"/>, and the log
    /// follows its own items, so the page holds no flyout state.
    /// </summary>
    public partial class MainView : UserControl
    {
        private MainWindowViewModel m_ViewModel;
        private bool m_ScrollPending;

        public MainView()
        {
            this.InitializeComponent();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (this.m_ViewModel != null)
                ((INotifyCollectionChanged)this.m_ViewModel.Log).CollectionChanged -= this.OnLogCollectionChanged;

            this.m_ViewModel = this.DataContext as MainWindowViewModel;

            if (this.m_ViewModel != null)
                ((INotifyCollectionChanged)this.m_ViewModel.Log).CollectionChanged += this.OnLogCollectionChanged;
        }

        private void OnLogCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // A run can add a burst of lines before the dispatcher gets a turn, so one pending
            // scroll is enough; scrolling on every add would queue a post per line.
            if (e.Action != NotifyCollectionChangedAction.Add || this.m_ScrollPending)
                return;

            this.m_ScrollPending = true;
            Dispatcher.UIThread.Post(() =>
            {
                this.m_ScrollPending = false;

                var last = this.LogList.ItemCount - 1;
                if (last >= 0)
                    this.LogList.ScrollIntoView(last);
            }, DispatcherPriority.Background);
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            FlyoutMotion.Close(this.BrowseButton);

            if (this.DataContext is MainWindowViewModel vm)
                vm.SelectFileCommand.Execute(null);
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            FlyoutMotion.Close(this.BrowseButton);

            if (this.DataContext is MainWindowViewModel vm)
                vm.SelectFolderCommand.Execute(null);
        }

        private void PluginList_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            // A pick ends the interaction, the same way a click outside the flyout does.
            if ((e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(true) != null)
                FlyoutMotion.Close(this.PluginButton);
        }

        private void PluginList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Escape or Key.Enter or Key.Space)
                FlyoutMotion.Close(this.PluginButton);
        }

        private void PluginList_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Enter or Key.Space)
                FlyoutMotion.Close(this.PluginButton);
        }
    }
}
