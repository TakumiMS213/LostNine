using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MessageWindowSystem.Core
{
    /// <summary>元の会話テキストを変更せず、キーワードIDごとの色を表示用テキストに適用する。</summary>
    public static class KeywordTextFormatter
    {
        private static readonly Regex LinkPattern = new(
            @"<(?:link|a\s+href)\s*=\s*(?:""(?<id>[^""]*)""|(?<id>[^\s>]+))\s*>(?<content>.*?)</(?:link|a)>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        private static readonly Regex ColorTagPattern = new("</?color[^>]*>", RegexOptions.CultureInvariant);

        public static bool ContainsKeywords(string text)
        {
            return !string.IsNullOrEmpty(text) && LinkPattern.IsMatch(text);
        }

        public static string ApplyColors(string text, IReadOnlyDictionary<string, string> colors)
        {
            if (string.IsNullOrEmpty(text) || colors == null || colors.Count == 0)
                return text;

            return LinkPattern.Replace(text, match =>
            {
                string id = match.Groups["id"].Value.Trim();
                if (!colors.TryGetValue(id, out string color))
                    return match.Value;

                string content = ColorTagPattern.Replace(match.Groups["content"].Value, string.Empty);
                return $"<link=\"{id}\"><color={color}>{content}</color></link>";
            });
        }
    }
}
