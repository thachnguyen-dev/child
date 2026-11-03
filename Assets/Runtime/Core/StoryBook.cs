using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChildGame
{
    /// <summary>A story part beyond the standard intro / win / lose / third-star lines (e.g. the mango raid's endings).</summary>
    [Serializable]
    public class StorySegment
    {
        public string titleKey;
        public string[] keys = new string[0];
        [Tooltip("Save flag that marks it seen (empty = seen once its lines have played).")]
        public string seenFlag = "";
    }

    /// <summary>
    /// Stories play once (owner 2026-10-08): each part is remembered in the save once it has played, then skipped;
    /// the player can watch any part seen so far again from the instructions card, and skip any story while it plays.
    /// A part is identified by its first and last line keys.
    /// </summary>
    public static class StoryBook
    {
        public struct Entry
        {
            public string titleKey;
            public string[] keys;
            public bool seen;
        }

        public static string Id(IReadOnlyList<string> keys) => keys == null || keys.Count == 0 ? "" : "seen:" + keys[0] + "|" + keys[keys.Count - 1];

        public static bool Seen(MiniGameRecord record, IReadOnlyList<string> keys) => keys != null && keys.Count > 0 && record.HasFlag(Id(keys));

        public static void MarkSeen(MiniGameRecord record, IReadOnlyList<string> keys)
        {
            if (keys != null && keys.Count > 0) record.SetFlag(Id(keys));
        }

        /// <summary>Lines to play now: <paramref name="keys"/> if not seen yet (and marks them seen), else nothing.</summary>
        public static IReadOnlyList<string> Once(MiniGameRecord record, IReadOnlyList<string> keys)
        {
            if (keys == null || keys.Count == 0 || Seen(record, keys)) return Array.Empty<string>();
            MarkSeen(record, keys);
            return keys;
        }

        /// <summary>Every story part of a mini game in watching order, with whether the player has seen it.</summary>
        public static List<Entry> Parts(MiniGameDefinition game, MiniGameRecord record)
        {
            var parts = new List<Entry>();
            void Add(string title, string[] keys, bool seen)
            {
                if (keys != null && keys.Length > 0) parts.Add(new Entry { titleKey = title, keys = keys, seen = seen });
            }
            Add("story.part.intro", game.storyKeys, record.storySeen || Seen(record, game.storyKeys));
            if (game.extraStories != null)
                foreach (var s in game.extraStories)
                    Add(s.titleKey, s.keys, string.IsNullOrEmpty(s.seenFlag) ? Seen(record, s.keys) : record.HasFlag(s.seenFlag));
            Add(game.hasDifficulty ? "story.part.win" : "story.part.reward", game.winKeys, Seen(record, game.winKeys));
            Add("story.part.lose", game.loseKeys, Seen(record, game.loseKeys));
            Add("story.part.complete", game.completeKeys, Seen(record, game.completeKeys));
            return parts;
        }

        public static bool AnySeen(MiniGameDefinition game, MiniGameRecord record)
        {
            foreach (var p in Parts(game, record))
                if (p.seen) return true;
            return false;
        }
    }
}
