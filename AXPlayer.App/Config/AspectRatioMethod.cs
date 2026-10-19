// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using AXPlayer.App.Localisation;

namespace AXPlayer.App.Config
{
    public enum AspectRatioMethod
    { 
        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.Letterbox))]
        Letterbox,

        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.Fill))]
        Fill,
    }
}
