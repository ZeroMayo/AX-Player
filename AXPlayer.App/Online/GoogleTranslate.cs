// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using AXPlayer.App.Localisation;

namespace AXPlayer.App.Online
{
    public class GoogleTranslate
    {
        private FrameworkConfigManager frameworkConfig;
        private AXPlayerAppBase app;
        private const string utf8charset = "utf-8";

        private static readonly HttpClient http_client = new HttpClient();

        public GoogleTranslate(AXPlayerAppBase app, FrameworkConfigManager frameworkConfig)
        {
            this.frameworkConfig = frameworkConfig;
            this.app = app;
        }

        // 기존 동기 메서드를 비동기로 변경
        public async Task<string> TranslateAsync(string text, GoogleTranslateLanguage translateLanguageFrom = GoogleTranslateLanguage.auto)
        {
            string responseText = string.Empty;

            try
            {
                string url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=" + translateLanguageFrom.ToString() + "&tl=" + app.CurrentLanguage.Value + "&dt=t&q=" + HttpUtility.HtmlEncode(text);

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0");

                using (var resp = await http_client.SendAsync(request))
                {
                    resp.EnsureSuccessStatusCode();
                    var characterSet = resp.Content.Headers.ContentType?.CharSet;
                    Encoding encode = (characterSet != null && characterSet.Equals(utf8charset, StringComparison.OrdinalIgnoreCase))
                        ? Encoding.UTF8
                        : Encoding.Default;

                    responseText = await resp.Content.ReadAsStringAsync();
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, e.GetDescription());
            }

            Logger.Log(responseText);

            string finalResult = parseTranslatedValue(responseText);

            return finalResult;
        }

        // 기존 Translate 메서드는 비동기 메서드 호출로 대체
        public string Translate(string text, GoogleTranslateLanguage translateLanguageFrom = GoogleTranslateLanguage.auto)
        {
            return TranslateAsync(text, translateLanguageFrom).GetAwaiter().GetResult();
        }

        private static string parseTranslatedValue(string jsonString)
        {
            // Get all json data
            var jsonData = JsonConvert.DeserializeObject<List<dynamic>>(jsonString);

            if (jsonData == null || jsonData.Count == 0 || jsonData[0] == null)
                return string.Empty;

            // Extract just the first array element (This is the only data we are interested in)
#pragma warning disable CS8602 // null 가능 참조에 대한 역참조입니다.
            var translationItems = jsonData[0];
#pragma warning restore CS8602 // null 가능 참조에 대한 역참조입니다.

            // Translation Data
            string translation = "";

            // Null 체크 추가
            if (translationItems is IEnumerable itemsEnumerable)
            {
                // Loop through the collection extracting the translated objects
                foreach (object? item in itemsEnumerable)
                {
                    // Convert the item array to IEnumerable
                    if (item is not IEnumerable translationLineObject)
                        continue;

                    // Convert the IEnumerable translationLineObject to a IEnumerator
                    IEnumerator translationLineString = translationLineObject.GetEnumerator();

                    // Get first object in IEnumerator
                    if (!translationLineString.MoveNext())
                        continue;

                    // Save its value (translated text)
                    translation += string.Format(" {0}", Convert.ToString(translationLineString.Current));
                }
            }

            // Remove first blank character
            if (translation.Length > 1) { translation = translation[1..]; }

            return translation;
        }
    }
}
