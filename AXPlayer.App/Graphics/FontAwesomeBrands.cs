// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;

namespace AXPlayer.App.Graphics
{
    /// <summary>
    /// Brand logos, rendered from the bundled <c>FontAwesome7Brands</c> font.
    /// These are the only icons that do not come from <see cref="MaterialSymbols"/>.
    /// </summary>
    /// <remarks>
    /// The font is registered as-is by file name, so <see cref="IconUsage.FontName"/> is just <see cref="FONT_FAMILY"/>
    /// (see <see cref="Config.AXPlayerFontLoader"/>).
    /// </remarks>
    public static class FontAwesomeBrands
    {
        public const string FONT_FAMILY = "FontAwesome7Brands";

        public static IconUsage Youtube => new IconUsage('\uf167', FONT_FAMILY); // youtube

        public static IconUsage Discord => new IconUsage('\uf392', FONT_FAMILY); // discord

        public static IconUsage Google => new IconUsage('\uf1a0', FONT_FAMILY); // google

        public static IconUsage Twitch => new IconUsage('\uf1e8', FONT_FAMILY); // twitch

        public static IconUsage Instagram => new IconUsage('\uf16d', FONT_FAMILY); // instagram

        public static IconUsage Twitter => new IconUsage('\ue61b', FONT_FAMILY); // x-twitter
    }
}
