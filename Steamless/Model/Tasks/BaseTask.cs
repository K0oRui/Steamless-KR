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

namespace Steamless.Model.Tasks
{
    using API.Model;
    using CommunityToolkit.Mvvm.ComponentModel;
    using System.Threading.Tasks;

    public abstract partial class BaseTask : ObservableObject
    {
        [ObservableProperty] private bool _completed;
        [ObservableProperty] private double _progress;
        [ObservableProperty] private double _progressTotal;
        [ObservableProperty] private string _text;

        /// <summary>
        /// Starts the task.
        /// </summary>
        public Task StartTask()
        {
            return Task.Run(async () =>
                {
                    this.Completed = false;
                    await this.DoTask();
                    this.Completed = true;
                });
        }

        /// <summary>
        /// The tasks main function to execute when started.
        /// </summary>
        public abstract Task DoTask();
    }
}