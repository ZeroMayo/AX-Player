// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using AXPlayer.App.Localisation;

namespace AXPlayer.App.Online
{
    public enum PrivacyStatus
    {
        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.Public))]
        Public,

        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.Unlisted))]
        Unlisted,

        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.Private))]
        Private
    }
}
