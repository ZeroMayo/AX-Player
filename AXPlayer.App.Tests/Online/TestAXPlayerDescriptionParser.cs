// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System.Linq;
using AXPlayer.App.Online;
using NUnit.Framework;

namespace AXPlayer.App.Tests.Online
{
    /// <summary>
    /// The parser of the token stream that the description and the comment text are rendered from.
    /// </summary>
    [TestFixture]
    public partial class TestAXPlayerDescriptionParser
    {
        [Test]
        public void TestTextIsSplitIntoLinksAndTimestamps()
        {
            var tokens = AXPlayerDescriptionParser.Parse("Intro 1:23 https://example.com/x @handle #tag");

            Assert.That(tokens.Select(token => token.Type), Is.EqualTo(new[]
            {
                YouTubeDescriptionTokenType.Text,
                YouTubeDescriptionTokenType.Timestamp,
                YouTubeDescriptionTokenType.Text,
                YouTubeDescriptionTokenType.Url,
                YouTubeDescriptionTokenType.Text,
                YouTubeDescriptionTokenType.Mention,
                YouTubeDescriptionTokenType.Text,
                YouTubeDescriptionTokenType.Hashtag,
            }));

            Assert.That(tokens[1].Value, Is.EqualTo("1:23"));
            Assert.That(tokens[3].Value, Is.EqualTo("https://example.com/x"));
        }

        [Test]
        public void TestMissingTextIsTolerated()
        {
            // A comment whose entity payload was not rendered arrives without text, which used to throw
            // from the regular expression matcher.
            Assert.That(AXPlayerDescriptionParser.Parse(null), Is.Empty);
            Assert.That(AXPlayerDescriptionParser.Parse(string.Empty), Is.Empty);
        }
    }
}
