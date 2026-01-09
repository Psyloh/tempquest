using System;
using Vintagestory.API.Config;

namespace VsQuest
{
    public static class LangUtil
    {
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
    }
}
