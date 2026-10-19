// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AXPlayer.App.Utils
{
    internal static class Json
    {
        public static JsonElement Parse(string source)
        {
            using var document = JsonDocument.Parse(source);
            return document.RootElement.Clone();
        }

        public static JsonElement? TryParse(string source)
        {
            try
            {
                return Parse(source);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static string Encode(string value)
        {
            var buffer = new StringBuilder(value.Length);

            foreach (var c in value)
            {
                if (c == '\n')
                    buffer.Append("\\n");
                else if (c == '\r')
                    buffer.Append("\\r");
                else if (c == '\t')
                    buffer.Append("\\t");
                else if (c == '\\')
                    buffer.Append("\\\\");
                else if (c == '"')
                    buffer.Append("\\\"");
                else
                    buffer.Append(c);
            }

            return buffer.ToString();
        }

        // AOT-compatible serialization
        public static string Serialize(string? value) =>
            value is not null ? '"' + Encode(value) + '"' : "null";

        // AOT-compatible serialization
        public static string Serialize(int? value) =>
            value is not null ? value.Value.ToString(CultureInfo.InvariantCulture) : "null";
    }
}
