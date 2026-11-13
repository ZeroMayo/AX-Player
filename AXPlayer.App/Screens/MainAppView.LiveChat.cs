// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Localisation;
using AXPlayer.App.Online;
using osu.Framework.Extensions;
using osu.Framework.Logging;

namespace AXPlayer.App.Screens
{
    public partial class MainAppView
    {
        private const int live_chat_replay_preload_cache_size = 4;

        private readonly Dictionary<string, Task<IReadOnlyList<LiveChatReplayMessage>>> liveChatReplayPreloads = new();
        private readonly Queue<string> liveChatReplayPreloadOrder = new();
        private readonly object liveChatReplayPreloadLock = new();

        private string liveChatReplayVideoId;
        private CancellationTokenSource liveChatReplayCancellation;

        /// <summary>
        /// Starts (or reuses) a background fetch of the live chat replay for the given video, so that
        /// both the side overlay and the in-player overlay can use the data without fetching it again.
        /// </summary>
        private Task<IReadOnlyList<LiveChatReplayMessage>> getOrStartLiveChatReplayPreload(string videoId)
        {
            if (string.IsNullOrEmpty(videoId))
                return Task.FromResult<IReadOnlyList<LiveChatReplayMessage>>(Array.Empty<LiveChatReplayMessage>());

            lock (liveChatReplayPreloadLock)
            {
                if (liveChatReplayPreloads.TryGetValue(videoId, out Task<IReadOnlyList<LiveChatReplayMessage>> existing))
                    return existing;

                Task<IReadOnlyList<LiveChatReplayMessage>> task = api.GetLiveChatReplayAsync(videoId);

                liveChatReplayPreloads[videoId] = task;
                liveChatReplayPreloadOrder.Enqueue(videoId);

                while (liveChatReplayPreloadOrder.Count > live_chat_replay_preload_cache_size)
                    liveChatReplayPreloads.Remove(liveChatReplayPreloadOrder.Dequeue());

                return task;
            }
        }

        /// <summary>
        /// Toggles the live chat replay overlay, loading the replay on first open.
        /// </summary>
        private void openLiveChatReplay()
        {
            if (liveChatReplayOverlay == null)
                return;

            if (!liveChatReplayOverlay.IsVisible)
            {
                hideOverlays();
                showOverlayContainer(liveChatReplayOverlay);
                loadLiveChatReplay();
            }
            else
            {
                hideOverlayContainer(liveChatReplayOverlay);
            }
        }

        /// <summary>
        /// Drops any replay that was loaded for a previous video.
        /// </summary>
        private void resetLiveChatReplay()
        {
            liveChatReplayVideoId = null;

            liveChatReplayCancellation?.Cancel();
            liveChatReplayCancellation?.Dispose();
            liveChatReplayCancellation = null;

            liveChatReplayOverlay?.ClearMessages();

            if (quickLiveChatOpenButton != null)
                quickLiveChatOpenButton.TooltipText = AXPlayerStrings.LiveChatReplay("0");

            if (liveChatOpenButton != null)
                liveChatOpenButton.Text = AXPlayerStrings.LiveChatReplay("0");
        }

        private void loadLiveChatReplay()
        {
            string currentVideoId = videoId;

            if (string.IsNullOrEmpty(currentVideoId) || liveChatReplayOverlay == null)
                return;

            if (liveChatReplayVideoId == currentVideoId)
                return;

            liveChatReplayVideoId = currentVideoId;

            liveChatReplayCancellation?.Cancel();
            liveChatReplayCancellation?.Dispose();
            liveChatReplayCancellation = new CancellationTokenSource();

            CancellationToken cancellationToken = liveChatReplayCancellation.Token;

            liveChatReplayOverlay.SetLoading();

            Task<IReadOnlyList<LiveChatReplayMessage>> preloadTask = getOrStartLiveChatReplayPreload(currentVideoId);

            Task.Run(async () =>
            {
                try
                {
                    IReadOnlyList<LiveChatReplayMessage> messages = await preloadTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();

                    Schedule(() =>
                    {
                        if (cancellationToken.IsCancellationRequested || liveChatReplayVideoId != currentVideoId)
                            return;

                        if (messages.Count == 0)
                        {
                            liveChatReplayOverlay.ShowUnavailable();
                            return;
                        }

                        liveChatReplayOverlay.SetMessages(messages);
                        quickLiveChatOpenButton.TooltipText = AXPlayerStrings.LiveChatReplay(messages.Count.ToString(CultureInfo.InvariantCulture));
                        liveChatOpenButton.Text = AXPlayerStrings.LiveChatReplay(messages.Count.ToString(CultureInfo.InvariantCulture));
                    });
                }
                catch (OperationCanceledException)
                {
                    // A different video was loaded, or the player is shutting down.
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, exception.GetDescription());

                    Schedule(() =>
                    {
                        if (liveChatReplayVideoId != currentVideoId)
                            return;

                        liveChatReplayOverlay.ShowUnavailable();
                    });
                }
            }, cancellationToken);
        }
    }
}
