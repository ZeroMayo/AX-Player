// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.UserInterface;
using NUnit.Framework;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneNekoPlayerLoadingSpinner : AXPlayerTestScene
    {
        // Add visual tests to ensure correct behaviour of your game: https://github.com/ppy/osu-framework/wiki/Development-and-Testing
        // You can make changes to classes associated with the tests and they will recompile and update immediately.

        private AXPlayerLoadingSpinner spinner;

        public TestSceneNekoPlayerLoadingSpinner()
        {
            Add(spinner = new AXPlayerLoadingSpinner(true, true));

            spinner.Show();
        }
    }
}
