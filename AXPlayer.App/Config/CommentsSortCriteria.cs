// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Localisation;
using osu.Framework.Localisation;

namespace AXPlayer.App.Config
{
    public enum CommentsSortCriteria
    {
        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.CommentsSortTop))]
        Top,

        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.CommentsSortNewest))]
        Newest,
    }
}
