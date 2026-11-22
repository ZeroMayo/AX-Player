// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using AXPlayer.App.Extensions;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Input.Binding;
using AXPlayer.App.Localisation;
using AXPlayer.App.Resources;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osuTK;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    /// <summary>
    /// Renders the descriptions of the global shortcuts and the Innertube dump settings, which are
    /// resolved from the localisation resources of every shipped language.
    /// </summary>
    [TestFixture]
    public partial class TestSceneShortcutDescriptionLocalisation : AXPlayerTestScene
    {
        private const string resource_name = @"AXPlayer.App.Resources.Localisation.AXPlayer";

        /// <summary>
        /// The strings that are rendered by the debug section of the settings and by the shortcut list.
        /// </summary>
        private static readonly string[] translated_keys =
        {
            @"innertube_dump_toggle",
            @"innertube_dump_toggle_desc",
            @"shortcut_openwatchhistory",
        };

        private static readonly ResourceManager resource_manager = new ResourceManager(resource_name, AXPlayerAppResources.ResourceAssembly);

        private FillFlowContainer lines = null!;

        public TestSceneShortcutDescriptionLocalisation()
        {
            Add(new BasicScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = lines = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 4),
                },
            });
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("clear the rendered lines", () => lines.Clear());
        }

        [Test]
        public void TestEveryShortcutRendersItsDescription()
        {
            AddStep("show every shortcut description", () =>
            {
                lines.Clear();

                foreach (GlobalAction action in Enum.GetValues<GlobalAction>())
                    lines.Add(new ProjectYomiSpriteText { Text = AXPlayerStrings.ShortcutDescription(action) });
            });

            AddAssert("every shortcut has a description", () => Enum.GetValues<GlobalAction>().All(action => !string.IsNullOrWhiteSpace(resolve(@"shortcut_" + action.ToString().ToLowerInvariant()))));
        }

        [Test]
        public void TestTheInnertubeDumpStringsRender()
        {
            AddStep("show the Innertube dump strings", () =>
            {
                lines.Clear();

                lines.Add(new ProjectYomiSpriteText { Text = AXPlayerStrings.InnertubeDumpToggle, Colour = Colour4.White });
                lines.Add(new ProjectYomiSpriteText { Text = AXPlayerStrings.InnertubeDumpToggleDesc, Colour = Colour4.LightGray });
            });
        }

        /// <summary>
        /// A resource that is not translated falls back to the English text, so a language only
        /// translates a key when its value is present and differs from the English one.
        /// </summary>
        [Test]
        public void TestEveryLanguageTranslatesTheStrings()
        {
            var untranslated = new List<string>();

            foreach (Language language in Enum.GetValues<Language>())
            {
                // the debug language is only compiled into debug builds and never has resources of its own
                if (language.ToString() == @"debug")
                    continue;

                var culture = new CultureInfo(language.ToCultureCode());

                foreach (string key in translated_keys)
                {
                    string english = resource_manager.GetString(key, CultureInfo.InvariantCulture) ?? string.Empty;
                    string localised = resource_manager.GetString(key, culture) ?? string.Empty;

                    Assert.That(english, Is.Not.Empty, $"The English text of \"{key}\" is missing.");

                    if (string.IsNullOrWhiteSpace(localised) || (language != Language.en && localised == english))
                        untranslated.Add($"{language}: {key}");
                }
            }

            Assert.That(untranslated, Is.Empty, "These strings are not translated: " + string.Join(", ", untranslated));
        }

        private static string resolve(string key) => resource_manager.GetString(key, CultureInfo.CurrentCulture) ?? string.Empty;
    }
}
