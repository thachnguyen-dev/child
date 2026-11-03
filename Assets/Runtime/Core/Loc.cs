using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ChildGame
{
    /// <summary>
    /// Text lookup for Vietnamese and English (design §2). Tables are <c>Resources/Localization/vi.txt</c> and
    /// <c>en.txt</c>, one <c>key = value</c> per line (<c>#</c> comments, <c>\n</c> for a line break).
    /// <list type="bullet">
    /// <item>A key may have gender variants, <c>key@boy</c> / <c>key@girl</c>, picked by the protagonist's gender.</item>
    /// <item><c>{name}</c> is replaced with the protagonist's name.</item>
    /// <item>A missing key shows as <c>#key</c> so gaps are visible on screen.</item>
    /// </list>
    /// </summary>
    public static class Loc
    {
        private static Dictionary<string, string> table = new Dictionary<string, string>();

        public static Language Current { get; private set; }
        public static string PlayerName = "";
        public static Gender PlayerGender;

        /// <summary>Raised after a language switch; <see cref="UI.LocText"/> refreshes on it.</summary>
        public static event Action Changed;

        public static string FileCode(Language language) => language == Language.Vietnamese ? "vi" : "en";

        public static void Load(Language language)
        {
            Current = language;
            var asset = Resources.Load<TextAsset>("Localization/" + FileCode(language));
            table = asset != null ? Parse(asset.text) : new Dictionary<string, string>();
            Changed?.Invoke();
        }

        public static bool Has(string key) => table.ContainsKey(key);

        /// <summary>The raw table text (no gender variant, no {name}), or null.</summary>
        public static string Raw(string key) => key != null && table.TryGetValue(key, out var v) ? v : null;

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            var variant = key + (PlayerGender == Gender.Girl ? "@girl" : "@boy");
            if (!table.TryGetValue(variant, out var text) && !table.TryGetValue(key, out text))
                return "#" + key;
            return text.Replace("{name}", string.IsNullOrEmpty(PlayerName) ? "…" : PlayerName);
        }

        public static string Format(string key, params object[] args)
        {
            try
            {
                return string.Format(Get(key), args);
            }
            catch (FormatException)
            {
                return Get(key);
            }
        }

        public static Dictionary<string, string> Parse(string text)
        {
            var result = new Dictionary<string, string>();
            var lines = text.Replace("\r", "").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    Debug.LogWarning($"[CHILD-LOC] Line {i + 1} has no 'key = value': {line}");
                    continue;
                }
                var key = line.Substring(0, eq).Trim();
                var value = Unescape(line.Substring(eq + 1).Trim());
                if (result.ContainsKey(key)) Debug.LogWarning($"[CHILD-LOC] Duplicate key '{key}' on line {i + 1}");
                result[key] = value;
            }
            return result;
        }

        private static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (var i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    var c = s[++i];
                    sb.Append(c == 'n' ? '\n' : c);
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }
}
