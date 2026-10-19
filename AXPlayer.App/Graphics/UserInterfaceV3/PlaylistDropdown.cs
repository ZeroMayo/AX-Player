// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Google.Apis.YouTube.v3.Data;
using AXPlayer.App.Graphics.UserInterfaceV2;
using osu.Framework.Localisation;

namespace AXPlayer.App.Graphics.UserInterfaceV3
{
    public partial class PlaylistDropdown : FormDropdown<Playlist>
    {
        protected override LocalisableString GenerateItemText(Playlist item)
            => item.Snippet.Title;
    }
}
