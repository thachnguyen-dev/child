using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChildGame
{
    /// <summary>What an achievement checks. New kinds can be added when the owner's achievement list needs them.</summary>
    public enum AchievementCondition
    {
        /// <summary>Total common points across all mini games ≥ amount.</summary>
        TotalPoints,
        /// <summary>Points in mini game <c>gameId</c> ≥ amount (3 = all difficulties won).</summary>
        GamePoints,
        /// <summary>Mini game <c>gameId</c> won on <c>difficulty</c>.</summary>
        WinOnDifficulty,
        /// <summary>Total wins across all mini games (replays included) ≥ amount.</summary>
        TotalWins,
        /// <summary>Total rounds played ≥ amount.</summary>
        TotalPlays,
    }

    /// <summary>One achievement (design §8). Pure tracking; achievements give no rewards.</summary>
    [Serializable]
    public class Achievement
    {
        public string id;
        public string titleKey;
        public string descriptionKey;
        public AchievementCondition condition;
        public string gameId;
        public Difficulty difficulty;
        public int amount;
    }

    [Serializable]
    public class AchievementList
    {
        public List<Achievement> items = new List<Achievement>();
    }

    /// <summary>Loads the achievement list (Resources/Achievements/achievements.json) and unlocks earned ones.</summary>
    public static class Achievements
    {
        private static AchievementList cached;

        public static IReadOnlyList<Achievement> All
        {
            get
            {
                if (cached == null)
                {
                    var asset = Resources.Load<TextAsset>("Achievements/achievements");
                    cached = asset != null ? JsonUtility.FromJson<AchievementList>(asset.text) : null;
                    cached ??= new AchievementList();
                    cached.items ??= new List<Achievement>();
                }
                return cached.items;
            }
        }

        public static bool IsMet(SaveData save, Achievement a)
        {
            switch (a.condition)
            {
                case AchievementCondition.TotalPoints: return Progress.TotalPoints(save) >= a.amount;
                case AchievementCondition.GamePoints: return Progress.Points(save, a.gameId) >= a.amount;
                case AchievementCondition.WinOnDifficulty: return save.Record(a.gameId, false)?.Won(a.difficulty) ?? false;
                case AchievementCondition.TotalWins: return Progress.TotalWins(save) >= a.amount;
                case AchievementCondition.TotalPlays: return Progress.TotalPlays(save) >= a.amount;
                default: return false;
            }
        }

        /// <summary>Every achievement earned: the player holds the title "Trùm xóm" (owner 2026-10-08).</summary>
        public static bool AllDone(SaveData save)
        {
            if (All.Count == 0) return false;
            foreach (var a in All)
                if (!save.HasAchievement(a.id)) return false;
            return true;
        }

        public static int Earned(SaveData save)
        {
            var n = 0;
            foreach (var a in All)
                if (save.HasAchievement(a.id)) n++;
            return n;
        }

        /// <summary>Unlocks every met achievement and returns the newly unlocked ones.</summary>
        public static List<Achievement> Evaluate(SaveData save, IReadOnlyList<Achievement> list = null)
        {
            var unlocked = new List<Achievement>();
            foreach (var a in list ?? All)
            {
                if (save.HasAchievement(a.id) || !IsMet(save, a)) continue;
                save.achievements.Add(a.id);
                unlocked.Add(a);
            }
            return unlocked;
        }
    }
}
