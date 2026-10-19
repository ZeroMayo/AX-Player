// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Config;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterfaceV2
{
    public partial class SwitchButton : Checkbox
    {
        private const float border_thickness = 3f;
        private const float padding = 1.25f;

        private readonly Box fill;
        private readonly Container nubContainer;
        private readonly Drawable nub;
        private readonly CircularContainer content;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private Sample? sampleChecked;
        private Sample? sampleUnchecked;

        public SwitchButton()
        {
            Size = new Vector2(45, 20);

            InternalChild = content = new CircularContainer
            {
                RelativeSizeAxes = Axes.Both,
                BorderColour = Color4.White,
                BorderThickness = border_thickness,
                Masking = true,
                Children = new Drawable[]
                {
                    fill = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        AlwaysPresent = true,
                        Alpha = 0
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding(border_thickness + padding),
                        Child = nubContainer = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = nub = new Circle
                            {
                                RelativeSizeAxes = Axes.Both,
                                FillMode = FillMode.Fit,
                                Masking = true,
                            }
                        }
                    }
                }
            };
        }

        [BackgroundDependencyLoader(true)]
        private void load(AudioManager audio)
        {
            sampleChecked = audio.Samples.Get(@"UI/check-on");
            sampleUnchecked = audio.Samples.Get(@"UI/check-off");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindDisabledChanged(_ => updateColours());
            Current.BindValueChanged(_ => updateState(), true);

            FinishTransforms(true);
        }

        private void updateState()
        {
            nub.MoveToX(Current.Value ? nubContainer.DrawWidth - nub.DrawWidth : 0, 200, Easing.OutQuint);
            fill.FadeTo(Current.Value ? 1 : 0, 250, Easing.OutQuint);

            updateColours();
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateColours();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateColours();
            base.OnHoverLost(e);
        }

        [Resolved]
        private AXPlayerConfigManager config { get; set; }

        protected override void OnUserChange(bool value)
        {
            base.OnUserChange(value);

            if (config.Get<bool>(AXPlayerSetting.PlayOverlaySFX))
            {
                if (value)
                    sampleChecked?.Play();
                else
                    sampleUnchecked?.Play();
            }
        }

        private void updateColours()
        {
            ColourInfo borderColour;
            ColourInfo switchColour;

            if (Current.Disabled)
            {
                borderColour = colourProvider.Dark2;
                switchColour = colourProvider.Dark1;
                fill.Colour = colourProvider.Dark5;
            }
            else
            {
                bool hover = IsHovered && !Current.Disabled;

                borderColour = hover ? colourProvider.Foreground1.Opacity(0.5f) : colourProvider.Foreground1.Opacity(0.3f);
                switchColour = hover ? colourProvider.Foreground1 : colourProvider.Foreground1.Lighten(0.1f);

                if (!Current.Value)
                {
                    borderColour = borderColour.MultiplyAlpha(0.8f);
                    switchColour = switchColour.MultiplyAlpha(0.8f);
                }

                if (Current.Value)
                {
                    borderColour = colourProvider.Content2;
                    switchColour = hover ? colourProvider.Highlight1 : colourProvider.Light4;
                }

                fill.Colour = colourProvider.Content2;
            }

            nubContainer.FadeColour(switchColour, 250, Easing.OutQuint);
            content.TransformTo(nameof(BorderColour), borderColour, 250, Easing.OutQuint);
        }
    }
}
