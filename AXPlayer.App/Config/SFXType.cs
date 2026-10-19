// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AXPlayer.App.Localisation;
using osu.Framework.Localisation;

namespace AXPlayer.App.Config
{
    public enum SFXType
    {
        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.SFXNew))]
        Normal,

        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.SFXLegacy))]
        Legacy,
    }
}
