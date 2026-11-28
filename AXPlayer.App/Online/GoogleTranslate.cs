// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using AXPlayer.App.Extensions;
using AXPlayer.App.Localisation;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// Translates text through Google's public translate endpoint, which is used when the
    /// Google Translate metadata source is selected.
    /// </summary>
    public class GoogleTranslate
    {
        private const string endpoint_url = "https://translate.googleapis.com/translate_a/single?client=gtx&dt=t";

        /// <summary>
        /// The number of translations that are kept in memory.
        /// </summary>
        /// <remarks>
        /// The cache only exists to spare repeated requests for the same string, as a title is often
        /// translated several times while a single screen is built. Dropping the whole cache when the
        /// bound is reached keeps the memory cost predictable without an eviction policy.
        /// </remarks>
        private const int max_cached_translations = 512;

        private const int request_timeout_seconds = 30;

        // A single client is reused for the lifetime of the app so that connections (and their TLS
        // handshakes) are pooled instead of being rebuilt for every translated string.
        private static readonly HttpClient http_client = createHttpClient();

        private static readonly ConcurrentDictionary<(GoogleTranslateLanguage Source, string Target, string Text), string> translation_cache = new ConcurrentDictionary<(GoogleTranslateLanguage Source, string Target, string Text), string>();

        private readonly AXPlayerAppBase app;

        /// <param name="app">The app that supplies the language to translate into.</param>
        /// <param name="frameworkConfig">The framework configuration, kept so that existing constructions do not have to change.</param>
        public GoogleTranslate(AXPlayerAppBase app, FrameworkConfigManager frameworkConfig)
        {
            this.app = app;
        }

        /// <summary>
        /// Translates <paramref name="text"/> into the language the app is currently displayed in.
        /// </summary>
        /// <param name="text">The text to translate. Whitespace-only text is returned unchanged.</param>
        /// <param name="translateLanguageFrom">The language of <paramref name="text"/>, or automatic detection.</param>
        /// <param name="cancellationToken">The cancellation token for the request.</param>
        /// <returns>The translated text, or <see cref="string.Empty"/> if the translation failed.</returns>
        public async Task<string> TranslateAsync(string text, GoogleTranslateLanguage translateLanguageFrom = GoogleTranslateLanguage.auto, CancellationToken cancellationToken = default)
        {
            if (tryGetCachedTranslation(text, translateLanguageFrom, out string? cachedTranslation))
                return cachedTranslation;

            string targetLanguage = app.CurrentLanguage.Value.ToCultureCode();
            string responseText;

            try
            {
                string requestUrl = buildRequestUrl(text, translateLanguageFrom, targetLanguage);

                using (HttpResponseMessage response = await http_client.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    // ReadAsStringAsync() decodes the payload with the charset that the server declares,
                    // so the response never has to be decoded by hand.
                    responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                Logger.Error(e, e.GetDescription());
                return string.Empty;
            }

            Logger.Log(responseText);

            string translation = ParseTranslatedValue(responseText);

            if (!string.IsNullOrEmpty(translation))
                cacheTranslation(text, translateLanguageFrom, targetLanguage, translation);

            return translation;
        }

        /// <summary>
        /// Translates <paramref name="text"/> into the language the app is currently displayed in, blocking
        /// the calling thread until the translation is available.
        /// </summary>
        /// <param name="text">The text to translate. Whitespace-only text is returned unchanged.</param>
        /// <param name="translateLanguageFrom">The language of <paramref name="text"/>, or automatic detection.</param>
        /// <remarks>
        /// Prefer <see cref="TranslateAsync"/> wherever the caller can await; this exists for the synchronous
        /// metadata accessors that the UI binds to.
        /// </remarks>
        public string Translate(string text, GoogleTranslateLanguage translateLanguageFrom = GoogleTranslateLanguage.auto)
        {
            if (tryGetCachedTranslation(text, translateLanguageFrom, out string? cachedTranslation))
                return cachedTranslation;

            // Starting the request on the thread pool keeps every continuation off the caller's
            // synchronization context, so blocking an update-thread caller cannot deadlock.
            return Task.Run(() => TranslateAsync(text, translateLanguageFrom)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Extracts the translated text from the JSON response of the translate endpoint.
        /// </summary>
        /// <param name="responseText">The raw response body.</param>
        /// <returns>The translated text, or <see cref="string.Empty"/> if the response holds none.</returns>
        internal static string ParseTranslatedValue(string? responseText)
        {
            if (string.IsNullOrEmpty(responseText))
                return string.Empty;

            try
            {
                using (JsonDocument document = JsonDocument.Parse(responseText))
                {
                    JsonElement root = document.RootElement;

                    // The response is [[segment, segment, ...], ...metadata], where each segment is
                    // [translated text, original text, ...]. Only the translated text is of interest.
                    if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                        return string.Empty;

                    JsonElement segments = root[0];

                    if (segments.ValueKind != JsonValueKind.Array)
                        return string.Empty;

                    var builder = new StringBuilder();

                    foreach (JsonElement segment in segments.EnumerateArray())
                    {
                        if (segment.ValueKind != JsonValueKind.Array || segment.GetArrayLength() == 0)
                            continue;

                        JsonElement translatedText = segment[0];

                        if (translatedText.ValueKind != JsonValueKind.String)
                            continue;

                        string? value = translatedText.GetString();

                        if (string.IsNullOrEmpty(value))
                            continue;

                        // The endpoint splits longer input into sentence-sized segments and does not always
                        // keep the whitespace between them, so a single space is put back where it is missing.
                        if (builder.Length > 0 && !char.IsWhiteSpace(builder[builder.Length - 1]) && !char.IsWhiteSpace(value[0]))
                            builder.Append(' ');

                        builder.Append(value);
                    }

                    return builder.ToString();
                }
            }
            catch (JsonException e)
            {
                Logger.Error(e, e.GetDescription());
                return string.Empty;
            }
        }

        /// <summary>
        /// Looks up an already translated <paramref name="text"/>.
        /// </summary>
        private bool tryGetCachedTranslation(string text, GoogleTranslateLanguage translateLanguageFrom, [NotNullWhen(true)] out string? translation)
        {
            translation = null;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            return translation_cache.TryGetValue((translateLanguageFrom, app.CurrentLanguage.Value.ToCultureCode(), text), out translation);
        }

        private static void cacheTranslation(string text, GoogleTranslateLanguage translateLanguageFrom, string targetLanguage, string translation)
        {
            if (translation_cache.Count >= max_cached_translations)
                translation_cache.Clear();

            translation_cache[(translateLanguageFrom, targetLanguage, text)] = translation;
        }

        private static string buildRequestUrl(string text, GoogleTranslateLanguage translateLanguageFrom, string targetLanguage)
        {
            // Uri.EscapeDataString() escapes the value for the query component; HTML encoding would turn
            // ampersands into entities and change the text that is being translated.
            return string.Create(CultureInfo.InvariantCulture, $"{endpoint_url}&sl={translateLanguageFrom}&tl={targetLanguage}&q={Uri.EscapeDataString(text)}");
        }

        private static HttpClient createHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                // Recycling pooled connections lets a DNS change take effect, and decompression keeps
                // the transferred payload as small as the server allows.
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                AutomaticDecompression = DecompressionMethods.All,
            };

            var client = new HttpClient(handler)
            {
                // The synchronous Translate() callers block the update thread, so a request that hangs
                // must not be able to stall the interface for the default two minutes.
                Timeout = TimeSpan.FromSeconds(request_timeout_seconds),
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

            return client;
        }
    }
}
