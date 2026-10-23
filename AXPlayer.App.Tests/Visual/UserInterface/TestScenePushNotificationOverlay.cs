// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Overlays;
using NUnit.Framework;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestScenePushNotificationOverlay : AXPlayerTestScene
    {
        // Add visual tests to ensure correct behaviour of your game: https://github.com/ppy/osu-framework/wiki/Development-and-Testing
        // You can make changes to classes associated with the tests and they will recompile and update immediately.

        private PushNotificationOverlay pushNotificationOverlay;

        public TestScenePushNotificationOverlay()
        {
            Add(new Box
            {
                RelativeSizeAxes = osu.Framework.Graphics.Axes.Both
            });
        }

        [Test]
        public void PrepareOverlay()
        {
            AddStep("create overlay", () => Add(pushNotificationOverlay = new PushNotificationOverlay
            {
                Anchor = osu.Framework.Graphics.Anchor.Centre,
                Origin = osu.Framework.Graphics.Anchor.Centre,
            }));
        }

        [Test]
        public void PushNotification()
        {
            AddStep("push notification", () =>
            {
                pushNotificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.Bell, Color4.Yellow, "Test notification", "test テスト 테스트 123"));
            });
        }

        [Test]
        public void ShowHideOverlay()
        {
            AddStep("show overlay", () =>
            {
                pushNotificationOverlay.OpenOverlay();
            });

            AddStep("hide overlay", () =>
            {
                pushNotificationOverlay.HideOverlay();
            });
        }
    }
}
