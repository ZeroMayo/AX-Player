// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using AXPlayer.App.Online;
using NUnit.Framework;

namespace AXPlayer.App.Tests.Online
{
    /// <summary>
    /// The parser of the translate endpoint response that the translated metadata is built from.
    /// </summary>
    [TestFixture]
    public class TestGoogleTranslate
    {
        [Test]
        public void TestSegmentsAreJoinedWithoutLosingSpacing()
        {
            const string response = """[[["안녕하세요. ","Hello. "],["반갑습니다.","Nice to meet you."]],null,"en"]""";

            Assert.That(GoogleTranslate.ParseTranslatedValue(response), Is.EqualTo("안녕하세요. 반갑습니다."));
        }

        [Test]
        public void TestSingleSegmentIsNotPadded()
        {
            const string response = """[[["안녕하세요","Hello",null,null,10]],null,"en"]""";

            Assert.That(GoogleTranslate.ParseTranslatedValue(response), Is.EqualTo("안녕하세요"));
        }

        [Test]
        public void TestResponseWithoutTranslationIsTolerated()
        {
            // A blocked or empty response used to reach the JSON parser as-is and throw.
            Assert.That(GoogleTranslate.ParseTranslatedValue(null), Is.Empty);
            Assert.That(GoogleTranslate.ParseTranslatedValue(string.Empty), Is.Empty);
            Assert.That(GoogleTranslate.ParseTranslatedValue("[]"), Is.Empty);
            Assert.That(GoogleTranslate.ParseTranslatedValue("""[null,null,"en"]"""), Is.Empty);
            Assert.That(GoogleTranslate.ParseTranslatedValue("""[[null,null]]"""), Is.Empty);
            Assert.That(GoogleTranslate.ParseTranslatedValue("not json"), Is.Empty);
        }
    }
}
