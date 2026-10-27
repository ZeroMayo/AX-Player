// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Reflection;
using AXPlayer.App.Graphics.Sprites.Lottie;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Video;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterface
{
    /// <summary>
    /// A loading spinner.
    /// </summary>
    public partial class AXPlayerLoadingSpinner : VisibilityContainer
    {
        private readonly LottieAnimationView spinner;
        private readonly Box bg;

        protected override bool StartHidden => true;

        protected CircularContainer MainContents;

        public const float TRANSITION_DURATION = 500;

        private const float spin_duration = 900;

        private readonly bool inverted;

        private Color4 bgColor;

        public Color4 BackgroundColour
        {
            get => bgColor;
            set
            {
                bgColor = value;

                bg.FadeColour(value, 500);
            }
        }

        private Color4 accentColor;

        public Color4 AccentColor
        {
            get => accentColor;
            set
            {
                accentColor = value;

                spinner.FadeColour(value, 500);
            }
        }

        /// <summary>
        /// Constuct a new loading spinner.
        /// </summary>
        public AXPlayerLoadingSpinner(bool withBox = false, bool inverted = false)
        {
            this.inverted = inverted;
            Size = new Vector2(70);

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            Child = MainContents = new CircularContainer
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Children = new Drawable[]
                {
                    bg = new Box
                    {
                        Colour = Color4.White,
                        RelativeSizeAxes = Axes.Both,
                        Alpha = withBox ? 0.7f : 0
                    },
                    spinner = new LottieAnimationView
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Scale = new Vector2(withBox ? 1.5f : 2.75f),
                        RelativeSizeAxes = Axes.Both,
                        FileName = "Lottie/AXPlayer_Loading.json",
                        AutoPlay = true,
                        RepeatCount = -1,
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider, TextureStore textures)
        {
            bg.Colour = inverted ? overlayColourProvider.Background3 : overlayColourProvider.Content2;
            spinner.Colour = inverted ? overlayColourProvider.Content2 : overlayColourProvider.Background3;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
        }

        protected override void Dispose(bool isDisposing)
        {
            spinner.Dispose();
            base.Dispose(isDisposing);
        }

        protected override void PopIn()
        {
            MainContents.ScaleTo(1, TRANSITION_DURATION, Easing.OutQuint);
            this.FadeIn(TRANSITION_DURATION * 2, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            MainContents.ScaleTo(0.8f, TRANSITION_DURATION / 2, Easing.In);
            this.FadeOut(TRANSITION_DURATION, Easing.OutQuint);
        }
    }
}
