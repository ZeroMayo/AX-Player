// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;

namespace AXPlayer.App.Graphics.Sprites.Odometer
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
}
