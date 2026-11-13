// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace AXPlayer.App.Graphics.UserInterfaceV2
{
    /// <summary>
    /// A container used for settings content which has no search terms of its own (e.g. previews or credits).
    /// </summary>
    /// <remarks>
    /// The container removes itself (including the layout space it occupies) while a settings search is active.
    /// </remarks>
    public partial class SettingsFilterableContainer : FillFlowContainer
    {
        /// <summary>
        /// Whether this container should be displayed. This is only used to hide content which can never match a settings search.
        /// </summary>
        public readonly BindableBool CanBeShown = new BindableBool(true);

        public SettingsFilterableContainer()
        {
            CanBeShown.BindValueChanged(_ => Invalidate(Invalidation.Presence));
        }

        public override bool IsPresent => base.IsPresent && CanBeShown.Value;
    }
}
