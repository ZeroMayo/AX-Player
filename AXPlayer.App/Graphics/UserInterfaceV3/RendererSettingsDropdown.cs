// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Localisation;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Localisation;
using osu.Framework.Platform;

namespace AXPlayer.App.Graphics.UserInterfaceV3
{
    public partial class RendererSettingsDropdown : FormEnumDropdown<RendererType>
    {
        private RendererType hostResolvedRenderer;
        private bool automaticRendererInUse;

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager config, GameHost host)
        {
            var renderer = config.GetBindable<RendererType>(FrameworkSetting.Renderer);
            automaticRendererInUse = renderer.Value == RendererType.Automatic;
            hostResolvedRenderer = host.ResolvedRenderer;
        }

        protected override LocalisableString GenerateItemText(RendererType item)
        {
            if (item == RendererType.Automatic && automaticRendererInUse)
                return AXPlayerStrings.RenderTypeAutomaticIsUse(hostResolvedRenderer.GetDescription());

            if (item == RendererType.Automatic)
            {
                return AXPlayerStrings.RenderTypeAutomatic;
            }

            return base.GenerateItemText(item);
        }
    }
}
