// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace AXPlayer.App.Utils
{
    public interface IAXPlayerAppMessageHandler
    {
        void OpenSettings();
        void TogglePreservePitch();
        void SelectPlaylist(string id);
        void OpenMyPlaylists();
        void SelectVideo(string id);
        void OpenAudioEffects();
    }
}
