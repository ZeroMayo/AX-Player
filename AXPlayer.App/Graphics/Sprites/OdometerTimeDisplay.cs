// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;

namespace AXPlayer.App.Graphics.Sprites
{
    public partial class OdometerTimeDisplay : FillFlowContainer
    {
        private readonly OdometerTextTicker currentTime;
        private readonly ProjectYomiSpriteText separator;
        private readonly ProjectYomiSpriteText totalTime;

        public OdometerTimeDisplay()
        {
            AutoSizeAxes = Axes.Both;
            Direction = FillDirection.Horizontal;

            Children = new Drawable[]
            {
                currentTime = new OdometerTextTicker(),
                separator = new ProjectYomiSpriteText
                {
                    Font = AXPlayerApp.DefaultFont.With(weight: "Bold"),
                    Text = " / ",
                },
                totalTime = new ProjectYomiSpriteText
                {
                    Font = AXPlayerApp.DefaultFont.With(weight: "Bold"),
                    Text = "0:00",
                },
            };
        }

        public void SetTime(string current, string total)
        {
            currentTime.SetText(current);
            if (totalTime.Text != total)
                totalTime.Text = total;
        }
    }

    internal partial class OdometerTextTicker : Container
    {
        private const float tickerHeight = 22;
        private const double animationDuration = 180;

        private ProjectYomiSpriteText currentText;

        public OdometerTextTicker()
        {
            AutoSizeAxes = Axes.X;
            Height = tickerHeight;
            Masking = true;
            currentText = createText("0:00");
            Child = currentText;
        }

        public void SetText(string text)
        {
            if (currentText.Text == text)
                return;

            ProjectYomiSpriteText previousText = currentText;
            currentText = createText(text);
            currentText.Y = tickerHeight;
            currentText.Alpha = 0;
            AddInternal(currentText);

            previousText.MoveToY(-tickerHeight, animationDuration, Easing.OutCubic)
                        .FadeOut(animationDuration, Easing.OutCubic)
                        .OnComplete(_ => previousText.Expire());

            currentText.MoveToY(0, animationDuration, Easing.OutCubic)
                       .FadeIn(animationDuration, Easing.OutCubic);
        }

        private ProjectYomiSpriteText createText(string text)
        {
            return new ProjectYomiSpriteText
            {
                AlwaysPresent = true,
                Font = AXPlayerApp.DefaultFont.With(weight: "Bold"),
                Text = text,
            };
        }
    }
}
