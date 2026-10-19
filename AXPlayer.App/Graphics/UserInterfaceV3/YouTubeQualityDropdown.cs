// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Online;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using YoutubeExplode.Videos.Streams;

namespace AXPlayer.App.Graphics.UserInterfaceV3
{
    public partial class YouTubeQualityDropdown : FormDropdown<string>
    {
        [Resolved]
        private AXPlayerApp app { get; set; }

        [Resolved]
        private AXPlayerConfigManager config { get; set; }

        [Resolved]
        private YoutubeExplode.YoutubeClient youtubeService { get; set; }

        [Resolved]
        private GoogleTranslate googleTranslate { get; set; }

        public YoutubeExplode.Videos.Streams.VideoQuality CurrentVideoQuality;

        [BackgroundDependencyLoader]
        private void load()
        {
        }

        public async Task RefreshQualityList(string videoId)
        {
            try
            {
                var streamManifest = await app.YouTubeClient.Videos.Streams.GetManifestAsync(videoId);

                List<string> items = new List<string>();

                List<VideoOnlyStreamInfo> videoStreamInfo;
                IVideoStreamInfo maxVideoStreamInfo;

                try
                {
                    videoStreamInfo = streamManifest
                                .GetVideoOnlyStreams()
                                .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.WebM)
                                .ToList();

                    maxVideoStreamInfo = streamManifest
                                .GetVideoOnlyStreams()
                                .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.WebM)
                                .GetWithHighestVideoQuality();
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());
                    videoStreamInfo = streamManifest
                                .GetVideoOnlyStreams()
                                .ToList();

                    maxVideoStreamInfo = streamManifest
                                .GetVideoOnlyStreams()
                                .GetWithHighestVideoQuality();
                }

                foreach (var item in videoStreamInfo)
                {
                    items.Add(item.VideoQuality.Label);
                }

                Items = items;

                if (!Current.Disabled && string.IsNullOrEmpty(Current.Value))
                {
                    Current.Value = items.Where(quality => quality.Contains(maxVideoStreamInfo.VideoQuality.Label)).First();
                    Current.Default = items.Where(quality => quality.Contains(maxVideoStreamInfo.VideoQuality.Label)).First();
                }
            }
            catch (Exception e)
            {
                Current.Disabled = false;
                Logger.Error(e, e.GetDescription());
            }
        }
    }
}
