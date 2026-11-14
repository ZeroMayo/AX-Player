// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Utils.VoiceSearch;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;

namespace AXPlayer.Desktop.Windows
{
    /// <summary>
    /// A <see cref="IVoiceSearchService"/> backed by the Windows speech recognition APIs.
    /// </summary>
    internal sealed class WindowsVoiceSearchService : IVoiceSearchService
    {
        private static readonly Lazy<bool> available = new Lazy<bool>(checkAvailable);

        public bool IsAvailable => available.Value;

        public async Task<VoiceSearchResult> RecognizeAsync(string? cultureCode, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
                return new VoiceSearchResult(VoiceSearchStatus.Unavailable);

            cancellationToken.ThrowIfCancellationRequested();

            SpeechRecognizer? recognizer = null;

            try
            {
                recognizer = createRecognizer(cultureCode);
                recognizer.Constraints.Add(new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "dictation"));

                SpeechRecognitionCompilationResult compilation = await recognizer.CompileConstraintsAsync();

                if (compilation.Status != SpeechRecognitionResultStatus.Success)
                    return new VoiceSearchResult(VoiceSearchStatus.Unavailable, Error: compilation.Status.ToString());

                cancellationToken.ThrowIfCancellationRequested();

                SpeechRecognitionResult result = await recognizer.RecognizeAsync();

                if (cancellationToken.IsCancellationRequested)
                    return new VoiceSearchResult(VoiceSearchStatus.Cancelled);

                switch (result.Status)
                {
                    case SpeechRecognitionResultStatus.Success:
                        return string.IsNullOrWhiteSpace(result.Text)
                            ? new VoiceSearchResult(VoiceSearchStatus.NoSpeech)
                            : new VoiceSearchResult(VoiceSearchStatus.Success, result.Text.Trim());

                    case SpeechRecognitionResultStatus.UserCanceled:
                        return new VoiceSearchResult(VoiceSearchStatus.Cancelled);

                    case SpeechRecognitionResultStatus.MicrophoneUnavailable:
                        return new VoiceSearchResult(VoiceSearchStatus.PermissionDenied);

                    case SpeechRecognitionResultStatus.TopicLanguageNotSupported:
                    case SpeechRecognitionResultStatus.GrammarLanguageMismatch:
                    case SpeechRecognitionResultStatus.GrammarCompilationFailure:
                        return new VoiceSearchResult(VoiceSearchStatus.Unavailable, Error: result.Status.ToString());

                    default:
                        return new VoiceSearchResult(VoiceSearchStatus.Error, Error: result.Status.ToString());
                }
            }
            catch (OperationCanceledException)
            {
                return new VoiceSearchResult(VoiceSearchStatus.Cancelled);
            }
            catch (UnauthorizedAccessException e)
            {
                return new VoiceSearchResult(VoiceSearchStatus.PermissionDenied, Error: e.Message);
            }
            catch (Exception e)
            {
                return new VoiceSearchResult(VoiceSearchStatus.Error, Error: e.Message);
            }
            finally
            {
                recognizer?.Dispose();
            }
        }

        private static SpeechRecognizer createRecognizer(string? cultureCode)
        {
            if (string.IsNullOrEmpty(cultureCode))
                return new SpeechRecognizer();

            Language? match = SpeechRecognizer.SupportedTopicLanguages.FirstOrDefault(language => matches(language, cultureCode));

            return match == null ? new SpeechRecognizer() : new SpeechRecognizer(new Language(match.LanguageTag));
        }

        private static bool matches(Language language, string cultureCode)
        {
            if (language.LanguageTag.Equals(cultureCode, StringComparison.OrdinalIgnoreCase))
                return true;

            return language.LanguageTag.StartsWith(cultureCode + "-", StringComparison.OrdinalIgnoreCase)
                   || cultureCode.StartsWith(language.LanguageTag + "-", StringComparison.OrdinalIgnoreCase);
        }

        private static bool checkAvailable()
        {
            if (!OperatingSystem.IsWindows())
                return false;

            try
            {
                return SpeechRecognizer.SupportedTopicLanguages.Count > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
