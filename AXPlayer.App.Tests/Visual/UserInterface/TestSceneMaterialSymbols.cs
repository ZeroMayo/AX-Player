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
    public partial class TestSceneMaterialSymbols : AXPlayerTestScene
    {
        [Resolved]
        private FontStore fontStore { get; set; } = null!;

        public TestSceneMaterialSymbols()
        {
            Add(new BasicScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new FillFlowContainer
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(16),
                    Padding = new MarginPadding(16),
                    Children = new Drawable[]
                    {
                        createSection("Filled", typeof(MaterialSymbols.Filled)),
                        createSection("Outlined", typeof(MaterialSymbols.Outlined)),
                    },
                },
            });
        }

        [Test]
        public void TestEveryIconResolvesAGlyph()
        {
            AddStep("every catalogue icon resolves a glyph", () =>
            {
                var missing = enumerateIcons()
                              .Where(i => fontStore.Get(i.icon.FontName, i.icon.Icon) == null)
                              .Select(i => $"{i.name} ({i.icon.FontName})")
                              .ToArray();

                Assert.That(missing, Is.Empty, () => "material symbols without a glyph: " + string.Join(", ", missing));
            });
        }

        private static IEnumerable<(string name, IconUsage icon)> enumerateIcons()
            => new[] { typeof(MaterialSymbols.Filled), typeof(MaterialSymbols.Outlined) }
               .SelectMany(iconProperties)
               .Select(p => (p.Name, (IconUsage)p.GetValue(null)!));

        private static IEnumerable<PropertyInfo> iconProperties(Type iconSource)
            => iconSource.GetProperties(BindingFlags.Public | BindingFlags.Static)
                         .Where(p => p.PropertyType == typeof(IconUsage))
                         .OrderBy(p => p.Name, StringComparer.Ordinal);

        private static Drawable createSection(string title, Type iconSource)
        {
            var grid = new FillFlowContainer
            {
                Width = 880,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(4),
                ChildrenEnumerable = iconProperties(iconSource).Select(createTile),
            };

            return new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(8),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = title,
                        Font = new FontUsage(size: 20),
                    },
                    grid,
                },
            };
        }

        private static Drawable createTile(PropertyInfo property)
        {
            var icon = (IconUsage)property.GetValue(null)!;

            return new FillFlowContainer
            {
                Size = new Vector2(86, 80),
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(2),
                Children = new Drawable[]
                {
                    new SpriteIcon
                    {
                        Icon = icon,
                        Size = new Vector2(32),
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                    },
                    new SpriteText
                    {
                        Text = property.Name,
                        Font = new FontUsage(size: 10),
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                    },
                },
            };
        }
    }
}
