// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AXPlayer.App.Online
{
    public static class AXPlayerDescriptionParser
    {
        public static bool IsTwitter(string url)
        {
            if (url.Contains("dropbox.com"))
                return false;

            if (url.Contains("x.com"))
                return true;

            if (url.Contains("twitter.com"))
                return true;

            return false;
        }

        public static bool IsYouTubeVideo(string url)
        {
            if (url.Contains("youtu.be"))
                return true;

            if (url.Contains("youtube.com/watch?v="))
                return true;

            return false;
        }

        public static bool IsYouTubePlaylist(string url)
        {
            if (url.Contains("youtube.com/playlist?list="))
                return true;

            return false;
        }

        public static bool IsDiscord(string url)
        {
            if (url.Contains("discord.com"))
                return true;

            return false;
        }

        public static bool IsTwitch(string url)
        {
            if (url.Contains("twitch.tv"))
                return true;

            return false;
        }

        public static bool IsYouTubeChannel(string url)
        {
            if (url.Contains("youtube.com/channel/"))
                return true;

            return false;
        }

        public static List<YouTubeDescriptionTextToken> Parse(string input)
        {
            var tokens = new List<YouTubeDescriptionTextToken>();
            var regex = new Regex(@"https?://[^\s]+|@\w+|#\w+|\d{1,2}:\d{2}(?::\d{2})?");

            int lastIndex = 0;

            foreach (Match match in regex.Matches(input))
            {
                // 일반 텍스트
                if (match.Index > lastIndex)
                {
                    tokens.Add(new YouTubeDescriptionTextToken
                    {
                        Type = YouTubeDescriptionTokenType.Text,
                        Value = input[lastIndex..match.Index]
                    });
                }

                // URL or Mention
                if (Regex.IsMatch(match.Value, @"^\d{1,2}:\d{2}(:\d{2})?$"))
                {
                    tokens.Add(new YouTubeDescriptionTextToken
                    {
                        Type = YouTubeDescriptionTokenType.Timestamp,
                        Value = match.Value
                    });
                }
                else if (match.Value.StartsWith('@'))
                {
                    tokens.Add(new YouTubeDescriptionTextToken
                    {
                        Type = YouTubeDescriptionTokenType.Mention,
                        Value = match.Value
                    });
                }
                else if (match.Value.StartsWith('#'))
                {
                    tokens.Add(new YouTubeDescriptionTextToken
                    {
                        Type = YouTubeDescriptionTokenType.Hashtag,
                        Value = match.Value
                    });
                }
                else
                {
                    tokens.Add(new YouTubeDescriptionTextToken
                    {
                        Type = YouTubeDescriptionTokenType.Url,
                        Value = match.Value
                    });
                }

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < input.Length)
            {
                tokens.Add(new YouTubeDescriptionTextToken
                {
                    Type = YouTubeDescriptionTokenType.Text,
                    Value = input[lastIndex..]
                });
            }

            return tokens;
        }
    }
}
