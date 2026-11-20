// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AXPlayer.App.Extensions;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Graphics.UserInterface.LiveChat;
using AXPlayer.App.Localisation;
using AXPlayer.App.Online;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterface
{
    /// <summary>
    /// A side overlay which replays the messages that appeared in a video's live chat,
    /// keeping them in sync with the video's playback position.
    /// </summary>
    public partial class LiveChatReplayOverlay : SideOverlayContainer
    {
        /// <summary>
        /// The maximum amount of chat entries kept in the draw hierarchy at once.
        /// Older entries are discarded as new ones appear to keep scrolling cheap.
        /// </summary>
        public const int MAX_DISPLAYED_MESSAGES = 200;

        /// <summary>
        /// How many messages are rendered when the overlay is opened or the video is seeked.
        /// Keeping this small avoids a large layout/avatar-loading spike on the first open.
        /// </summary>
        private const int initial_displayed_messages = 60;
        private const int initial_displayed_messages_compact = 25;
        private const int max_displayed_messages_compact = 80;

        /// <summary>
        /// How many messages are added to the draw hierarchy per update frame while a replay is being
        /// rendered. Splitting the work keeps the frame the overlay was opened on short, instead of
        /// building the whole window (and its avatars) in one go.
        /// </summary>
        private const int messages_per_frame = 24;

        private int initialDisplayedMessages => compact ? initial_displayed_messages_compact : initial_displayed_messages;
        private int maxDisplayedMessages => compact ? max_displayed_messages_compact : MAX_DISPLAYED_MESSAGES;

        /// <summary>
        /// Invoked when the user requests the overlay to close.
        /// </summary>
        public Action? CloseAction { get; set; }

        /// <summary>
        /// The current video position, in seconds. Messages are revealed as this value increases.
        /// </summary>
        public BindableDouble PlaybackPosition { get; } = new BindableDouble();

        public bool HasMessages => messages.Count > 0;

        public int MessageCount => messages.Count;

        private readonly List<LiveChatReplayMessage> messages = new();

        private FillFlowContainer<LiveChatMessageDisplay> messageFlow = null!;
        private ProjectYomiScrollContainer scrollContainer = null!;
        private ProjectYomiSpriteText title = null!;
        private Container emptyState = null!;
        private ProjectYomiSpriteText emptyTitle = null!;
        private ProjectYomiTextFlowContainer emptyDescription = null!;
        private Container loadingState = null!;

        private readonly bool compact;

        private int nextMessageIndex;
        private double? lastRenderedTime;
        private bool isLoading;
        private bool unavailable;
        private bool scrollToEndScheduled;

        /// <summary>
        /// Identifies the newest render pass. A pass which finds a newer version after one of its frame
        /// boundaries stops without touching the hierarchy again, so state changes never interleave.
        /// </summary>
        private int renderVersion;

        /// <summary>
        /// Whether a render pass is currently adding messages across frames. While it is running it owns
        /// the hierarchy, so <see cref="Update"/> does not reveal or rebuild anything on top of it.
        /// </summary>
        private bool isRebuilding;

        public LiveChatReplayOverlay(bool compact = false)
        {
            this.compact = compact;

            RelativeSizeAxes = Axes.Both;
            Masking = true;

            if (compact)
            {
                // A small floating panel which lives inside the video player itself.
                Size = Vector2.One;
                CornerRadius = 10;
            }
            else
            {
                Size = new Vector2(0.45f, 1f);
                CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, AXPlayerApp.UI_CORNER_RADIUS, 0, 0);
                Origin = Anchor.CentreRight;
                Anchor = Anchor.CentreRight;
            }
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            Children = new Drawable[]
            {
                new OverlayBackground
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = scrollContainer = new ProjectYomiScrollContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        ScrollbarVisible = false,
                        ProgressiveBlurBottomHeight = compact ? 0 : 56,
                        ProgressiveBlurTopHeight = compact ? 38 : 56,
                        Padding = new MarginPadding
                        {
                            Top = compact ? 38 : 56,
                            Bottom = compact ? 10 : 56,
                            Horizontal = compact ? 8 : 16,
                        },
                        Children = new Drawable[]
                        {
                            messageFlow = new FillFlowContainer<LiveChatMessageDisplay>
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, compact ? 1 : 2),
                                AlwaysPresent = true,
                            },
                        },
                    },
                },
                emptyState = createEmptyState(overlayColourProvider),
                loadingState = createLoadingState(),
                new Box
                {
                    Name = "masking of overlay",
                    RelativeSizeAxes = Axes.X,
                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5, overlayColourProvider.Background5.Opacity(0)),
                    Height = compact ? 44 : 56 + 20,
                },
                new Box
                {
                    Name = "masking of overlay",
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5.Opacity(0), overlayColourProvider.Background5),
                    Height = compact ? 44 : 56 + 20,
                    Alpha = compact ? 0 : 1,
                },
                new IconButton
                {
                    Enabled = { Value = true },
                    Origin = Anchor.TopRight,
                    Anchor = Anchor.TopRight,
                    Size = new Vector2(compact ? 26 : 35),
                    IconScale = new Vector2(compact ? 0.65f : 0.8f),
                    Margin = new MarginPadding(compact ? 8 : 14),
                    Icon = FontAwesome.Solid.Times,
                    BackgroundColour = overlayColourProvider.Background4,
                    Action = () => CloseAction?.Invoke(),
                },
                title = new ProjectYomiSpriteText
                {
                    Origin = Anchor.TopLeft,
                    Anchor = Anchor.TopLeft,
                    Text = AXPlayerStrings.LiveChatReplay("0"),
                    Margin = new MarginPadding
                    {
                        Top = compact ? 10 : 16,
                        Left = compact ? 10 : 16,
                    },
                    Font = AXPlayerApp.DefaultFont.With(size: compact ? 16 : 30, weight: "ExtraBold"),
                    Colour = overlayColourProvider.Content2,
                },
            };

            updateState();

            if (messages.Count > 0)
                rebuildAsync(PlaybackPosition.Value).FireAndForget();
        }

        /// <summary>
        /// Replaces the currently loaded replay with the given messages.
        /// Messages are sorted by their video offset, so the source order does not matter.
        /// The window of messages around the current playback position is rendered across the following
        /// frames, so the returned task completes once the overlay has settled on the new replay.
        /// </summary>
        public async Task SetMessages(IEnumerable<LiveChatReplayMessage> replayMessages)
        {
            messages.Clear();
            messages.AddRange(replayMessages);
            messages.Sort(static (a, b) => a.OffsetTimeMsec.CompareTo(b.OffsetTimeMsec));

            isLoading = false;
            unavailable = false;

            resetRender();
            updateState();

            if (messages.Count > 0)
                await rebuildAsync(PlaybackPosition.Value);
        }

        /// <summary>
        /// Clears the replay and puts the overlay back into its initial state.
        /// The returned task completes once the overlay has settled on the cleared state.
        /// </summary>
        public async Task ClearMessages()
        {
            messages.Clear();

            isLoading = false;
            unavailable = false;

            resetRender();
            updateState();

            await awaitNextFrame();
        }

        /// <summary>
        /// Shows a "loading" placeholder until <see cref="SetMessages"/> is called.
        /// The returned task completes once the overlay has settled on the loading state.
        /// </summary>
        public async Task SetLoading()
        {
            messages.Clear();
            unavailable = false;
            isLoading = true;

            resetRender();
            updateState();

            await awaitNextFrame();
        }

        /// <summary>
        /// Shows a message which tells the user that this video has no chat replay.
        /// The returned task completes once the overlay has settled on the unavailable state.
        /// </summary>
        public async Task ShowUnavailable()
        {
            messages.Clear();
            isLoading = false;
            unavailable = true;

            resetRender();
            updateState();

            await awaitNextFrame();
        }

        public override void Show()
        {
            base.Show();

            if (messageFlow == null)
                return;

            // The overlay is hidden while the video keeps playing, so the rendered state
            // is no longer valid when it is opened again.
            lastRenderedTime = null;

            if (messages.Count > 0)
                rebuildAsync(PlaybackPosition.Value).FireAndForget();
        }

        protected override void Update()
        {
            base.Update();

            // A pass which is still adding messages owns the hierarchy, and records the position it
            // rendered once it finishes, so revealing resumes right after it.
            if (messages.Count == 0 || isRebuilding)
                return;

            double time = Math.Max(0, PlaybackPosition.Value);

            // Seeking backwards, or jumping far forwards, invalidates the whole rendered window.
            if (lastRenderedTime == null || time < lastRenderedTime.Value - 0.5 || time - lastRenderedTime.Value > 10)
            {
                rebuildAsync(time).FireAndForget();
                return;
            }

            if (time > lastRenderedTime.Value)
            {
                advanceTo(time);
                lastRenderedTime = time;
            }
        }

        private void resetRender()
        {
            // Invalidate a pass which is still running; it stops at its next frame boundary.
            renderVersion++;
            isRebuilding = false;

            nextMessageIndex = 0;
            lastRenderedTime = null;

            messageFlow?.Clear();
        }

        /// <summary>
        /// Renders the window of messages around the given position. The messages are added across the
        /// following frames, so a long replay never stalls a single frame, and a state change which
        /// happens in the meantime supersedes this pass.
        /// </summary>
        private async Task rebuildAsync(double time)
        {
            double clampedTime = Math.Max(0, time);

            if (messageFlow == null)
            {
                // Nothing to render before the hierarchy exists; the state is picked up once it is loaded.
                nextMessageIndex = findFirstIndexAfter(clampedTime * 1000);
                lastRenderedTime = clampedTime;
                return;
            }

            int version = ++renderVersion;
            isRebuilding = true;

            try
            {
                messageFlow.Clear();

                int endIndex = findFirstIndexAfter(clampedTime * 1000);
                int startIndex = Math.Max(0, endIndex - Math.Min(initialDisplayedMessages, maxDisplayedMessages));

                for (int i = startIndex; i < endIndex; i++)
                {
                    messageFlow.Add(new LiveChatMessageDisplay(messages[i], compact));

                    if ((i - startIndex + 1) % messages_per_frame != 0)
                        continue;

                    // Give the remaining messages of the window to the next frames.
                    await awaitNextFrame();

                    if (version != renderVersion)
                        return;
                }

                nextMessageIndex = endIndex;
                lastRenderedTime = clampedTime;

                scrollToEnd();
            }
            finally
            {
                if (version == renderVersion)
                    isRebuilding = false;
            }
        }

        private void advanceTo(double time)
        {
            double timeMsec = time * 1000;
            bool added = false;
            bool followNewMessages = isNearEnd();

            while (nextMessageIndex < messages.Count && messages[nextMessageIndex].OffsetTimeMsec <= timeMsec)
            {
                messageFlow.Add(new LiveChatMessageDisplay(messages[nextMessageIndex], compact));
                nextMessageIndex++;
                added = true;
            }

            if (!added)
                return;

            trimHistory();

            // Don't fight the user when they have scrolled up to read older messages.
            if (followNewMessages)
                scrollToEnd();
        }

        private bool isNearEnd()
        {
            if (messageFlow.Children.Count == 0)
                return true;

            return scrollContainer.Current + scrollContainer.DisplayableContent >= scrollContainer.ScrollableExtent - 8;
        }

        private void trimHistory()
        {
            while (messageFlow.Children.Count() > maxDisplayedMessages)
            {
                LiveChatMessageDisplay? oldest = messageFlow.Children.FirstOrDefault();

                if (oldest == null)
                    return;

                // Expire() only schedules removal, so remove immediately to guarantee
                // this loop terminates when many messages arrive at once.
                messageFlow.Remove(oldest, true);
            }
        }

        private void scrollToEnd()
        {
            // Newly added children are laid out in the next update, so scrolling immediately
            // would use stale child positions and could jump to the top of the content.
            if (scrollToEndScheduled)
                return;

            scrollToEndScheduled = true;

            // Defer by one frame: the newly added children are laid out during the current frame,
            // after this method returns. Scheduling for the next frame guarantees the layout is
            // up to date before the scroll target is calculated.
            Scheduler.AddDelayed(() =>
            {
                scrollToEndScheduled = false;

                if (messageFlow == null || messageFlow.Children.Count == 0)
                    return;

                scrollContainer.ScrollToEnd(false);
            }, 1);
        }

        /// <summary>
        /// Awaits the next update frame. The continuation always resumes on the update thread, so the
        /// draw hierarchy may be changed right after it, no matter which thread started the operation.
        /// </summary>
        private NextFrameAwaiter awaitNextFrame() => new NextFrameAwaiter(this);

        private readonly struct NextFrameAwaiter : ICriticalNotifyCompletion
        {
            private readonly LiveChatReplayOverlay overlay;

            public NextFrameAwaiter(LiveChatReplayOverlay overlay)
            {
                this.overlay = overlay;
            }

            public NextFrameAwaiter GetAwaiter() => this;

            public bool IsCompleted => false;

            public void GetResult()
            {
            }

            public void OnCompleted(Action continuation)
            {
                // A one millisecond delay keeps the continuation out of the current frame's scheduler
                // pass, which is what defers it to the next frame's update.
                overlay.Scheduler.AddDelayed(continuation, 1);
            }

            public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);
        }

        /// <summary>
        /// Returns the index of the first message that appears after the given video offset.
        /// </summary>
        private int findFirstIndexAfter(double timeMsec)
        {
            int low = 0;
            int high = messages.Count;

            while (low < high)
            {
                int middle = low + ((high - low) / 2);

                if (messages[middle].OffsetTimeMsec <= timeMsec)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        private Container createEmptyState(OverlayColourProvider overlayColourProvider)
        {
            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding
                {
                    Horizontal = compact ? 12 : 32,
                },
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        Direction = FillDirection.Vertical,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Width = compact ? 220 : 320,
                        AutoSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            new SpriteIcon
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Margin = new MarginPadding(compact ? 4 : 10),
                                Size = new Vector2(compact ? 24 : 42),
                                Icon = FontAwesome.Solid.CommentAlt,
                                Colour = overlayColourProvider.Content2.Opacity(0.7f),
                            },
                            emptyTitle = new ProjectYomiSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Font = AXPlayerApp.DefaultFont.With(size: compact ? 15 : 26, weight: "Bold"),
                                Text = AXPlayerStrings.LiveChatReplayEmptyTitle,
                                Colour = overlayColourProvider.Content2,
                            },
                            emptyDescription = new ProjectYomiTextFlowContainer(font => font.Font = AXPlayerApp.DefaultFont.With(size: compact ? 11.5f : 15, weight: "Regular"))
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Padding = new MarginPadding { Top = compact ? 4 : 8 },
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                TextAnchor = Anchor.Centre,
                                Text = AXPlayerStrings.LiveChatReplayEmptyDescription,
                                Colour = overlayColourProvider.Foreground2,
                            },
                        },
                    },
                },
            };
        }

        private Container createLoadingState()
        {
            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                Child = new AXPlayerLoadingLayer()
            };
        }

        private void updateState()
        {
            if (messageFlow == null)
                return;

            title.Text = AXPlayerStrings.LiveChatReplay(messages.Count.ToString(CultureInfo.InvariantCulture));

            emptyTitle.Text = AXPlayerStrings.LiveChatReplayEmptyTitle;
            emptyDescription.Text = unavailable ? AXPlayerStrings.LiveChatReplayUnavailable : AXPlayerStrings.LiveChatReplayEmptyDescription;

            emptyState.FadeTo(messages.Count == 0 && !isLoading ? 1 : 0, 200, Easing.OutQuint);
            loadingState.FadeTo(isLoading ? 1 : 0, 200, Easing.OutQuint);
        }
    }
}
