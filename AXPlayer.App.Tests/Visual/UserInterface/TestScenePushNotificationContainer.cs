// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.Containers;
using NUnit.Framework;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestScenePushNotificationContainer : NekoPlayerTestScene
    {
        // Add visual tests to ensure correct behaviour of your game: https://github.com/ppy/osu-framework/wiki/Development-and-Testing
        // You can make changes to classes associated with the tests and they will recompile and update immediately.

        public TestScenePushNotificationContainer()
        {
            Add(new PushNotificationContainer(FontAwesome.Solid.Bell, Color4.Yellow, "Test notification test テスト 테스트 123\nmultiline test\nmultiline test", "test テスト 테스트 123")
            {
                Anchor = osu.Framework.Graphics.Anchor.Centre,
                Origin = osu.Framework.Graphics.Anchor.Centre,
            });
        }
    }
}
