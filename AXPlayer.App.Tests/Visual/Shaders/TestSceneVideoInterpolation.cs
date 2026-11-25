// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using AXPlayer.App.Graphics.Shaders.New;
using AXPlayer.App.Graphics.Shaders.New.Interpolation;
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
    /// Shows a raw 30 FPS motion source next to the same source processed by
    /// <see cref="InterpolationContainer"/>, so the temporal smoothing can be compared directly.
    /// </summary>
    [TestFixture]
    public partial class TestSceneVideoInterpolation : AXPlayerTestScene
    {
        private InterpolationContainer interpolation = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            interpolation = new InterpolationContainer
            {
                RelativeSizeAxes = Axes.Both,
                Strength = 1,
                Child = new SteppedMotionSource(),
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
                        createPanel("Raw (30 FPS)", new SteppedMotionSource()),
                        createPanel("Interpolated", interpolation),
                    },
                });
            });

            AddSliderStep("strength", 0f, 1f, 1f, value => interpolation.Strength = value);
        }

        [Test]
        public void TestStrengthAndType()
        {
            AddAssert("shader type is interpolation", () => interpolation.Type == ShaderType.Interpolation);
            AddStep("set half strength", () => interpolation.Strength = 0.5f);
            AddAssert("strength applied", () => interpolation.Strength == 0.5f);
            AddUntilStep("interpolation is loaded", () => interpolation.IsLoaded);
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

        private partial class SteppedMotionSource : Container
        {
            private const double frame_duration = 1000.0 / 30.0;
            private const float step = 3.5f;

            private readonly Circle[] markers;

            public SteppedMotionSource()
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

                double frame = Math.Floor(Time.Current / frame_duration);
                float width = DrawWidth > 0 ? DrawWidth : 520;

                for (int i = 0; i < markers.Length; i++)
                {
                    Circle marker = markers[i];
                    float travel = width + marker.Width * 2;
                    float x = (float)((i * 173 + frame * step * (1.0f + i * 0.15f)) % travel);
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
