namespace ChildGame
{
    /// <summary>The common point rules (design §7). Pure logic so it can be unit tested.</summary>
    public static class Progress
    {
        public const int PointsPerGame = 3;

        /// <summary>
        /// Records a finished round. Returns true when this win earned the difficulty's point (its first win).
        /// Replay wins and losses never award points.
        /// </summary>
        public static bool RecordResult(SaveData save, string gameId, Difficulty difficulty, bool won)
        {
            var record = save.Record(gameId);
            record.plays++;
            if (!won) return false;
            record.totalWins++;
            if (record.Won(difficulty)) return false;
            record.wins[(int)difficulty] = true;
            return true;
        }

        /// <summary>
        /// Difficulties unlock in order (owner 2026-10-07, all mini games): Easy is always open, each higher level opens
        /// after the first win on the level below.
        /// </summary>
        public static bool IsUnlocked(SaveData save, string gameId, Difficulty difficulty) =>
            difficulty == Difficulty.Easy || (save.Record(gameId, false)?.Won(difficulty - 1) ?? false);

        public static int Points(SaveData save, string gameId) => save.Record(gameId, false)?.Points ?? 0;

        public static int TotalPoints(SaveData save)
        {
            var n = 0;
            foreach (var r in save.games) n += r.Points;
            return n;
        }

        public static int TotalWins(SaveData save)
        {
            var n = 0;
            foreach (var r in save.games) n += r.totalWins;
            return n;
        }

        public static int TotalPlays(SaveData save)
        {
            var n = 0;
            foreach (var r in save.games) n += r.plays;
            return n;
        }
    }
}
