// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using AXPlayer.App.Graphics.Sprites;
using osu.Framework.Bindables;
using osu.Framework.Graphics;

namespace AXPlayer.App.Graphics.UserInterfaceV2
{
    /// <summary>
    /// A heading which introduces a section in the settings overlay.
    /// </summary>
    /// <remarks>
    /// The heading removes itself (including the layout space it occupies) while a settings search is active
    /// and none of the settings belonging to its section match the search query.
    /// </remarks>
    public partial class SettingsSectionHeader : ProjectYomiSpriteText
    {
        /// <summary>
        /// Whether this heading should be displayed. This is only used to hide headings which don't match an active settings search.
        /// </summary>
        public readonly BindableBool CanBeShown = new BindableBool(true);

        public SettingsSectionHeader()
        {
            CanBeShown.BindValueChanged(_ => Invalidate(Invalidation.Presence));
        }

        public override bool IsPresent => base.IsPresent && CanBeShown.Value;
    }
}
