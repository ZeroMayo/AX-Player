// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using Google.Apis.YouTube.v3.Data;
using osu.Framework.Graphics.Containers;
using AXPlayer.App.Online;

namespace AXPlayer.App
{
    public partial class MediaSession : CompositeDrawable
    {
        protected YouTubeAPI YouTubeAPI;
        protected bool IsLoaded;

        public MediaSession()
        {
        }

        public virtual void CreateMediaSession(YouTubeAPI youtubeAPI, string audioPath)
        {
        }

        public virtual void UpdateMediaSession(Video video)
        {
        }

        public virtual void UpdateTimestamp(Video video, double pos)
        {
        }

        public virtual void DeleteMediaSession()
        {
        }

        public virtual void RegisterControlEvents(MediaSessionControls controls)
        {
        }

        public virtual void UnregisterControlEvents()
        {
        }

        public virtual void UpdatePlaybackSpeed(double speed)
        {
        }

        public virtual void UpdatePlayingState(bool playing)
        {
        }
    }

    public class MediaSessionControls
    {
        public Action PlayButtonPressed;
        public Action PauseButtonPressed;
        public Action PrevButtonPressed;
        public Action NextButtonPressed;
        public Action<double> OnSeek;
    } 
}
