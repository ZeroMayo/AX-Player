// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
                    Alpha = 0.45f,
                },
                totalTime = new ProjectYomiSpriteText
                {
                    Font = AXPlayerApp.DefaultFont.With(weight: "Bold"),
                    Text = "0:00",
                    Alpha = 0.65f,
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

    internal partial class OdometerTextTicker : BufferedContainer
    {
        private const float tickerHeight = 22;
        private const double animationDuration = 180;

        private readonly FillFlowContainer tickerContent;
        private string currentValue = "0:00";
        private Drawable[] characters;

        public OdometerTextTicker()
        {
            AutoSizeAxes = Axes.X;
            Height = tickerHeight;
            Masking = true;
            BlurSigma = new osuTK.Vector2(0);
            Child = tickerContent = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
            };
            setCharacters(currentValue);
        }

        public void SetText(string text)
        {
            if (currentValue == text)
                return;

            if (text.Length != characters.Length)
            {
                currentValue = text;
                setCharacters(text);
            }
            else
            {
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == currentValue[i])
                        continue;

                    if (char.IsDigit(text[i]) && characters[i] is OdometerDigitTicker digitTicker)
                        digitTicker.SetDigit(text[i]);
                    else if (characters[i] is ProjectYomiSpriteText characterText)
                        characterText.Text = text[i].ToString();
                    else
                    {
                        currentValue = text;
                        setCharacters(text);
                        break;
                    }
                }

                currentValue = text;
            }

            this.TransformTo(nameof(BlurSigma), new osuTK.Vector2(2.5f), 60, Easing.OutCubic)
                .Then()
                .TransformTo(nameof(BlurSigma), new osuTK.Vector2(0), animationDuration, Easing.OutCubic);
        }

        private void setCharacters(string text)
        {
            var newCharacters = new List<Drawable>(text.Length);
            foreach (char character in text)
            {
                if (char.IsDigit(character))
                {
                    newCharacters.Add(new OdometerDigitTicker(character));
                }
                else
                {
                    newCharacters.Add(new ProjectYomiSpriteText
                    {
                        Font = AXPlayerApp.DefaultFont.With(weight: "Bold"),
                        Text = character.ToString(),
                    });
                }
            }

            characters = newCharacters.ToArray();
            tickerContent.Children = characters;
        }
    }

    internal partial class OdometerDigitTicker : Container
    {
        private const float tickerHeight = 22;
        private const float digitWidth = 12;
        private const double animationDuration = 180;

        private ProjectYomiSpriteText currentText;

        public OdometerDigitTicker(char digit)
        {
            Width = digitWidth;
            Height = tickerHeight;
            Masking = true;
            currentText = createText(digit);
            Child = currentText;
        }

        public void SetDigit(char digit)
        {
            if (currentText.Text == digit.ToString())
                return;

            ProjectYomiSpriteText previousText = currentText;
            currentText = createText(digit);
            currentText.Y = tickerHeight;
            currentText.Alpha = 0;
            AddInternal(currentText);

            previousText.MoveToY(-tickerHeight, animationDuration, Easing.OutCubic)
                        .FadeOut(animationDuration, Easing.OutCubic)
                        .OnComplete(_ => previousText.Expire());

            currentText.MoveToY(0, animationDuration, Easing.OutCubic)
                       .FadeIn(animationDuration, Easing.OutCubic);
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
