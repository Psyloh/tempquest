using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest
{
    public static class LocalizationUtils
    {
        private static Dictionary<string, string> displayNameMap;

        public static void LoadFromAssets(ICoreAPI api)
        {
            if (api == null) return;

            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void TryMergeFromDomain(string domain)
            {
                if (string.IsNullOrWhiteSpace(domain)) return;
                try
                {
                    var dicts = api.Assets.GetMany<Dictionary<string, string>>(api.Logger, "config/mobdisplaynames", domain);
                    foreach (var pair in dicts)
                    {
                        if (pair.Value == null) continue;
                        foreach (var kvp in pair.Value)
                        {
                            if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value)) continue;
                            merged[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch (Exception e)
                {
                    api.Logger.Warning($"[vsquest] Could not load mobdisplaynames from domain '{domain}': {e.Message}");
                }
            }

            foreach (var mod in api.ModLoader.Mods)
            {
                TryMergeFromDomain(mod?.Info?.ModID);
            }

            displayNameMap = merged.Count > 0 ? merged : null;
        }

        public static string GetSafe(string langKey)
        {
            if (string.IsNullOrEmpty(langKey)) return "";

            try
            {
                return Lang.Get(langKey);
            }
            catch
            {
                return langKey;
            }
        }

        public static string GetSafe(string langKey, params object[] args)
        {
            if (string.IsNullOrEmpty(langKey)) return "";

            try
            {
                return Lang.Get(langKey, args);
            }
            catch
            {
                return langKey;
            }
        }

        public static string GetFallback(string primaryLangKey, string fallbackLangKey)
        {
            if (!string.IsNullOrEmpty(primaryLangKey))
            {
                return GetSafe(primaryLangKey);
            }

            return GetSafe(fallbackLangKey);
        }

        public static string GetMobDisplayName(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return code;

            string normalized = NormalizeMobCode(code);

            // 1) mobdisplaynames.json overrides (exact only)
            if (displayNameMap != null && displayNameMap.TryGetValue(normalized, out var exactOverride) && !string.IsNullOrWhiteSpace(exactOverride))
            {
                return exactOverride;
            }

            // 2) Game localization (exact, then progressively shorter prefixes)
            foreach (string candidate in GetFallbackCandidates(normalized))
            {
                string t;
                t = TryLangGet($"item-creature-{candidate}");
                if (!string.IsNullOrWhiteSpace(t)) return t;
                t = TryLangGet($"game:item-creature-{candidate}");
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }

            // 3) mobdisplaynames.json overrides (prefixes/base), only if game localization has no match
            if (displayNameMap != null)
            {
                foreach (string candidate in GetFallbackCandidates(normalized))
                {
                    if (displayNameMap.TryGetValue(candidate, out var mappedName) && !string.IsNullOrWhiteSpace(mappedName))
                    {
                        return mappedName;
                    }
                }
            }

            // 4) Wildcard translations as a last resort
            foreach (string candidate in GetFallbackCandidates(normalized))
            {
                string t;
                t = TryLangGet($"item-creature-{candidate}-*");
                if (!string.IsNullOrWhiteSpace(t)) return t;
                t = TryLangGet($"game:item-creature-{candidate}-*");
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }

            string fallback = MapCode(normalized);
            return fallback;
        }

        public static bool MobCodeMatches(string targetCode, string killedCode)
        {
            if (string.IsNullOrWhiteSpace(targetCode) || string.IsNullOrWhiteSpace(killedCode)) return false;

            targetCode = NormalizeMobCode(targetCode);
            killedCode = NormalizeMobCode(killedCode);

            if (string.Equals(targetCode, killedCode, StringComparison.OrdinalIgnoreCase)) return true;
            if (killedCode.StartsWith(targetCode + "-", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        public static string NormalizeMobCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return code;

            int colonIndex = code.IndexOf(':');
            if (colonIndex > 0 && colonIndex < code.Length - 1)
            {
                code = code.Substring(colonIndex + 1);
            }

            return code;
        }

        private static IEnumerable<string> GetFallbackCandidates(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) yield break;

            string cur = code;
            while (!string.IsNullOrWhiteSpace(cur))
            {
                yield return cur;
                int idx = cur.LastIndexOf('-');
                if (idx <= 0) break;
                cur = cur.Substring(0, idx);
            }
        }

        private static string TryLangGet(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            try
            {
                string t = Lang.Get(key);
                if (!string.IsNullOrWhiteSpace(t) && !string.Equals(t, key, StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }
            catch
            {
            }

            return null;
        }

        private static string MapCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return code;

            code = NormalizeMobCode(code);

            int dashIndex = code.IndexOf('-');
            if (dashIndex > 0)
            {
                return code.Substring(0, dashIndex);
            }

            return code;
        }
    }
}
