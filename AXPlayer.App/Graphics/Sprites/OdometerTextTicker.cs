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

            int sharedLength = System.Math.Min(text.Length, characters.Length);
            int oldOffset = characters.Length - sharedLength;
            int newOffset = text.Length - sharedLength;
            var newCharacters = new Drawable[text.Length];
            var retainedCharacters = new HashSet<Drawable>();
            var newLeadingDigits = new OdometerDigitTicker[newOffset];

            for (int i = 0; i < newOffset; i++)
            {
                var digitTicker = new OdometerDigitTicker('0')
                {
                    Alpha = 0,
                };
                newCharacters[i] = newLeadingDigits[i] = digitTicker;
            }

            for (int i = 0; i < sharedLength; i++)
            {
                int oldIndex = oldOffset + i;
                int newIndex = newOffset + i;
                char oldCharacter = currentValue[oldIndex];
                char newCharacter = text[newIndex];
                Drawable oldDrawable = characters[oldIndex];

                if (char.IsDigit(oldCharacter) && char.IsDigit(newCharacter)
                    && oldDrawable is OdometerDigitTicker digitTicker)
                {
                    digitTicker.SetDigit(newCharacter);
                    newCharacters[newIndex] = digitTicker;
                    retainedCharacters.Add(digitTicker);
                }
                else if (!char.IsDigit(oldCharacter) && !char.IsDigit(newCharacter)
                         && oldDrawable is Container characterContainer
                         && characterContainer.Children[0] is ProjectYomiSpriteText characterText)
                {
                    if (oldCharacter != newCharacter)
                        characterText.Text = newCharacter.ToString();

                    newCharacters[newIndex] = characterContainer;
                    retainedCharacters.Add(characterContainer);
                }
                else
                    newCharacters[newIndex] = createTransitionCharacter(oldCharacter, newCharacter);
            }

            foreach (Drawable oldCharacter in characters)
            {
                if (!retainedCharacters.Contains(oldCharacter))
                    tickerContent.Remove(oldCharacter, true);
            }

            for (int i = 0; i < newCharacters.Length; i++)
            {
                if (!retainedCharacters.Contains(newCharacters[i]))
                    tickerContent.Add(newCharacters[i]);

                tickerContent.SetLayoutPosition(newCharacters[i], i);
            }

            for (int i = 0; i < newLeadingDigits.Length; i++)
            {
                newLeadingDigits[i].SetDigit(text[i]);
                newLeadingDigits[i].FadeIn(animationDuration, Easing.OutExpo);
            }

            currentValue = text;
            characters = newCharacters;
        }

        private void setCharacters(string text)
        {
            var newCharacters = new List<Drawable>(text.Length);
            foreach (char character in text)
                newCharacters.Add(createCharacter(character));

            characters = newCharacters.ToArray();
            tickerContent.Children = characters;
        }

        private Drawable createCharacter(char character)
        {
            if (char.IsDigit(character))
                return new OdometerDigitTicker(character);

            return createNonDigitCharacter(character);
        }

        private Drawable createTransitionCharacter(char previousCharacter, char currentCharacter)
        {
            if (!char.IsDigit(currentCharacter))
                return createNonDigitCharacter(currentCharacter);

            char initialDigit = char.IsDigit(previousCharacter) ? previousCharacter : '0';
            var digitTicker = new OdometerDigitTicker(initialDigit);
            if (initialDigit != currentCharacter)
                digitTicker.SetDigit(currentCharacter);

            return digitTicker;
        }

        private Drawable createNonDigitCharacter(char character)
        {
            return new Container
            {
                AutoSizeAxes = Axes.X,
                Height = tickerHeight,
                Child = new ProjectYomiSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Font = AXPlayerApp.DefaultFont.With(weight: "Bold"),
                    Text = character.ToString(),
                }
            };
        }
    }
}
