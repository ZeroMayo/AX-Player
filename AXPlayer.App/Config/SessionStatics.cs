// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.UserInterface;

namespace AXPlayer.App.Config
{
    /// <summary>
    /// Stores global per-session statics. These will not be stored after exiting the game.
    /// </summary>
    public class SessionStatics : InMemoryConfigManager<Static>
    {
        protected override void InitialiseDefaults()
        {
            SetDefault(Static.LastHoverSoundPlaybackTime, (double?)null);

            SetDefault(Static.IsControlVisible, false);

            SetDefault(Static.IsVideoPlaying, false);
            SetDefault(Static.CurrentThumbnailUrl, string.Empty);
            SetDefault(Static.IsAnyOverlayOpen, false);
            SetDefault(Static.WindowIsTray, false);
        }
    }

    public enum Static
    {
        /// <summary>
        /// The last playback time in milliseconds of a hover sample (from <see cref="HoverSounds"/>).
        /// </summary>
        LastHoverSoundPlaybackTime,

        IsControlVisible,
        IsVideoPlaying,
        CurrentThumbnailUrl,
        IsAnyOverlayOpen,
        WindowIsTray,
    }
}
