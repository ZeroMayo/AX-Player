// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.IO;
using System.Linq;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics.Caption;
using AXPlayer.App.Graphics.Videos;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;

namespace AXPlayer.App.Tests.Visual.Caption
{
    /// <summary>
    /// Covers the fonts that the user adds to the caption-fonts folder, which the closed captions
    /// are drawn with instead of the fonts that ship with the player.
    /// </summary>
    [TestFixture]
    public partial class TestSceneCustomCaptionFonts : AXPlayerTestScene
    {
        private const string font_file = @"TestCaptionFont.ttf";
        private const string font_family = @"TestCaptionFont";
        private const string late_font_file = @"LateCaptionFont.ttf";

        private const string srv3_xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<timedtext format=""3"">
  <head>
    <pen id=""0"" fc=""#FFFFFF"" fo=""255"" />
    <ws id=""0"" ju=""0"" />
    <wp id=""0"" ap=""1"" ah=""50"" av=""100"" />
  </head>
  <body>
    <p t=""1000"" d=""5000"" wp=""0"" ws=""0"" p=""0"">Caption text</p>
  </body>
</timedtext>";

        private YouTubeVideoPlayer videoPlayer;
        private ClosedCaptionContainer captionContainer;

        [Resolved]
        private AXPlayerConfigManager config { get; set; } = null!;

        [Resolved]
        private CustomCaptionFonts captionFonts { get; set; } = null!;

        public TestSceneCustomCaptionFonts()
        {
            videoPlayer = new YouTubeVideoPlayer(string.Empty, string.Empty, null!, srv3_xml, null!, 0);
            videoPlayer.VideoProgress.MaxValue = 10;

            Add(captionContainer = new ClosedCaptionContainer(videoPlayer, srv3_xml));
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("empty the caption-fonts folder", () =>
            {
                foreach (string file in captionFonts.Storage.GetFiles(string.Empty))
                    captionFonts.Storage.Delete(file);

                captionFonts.Refresh();
            });

            AddStep("use the font that ships with the player", () =>
            {
                captionFonts.Enabled.Value = false;
                captionFonts.SelectedFont.Value = string.Empty;
            });
        }

        [Test]
        public void TestAddedFontIsUsedByTheCaptions()
        {
            AddStep("add a font to the caption-fonts folder", () => addFont(font_file));

            AddAssert("the added font is listed", () => captionFonts.AvailableFonts.Contains(font_file));

            AddStep("select the added font", () =>
            {
                captionFonts.Enabled.Value = true;
                captionFonts.SelectedFont.Value = font_file;
            });

            AddUntilStep("the game loads the added font", () => captionFonts.GetGlyph(font_family, 'C') != null);
            AddAssert("the captions are drawn with the added font", () => captionContainer.CaptionTextFont.Family == font_family);
        }

        [Test]
        public void TestDisabledCustomFontKeepsTheShippedFont()
        {
            AddStep("add a font to the caption-fonts folder", () => addFont(font_file));

            AddStep("select the added font without using it", () =>
            {
                captionFonts.Enabled.Value = false;
                captionFonts.SelectedFont.Value = font_file;
            });

            AddAssert("the shipped font is still used", () => captionContainer.CaptionTextFont.Family != font_family);

            AddStep("use the added font", () => captionFonts.Enabled.Value = true);

            AddAssert("the added font is used now", () => captionContainer.CaptionTextFont.Family == font_family);
        }

        [Test]
        public void TestRemovingTheSelectedFontFallsBackToTheShippedFont()
        {
            AddStep("add a font to the caption-fonts folder", () => addFont(font_file));

            AddStep("select the added font", () =>
            {
                captionFonts.Enabled.Value = true;
                captionFonts.SelectedFont.Value = font_file;
            });

            AddAssert("the added font is used", () => captionContainer.CaptionTextFont.Family == font_family);
            AddAssert("the selected font is available", () => captionFonts.IsSelectedFontAvailable());

            AddStep("remove the font", () =>
            {
                captionFonts.Storage.Delete(font_file);
                captionFonts.Refresh();
            });

            AddAssert("the removed font is no longer offered", () => !captionFonts.AvailableFonts.Contains(font_file));
            AddAssert("the selected font is reported as missing", () => !captionFonts.IsSelectedFontAvailable());
            AddAssert("the shipped font is used instead", () => captionContainer.CaptionTextFont.Family != font_family);
        }

        [Test]
        public void TestFontsAddedWhileRunningAreFoundAfterArefresh()
        {
            AddStep("add a font without refreshing", () => writeShippedFont(late_font_file));

            AddAssert("the font is not offered yet", () => !captionFonts.AvailableFonts.Contains(late_font_file));

            AddStep("refresh the fonts", () => captionFonts.Refresh());

            AddAssert("the font is offered", () => captionFonts.AvailableFonts.Contains(late_font_file));
            AddUntilStep("the game loads the added font", () => captionFonts.GetGlyph(@"LateCaptionFont", 'C') != null);
        }

        [Test]
        public void TestOnlySupportedFilesAreOffered()
        {
            AddStep("add unsupported files", () =>
            {
                writeFile(@"notes.txt");
                writeFile(@"font.ttf.bak");
                writeFile(@"broken.ttf");
            });

            AddStep("add a supported font", () => addFont(font_file));

            AddAssert("only the font is offered", () => captionFonts.AvailableFonts.SequenceEqual(new[] { font_file }));
        }

        [Test]
        public void TestFolderHoldsTheCaptionFonts()
        {
            AddAssert("the fonts are read from the caption-fonts folder", () => captionFonts.FolderPath.EndsWith(CustomCaptionFonts.FOLDER_NAME, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Copies a font that ships with the player into the caption-fonts folder and refreshes the list.
        /// </summary>
        private void addFont(string fileName)
        {
            writeShippedFont(fileName);
            captionFonts.Refresh();
        }

        private void writeShippedFont(string fileName) => writeFile(fileName, readShippedFont());

        private void writeFile(string fileName, byte[]? content = null)
            => File.WriteAllBytes(captionFonts.Storage.GetFullPath(fileName), content ?? new byte[] { 0 });

        private static byte[] readShippedFont()
        {
            var resources = new DllResourceStore(typeof(Resources.AXPlayerAppResources).Assembly);

            string[] files = resources.GetAvailableResources().ToArray();
            string? name = files.FirstOrDefault(file => file.Contains("DejaVuSans-Regular", StringComparison.OrdinalIgnoreCase) && file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase));

            Assert.That(name, Is.Not.Null, $"The shipped font could not be found among: {string.Join(", ", files.Take(20))}");

            using var stream = resources.GetStream(name!);

            Assert.That(stream, Is.Not.Null, $"The shipped font '{name}' could not be read.");

            using var memory = new MemoryStream();
            stream!.CopyTo(memory);

            return memory.ToArray();
        }
    }
}
