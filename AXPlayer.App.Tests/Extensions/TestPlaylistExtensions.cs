// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using AXPlayer.App.Extensions;
using Google.Apis.YouTube.v3.Data;
using NUnit.Framework;

namespace AXPlayer.App.Tests.Extensions
{
    /// <summary>
    /// Reads the parts of a playlist that a response is not required to carry.
    /// </summary>
    [TestFixture]
    public class TestPlaylistExtensions
    {
        [Test]
        public void TestTheWidestThumbnailIsPreferred()
        {
            var playlist = new Playlist
            {
                Snippet = new PlaylistSnippet
                {
                    Thumbnails = new ThumbnailDetails
                    {
                        Default__ = new Thumbnail { Url = "https://example.com/default.jpg" },
                        High = new Thumbnail { Url = "https://example.com/high.jpg" },
                    },
                },
            };

            Assert.That(playlist.bestThumbnailUrl(), Is.EqualTo("https://example.com/high.jpg"));
        }

        [Test]
        public void TestAPlaylistOfTheLibraryWithoutAThumbnailIsTolerated()
        {
            // A playlist header used to read the high thumbnail directly, which ended the game when the
            // playlist carried none.
            Assert.That(new Playlist { Snippet = new PlaylistSnippet() }.bestThumbnailUrl(), Is.Null);
            Assert.That(new Playlist { Snippet = new PlaylistSnippet { Thumbnails = new ThumbnailDetails() } }.bestThumbnailUrl(), Is.Null);
            Assert.That(((Playlist?)null).bestThumbnailUrl(), Is.Null);
        }
    }
}
