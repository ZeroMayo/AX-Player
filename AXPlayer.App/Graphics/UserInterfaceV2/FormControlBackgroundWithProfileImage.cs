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
    public partial class FormControlBackgroundWithProfileImage : CompositeDrawable
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

        private readonly ProfileImageWithoutMasking profileImage;

        public FormControlBackgroundWithProfileImage()
        {
            RelativeSizeAxes = Axes.Both;

            Masking = true;
            CornerRadius = AXPlayerApp.UI_CORNER_RADIUS / 1.5f;

            BorderThickness = BORDER_THICKNESS;

            InternalChildren = new Drawable[]
            {
                box = new Box
                {
                    Colour = Color4.White,
                    RelativeSizeAxes = Axes.Both,
                },
                profileImage = new ProfileImageWithoutMasking(100)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
                flashLayer = new Box
                {
                    Colour = Colour4.Transparent,
                    RelativeSizeAxes = Axes.Both,
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
            if (googleOAuth2.SignedIn.Value)
            {
                profileImage.FadeIn(250);
                profileImage.UpdateProfileImage(yt_api.GetMineChannel().Id);
            }
            else
            {
                profileImage.FadeOut(250);
            }
        }

        [Resolved]
        private GoogleOAuth2 googleOAuth2 { get; set; }

        [Resolved]
        private YouTubeAPI yt_api { get; set; }

        private void flash(Colour4 flashColour, double duration)
        {
            flashLayer.Colour = ColourInfo.GradientHorizontal(flashColour.Opacity(0), flashColour);
            flashLayer.FadeOutFromOne(duration, Easing.OutQuint);
        }

        /// <summary>
        /// Use when indicating that a change in value or a definitive action has occurred.
        /// </summary>
        public void FlashOnCommit() => flash(colourProvider.Dark2, 800);

        /// <summary>
        /// Use when rejecting the user's input as incorrect.
        /// </summary>
        public void FlashOnInputError() => flash(Colour4.Red, 200);

        private void updateStyle()
        {
            sounds.Enabled.Value = visualStyle != VisualStyle.Disabled;

            ColourInfo colour;
            ColourInfo borderColour;

            bool border = false;

            switch (visualStyle)
            {
                case VisualStyle.Normal:
                    colour = colourProvider.Background4.Darken(0.1f);
                    borderColour = colourProvider.Light4;
                    break;

                case VisualStyle.Disabled:
                    colour = colourProvider.Background4;
                    borderColour = colourProvider.Dark1;
                    break;

                case VisualStyle.Hovered:
                    colour = ColourInfo.GradientHorizontal(colourProvider.Background5, colourProvider.Dark4);
                    borderColour = colourProvider.Light4;
                    border = true;
                    break;

                case VisualStyle.Focused:
                    colour = ColourInfo.GradientHorizontal(colourProvider.Background5, colourProvider.Dark3);
                    border = true;
                    borderColour = colourProvider.Highlight1;
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            this.TransformTo(nameof(BorderColour), border ? borderColour : colour, 250, Easing.OutQuint);

            if (IsCheckbox)
                this.TransformTo(nameof(CornerRadius), enabled ? new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS / 1.5f) : new CornersInfo(DrawHeight / 2), 250, Easing.OutQuint);

            box.FadeColour(colour, 250, Easing.OutQuint);
        }
    }
}
