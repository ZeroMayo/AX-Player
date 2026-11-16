// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System.Collections.Generic;
using System.Xml.Linq;
using AXPlayer.App.Graphics.Caption;
using NUnit.Framework;
using osuTK.Graphics;

namespace AXPlayer.App.Tests.Caption
{
    /// <summary>
    /// The background of an SRV3 caption, which a pen defines through its <c>bc</c> and <c>bo</c>
    /// attributes.
    /// </summary>
    [TestFixture]
    public class TestSrv3CaptionBackground
    {
        [Test]
        public void TestAPenWithoutBackgroundOpacityHasNone()
        {
            Assert.That(parseCaptionBackground("""<pen id="1" fc="#FF0000" fo="255" bc="#000000" bo="0" />"""), Is.EqualTo(0f));
        }

        [Test]
        public void TestAPenWithBackgroundOpacityKeepsTheBox()
        {
            Assert.That(parseCaptionBackground("""<pen id="1" fc="#FF0000" fo="255" bc="#000000" bo="255" />"""), Is.EqualTo(1f));
            Assert.That(parseCaptionBackground("""<pen id="1" fc="#FF0000" fo="255" bc="#000000" bo="128" />"""), Is.EqualTo(1f));
        }

        [Test]
        public void TestAPenWithoutABackgroundColourUsesItsOpacity()
        {
            // bo alone describes the transparency of the default background of SRV3.
            Assert.That(parseCaptionBackground("""<pen id="1" fc="#FF0000" fo="255" bo="0" />"""), Is.EqualTo(0f));
            Assert.That(parseCaptionBackground("""<pen id="1" fc="#FF0000" fo="255" bo="255" />"""), Is.EqualTo(1f));
        }

        [Test]
        public void TestAPenWithOnlyBackgroundOpacityIsBlack()
        {
            Srv3Caption caption = parseCaption("""<pen id="1" fc="#FF0000" fo="255" bo="128" />""");

            Assert.That(caption.BackgroundColour, Is.Not.Null);
            Assert.That(caption.BackgroundColour!.Value.A, Is.EqualTo(128 / 255f).Within(0.01f));
            Assert.That(caption.BackgroundColour.Value.R, Is.EqualTo(0f));
            Assert.That(caption.BackgroundColour.Value.G, Is.EqualTo(0f));
            Assert.That(caption.BackgroundColour.Value.B, Is.EqualTo(0f));
        }

        [Test]
        public void TestAPenWithoutABackgroundKeepsTheBackgroundOfThePlayer()
        {
            // SRV3 states the background of a pen through bc and bo, so a pen that states neither has
            // none of its own.
            Assert.That(parseCaptionBackground("""<pen id="1" fc="#FF0000" fo="255" />"""), Is.EqualTo(0.5f));
        }

        [Test]
        public void TestTheBoxFollowsThePlayerWithoutAPenBackground()
        {
            // Only a caption without any pen background is drawn with the configured opacity.
            Assert.That(ClosedCaptionContainer.backgroundAlpha(null, 0.5f), Is.EqualTo(0.5f));
        }

        [Test]
        public void TestTheBackgroundOfThePenIsTheColourOfTheBox()
        {
            Srv3Caption caption = parseCaption("""<pen id="1" fc="#FF0000" fo="255" bc="#000000" bo="0" />""");

            Assert.That(caption.BackgroundColour, Is.Not.Null);
            Assert.That(caption.BackgroundColour!.Value.A, Is.EqualTo(0f));
        }

        private static float parseCaptionBackground(string pen) => ClosedCaptionContainer.backgroundAlpha(parseCaption(pen).BackgroundColour, 0.5f);

        private static Srv3Caption parseCaption(string pen)
        {
            var pens = new Dictionary<int, Srv3Pen> { [1] = Srv3Pen.Parse(XElement.Parse(pen)) };

            return Srv3Caption.Parse(
                XElement.Parse("""<p t="0" d="1000" p="1">text</p>"""),
                pens,
                new Dictionary<int, Srv3Window>(),
                new Dictionary<int, Srv3Position>());
        }
    }
}
