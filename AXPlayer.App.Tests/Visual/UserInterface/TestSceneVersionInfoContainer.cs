// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Overlays;
using AXPlayer.App.Updater;
using NUnit.Framework;
using osu.Framework.Graphics.Sprites;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneVersionInfoContainer : AXPlayerTestScene
    {
        // Add visual tests to ensure correct behaviour of your game: https://github.com/ppy/osu-framework/wiki/Development-and-Testing
        // You can make changes to classes associated with the tests and they will recompile and update immediately.

        private VersionInfoContainer versionInfoContainer;

        public TestSceneVersionInfoContainer()
        {
            Add(versionInfoContainer = new VersionInfoContainer(new AXPlayerUpdateInfo
            {
                UpdateFileSize = 1024,
                IsBetaVersion = true,
                Version = "a.b.c-stream",
            }));
        }

        [Test]
        public void UpdateState()
        {
            AddStep("update state to Updating", () =>
            {
                versionInfoContainer.UpdateState(UpdaterStatus.Updating);
            });

            AddStep("update state to Checking", () =>
            {
                versionInfoContainer.UpdateState(UpdaterStatus.Checking);
            });

            AddStep("update state to Error", () =>
            {
                versionInfoContainer.UpdateState(UpdaterStatus.Error);
            });

            AddStep("update state to NoUpdates", () =>
            {
                versionInfoContainer.UpdateState(UpdaterStatus.NoUpdates);
            });

            AddStep("update state to RestartRequired", () =>
            {
                versionInfoContainer.UpdateState(UpdaterStatus.RestartRequired);
            });
        }
    }
}
