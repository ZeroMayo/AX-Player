// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class AXPlayerSeekBar
    {
        public partial class SliderNub : Container
        {
            public const float HEIGHT = 30;

            public const float DEFAULT_EXPANDED_SIZE = 20;

            public SliderNub(float expandedSize = DEFAULT_EXPANDED_SIZE)
            {
                Size = new Vector2(expandedSize, expandedSize);

                InternalChildren = new[]
                {
                    new CircularContainer
                    {
                        Masking = true,
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Alpha = 1,
                                AlwaysPresent = true,
                            },
                        }
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
            }
        }
    }
}
