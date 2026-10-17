// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Text;
using NekoPlayer.App.Localisation;
using osu.Framework.Localisation;

namespace NekoPlayer.App.Config
{
    public enum PlayerControllerFadeStyle
    {
        [LocalisableDescription(typeof(NekoPlayerStrings), nameof(NekoPlayerStrings.Modern))]
        Modern,

        [LocalisableDescription(typeof(NekoPlayerStrings), nameof(NekoPlayerStrings.SFXLegacy))]
        Legacy,
    }
}
