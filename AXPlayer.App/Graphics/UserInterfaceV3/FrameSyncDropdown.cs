// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Localisation;
using osu.Framework.Configuration;
using osu.Framework.Localisation;

namespace AXPlayer.App.Graphics.UserInterfaceV3
{
    public partial class FrameSyncDropdown : FormEnumDropdown<FrameSync>
    {
        protected override LocalisableString GenerateItemText(FrameSync item)
        {
            switch (item)
            {
                case FrameSync.VSync:
                    return AXPlayerStrings.VSync;

                case FrameSync.VSyncVRR:
                    return AXPlayerStrings.VSyncVRR;

                case FrameSync.Limit2x:
                    return AXPlayerStrings.RefreshRate2X;

                case FrameSync.Limit4x:
                    return AXPlayerStrings.RefreshRate4X;

                case FrameSync.Limit8x:
                    return AXPlayerStrings.RefreshRate8X;

                case FrameSync.Unlimited:
                    return AXPlayerStrings.Unlimited;
            }
            return base.GenerateItemText(item);
        }
    }
}
