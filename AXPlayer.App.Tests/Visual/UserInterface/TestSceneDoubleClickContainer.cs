// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using AXPlayer.App.Graphics.Containers;
using NUnit.Framework;
using osu.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Testing;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneDoubleClickContainer : AXPlayerTestScene
    {
        private TestableDoubleClickContainer container = null!;
        private int doubleClickCount;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create container", () =>
            {
                doubleClickCount = 0;

                Child = container = new TestableDoubleClickContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(320, 180),
                    DoubleClickAction = () => doubleClickCount++,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(0.15f, 0.15f, 0.15f, 1f),
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = "Double click me",
                        },
                    },
                };
            });
        }

        [Test]
        public void TestDefaultEnabledState()
        {
            AddAssert("matches platform default", () => container.Enabled.Value == !RuntimeInfo.IsDesktop);
        }

        [Test]
        public void TestDoubleClickInvokesAction()
        {
            AddStep("enable gesture", () => container.Enabled.Value = true);
            AddStep("double click", () => container.SimulateDoubleClick());

            AddAssert("action invoked once", () => doubleClickCount == 1);
        }

        [Test]
        public void TestDisabledGestureIsIgnored()
        {
            AddStep("disable gesture", () => container.Enabled.Value = false);
            AddStep("double click", () => container.SimulateDoubleClick());

            AddAssert("action not invoked", () => doubleClickCount == 0);
        }

        private partial class TestableDoubleClickContainer : DoubleClickContainer
        {
            public void SimulateDoubleClick() => OnDoubleClick(new DoubleClickEvent(new InputState(), MouseButton.Left));
        }
    }
}
