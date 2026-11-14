// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using System.Threading.Tasks;

namespace AXPlayer.App.Utils.VoiceSearch
{
    /// <summary>
    /// A <see cref="IVoiceSearchService"/> used on platforms which do not provide speech recognition.
    /// </summary>
    public sealed class UnsupportedVoiceSearchService : IVoiceSearchService
    {
        public bool IsAvailable => false;

        public Task<VoiceSearchResult> RecognizeAsync(string? cultureCode, CancellationToken cancellationToken = default)
            => Task.FromResult(new VoiceSearchResult(VoiceSearchStatus.Unavailable));
    }
}
