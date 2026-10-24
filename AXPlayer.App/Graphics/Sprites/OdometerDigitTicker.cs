// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace AXPlayer.App.Graphics.Sprites
{
    public partial class OdometerDigitTicker : BufferedContainer
    {
        private const float tickerHeight = 30;
        private const float digitWidth = 9;
        private const double animationDuration = 500;

        private ProjectYomiSpriteText currentText;
        private char currentDigit;

        public OdometerDigitTicker(char digit)
        {
            Width = digitWidth;
            Height = tickerHeight;
            Masking = true;
            currentDigit = digit;
            currentText = createText(digit);
            Child = currentText;
        }

        public void SetDigit(char digit)
        {
            if (currentDigit == digit)
                return;

            bool isIncreasing = digit > currentDigit;
            currentDigit = digit;
            float direction = isIncreasing ? 1 : -1;
            ProjectYomiSpriteText previousText = currentText;
            currentText = createText(digit);
            currentText.Y = tickerHeight * direction;
            currentText.Scale = new osuTK.Vector2(0.25f);
            currentText.Alpha = 0;
            AddInternal(currentText);

            this.TransformTo(nameof(BlurSigma), new osuTK.Vector2(0, 1.5f), 60, Easing.OutCubic)
                .Then()
                .TransformTo(nameof(BlurSigma), new osuTK.Vector2(0), animationDuration, Easing.OutCubic);

            previousText.MoveToY(-tickerHeight * direction, animationDuration, Easing.OutExpo)
                        .ScaleTo(0.25f, animationDuration, Easing.OutExpo)
                        .FadeOut(animationDuration, Easing.OutExpo)
                        .OnComplete(_ => previousText.Expire());

            currentText.MoveToY(0, animationDuration, Easing.OutExpo)
                       .ScaleTo(1, animationDuration, Easing.OutExpo)
                       .FadeIn(animationDuration, Easing.OutExpo);
        }

        private ProjectYomiSpriteText createText(char digit)
        {
            return new ProjectYomiSpriteText
            {
                AlwaysPresent = true,
                Font = AXPlayerApp.DefaultFont.With(weight: "Bold"),
                Text = digit.ToString(),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };
        }
    }
}
