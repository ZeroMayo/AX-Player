// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Localisation;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class TimestampButton : ProjectYomiClickableContainer
    {
        private string text;
        public Action<double> TimestampClicked;

        public TimestampButton(string text)
            : base(HoverSampleSet.Button)
        {
            Anchor = Anchor.CentreLeft;
            Origin = Anchor.CentreLeft;
            this.text = text;
            Enabled.Value = true;
            Masking = true;
            TooltipText = AXPlayerStrings.JumpTo(text);
        }

        private ProjectYomiTextFlowContainer textFlow;
        protected Box Hover;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            AutoSizeAxes = Axes.Both;

            AddRangeInternal(new Drawable[]
            {
                new CircularContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = overlayColourProvider.Background2,
                        },
                        textFlow = new ProjectYomiTextFlowContainer(f =>
                        {
                            f.Font = AXPlayerApp.DefaultFont.With(size: 13.5f, weight: "Bold");
                        })
                        {
                            AutoSizeAxes = Axes.Both,
                            Margin = new MarginPadding(4),
                        },
                        Hover = new Box
                        {
                            Alpha = 0,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.White,
                            Blending = BlendingParameters.Additive,
                            Depth = float.MinValue
                        },
                    }
                }
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            textFlow.AddIcon(FontAwesome.Solid.Stopwatch, o => o.Margin = new MarginPadding() { Right = 4 });
            textFlow.AddText(text);
        }

        protected virtual float HoverLayerFinalAlpha => 0.1f;

        protected override bool OnHover(HoverEvent e)
        {
            if (Enabled.Value)
            {
                Hover.FadeTo(0.2f, 40, Easing.OutQuint)
                     .Then()
                     .FadeTo(HoverLayerFinalAlpha, 800, Easing.OutQuint);
            }

            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);

            Hover.FadeOut(800, Easing.OutQuint);
        }

        protected override bool OnClick(ClickEvent e)
        {
            TimeSpan ts = TimeSpan.Parse(text);
            int seconds = (int)ts.TotalSeconds;

            Logger.Log(seconds.ToString());

            TimestampClicked.Invoke(Convert.ToDouble(seconds));

            return base.OnClick(e);
        }
    }
}
