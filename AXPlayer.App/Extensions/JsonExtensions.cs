// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;

namespace AXPlayer.App.Extensions
{
    public static class JsonExtensions
    {
        public static JsonElement? GetPropertyOrNull(this JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (
                element.TryGetProperty(propertyName, out var result)
                && result.ValueKind != JsonValueKind.Null
                && result.ValueKind != JsonValueKind.Undefined
            )
            {
                return result;
            }

            return null;
        }

        public static string? GetStringOrNull(this JsonElement element) =>
           element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }
}
