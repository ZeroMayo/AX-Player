// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Localisation;
using AXPlayer.App.Online;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using YoutubeExplode.Videos.Streams;

namespace AXPlayer.App.Graphics.UserInterfaceV3
{
    public partial class YouTubeAudioStreamDropdown : FormDropdown<IAudioStreamInfo>
    {
        [Resolved]
        private AXPlayerApp app { get; set; }

        [Resolved]
        private AXPlayerConfigManager config { get; set; }

        [Resolved]
        private YoutubeExplode.YoutubeClient youtubeService { get; set; }

        [Resolved]
        private GoogleTranslate googleTranslate { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
        }

        public async Task RefreshAudioStreamList(string videoId)
        {
            try
            {
                var streamManifest = await app.YouTubeClient.Videos.Streams.GetManifestAsync(videoId);

                List<AudioOnlyStreamInfo> audioStreamInfo;

                try
                {
                    audioStreamInfo = streamManifest
                                .GetAudioOnlyStreams()
                                .ToList();
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());
                    audioStreamInfo = streamManifest
                                .GetAudioOnlyStreams()
                                .ToList();
                }

                Items = audioStreamInfo;

                if (!Current.Disabled)
                {
                    try
                    {
                        Current.Value = (IAudioStreamInfo)audioStreamInfo.Where(i => i.IsAudioLanguageDefault == true).GetWithHighestBitrate();
                        Current.Default = (IAudioStreamInfo)audioStreamInfo.Where(i => i.IsAudioLanguageDefault == true).GetWithHighestBitrate();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, e.GetDescription());
                        Current.Value = (IAudioStreamInfo)audioStreamInfo.GetWithHighestBitrate();
                        Current.Default = (IAudioStreamInfo)audioStreamInfo.GetWithHighestBitrate();
                    }
                }
            }
            catch (Exception e)
            {
                Current.Disabled = false;
                Logger.Error(e, e.GetDescription());
            }
        }

        protected override LocalisableString GenerateItemText(IAudioStreamInfo item)
        {
            if (item.AudioLanguage.HasValue)
                return $"[{item.AudioCodec}] {item.AudioLanguage.Value.Name} ({item.Bitrate.KiloBitsPerSecond:N0}kbps)";
            else
                return $"{item.AudioCodec} ({item.Bitrate.KiloBitsPerSecond:N0}kbps)";
        }
    }
}
