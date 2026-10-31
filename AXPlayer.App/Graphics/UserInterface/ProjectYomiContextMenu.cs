// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Config;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class ProjectYomiContextMenu : ProjectYomiMenu
    {
        [Resolved]
        private ProjectYomiMenuSamples menuSamples { get; set; } = null!;

        public ProjectYomiContextMenu(bool playSamples)
            : base(Direction.Vertical, topLevelMenu: false, playSamples)
        {
            MaskingContainer.CornerRadius = AXPlayerApp.UI_CORNER_RADIUS;

            ItemsContainer.Padding = new MarginPadding { Vertical = DrawableAdaptiveMenuItem.MARGIN_VERTICAL };

            MaxHeight = 250;
        }

        [BackgroundDependencyLoader]
        private void load(ProjectYomiColour colours, OverlayColourProvider overlayColourProvider)
        {
            BackgroundColour = overlayColourProvider.Background4;
        }

        [Resolved]
        private AXPlayerConfigManager config { get; set; }

        protected override void AnimateOpen()
        {
            if (PlaySamples && !WasOpened)
                if (config.Get<bool>(AXPlayerSetting.PlayOverlaySFX))
                    menuSamples.PlayClickSample();

            base.AnimateOpen();
        }

        protected override Menu CreateSubMenu() => new ProjectYomiContextMenu(false); // sub menu samples are handled by OsuMenu.OnSubmenuOpen.
    }
}
