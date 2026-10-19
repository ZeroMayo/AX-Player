// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Config;
using AXPlayer.App.Extensions;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;

namespace AXPlayer.App.Updater
{
    public partial class UpdateManager : CompositeDrawable
    {
        /// <summary>
        /// Whether this UpdateManager should be or is capable of checking for updates.
        /// </summary>
        public bool CanCheckForUpdate => app.IsDeployedBuild &&
                                         // only implementations will actually check for updates.
                                         GetType() != typeof(UpdateManager);

        [Resolved]
        private AXPlayerConfigManager config { get; set; } = null!;

        [Resolved]
        private AXPlayerAppBase app { get; set; } = null!;

        protected IBindable<ReleaseStream> ReleaseStream => releaseStream;

        private readonly Bindable<ReleaseStream> releaseStream = new Bindable<ReleaseStream>();

        protected override void LoadComplete()
        {
            base.LoadComplete();

            config.BindWith(AXPlayerSetting.ReleaseStream, releaseStream);
            //releaseStream.BindValueChanged(_ => CheckForUpdate());
        }

        /// <summary>
        /// Immediately checks for any available update.
        /// </summary>
        public void CheckForUpdate()
        {
            CheckForUpdateAsync().FireAndForget();
        }

        private CancellationTokenSource updateCancellationSource = new CancellationTokenSource();

        /// <summary>
        /// Immediately checks for any available update.
        /// </summary>
        /// <returns>
        /// <c>true</c> if any updates are available, <c>false</c> otherwise.
        /// May return true if an error occured (there is potentially an update available).
        /// </returns>
        public async Task<bool> CheckForUpdateAsync(CancellationToken cancellationToken = default) => await Task.Run(async () =>
        {
            if (!CanCheckForUpdate)
                return false;

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Cancels the last update and closes any existing notifications as stale.
            using (var lastCts = Interlocked.Exchange(ref updateCancellationSource, cts))
                await lastCts.CancelAsync().ConfigureAwait(false);

            try
            {
                return await PerformUpdateCheck(cts.Token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.Log($"{nameof(PerformUpdateCheck)} failed ({e.Message})");
                return true;
            }
        }, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Performs an asynchronous check for application updates.
        /// </summary>
        /// <returns>Whether any update is waiting. May return true if an error occured (there is potentially an update available).</returns>
        protected virtual Task<bool> PerformUpdateCheck(CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
