using System.Collections.Generic;
using UnityEngine;

namespace ChildGame
{
    /// <summary>
    /// Describes one mini game. One asset per mini game in <c>Resources/MiniGames</c>; the hub shows every asset found
    /// there. Texts are localization keys.
    /// </summary>
    [CreateAssetMenu(menuName = "Child/Mini Game Definition", fileName = "MiniGame")]
    public class MiniGameDefinition : ScriptableObject
    {
        [Tooltip("Stable id used in the save file. Never change it after release.")]
        public string id;
        [Tooltip("Scene loaded to play it (must be in the build scene list).")]
        public string sceneName;
        public string titleKey;
        [Tooltip("First-time story/dialogue lines (design §13), shown once before the first play.")]
        public string[] storyKeys = new string[0];
        [Tooltip("Story lines after a win (once; see StoryBook).")]
        public string[] winKeys = new string[0];
        [Tooltip("Story lines after a loss (once).")]
        public string[] loseKeys = new string[0];
        [Tooltip("Story lines when the third star is earned (once).")]
        public string[] completeKeys = new string[0];
        [Tooltip("Other story parts (endings, mid-game talks), listed for watching again.")]
        public List<StorySegment> extraStories = new List<StorySegment>();
        [Tooltip("Instructions shown before every round.")]
        public string instructionsKey;
        [Tooltip("Order in lists.")]
        public int order;
        [Tooltip("Development-only (the flow test game): hidden from the hub except in capture runs.")]
        public bool devOnly;
        [Tooltip("False for special mini games: no Easy/Normal/Hard, no points, they give currency instead.")]
        public bool hasDifficulty = true;
        [Tooltip("Sessions per day (0 = unlimited). Special 01: 3.")]
        public int dailyLimit;
        [Tooltip("Shop item that must be bought once to play (e.g. kite, crayons).")]
        public string unlockItem = "";
        [Tooltip("Sheets of paper used per play.")]
        public int paperPerPlay;
        [Tooltip("Needs a pair of sandals (Trộm xoài).")]
        public bool needsSandals;

        [Header("Hub")]
        [Tooltip("Where the placeholder hub object goes when the hub scene has no HubObject for this game.")]
        public Vector2 hubPosition;
        public Sprite hubSprite;
        public Color hubColor = Color.white;
    }

    /// <summary>All mini games, from <c>Resources/MiniGames</c>.</summary>
    public static class MiniGameCatalog
    {
        private static List<MiniGameDefinition> all;

        public static IReadOnlyList<MiniGameDefinition> All
        {
            get
            {
                if (all == null)
                {
                    all = new List<MiniGameDefinition>(Resources.LoadAll<MiniGameDefinition>("MiniGames"));
                    all.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : string.CompareOrdinal(a.id, b.id));
                }
                return all;
            }
        }

        public static MiniGameDefinition Find(string id)
        {
            foreach (var d in All)
                if (d.id == id) return d;
            return null;
        }
    }
}
