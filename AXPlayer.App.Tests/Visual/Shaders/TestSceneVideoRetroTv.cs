// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.Shaders.New;
using AXPlayer.App.Graphics.Shaders.New.RetroTV;
using AXPlayer.App.Graphics.Sprites;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Tests.Visual.Shaders
{
    /// <summary>
    /// Shows a clean broadcast test pattern next to the same pattern processed by
    /// <see cref="RetroTvContainer"/>, so the curved screen, VHS colour bleed, scanlines
    /// and tape grain can be compared directly.
    /// </summary>
    [TestFixture]
    public partial class TestSceneVideoRetroTv : AXPlayerTestScene
    {
        private RetroTvContainer retroTv = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            retroTv = new RetroTvContainer
            {
                RelativeSizeAxes = Axes.Both,
                Strength = 1,
                Curvature = 1,
                Noise = 1,
                Child = new TestPattern(),
            };

            AddStep("create comparison", () =>
            {
                Add(new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(24, 0),
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        createPanel("Raw", new TestPattern()),
                        createPanel("80s TV", retroTv),
                    },
                });
            });

            AddSliderStep("strength", 0f, 1f, 1f, value => retroTv.Strength = value);
            AddSliderStep("curvature", 0f, 1f, 1f, value => retroTv.Curvature = value);
            AddSliderStep("noise", 0f, 1f, 1f, value => retroTv.Noise = value);
        }

        [Test]
        public void TestStrengthCurvatureAndType()
        {
            AddAssert("shader type is retro TV", () => retroTv.Type == ShaderType.RetroTv);
            AddStep("set half strength", () => retroTv.Strength = 0.5f);
            AddStep("flatten the tube", () => retroTv.Curvature = 0f);
            AddStep("remove the grain", () => retroTv.Noise = 0f);
            AddAssert("values applied", () => retroTv.Strength == 0.5f && retroTv.Curvature == 0f && retroTv.Noise == 0f);
            AddUntilStep("effect is loaded", () => retroTv.IsLoaded);
        }

        [Test]
        public void TestDisabledPassesSourceThrough()
        {
            AddStep("disable effect", () => retroTv.Strength = 0);
            AddUntilStep("effect is loaded", () => retroTv.IsLoaded);
            AddAssert("strength is zero", () => retroTv.Strength == 0);
            AddStep("re-enable effect", () => retroTv.Strength = 1);
            AddAssert("strength is restored", () => retroTv.Strength == 1);
        }

        [Test]
        public void TestElapsedTimeAdvances()
        {
            AddUntilStep("effect is loaded", () => retroTv.IsLoaded);
            AddUntilStep("time advances", () => retroTv.ElapsedTime > 0);
        }

        private static Drawable createPanel(string title, Drawable content)
        {
            return new FillFlowContainer
            {
                Direction = FillDirection.Vertical,
                AutoSizeAxes = Axes.Both,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    new ProjectYomiSpriteText
                    {
                        Text = title,
                        Font = AXPlayerApp.DefaultFont.With(size: 18),
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                    },
                    new Container
                    {
                        Size = new Vector2(520, 292),
                        Masking = true,
                        CornerRadius = 8,
                        Child = content,
                    },
                },
            };
        }

        private partial class TestPattern : Container
        {
            private static readonly Color4[] bars =
            {
                new(1f, 1f, 1f, 1f),
                new(1f, 1f, 0f, 1f),
                new(0f, 1f, 1f, 1f),
                new(0f, 1f, 0f, 1f),
                new(1f, 0f, 1f, 1f),
                new(1f, 0f, 0f, 1f),
                new(0f, 0f, 1f, 1f),
            };

            private readonly Circle marker;

            public TestPattern()
            {
                RelativeSizeAxes = Axes.Both;
                Masking = true;

                Add(new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(18 / 255f, 20 / 255f, 27 / 255f, 1f),
                });

                for (int i = 0; i < bars.Length; i++)
                {
                    Add(new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Size = new Vector2(1f / bars.Length, 0.68f),
                        RelativePositionAxes = Axes.X,
                        X = i / (float)bars.Length,
                        Colour = bars[i],
                    });
                }

                for (int i = 0; i < 8; i++)
                {
                    float level = i / 7f;

                    Add(new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Size = new Vector2(1f / 8f, 0.18f),
                        RelativePositionAxes = Axes.Both,
                        Position = new Vector2(i / 8f, 0.68f),
                        Colour = new Color4(level, level, level, 1f),
                    });
                }

                Add(new ProjectYomiSpriteText
                {
                    Text = "80s",
                    Font = AXPlayerApp.DefaultFont.With(size: 64),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = Color4.White,
                });

                Add(marker = new Circle
                {
                    Size = new Vector2(42),
                    Colour = Color4.White,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Y = 220,
                });
            }

            protected override void Update()
            {
                base.Update();

                float width = DrawWidth > 0 ? DrawWidth : 520;
                marker.X = (float)(Time.Current * 0.16 % (width + marker.Width)) - marker.Width;
            }
        }
    }
}