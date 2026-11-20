// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.UserInterface;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    public partial class TestSceneWavySeekBar : AXPlayerTestScene
    {
        public TestSceneWavySeekBar()
        {
            Add(new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Horizontal = 80, Vertical = 40 },
                Children = new Drawable[]
                {
                    new WavySeekBar<double>
                    {
                        Width = 720,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Current = new BindableNumber<double>(0.62)
                        {
                            MinValue = 0,
                            MaxValue = 1,
                            Default = 0.25,
                        },
                        PlaybackSpeed = { Value = 1.25 },
                        IsPlaying = { Value = true },
                    },
                },
            });
        }
    }
}
