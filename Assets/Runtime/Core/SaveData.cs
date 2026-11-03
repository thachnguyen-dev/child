using System;
using System.Collections.Generic;

namespace ChildGame
{
    /// <summary>Everything the game remembers between sessions. Stored as JSON by <see cref="SaveStore"/>.</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;

        // Profile (design §5): chosen once at the first start.
        public bool profileDone;
        public string playerName = "";
        public Gender gender;
        /// <summary>The main story (owner 2026-10-08) has played once, after the profile.</summary>
        public bool introSeen;

        // Settings.
        public Language language = Language.Vietnamese;
        public float sfxVolume = 1f;

        // Progress (design §7, §8).
        public List<MiniGameRecord> games = new List<MiniGameRecord>();
        public List<string> achievements = new List<string>();

        // Shop (docs/3-shop-design.md). New game: 0 đồng, 1 sheet of paper, 1 pair of sandals.
        public int currency;
        public int paper = 1;
        public int sandalPairs = 1;
        public List<string> owned = new List<string>();
        public int snacksEaten;
        public MiniGameRecord adsToday = new MiniGameRecord { id = "ads" };
        /// <summary>Store order IDs already credited, so a purchase delivered twice is only granted once.</summary>
        public List<string> grantedOrders = new List<string>();

        /// <summary>The record for a mini game; created on demand when <paramref name="create"/> is true.</summary>
        public MiniGameRecord Record(string id, bool create = true)
        {
            foreach (var r in games)
                if (r.id == id) return r;
            if (!create) return null;
            var record = new MiniGameRecord { id = id };
            games.Add(record);
            return record;
        }

        public bool HasAchievement(string id) => achievements.Contains(id);
    }

    /// <summary>Per-mini-game progress.</summary>
    [Serializable]
    public class MiniGameRecord
    {
        public string id;
        public bool storySeen;
        /// <summary>First win per difficulty, indexed by <see cref="Difficulty"/>. Each one is worth one common point.</summary>
        public bool[] wins = new bool[3];
        public int plays;
        public int totalWins;
        /// <summary>Daily session limit bookkeeping (special mini games): the local date of the count, and the count.</summary>
        public string dailyDate = "";
        public int dailyCount;
        /// <summary>Per-mini-game progress flags (e.g. MG07 "swim" once swimming is unlocked).</summary>
        public List<string> flags = new List<string>();

        public bool HasFlag(string flag) => flags != null && flags.Contains(flag);

        public void SetFlag(string flag)
        {
            flags ??= new List<string>();
            if (!flags.Contains(flag)) flags.Add(flag);
        }

        public bool Won(Difficulty d) => wins != null && wins.Length > (int)d && wins[(int)d];

        public int Points
        {
            get
            {
                var n = 0;
                if (wins != null)
                    foreach (var w in wins) if (w) n++;
                return n;
            }
        }
    }
}
