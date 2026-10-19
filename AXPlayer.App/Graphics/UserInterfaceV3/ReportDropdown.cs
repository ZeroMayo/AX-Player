// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Online;
using osu.Framework.Localisation;

namespace AXPlayer.App.Graphics.UserInterfaceV3
{
    public partial class ReportDropdown : FormDropdown<VideoAbuseReportReasonItem>
    {
        protected override LocalisableString GenerateItemText(VideoAbuseReportReasonItem item)
            => item.Label;
    }
}
