// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using AXPlayer.App.Graphics.Shaders.New;
using AXPlayer.App.Graphics.Shaders.New.MotionBlur;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Tests.Visual.Shaders
{
    /// <summary>
    /// Shows an unprocessed motion source next to the same source processed by
    /// <see cref="MotionBlurContainer"/>, so the estimated motion smear can be compared directly.
    /// </summary>
    [TestFixture]
    public partial class TestSceneVideoMotionBlur : AXPlayerTestScene
    {
        private MotionBlurContainer motionBlur = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            motionBlur = new MotionBlurContainer
            {
                RelativeSizeAxes = Axes.Both,
                Strength = 1,
                Child = new MovingSource(),
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
                        createPanel("Raw", new MovingSource()),
                        createPanel("Motion blurred", motionBlur),
                    },
                });
            });

            AddSliderStep("strength", 0f, 1f, 1f, value => motionBlur.Strength = value);
        }

        [Test]
        public void TestStrengthAndType()
        {
            AddAssert("shader type is motion blur", () => motionBlur.Type == ShaderType.MotionBlur);
            AddStep("set half strength", () => motionBlur.Strength = 0.5f);
            AddAssert("strength applied", () => motionBlur.Strength == 0.5f);
            AddUntilStep("motion blur is loaded", () => motionBlur.IsLoaded);
        }

        [Test]
        public void TestDisabledPassesSourceThrough()
        {
            AddStep("disable effect", () => motionBlur.Strength = 0);
            AddUntilStep("motion blur is loaded", () => motionBlur.IsLoaded);
            AddAssert("strength is zero", () => motionBlur.Strength == 0);
            AddStep("re-enable effect", () => motionBlur.Strength = 1);
            AddAssert("strength is restored", () => motionBlur.Strength == 1);
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
                    new SpriteText
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

        private partial class MovingSource : Container
        {
            private readonly Circle[] markers;

            public MovingSource()
            {
                RelativeSizeAxes = Axes.Both;
                Masking = true;

                Add(new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(18 / 255f, 20 / 255f, 27 / 255f, 1f),
                });

                markers = new[]
                {
                    createMarker(new Color4(1f, 92 / 255f, 92 / 255f, 1f), 54),
                    createMarker(new Color4(86 / 255f, 196 / 255f, 1f, 1f), 38),
                    createMarker(new Color4(1f, 196 / 255f, 76 / 255f, 1f), 26),
                };

                AddRange(markers);
            }

            protected override void Update()
            {
                base.Update();

                double time = Time.Current;
                float width = DrawWidth > 0 ? DrawWidth : 520;

                for (int i = 0; i < markers.Length; i++)
                {
                    Circle marker = markers[i];
                    float travel = width + marker.Width * 2;
                    float x = (float)((i * 173 + time * 0.18 * (1.0 + i * 0.35)) % travel);

                    marker.X = x - marker.Width;
                    marker.Y = 70 + i * 78;
                }
            }

            private static Circle createMarker(Color4 colour, float size)
            {
                return new Circle
                {
                    Size = new Vector2(size),
                    Colour = colour,
                };
            }
        }
    }
}
