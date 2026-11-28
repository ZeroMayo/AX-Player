// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AXPlayer.App.Graphics;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.IO.Stores;
using osuTK;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneFontAwesomeBrands : AXPlayerTestScene
    {
        [Resolved]
        private FontStore fontStore { get; set; } = null!;

        public TestSceneFontAwesomeBrands()
        {
            Add(new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Full,
                Width = 720,
                Spacing = new Vector2(8),
                ChildrenEnumerable = brandProperties().Select(createTile),
            });
        }

        [Test]
        public void TestEveryBrandResolvesAGlyph()
        {
            AddStep("every brand icon resolves a glyph", () =>
            {
                var missing = brandProperties()
                              .Select(p => (p.Name, icon: (IconUsage)p.GetValue(null)!))
                              .Where(i => fontStore.Get(i.icon.FontName, i.icon.Icon) == null)
                              .Select(i => $"{i.Name} ({i.icon.FontName})")
                              .ToArray();

                Assert.That(missing, Is.Empty, () => "brand icons without a glyph: " + string.Join(", ", missing));
            });
        }

        private static IEnumerable<PropertyInfo> brandProperties()
            => typeof(FontAwesomeBrands).GetProperties(BindingFlags.Public | BindingFlags.Static)
                                              .Where(p => p.PropertyType == typeof(IconUsage))
                                              .OrderBy(p => p.Name, StringComparer.Ordinal);

        private static Drawable createTile(PropertyInfo property)
        {
            var icon = (IconUsage)property.GetValue(null)!;

            return new FillFlowContainer
            {
                Size = new Vector2(160, 120),
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(4),
                Children = new Drawable[]
                {
                    new SpriteIcon
                    {
                        Icon = icon,
                        Size = new Vector2(48),
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                    },
                    new SpriteText
                    {
                        Text = property.Name,
                        Font = new FontUsage(size: 12),
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                    },
                },
            };
        }
    }
}
