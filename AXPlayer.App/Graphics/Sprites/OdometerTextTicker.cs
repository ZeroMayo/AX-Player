// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace AXPlayer.App.Graphics.Sprites
{
    public partial class OdometerTextTicker : Container
    {
        private const float tickerHeight = 30;
        private const double animationDuration = 500;

        private readonly FillFlowContainer tickerContent;
        private string currentValue = "0:00";
        private Drawable[] characters;

        public OdometerTextTicker(string text = "0:00")
        {
            currentValue = text;
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
}
