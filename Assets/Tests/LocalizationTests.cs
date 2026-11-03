using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ChildGame.Tests
{
    public class LocalizationTests
    {
        /// <summary>A key written twice silently replaces the first text (it once hid "Chào bạn nhỏ!").</summary>
        [TestCase("vi")]
        [TestCase("en")]
        public void NoKeyIsDefinedTwice(string language)
        {
            var seen = new HashSet<string>();
            var twice = new List<string>();
            foreach (var raw in Resources.Load<TextAsset>("Localization/" + language).text.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line.Substring(0, eq).Trim();
                if (!seen.Add(key)) twice.Add(key);
            }
            CollectionAssert.IsEmpty(twice, "keys defined twice in " + language);
        }
    }
}
