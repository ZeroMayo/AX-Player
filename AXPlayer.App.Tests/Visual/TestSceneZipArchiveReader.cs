// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Graphics;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.IO.Archives;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;

namespace AXPlayer.App.Tests.Visual
{
    /// <summary>
    /// Covers <see cref="ZipArchiveReader"/> being read from multiple threads at once, which is what happens when the game
    /// loads emoji textures in the background. A shared SharpCompress archive must not be read concurrently without
    /// synchronisation, otherwise entries are read from garbage offsets
    /// ("Cannot seek to position X. End of stream reached at position Y") and take the game down.
    /// </summary>
    [TestFixture]
    public partial class TestSceneZipArchiveReader : AXPlayerTestScene
    {
        private const string archive_name = @"Textures/Emoji/Emoji.zip";

        private const int emoji_size = 50;
        private const int emoji_bytes = emoji_size * emoji_size * 4;

        private ResourceStore<byte[]> resources;

        private ZipArchiveReader reader;
        private EmojiStore emojiStore;

        private FillFlowContainer<Container> emojiGrid;
        private ProjectYomiSpriteText statusText;

        private string[] emojiNames;

        private int failedReads;
        private int nullTextures;

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer, TextureStore textures)
        {
            resources = new ResourceStore<byte[]>();
            resources.AddStore(new DllResourceStore(typeof(AXPlayer.App.Resources.AXPlayerAppResources).Assembly));

            reader = new ZipArchiveReader(resources.GetStream(archive_name), "Emoji.zip");
            emojiStore = new EmojiStore(renderer, textures.Atlas, resources);

            emojiNames = reader.Filenames.Where(n => n.EndsWith(".raw", StringComparison.Ordinal)).ToArray();

            AddRange(new Drawable[]
            {
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new osuTK.Vector2(0, 8),
                    Children = new Drawable[]
                    {
                        statusText = new ProjectYomiSpriteText(false) { Text = "no emoji read yet" },
                        emojiGrid = new FillFlowContainer<Container>
                        {
                            AutoSizeAxes = Axes.Both,
                            Spacing = new osuTK.Vector2(4),
                        },
                    },
                },
            });
        }

        [Test]
        public void TestConcurrentEntryReads()
        {
            AddStep("read all emoji from multiple threads", () =>
            {
                failedReads = 0;

                Parallel.For(0, emojiNames.Length * 2, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
                {
                    string name = emojiNames[i % emojiNames.Length];

                    try
                    {
                        using (Stream stream = reader.GetStream(name))
                        {
                            if (stream == null || stream.Length != emoji_bytes)
                            {
                                Interlocked.Increment(ref failedReads);
                                return;
                            }

                            var buffer = new byte[emoji_bytes];
                            stream.ReadExactly(buffer);

                            if (buffer.All(b => b == 0))
                                Interlocked.Increment(ref failedReads);
                        }
                    }
                    catch
                    {
                        Interlocked.Increment(ref failedReads);
                    }
                });

                statusText.Text = $"read {emojiNames.Length * 2} entries concurrently, {failedReads} failed";
            });

            AddAssert("every entry read correctly", () => failedReads == 0);
            AddAssert("unknown entry returns null", () => reader.GetStream("this-entry-does-not-exist.raw") == null);
            AddAssert("available filenames populated", () => reader.Filenames.Count() == emojiNames.Length);
        }

        [Test]
        public void TestEmojiTextures()
        {
            AddStep("load emoji textures", () =>
            {
                nullTextures = 0;
                emojiGrid.Clear();

                // a small, visually representative subset of the archive
                foreach (string name in emojiNames.Where((_, i) => i % 60 == 0).Take(24))
                {
                    // the emoji store reads from its own ZipArchiveReader over the same archive
                    Texture texture = emojiStore.Get(name);

                    if (texture == null)
                    {
                        nullTextures++;
                        continue;
                    }

                    emojiGrid.Add(new Container
                    {
                        Size = new osuTK.Vector2(32),
                        Child = new Sprite { RelativeSizeAxes = Axes.Both, Texture = texture },
                    });
                }

                statusText.Text = $"loaded {emojiGrid.Count} emoji textures, {nullTextures} failed";
            });

            AddAssert("every emoji texture loaded", () => nullTextures == 0);
            AddAssert("emoji textures visible", () => emojiGrid.Count > 0);
        }

        [Test]
        public void TestDisposedReader()
        {
            ZipArchiveReader disposedReader = null;

            AddStep("create second reader", () =>
            {
                disposedReader = new ZipArchiveReader(resources.GetStream(archive_name), "Emoji.zip");
            });

            AddAssert("second reader reads entries", () => disposedReader.GetStream(emojiNames.First()) != null);

            AddStep("dispose twice", () =>
            {
                disposedReader.Dispose();
                disposedReader.Dispose();
            });

            AddAssert("disposed reader returns null", () => disposedReader.GetStream(emojiNames.First()) == null);
            AddAssert("disposed reader has no filenames", () => !disposedReader.Filenames.Any());
        }

        protected override void Dispose(bool isDisposing)
        {
            reader?.Dispose();
            emojiStore?.Dispose();

            base.Dispose(isDisposing);
        }
    }
}
