// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Graphics.UserInterfaceV2;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osuTK;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneSettingsSearch : AXPlayerTestScene
    {
        // Add visual tests to ensure correct behaviour of your game: https://github.com/ppy/osu-framework/wiki/Development-and-Testing
        // You can make changes to classes associated with the tests and they will recompile and update immediately.

        private SearchTextBox searchTextBox;

        private FillFlowContainer settingsContent;

        private SettingsSectionHeader audioHeader;
        private SettingsSectionHeader graphicsHeader;

        private SettingsItemV2 volumeSetting;
        private SettingsItemV2 fpsSetting;
        private SettingsButtonV2 clearCachesButton;
        private SettingsFilterableContainer captionPreview;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create settings list", () =>
            {
                Child = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 500,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Children = new Drawable[]
                    {
                        searchTextBox = new SearchTextBox(),
                        settingsContent = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 2),
                            Children = new Drawable[]
                            {
                                audioHeader = new SettingsSectionHeader { Text = "Audio Settings" },
                                volumeSetting = new SettingsItemV2(new FormCheckBox
                                {
                                    Caption = "Video volume",
                                    Current = new BindableBool(),
                                }),
                                graphicsHeader = new SettingsSectionHeader { Text = "Graphics Settings" },
                                fpsSetting = new SettingsItemV2(new FormCheckBox
                                {
                                    Caption = "Show FPS",
                                    Current = new BindableBool(),
                                })
                                {
                                    Keywords = new[] { "performance" },
                                },
                                clearCachesButton = new SettingsButtonV2 { Text = "Clear all caches" },
                                captionPreview = new SettingsFilterableContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 40,
                                },
                            },
                        },
                    },
                };

                searchTextBox.Current.BindValueChanged(term => SettingsSearchFilter.Apply(settingsContent.Children, term.NewValue), true);
            });
        }

        [Test]
        public void TestMatchingSettingsAndHeaders()
        {
            AddStep("search for volume", () => searchTextBox.Current.Value = "volume");

            AddAssert("volume setting is shown", () => volumeSetting.IsPresent);
            AddAssert("audio header is shown", () => audioHeader.IsPresent);
            AddAssert("fps setting is hidden", () => !fpsSetting.IsPresent);
            AddAssert("graphics header is hidden", () => !graphicsHeader.IsPresent);
            AddAssert("content without search terms is hidden", () => !captionPreview.IsPresent);

            AddStep("search for performance", () => searchTextBox.Current.Value = "performance");

            AddAssert("fps setting is shown via keyword", () => fpsSetting.IsPresent);
            AddAssert("graphics header is shown", () => graphicsHeader.IsPresent);
            AddAssert("volume setting is hidden", () => !volumeSetting.IsPresent);
            AddAssert("audio header is hidden", () => !audioHeader.IsPresent);

            AddStep("search for graphics", () => searchTextBox.Current.Value = "graphics");

            AddAssert("whole graphics section is shown", () => graphicsHeader.IsPresent && fpsSetting.IsPresent && clearCachesButton.IsPresent);
            AddAssert("audio section is hidden", () => !audioHeader.IsPresent && !volumeSetting.IsPresent);

            AddStep("search for caches", () => searchTextBox.Current.Value = "caches");

            AddAssert("button is shown", () => clearCachesButton.IsPresent);

            AddStep("search for volume", () => searchTextBox.Current.Value = "volume");

            AddAssert("button is hidden", () => !clearCachesButton.IsPresent);
        }

        [Test]
        public void TestNoMatches()
        {
            bool anyMatch = true;

            AddStep("search for something unknown", () => anyMatch = SettingsSearchFilter.Apply(settingsContent.Children, "definitely not a setting"));

            AddAssert("no matches reported", () => !anyMatch);
            AddAssert("no settings are shown", () => !volumeSetting.IsPresent && !fpsSetting.IsPresent);
            AddAssert("no headers are shown", () => !audioHeader.IsPresent && !graphicsHeader.IsPresent);
        }

        [Test]
        public void TestClearingSearch()
        {
            AddStep("search for volume", () => searchTextBox.Current.Value = "volume");
            AddStep("clear search", () => searchTextBox.Current.Value = string.Empty);

            AddAssert("all settings are shown", () => volumeSetting.IsPresent && fpsSetting.IsPresent && clearCachesButton.IsPresent);
            AddAssert("all headers are shown", () => audioHeader.IsPresent && graphicsHeader.IsPresent);
            AddAssert("content without search terms is shown", () => captionPreview.IsPresent);
        }
    }
}
