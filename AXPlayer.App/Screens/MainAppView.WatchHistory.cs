// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Extensions;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Online;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;

namespace AXPlayer.App.Screens
{
    public partial class MainAppView
    {
        private string watchHistoryContinuation;
        private bool watchHistoryLoading;

        private CancellationTokenSource watchHistoryTrackingProcess;

        /// <summary>
        /// Starts reporting the actual playback progress to YouTube so that the video appears
        /// in the account's watch history, even though playback itself is performed from a local file.
        /// </summary>
        private void recordWatchHistory(string videoId)
        {
            if (string.IsNullOrEmpty(videoId))
                return;

            watchHistoryTrackingProcess?.Cancel();
            watchHistoryTrackingProcess = new CancellationTokenSource();

            CancellationToken token = watchHistoryTrackingProcess.Token;

            Task.Run(async () => await trackWatchHistoryAsync(videoId, token));
        }

        private async Task trackWatchHistoryAsync(string videoId, CancellationToken token)
        {
            try
            {
                // Let playback actually start before reporting.
                await Task.Delay(5000, token);

                if (this.videoId != videoId || token.IsCancellationRequested)
                    return;

                await api.AddVideoToWatchHistoryAsync(videoId, token);

                WatchHistoryTracking tracking = await api.GetWatchHistoryTrackingAsync(videoId, token);

                if (tracking == null)
                    return;

                await api.SendWatchHistoryPlaybackAsync(tracking, token);

                double reportedPosition = 0;

                while (!token.IsCancellationRequested && this.videoId == videoId)
                {
                    await Task.Delay(10000, token);

                    if (this.videoId != videoId)
                        break;

                    double position = await getCurrentVideoPositionAsync();

                    if (position <= reportedPosition)
                        continue;

                    await api.SendWatchHistoryWatchTimeAsync(tracking, reportedPosition, position, true, token);
                    reportedPosition = position;
                }
            }
            catch (OperationCanceledException)
            {
                // A new video started or the app is closing.
            }
            catch (Exception e)
            {
                Logger.Error(e, e.GetDescription());
            }
        }

        private async Task<double> getCurrentVideoPositionAsync()
        {
            var completionSource = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                Schedule(() => completionSource.TrySetResult(currentVideoSource?.VideoProgress.Value ?? 0));
                return await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                return 0;
            }
        }

        private void openWatchHistory()
        {
            hideOverlays();
            showOverlayContainer(watchHistoryOverlay);

            if (watchHistoryItemsView.Count == 0 && !watchHistoryLoading)
                fetchWatchHistory(true);
        }

        private void fetchWatchHistory(bool reset)
        {
            if (!googleOAuth2.SignedIn.Value)
                return;

            if (watchHistoryLoading)
                return;

            watchHistoryLoading = true;

            if (reset)
            {
                watchHistoryContinuation = null;

                foreach (var item in watchHistoryItemsView.Children)
                    item.Expire();
            }

            Schedule(() =>
            {
                watchHistoryEmpty.Hide();
                watchHistoryLoadMoreButton.Enabled.Value = false;
            });

            string continuation = watchHistoryContinuation;

            Task.Run(async () =>
            {
                try
                {
                    WatchHistoryPage page = await api.GetWatchHistoryPageAsync(continuation);

                    Schedule(() =>
                    {
                        if (!googleOAuth2.SignedIn.Value)
                        {
                            watchHistoryLoading = false;
                            return;
                        }

                        watchHistoryContinuation = page.Continuation;

                        foreach (WatchHistoryItem item in page.Items)
                            addWatchHistoryItem(item);

                        if (watchHistoryItemsView.Count == 0)
                            watchHistoryEmpty.Show();
                        else
                            watchHistoryEmpty.Hide();

                        watchHistoryLoadMoreButton.Enabled.Value = !string.IsNullOrEmpty(watchHistoryContinuation);

                        if (!string.IsNullOrEmpty(watchHistoryContinuation))
                            watchHistoryLoadMoreButton.Show();
                        else
                            watchHistoryLoadMoreButton.Hide();

                        watchHistoryLoading = false;
                    });
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());

                    Schedule(() =>
                    {
                        watchHistoryLoading = false;
                        watchHistoryLoadMoreButton.Enabled.Value = !string.IsNullOrEmpty(watchHistoryContinuation);

                        if (watchHistoryItemsView.Count == 0)
                            watchHistoryEmpty.Show();
                        else
                            watchHistoryEmpty.Hide();
                    });
                }
            });
        }

        private void addWatchHistoryItem(WatchHistoryItem item)
        {
            var view = new WatchHistoryView
            {
                RelativeSizeAxes = Axes.X,
                Data = item,
                Enabled = { Value = true },
                ClickAction = _ =>
                {
                    ClearPlaylistItems();

                    Schedule(() => SetVideoSource(item.VideoId));
                },
            };

            watchHistoryItemsView.Add(view);
            view.UpdateData();
        }
    }
}
