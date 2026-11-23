// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Text;

namespace AXPlayer.App.Config
{
    /// <summary>
    /// Makes the fonts the user drops into the <c>caption-fonts</c> folder available to the closed
    /// captions, so a font that is not shipped with the player can be used as the caption font.
    /// </summary>
    /// <remarks>
    /// A font is addressed by its file name, so <c>caption-fonts/MyFont.ttf</c> is used as the font family
    /// <c>MyFont</c>. The font is registered with the game as soon as it is found, and the states stored in
    /// <see cref="AXPlayerSetting.CustomCaptionFontEnabled"/> and <see cref="AXPlayerSetting.CustomCaptionFont"/>
    /// decide whether it is the font the captions are drawn with.
    /// </remarks>
    public class CustomCaptionFonts
    {
        /// <summary>
        /// The folder the user-provided caption fonts are read from, relative to the game storage.
        /// </summary>
        public const string FOLDER_NAME = @"caption-fonts";

        /// <summary>
        /// The font file extensions that the captions can be drawn with.
        /// </summary>
        public static readonly string[] SUPPORTED_EXTENSIONS = { @".ttf", @".otf" };

        private readonly Game game;

        /// <summary>
        /// The families that were handed to the game, so a font is only registered once.
        /// </summary>
        private readonly HashSet<string> registeredFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private IReadOnlyList<string> availableFonts = Array.Empty<string>();

        public CustomCaptionFonts(Game game, Storage gameStorage, AXPlayerConfigManager config)
        {
            this.game = game;

            Storage = gameStorage.GetStorageForDirectory(FOLDER_NAME);

            Enabled = config.GetBindable<bool>(AXPlayerSetting.CustomCaptionFontEnabled);
            SelectedFont = config.GetBindable<string>(AXPlayerSetting.CustomCaptionFont);

            Refresh();
        }

        /// <summary>
        /// The storage the user drops <c>.ttf</c> and <c>.otf</c> files into.
        /// </summary>
        public Storage Storage { get; }

        /// <summary>
        /// Whether a custom font is chosen instead of one of the fonts shipped with the player.
        /// </summary>
        public Bindable<bool> Enabled { get; }

        /// <summary>
        /// The name of the selected font file, relative to <see cref="Storage"/>.
        /// </summary>
        public Bindable<string> SelectedFont { get; }

        /// <summary>
        /// The font files that are currently in <see cref="Storage"/>, ordered by name.
        /// </summary>
        public IReadOnlyList<string> AvailableFonts
        {
            get => availableFonts;
            private set
            {
                if (availableFonts.SequenceEqual(value, StringComparer.OrdinalIgnoreCase))
                    return;

                availableFonts = value;
                AvailableFontsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Whether a font of <see cref="AvailableFonts"/> is selected and the user asked for it.
        /// </summary>
        public bool IsUsingCustomFont => Enabled.Value && !string.IsNullOrWhiteSpace(SelectedFont.Value);

        /// <summary>
        /// The absolute path of <see cref="Storage"/>, which is shown to the user.
        /// </summary>
        public string FolderPath => Storage.GetFullPath(string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        /// <summary>
        /// Invoked when the fonts that were found in <see cref="Storage"/> change.
        /// </summary>
        public event Action? AvailableFontsChanged;

        /// <summary>
        /// Rescans <see cref="Storage"/> and registers every font that was not registered yet.
        /// </summary>
        public void Refresh()
        {
            AvailableFonts = Storage.GetFiles(string.Empty, "*.*")
                                     .Where(file => hasSupportedExtension(file))
                                     .Where(file => isFontFile(Storage.GetFullPath(file)))
                                     .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                                     .ToArray();

            foreach (string font in AvailableFonts)
                register(font);

            Logger.Log($@"{AvailableFonts.Count} custom caption font(s) were found in '{FolderPath}'.", LoggingTarget.Runtime);
        }

        /// <summary>
        /// Opens <see cref="Storage"/> in the file explorer of the platform, so the user can drop fonts into it.
        /// </summary>
        public void OpenFolder() => Storage.PresentExternally();

        /// <summary>
        /// The registered font family of the selected font, or <c>null</c> when the captions should be drawn
        /// with one of the fonts shipped with the player.
        /// </summary>
        public string? GetSelectedFamily()
        {
            if (!IsUsingCustomFont)
                return null;

            string? file = SelectedFont.Value;

            if (file == null)
                return null;

            return register(file);
        }

        /// <summary>
        /// The glyph the game draws for <paramref name="character"/> with the font of <paramref name="family"/>, or
        /// <c>null</c> when the font was not loaded. This is the lookup the text of a caption is drawn with, so it
        /// tells a font that the game can draw apart from one that was only named.
        /// </summary>
        internal ITexturedCharacterGlyph? GetGlyph(string family, char character)
            => (game.Fonts as ITexturedGlyphLookupStore)?.Get(family, new Grapheme(character));

        /// <summary>
        /// Whether the selected font file is in <see cref="Storage"/>.
        /// </summary>
        public bool IsSelectedFontAvailable() => SelectedFont.Value is string file && !string.IsNullOrWhiteSpace(file) && File.Exists(Storage.GetFullPath(file));

        /// <summary>
        /// Registers the font of a file that is in <see cref="Storage"/>, and returns its family name.
        /// </summary>
        private string? register(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            string fullPath = Storage.GetFullPath(fileName);
            string family = Path.GetFileNameWithoutExtension(fileName);

            if (string.IsNullOrWhiteSpace(family))
                return null;

            if (!File.Exists(fullPath))
                return null;

            if (registeredFamilies.Contains(family))
                return family;

            try
            {
                game.AddOutlineFont(new ResourceStore<byte[]>(new ExternalFontSource(fullPath, family)), family);
                registeredFamilies.Add(family);

                return family;
            }
            catch (Exception e)
            {
                Logger.Error(e, $@"The custom caption font '{family}' could not be registered.");
                return null;
            }
        }

        private static bool hasSupportedExtension(string file)
            => SUPPORTED_EXTENSIONS.Any(extension => file.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Whether a file states one of the signatures a font file can start with, so a file that was only
        /// named like a font is not offered as a caption font.
        /// </summary>
        internal static bool isFontFile(string fullPath)
        {
            try
            {
                using var stream = File.OpenRead(fullPath);

                byte[] signature = new byte[4];

                if (stream.Read(signature, 0, signature.Length) < signature.Length)
                    return false;

                // TrueType outlines, CFF/OpenType outlines, the Apple variants, and font collections.
                return signature.SequenceEqual(new byte[] { 0x00, 0x01, 0x00, 0x00 })
                       || signature.SequenceEqual(new byte[] { (byte)'O', (byte)'T', (byte)'T', (byte)'O' })
                       || signature.SequenceEqual(new byte[] { (byte)'t', (byte)'r', (byte)'u', (byte)'e' })
                       || signature.SequenceEqual(new byte[] { (byte)'t', (byte)'y', (byte)'p', (byte)'1' })
                       || signature.SequenceEqual(new byte[] { (byte)'t', (byte)'t', (byte)'c', (byte)'f' });
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Serves a single font file of the user under the family name the captions ask for.
        /// </summary>
        /// <remarks>
        /// The captions address a font by the name of the file it was loaded from and append the weight of the
        /// text to it, so every lookup that names this family is answered with the same font.
        /// </remarks>
        private class ExternalFontSource : IResourceStore<byte[]>
        {
            private readonly byte[] data;
            private readonly string family;

            public ExternalFontSource(string fullPath, string family)
            {
                this.family = family;
                data = File.ReadAllBytes(fullPath);
            }

            [return: MaybeNull]
            public byte[] Get(string name) => matches(name) ? data : null;

            [return: MaybeNull]
            public Stream GetStream(string name) => matches(name) ? new MemoryStream(data, false) : null;

            public IEnumerable<string> GetAvailableResources() => new[] { family };

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Get(name));

            public Task<Stream> GetStreamAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(GetStream(name));

            public void Dispose()
            {
            }

            /// <summary>
            /// Whether a font lookup names this font. Lookups name either the file it was loaded from or the
            /// family with a weight, such as <c>MyFont-Bold</c>.
            /// </summary>
            private bool matches(string? name)
            {
                if (string.IsNullOrWhiteSpace(name))
                    return false;

                int lastSeparator = name.LastIndexOfAny(new[] { '/', '\\' });
                string candidate = lastSeparator >= 0 ? name[(lastSeparator + 1)..] : name;

                if (string.Equals(candidate, family, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.Equals(Path.GetFileNameWithoutExtension(candidate), family, StringComparison.OrdinalIgnoreCase))
                    return true;

                return candidate.StartsWith(family + "-", StringComparison.OrdinalIgnoreCase)
                       || candidate.StartsWith(family + " ", StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
