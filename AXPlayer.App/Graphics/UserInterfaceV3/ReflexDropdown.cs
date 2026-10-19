// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Localisation;
using osu.Framework.Graphics.Rendering.LowLatency;
using osu.Framework.Localisation;

namespace AXPlayer.App.Graphics.UserInterfaceV3
{
    public partial class ReflexDropdown : FormEnumDropdown<LatencyMode>
    {
        protected override LocalisableString GenerateItemText(LatencyMode item)
        {
            switch (item)
            {
                case LatencyMode.Off:
                    return AXPlayerStrings.ReflexOff;

                case LatencyMode.On:
                    return AXPlayerStrings.ReflexOn;

                case LatencyMode.Boost:
                    return AXPlayerStrings.ReflexBoost;
            }
            return base.GenerateItemText(item);
        }
    }
}
