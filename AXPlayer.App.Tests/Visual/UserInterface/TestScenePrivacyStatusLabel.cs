// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System.Collections.Generic;
using AXPlayer.App.Extensions;
using AXPlayer.App.Localisation;
using AXPlayer.App.Online;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    /// <summary>
    /// Renders the label of the privacy of a playlist row, which is read from the status the API
    /// reports for it.
    /// </summary>
    [TestFixture]
    public partial class TestScenePrivacyStatusLabel : AXPlayerTestScene
    {
        private static readonly string[] api_values = { "public", "unlisted", "private" };

        private FillFlowContainer labelContainer = null!;
        private readonly List<SpriteText> labels = new List<SpriteText>();

        public TestScenePrivacyStatusLabel()
        {
            Add(labelContainer = new FillFlowContainer
            {
                Width = 400,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
            });
        }

        [Test]
        public void TestEveryStatusRendersItsOwnLabel()
        {
            AddStep("show the labels of the three statuses", () =>
            {
                labelContainer.Clear();
                labels.Clear();

                foreach (string apiValue in api_values)
                {
                    var label = new SpriteText { RelativeSizeAxes = Axes.X };

                    label.Text = apiValue.toPrivacyLabel();
                    labels.Add(label);
                    labelContainer.Add(label);
                }
            });

            AddAssert("the labels are the translated ones", () => labels[0].Text.ToString(), () => Is.EqualTo(AXPlayerStrings.Public.ToString()));
            AddAssert("the unlisted label is shown", () => labels[1].Text.ToString(), () => Is.EqualTo(AXPlayerStrings.Unlisted.ToString()));
            AddAssert("the private label is shown", () => labels[2].Text.ToString(), () => Is.EqualTo(AXPlayerStrings.Private.ToString()));
        }

        [Test]
        public void TestAnUnknownStatusKeepsTheLabelEmpty()
        {
            // The library pages only render a badge for the playlists that are not public, so a row
            // without a status must not claim one.
            AddStep("show the label of an unknown status", () =>
            {
                labelContainer.Clear();
                labels.Clear();

                foreach (string? apiValue in new string?[] { null, string.Empty, "video" })
                {
                    var label = new SpriteText { RelativeSizeAxes = Axes.X };

                    label.Text = apiValue.toPrivacyLabel();
                    labels.Add(label);
                    labelContainer.Add(label);
                }
            });

            AddAssert("the labels are empty", () => labels[0].Text.ToString(), () => Is.Empty);
            AddAssert("the empty status keeps the label empty", () => labels[1].Text.ToString(), () => Is.Empty);
            AddAssert("a status that is no privacy keeps the label empty", () => labels[2].Text.ToString(), () => Is.Empty);
        }

        [Test]
        public void TestTheStatusMapsOntoTheEnum()
        {
            Assert.That("public".toPrivacyStatus(), Is.EqualTo(PrivacyStatus.Public));
            Assert.That("UNLISTED".toPrivacyStatus(), Is.EqualTo(PrivacyStatus.Unlisted));
            Assert.That("private".toPrivacyStatus(), Is.EqualTo(PrivacyStatus.Private));
            Assert.That("video".toPrivacyStatus(), Is.Null);
            Assert.That(((string?)null).toPrivacyStatus(), Is.Null);
        }
    }
}
