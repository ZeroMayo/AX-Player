// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace AXPlayer.App.Utils.VoiceSearch
{
    /// <summary>
    /// The outcome of a speech recognition request.
    /// </summary>
    public enum VoiceSearchStatus
    {
        /// <summary>
        /// Speech was recognised successfully. <see cref="VoiceSearchResult.Text"/> contains the transcript.
        /// </summary>
        Success,

        /// <summary>
        /// The request completed, but no speech could be recognised.
        /// </summary>
        NoSpeech,

        /// <summary>
        /// Voice search is not supported on the current platform or device.
        /// </summary>
        Unavailable,

        /// <summary>
        /// Access to the microphone was denied by the operating system.
        /// </summary>
        PermissionDenied,

        /// <summary>
        /// The recognition request was cancelled by the user or the application.
        /// </summary>
        Cancelled,

        /// <summary>
        /// Recognition failed for another reason.
        /// </summary>
        Error,
    }

    /// <summary>
    /// The result of a speech recognition request.
    /// </summary>
    /// <param name="Status">The outcome of the request.</param>
    /// <param name="Text">The recognised transcript, when <paramref name="Status"/> is <see cref="VoiceSearchStatus.Success"/>.</param>
    /// <param name="Error">Optional diagnostic information when <paramref name="Status"/> is <see cref="VoiceSearchStatus.Error"/>.</param>
    public readonly record struct VoiceSearchResult(VoiceSearchStatus Status, string Text = "", string? Error = null);
}
