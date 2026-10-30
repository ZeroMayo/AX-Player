// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK.Graphics;
using AXPlayer.App.Graphics.UserInterface;
using osu.Framework.Bindables;
using AXPlayer.App.Online;

namespace AXPlayer.App.Graphics.UserInterfaceV2
{
    public partial class FormControlBackgroundWithDiscordProfileImage : CompositeDrawable
    {
        public const float CORNER_EXPONENT = 2.5f;
        public const float BORDER_THICKNESS = 2.5f;

        private VisualStyle visualStyle;

        public VisualStyle VisualStyle
        {
            get => visualStyle;
            set
            {
                visualStyle = value;
                updateStyle();
            }
        }

        private bool enabled;

        public bool Enabled
        {
            get => enabled;
            set
            {
                enabled = value;
                updateStyle();
            }
        }

        public bool IsCheckbox;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private readonly Box box, box2;
        private readonly Box flashLayer;

        private readonly HoverSounds sounds;

        private readonly DiscordProfileImageWithoutMasking profileImage;

        public FormControlBackgroundWithDiscordProfileImage(FormControlCornerType cornerType)
        {
            RelativeSizeAxes = Axes.Both;

            Masking = true;

            switch (cornerType)
            {
                case FormControlCornerType.Top:
                    CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, 8, AXPlayerApp.UI_CORNER_RADIUS, 8);
                    break;
                case FormControlCornerType.Center:
                    CornerRadius = new CornersInfo(8, 8, 8, 8);
                    break;
                case FormControlCornerType.Bottom:
                    CornerRadius = new CornersInfo(8, AXPlayerApp.UI_CORNER_RADIUS, 8, AXPlayerApp.UI_CORNER_RADIUS);
                    break;
                case FormControlCornerType.None:
                default:
                    CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS);
                    break;
            }

            BorderThickness = BORDER_THICKNESS;

            InternalChildren = new Drawable[]
            {
                box = new Box
                {
                    Colour = Color4.White,
                    RelativeSizeAxes = Axes.Both,
                },
                profileImage = new DiscordProfileImageWithoutMasking(100)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
                flashLayer = new Box
                {
                    Colour = Colour4.Transparent,
                    RelativeSizeAxes = Axes.Both,
                    //Blending = BlendingParameters.Additive,
                },
                sounds = new HoverSounds(),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            updateStyle();
            UpdateLoginState();
            FinishTransforms(true);
        }

        public void UpdateLoginState()
        {
            if (discordRPC.GetCurrentUser() != null)
            {
                profileImage.FadeIn(250);
            }
            else
            {
                profileImage.FadeOut(250);
            }
        }

        [Resolved]
        private Online.DiscordRPCClient discordRPC { get; set; }

        [Resolved]
        private YouTubeAPI yt_api { get; set; }

        private void flash(Colour4 flashColour, double duration)
        {
            flashLayer.Colour = ColourInfo.GradientHorizontal(flashColour.Opacity(0), flashColour);
            flashLayer.FadeOutFromOne(duration, Easing.OutQuint);
        }

        private void updateStyle()
        {
            sounds.Enabled.Value = visualStyle != VisualStyle.Disabled;

            ColourInfo colour;

            switch (visualStyle)
            {
                case VisualStyle.Normal:
                    colour = colourProvider.Background4.Darken(0.1f);
                    break;

                case VisualStyle.Disabled:
                    colour = colourProvider.Background4;
                    break;

                case VisualStyle.Hovered:
                    colour = colourProvider.Background3.Darken(0.1f);
                    break;

                case VisualStyle.Focused:
                    colour = colourProvider.Background3.Darken(0.1f);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            this.TransformTo(nameof(BorderColour), colour, 250, Easing.OutQuint);

            //hoverLayer.FadeTo(focused ? 0.2f : 0f, 250, Easing.OutQuint);

            /*
            if (IsCheckbox)
                this.TransformTo(nameof(CornerRadius), enabled ? new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS / 1.5f) : new CornersInfo(DrawHeight / 2), 250, Easing.OutQuint);
            */

            box.FadeColour(colour, 250, Easing.OutQuint);
        }
    }
}
