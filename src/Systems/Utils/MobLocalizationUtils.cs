using System;
using System.Collections.Generic;
using Vintagestory.API.Config;

namespace VsQuest
{
    public static class MobLocalizationUtils
    {
        public static string GetMobDisplayName(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return code;

            foreach (var mapped in GetCandidateCodes(code))
            {
                string key = $"mob-{mapped}";
                try
                {
                    string t = Lang.Get(key);
                    if (!string.Equals(t, key, StringComparison.OrdinalIgnoreCase)) return t;
                }
                catch
                {
                }
            }

            return MapCode(code);
        }

        private static IEnumerable<string> GetCandidateCodes(string code)
        {
            yield return code;

            string baseCode = MapCode(code);
            if (!string.IsNullOrWhiteSpace(baseCode) && !string.Equals(baseCode, code, StringComparison.OrdinalIgnoreCase))
            {
                yield return baseCode;
            }
        }

        private static string MapCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return code;

            int dashIndex = code.IndexOf('-');
            if (dashIndex > 0)
            {
                return code.Substring(0, dashIndex);
            }

            return code;
        }
    }
}
