// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Testing;

namespace AXPlayer.App.Graphics.UserInterfaceV2
{
    /// <summary>
    /// Applies a free-text search query to a settings list built from <see cref="SettingsItemV2"/> and <see cref="SettingsSectionHeader"/>.
    /// </summary>
    public static class SettingsSearchFilter
    {
        /// <summary>
        /// Applies <paramref name="query"/> to the supplied settings list, hiding all settings (and section headings) which don't match.
        /// </summary>
        /// <param name="children">The settings list, in display order.</param>
        /// <param name="query">The search query. An empty query clears any active filtering.</param>
        /// <param name="localise">
        /// Optional localisation of filter terms, allowing a search in a non-English language to match translated captions.
        /// </param>
        /// <returns>Whether any setting matched <paramref name="query"/>. Always <c>false</c> for an empty query.</returns>
        public static bool Apply(IEnumerable<Drawable> children, string? query, Func<LocalisableString, string>? localise = null)
        {
            string[] terms = (query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool filtering = terms.Length > 0;

            bool anyMatch = false;
            bool sectionMatch = false;
            bool matchingHeader = false;
            SettingsSectionHeader? sectionHeader = null;

            foreach (Drawable child in children)
            {
                if (child is SettingsSectionHeader header)
                {
                    applyHeaderVisibility(sectionHeader, filtering, sectionMatch);

                    sectionHeader = header;
                    sectionMatch = false;

                    // a heading which matches the query matches its whole section, i.e. searching "audio" shows every audio setting.
                    matchingHeader = filtering && matchesTerms(new[] { header.Text }, terms, localise);

                    continue;
                }

                if (matchesQuery(child, matchingHeader ? Array.Empty<string>() : terms, filtering, localise))
                {
                    sectionMatch = true;
                    anyMatch = true;
                }
            }

            applyHeaderVisibility(sectionHeader, filtering, sectionMatch);

            return anyMatch;
        }

        private static void applyHeaderVisibility(SettingsSectionHeader? header, bool filtering, bool sectionMatch)
        {
            if (header != null)
                header.CanBeShown.Value = !filtering || sectionMatch;
        }

        private static bool matchesQuery(Drawable child, string[] terms, bool filtering, Func<LocalisableString, string>? localise)
        {
            switch (child)
            {
                case SettingsItemV2 item:
                    return applyFilter(item, terms, filtering, localise);

                case IFilterable filterable:
                    // buttons within the settings list (e.g. "Export logs")
                    return applyFilter(filterable, terms, filtering, localise);

                case SettingsFilterableContainer container:
                    // content without any search terms of its own, such as previews or credits
                    container.CanBeShown.Value = !filtering;
                    return false;

                default:
                    // containers which group settings together, such as the scaling settings
                    bool anyMatch = false;

                    foreach (SettingsItemV2 nestedItem in child.ChildrenOfType<SettingsItemV2>())
                        anyMatch |= applyFilter(nestedItem, terms, filtering, localise);

                    return anyMatch;
            }
        }

        private static bool applyFilter(IFilterable filterable, string[] terms, bool filtering, Func<LocalisableString, string>? localise)
        {
            // settings which can't be shown (e.g. because a related setting is turned off) can never match.
            bool canBeShown = !(filterable is IConditionalFilterable conditional) || conditional.CanBeShown.Value;

            bool matches = filtering && canBeShown && matchesTerms(filterable.FilterTerms, terms, localise);

            filterable.FilteringActive = filtering;
            filterable.MatchingFilter = !filtering || matches;

            return matches;
        }

        private static bool matchesTerms(IEnumerable<LocalisableString> filterTerms, string[] terms, Func<LocalisableString, string>? localise)
        {
            List<string> searchableText = new List<string>();

            foreach (LocalisableString filterTerm in filterTerms)
            {
                string text = filterTerm.ToString();

                if (!string.IsNullOrEmpty(text))
                    searchableText.Add(text);

                string? localised = localise?.Invoke(filterTerm);

                if (!string.IsNullOrEmpty(localised) && localised != text)
                    searchableText.Add(localised);
            }

            return terms.All(term => searchableText.Any(text => text.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
