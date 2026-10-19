// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using Newtonsoft.Json;
using osu.Framework.Extensions;
using osu.Framework.Logging;

namespace AXPlayer.App.Online
{
    /**
     * Handles fetching and creation/replacing of RYD dislike text spans.
     **/
    public class ReturnYouTubeDislike
    {
        public static ReturnYouTubeDislikesResponse GetDislikes(string videoId)
        {
            string responseText = string.Empty;

            try
            {
                // SYSLIB0014 및 OLOC001 수정: HttpClient 사용 및 문자열 지역화
                using (HttpClient client = new HttpClient())
                {
                    string url = string.Format(CultureInfo.InvariantCulture, "https://returnyoutubedislikeapi.com/Votes?videoId={0}", videoId);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

                    HttpResponseMessage resp = client.GetAsync(url).GetAwaiter().GetResult();
                    resp.EnsureSuccessStatusCode();

                    responseText = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, e.GetDescription());
            }

            Logger.Log(responseText);

            responseText = $"[{responseText}]";

            List<ReturnYouTubeDislikesResponse>? dislikes = JsonConvert.DeserializeObject<List<ReturnYouTubeDislikesResponse>>(responseText);
            if (dislikes == null || dislikes.Count == 0)
                return new ReturnYouTubeDislikesResponse(); // 또는 적절한 기본값 반환

            return dislikes[0];
        }
    }

    public class ReturnYouTubeDislikesResponse
    {
        public string? Id { get; set; }
        public string? DateCreated { get; set; }
        public int Likes { get; set; }
        public int RawDislikes { get; set; }
        public int RawLikes { get; set; }
        public int Dislikes { get; set; }
        public float Rating { get; set; }
        public int ViewCount { get; set; }
        public bool Deleted { get; set; }
    }
}
