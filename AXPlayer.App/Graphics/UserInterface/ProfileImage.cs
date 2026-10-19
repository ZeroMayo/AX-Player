// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.YouTube.v3.Data;
using AXPlayer.App.Config;
using AXPlayer.App.Extensions;
using AXPlayer.App.Online;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class ProfileImage : CompositeDrawable, IHasCustomTooltip<Channel>
    {
        private readonly Sprite profileImage;
        private readonly Container profileImageBase;
        private readonly AXPlayerLoadingLayer loading;
        private readonly Box hover;
        private readonly HoverSounds samples = new HoverClickSounds(HoverSampleSet.Default);

        private Channel channel;
        private Bindable<ProfileImageShape> profileImageShape;
        private CancellationTokenSource imageLoadCancellation;
        private int imageLoadVersion;

        [Resolved]
        private TextureStore textureStore { get; set; }

        [Resolved]
        private YouTubeAPI api { get; set; }

        [Resolved]
        private AXPlayerAppBase app { get; set; }

        [Resolved]
        private AXPlayerConfigManager appConfig { get; set; }

        public virtual LocalisableString TooltipText { get; protected set; }

        public Bindable<bool> Enabled { get; } = new BindableBool(true);

        public ProfileImage(float size = 30)
        {
            Size = new osuTK.Vector2(size);
            Masking = true;

            InternalChildren = new Drawable[]
            {
                samples,
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                },
                profileImageBase = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Masking = true,
                    Child = profileImage = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                },
                hover = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                },
                loading = new AXPlayerLoadingLayer(true, false, false),
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            BorderColour = overlayColourProvider.Light4;
            BorderThickness = 0;

            profileImageShape = appConfig.GetBindable<ProfileImageShape>(AXPlayerSetting.ProfileImageShape);
            profileImageShape.BindValueChanged(shape => applyShape(shape.NewValue), true);
        }

        private void applyShape(ProfileImageShape shape)
        {
            float cornerRadius = shape == ProfileImageShape.Circle ? Height / 2 : AXPlayerApp.UI_CORNER_RADIUS / 2;

            this.TransformTo(nameof(CornerRadius), new CornersInfo(cornerRadius), 500, Easing.OutQuint);
            profileImageBase.TransformTo(nameof(CornerRadius), new CornersInfo(cornerRadius), 500, Easing.OutQuint);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (Enabled.Value)
                profileImageBase.ScaleTo(0.8f, 2000, Easing.OutQuint);

            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            if (Enabled.Value)
                profileImageBase.ScaleTo(1f, 350, Easing.OutQuint);
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (Enabled.Value)
            {
                profileImage.ScaleTo(1.1f, 350, Easing.OutQuint);
                hover.FadeTo(0.1f, 500, Easing.OutQuint);
                this.TransformTo(nameof(BorderThickness), 2f, 250, Easing.OutQuint);
            }

            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);

            if (Enabled.Value)
            {
                profileImage.ScaleTo(1f, 350, Easing.OutQuint);
                this.TransformTo(nameof(BorderThickness), 0f, 250, Easing.OutQuint);
                hover.FadeOut(500, Easing.OutQuint);
            }
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (channel != null && Enabled.Value)
                app.Host.OpenUrlExternally($"https://www.youtube.com/channel/{channel.Id}");

            return base.OnClick(e);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            ((HoverClickSounds)samples).Enabled.Value = Enabled.Value;
        }

        public void UpdateProfileImage(string channelId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

            var cancellation = new CancellationTokenSource();
            var previousCancellation = Interlocked.Exchange(ref imageLoadCancellation, cancellation);
            previousCancellation?.Cancel();
            previousCancellation?.Dispose();

            int version = Interlocked.Increment(ref imageLoadVersion);
            loadChannelAndProfileImageAsync(channelId, version, cancellation.Token).FireAndForget();
        }

        private async Task loadChannelAndProfileImageAsync(string channelId, int version, CancellationToken cancellationToken)
        {
            try
            {
                Channel loadedChannel = await Task.Run(() => api.GetChannel(channelId), cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                string imageUrl = loadedChannel?.Snippet?.Thumbnails?.High?.Url;
                if (string.IsNullOrEmpty(imageUrl))
                    return;

                Schedule(() =>
                {
                    if (version == Volatile.Read(ref imageLoadVersion))
                        channel = loadedChannel;
                });

                await loadProfileImageAsync(imageUrl, version, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer channel was requested or this drawable was disposed.
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"Failed to load profile image for channel '{channelId}'.");
            }
        }

        public async Task GetProfileImage(string url, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);
            await loadProfileImageAsync(url, Volatile.Read(ref imageLoadVersion), cancellationToken).ConfigureAwait(false);
        }

        private async Task loadProfileImageAsync(string url, int version, CancellationToken cancellationToken)
        {
            Schedule(() =>
            {
                if (version == Volatile.Read(ref imageLoadVersion))
                    loading.Show();
            });

            try
            {
                Texture texture = await textureStore.GetAsync(url, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                Schedule(() =>
                {
                    if (version == Volatile.Read(ref imageLoadVersion))
                        profileImage.Texture = texture;
                });
            }
            finally
            {
                Schedule(() =>
                {
                    if (version == Volatile.Read(ref imageLoadVersion))
                        loading.Hide();
                });
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                Interlocked.Increment(ref imageLoadVersion);
                imageLoadCancellation?.Cancel();
                imageLoadCancellation?.Dispose();
                profileImageShape?.UnbindAll();
            }

            base.Dispose(isDisposing);
        }

        ITooltip<Channel> IHasCustomTooltip<Channel>.GetCustomTooltip() => new ProfileImageTooltip();

        Channel IHasCustomTooltip<Channel>.TooltipContent => channel;
    }
}
