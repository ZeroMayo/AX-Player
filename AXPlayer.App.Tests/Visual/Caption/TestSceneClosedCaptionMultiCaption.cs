// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics.Caption;
using AXPlayer.App.Graphics.Videos;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osuTK.Graphics;

namespace AXPlayer.App.Tests.Visual.Caption
{
    /// <summary>
    /// Covers SRV3 documents that contain multiple paragraphs active at the same
    /// time. Each active cue must be rendered instead of only the first one found.
    /// </summary>
    [TestFixture]
    public partial class TestSceneClosedCaptionMultiCaption : AXPlayerTestScene
    {
        private const string srv3_xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<timedtext format=""3"">
  <head>
    <pen id=""0"" fc=""#FFFFFF"" fo=""255"" />
    <pen id=""1"" fc=""#FF0000"" fo=""255"" bc=""#000000"" bo=""0"" />
    <pen id=""2"" fc=""#00FF00"" fo=""128"" bc=""#0000FF"" bo=""128"" />
    <ws id=""0"" ju=""0"" />
    <wp id=""0"" ap=""1"" ah=""50"" av=""100"" />
    <wp id=""1"" ap=""1"" ah=""50"" av=""20"" />
    <wp id=""2"" ap=""1"" ah=""30"" av=""70"" />
    <wp id=""3"" ap=""1"" ah=""70"" av=""70"" />
    <wp id=""4"" ap=""4"" ah=""50"" av=""50"" />
  </head>
  <body>
    <p t=""1000"" d=""3000"" wp=""0"" ws=""0"" p=""0"">Bottom line</p>
    <p t=""1000"" d=""3000"" wp=""1"" ws=""0"" p=""0"">Top line</p>
    <p t=""4000"" d=""1000"" wp=""0"" ws=""0"" p=""0"">Only one</p>
    <p t=""6000"" d=""4000"" wp=""0"" ws=""0"" p=""0"">Long line</p>
    <p t=""7000"" d=""1000"" wp=""1"" ws=""0"" p=""0"">Nested line</p>
    <p t=""15000"" d=""4000"" wp=""2"" ws=""0"" p=""0"">Left custom</p>
    <p t=""15000"" d=""4000"" wp=""3"" ws=""0"" p=""0"">Right custom</p>
    <p t=""20000"" d=""4000"" wp=""0"" ws=""0"" p=""1""><s p=""1"">Red</s><s p=""2"">Green</s></p>
    <p t=""25000"" d=""3000"" wp=""4"" ws=""0"" p=""0"">Centered line</p>
    <p t=""28000"" d=""3000"" wp=""0"" ws=""0"" p=""0"">Overlap A</p>
    <p t=""28000"" d=""3000"" wp=""0"" ws=""0"" p=""0"">Overlap B</p>
  </body>
</timedtext>";

        private YouTubeVideoPlayer videoPlayer;
        private ClosedCaptionContainer captionContainer;

        [Resolved]
        private AXPlayerConfigManager config { get; set; } = null!;

        public TestSceneClosedCaptionMultiCaption()
        {
            videoPlayer = new YouTubeVideoPlayer(string.Empty, string.Empty, null!, srv3_xml, null!, 0);
            videoPlayer.VideoProgress.MaxValue = 30;

            Add(captionContainer = new ClosedCaptionContainer(videoPlayer, srv3_xml));
        }

        [Test]
        public void TestOverlappingCaptionsShareStartTime()
        {
            AddStep("seek into overlapping captions", () => videoPlayer.VideoProgress.Value = 2);

            AddUntilStep("both cues are rendered", () => renderedTexts().SequenceEqual(new[] { "Bottom line", "Top line" }));

            AddStep("seek into single caption", () => videoPlayer.VideoProgress.Value = 4.5);

            AddUntilStep("only one cue is rendered", () => renderedTexts().SequenceEqual(new[] { "Only one" }));
        }

        [Test]
        public void TestCaptionNestedInsideLongerCaption()
        {
            AddStep("seek into nested captions", () => videoPlayer.VideoProgress.Value = 7.5);

            AddUntilStep("long and nested cues are rendered", () => renderedTexts().SequenceEqual(new[] { "Long line", "Nested line" }));

            AddStep("seek past every caption", () => videoPlayer.VideoProgress.Value = 12);

            AddUntilStep("no cue is rendered", () => renderedTexts().Length == 0);
        }

        [Test]
        public void TestCustomPositionCoordinates()
        {
            AddStep("seek into custom positions", () => videoPlayer.VideoProgress.Value = 16);

            AddUntilStep("custom cues are rendered", () => renderedTexts().SequenceEqual(new[] { "Left custom", "Right custom" }));

            AddAssert("ah/av place each caption at its custom coordinate", () =>
            {
                SpriteText[] texts = renderedSpriteTexts();
                SpriteText left = texts.Single(text => text.Text.ToString() == "Left custom");
                SpriteText right = texts.Single(text => text.Text.ToString() == "Right custom");

                osuTK.Vector2 containerOrigin = captionContainer.ToScreenSpace(osuTK.Vector2.Zero);
                float width = captionContainer.DrawWidth;
                float height = captionContainer.DrawHeight;

                float leftCenter = centerX(left) - containerOrigin.X;
                float rightCenter = centerX(right) - containerOrigin.X;
                float centerYValue = centerY(left) - containerOrigin.Y;

                return leftCenter > width * 0.2f && leftCenter < width * 0.4f
                       && rightCenter > width * 0.6f && rightCenter < width * 0.8f
                       && centerYValue > height * 0.55f && centerYValue < height * 0.85f;
            });
        }

        private static float centerX(SpriteText text)
            => (text.ToScreenSpace(osuTK.Vector2.Zero).X + text.ToScreenSpace(new osuTK.Vector2(text.DrawWidth, 0)).X) / 2;

        private static float centerY(SpriteText text)
            => (text.ToScreenSpace(osuTK.Vector2.Zero).Y + text.ToScreenSpace(new osuTK.Vector2(0, text.DrawHeight)).Y) / 2;

        [Test]
        public void TestCustomPenColours()
        {
            AddStep("use a known base background", () =>
            {
                config.GetBindable<Colour4>(AXPlayerSetting.CaptionBGColor).Value = Colour4.Black;
                config.GetBindable<float>(AXPlayerSetting.CaptionBGOpacity).Value = 0.5f;
            });
            AddStep("seek into custom pen colours", () => videoPlayer.VideoProgress.Value = 21);

            AddUntilStep("coloured spans are rendered", () => renderedTexts().SequenceEqual(new[] { "Green", "Red" }));

            AddAssert("foreground colours come from the pens", () =>
            {
                SpriteText[] texts = renderedSpriteTexts();
                Color4 red = texts.Single(text => text.Text.ToString() == "Red").Colour;
                Color4 green = texts.Single(text => text.Text.ToString() == "Green").Colour;

                return approximately(red, new Color4(1, 0, 0, 1))
                       && approximately(green, new Color4(0, 1, 0, 128 / 255f));
            });

            AddAssert("pen background transparency overrides the box opacity", () => captionContainer.ChildrenOfType<Box>()
                                                                                            .Any(box => box.Colour.AverageColour.SRGB.B > 0.9f
                                                                                                        && box.Colour.AverageColour.SRGB.R < 0.1f
                                                                                                        && Math.Abs(box.Alpha * box.Colour.AverageColour.SRGB.A - 128 / 255f) < 0.02f));
        }

        [Test]
        public void TestCenterAnchorIsCentered()
        {
            AddStep("seek into centered caption", () => videoPlayer.VideoProgress.Value = 26);

            AddUntilStep("centered cue is rendered", () => renderedTexts().SequenceEqual(new[] { "Centered line" }));

            AddUntilStep("caption centre matches the video centre", () =>
            {
                SpriteText text = renderedSpriteTexts().Single(candidate => candidate.Text.ToString() == "Centered line");
                osuTK.Vector2 origin = captionContainer.ToScreenSpace(osuTK.Vector2.Zero);

                float x = centerX(text) - origin.X;
                float y = centerY(text) - origin.Y;

                return Math.Abs(x - captionContainer.DrawWidth / 2) < 4
                       && Math.Abs(y - captionContainer.DrawHeight / 2) < 4;
            });
        }

        [Test]
        public void TestCaptionsAtTheSamePositionOverlap()
        {
            AddStep("seek into same-position captions", () => videoPlayer.VideoProgress.Value = 29);

            AddUntilStep("both cues are rendered", () => renderedTexts().SequenceEqual(new[] { "Overlap A", "Overlap B" }));
        }

        private static bool approximately(Color4 actual, Color4 expected)
            => Math.Abs(actual.R - expected.R) < 0.01f
               && Math.Abs(actual.G - expected.G) < 0.01f
               && Math.Abs(actual.B - expected.B) < 0.01f
               && Math.Abs(actual.A - expected.A) < 0.01f;

        private SpriteText[] renderedSpriteTexts() => captionContainer.ChildrenOfType<SpriteText>()
                                                                      .Where(text => !string.IsNullOrEmpty(text.Text.ToString()))
                                                                      .ToArray();

        private string[] renderedTexts() => renderedSpriteTexts()
                                            .Select(text => text.Text.ToString())
                                            .OrderBy(text => text)
                                            .ToArray();
    }
}
