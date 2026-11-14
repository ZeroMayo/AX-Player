// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using System.Threading.Tasks;

namespace AXPlayer.App.Utils.VoiceSearch
{
    /// <summary>
    /// Captures a single spoken utterance and converts it to text.
    /// </summary>
    public interface IVoiceSearchService
    {
        /// <summary>
        /// Whether speech recognition is available on the current platform and device.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Listens for a single utterance and returns the recognised text.
        /// </summary>
        /// <param name="cultureCode">The preferred BCP-47 language tag for recognition, or <see langword="null"/> to use the system default.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        Task<VoiceSearchResult> RecognizeAsync(string? cultureCode, CancellationToken cancellationToken = default);
    }
}
