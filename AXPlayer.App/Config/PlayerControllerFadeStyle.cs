// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Text;
using AXPlayer.App.Localisation;
using osu.Framework.Localisation;

namespace AXPlayer.App.Config
{
    public enum PlayerControllerFadeStyle
    {
        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.Modern))]
        Modern,

        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.SFXLegacy))]
        Legacy,
    }
}
