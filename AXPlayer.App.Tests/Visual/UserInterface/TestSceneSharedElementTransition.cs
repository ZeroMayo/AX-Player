// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.Transitions;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneSharedElementTransition : AXPlayerTestScene
    {
        private SharedElementTransitionLayer layer = null!;
        private Container source = null!;
        private Container destination = null!;
        private bool completed;

        public TestSceneSharedElementTransition()
        {
            Add(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    destination = new Container
                    {
                        Position = new Vector2(420, 120),
                        Size = new Vector2(360, 260),
                        Masking = true,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.SteelBlue,
                        },
                    },
                    source = new Container
                    {
                        Position = new Vector2(80, 320),
                        Size = new Vector2(180, 100),
                        Masking = true,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.OrangeRed,
                        },
                    },
                    layer = new SharedElementTransitionLayer(),
                },
            });
        }

        [Test]
        public void TestExpandingIntoDestination()
        {
            AddStep("play the transition", () =>
            {
                completed = false;

                layer.Play(source, destination, createContent, sourceCornerRadius: 12, destinationCornerRadius: 4, duration: 200, fadeOutDuration: 50,
                    completed: () => completed = true);
            });

            AddAssert("the source is hidden", () => source.Alpha, () => Is.Zero);
            AddAssert("one transition is active", () => layer.ActiveTransitionCount, () => Is.EqualTo(1));

            AddUntilStep("the transition finishes", () => completed);
            AddUntilStep("the proxy is removed", () => !layer.IsTransitioning);
            AddAssert("the source is visible again", () => source.Alpha, () => Is.EqualTo(1f));
        }

        [Test]
        public void TestCollapsingBackIntoSource()
        {
            AddStep("play the reverse transition", () =>
            {
                completed = false;

                layer.Play(destination, source, createContent, sourceCornerRadius: 4, destinationCornerRadius: 12, duration: 200, fadeOutDuration: 50,
                    completed: () => completed = true);
            });

            AddAssert("the destination is hidden", () => destination.Alpha, () => Is.Zero);
            AddAssert("one transition is active", () => layer.ActiveTransitionCount, () => Is.EqualTo(1));

            AddUntilStep("the transition finishes", () => completed);
            AddUntilStep("the proxy is removed", () => !layer.IsTransitioning);
            AddAssert("the destination is visible again", () => destination.Alpha, () => Is.EqualTo(1f));
        }

        private static Drawable createContent() => new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = Color4.White,
        };
    }
}
