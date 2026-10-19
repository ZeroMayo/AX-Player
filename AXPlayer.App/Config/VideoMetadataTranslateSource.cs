// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using AXPlayer.App.Localisation;

namespace AXPlayer.App.Config
{
    public enum VideoMetadataTranslateSource
    {
        YouTube,

        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.GoogleTranslate))]
        GoogleTranslate
    }
}
