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
                    Margin = new MarginPadding
                    {
                        Top = 7
                    },
                    Font = AXPlayerApp.DefaultFont.With(weight: "Bold"),
                    Text = " / ",
                    Alpha = 0.45f,
                },
                totalTime = new ProjectYomiSpriteText
                {
                    Margin = new MarginPadding
                    {
                        Top = 7
                    },
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

    internal partial class OdometerTextTicker : Container
    {
        private const float tickerHeight = 30;
        private const double animationDuration = 500;

        private readonly FillFlowContainer tickerContent;
        private string currentValue = "0:00";
        private Drawable[] characters;

        public OdometerTextTicker()
        {
            AutoSizeAxes = Axes.X;
            Height = tickerHeight;
            Masking = true;
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
                    newCharacters.Add(new Container
                    {
                        AutoSizeAxes = Axes.X,
                        Height = 30,
                        Child = new ProjectYomiSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Font = AXPlayerApp.DefaultFont.With(weight: "Bold"),
                            Text = character.ToString(),
                        }
                    });
                }
            }

            characters = newCharacters.ToArray();
            tickerContent.Children = characters;
        }
    }

    internal partial class OdometerDigitTicker : BufferedContainer
    {
        private const float tickerHeight = 30;
        private const float digitWidth = 9;
        private const double animationDuration = 500;

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
            currentText.Scale = new osuTK.Vector2(0.25f);
            currentText.Alpha = 0;
            AddInternal(currentText);

            this.TransformTo(nameof(BlurSigma), new osuTK.Vector2(0, 1.5f), 60, Easing.OutCubic)
                .Then()
                .TransformTo(nameof(BlurSigma), new osuTK.Vector2(0), animationDuration, Easing.OutCubic);

            previousText.MoveToY(-tickerHeight, animationDuration, Easing.OutExpo)
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
