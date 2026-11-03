using System;

namespace ChildGame
{
    /// <summary>
    /// Sessions-per-day limits for special mini games (Special 01: 3 per day). The day is the phone's local date; the
    /// count resets when the date moves forward. Setting the clock back does not reset it (the stored date wins).
    /// </summary>
    public static class DailyLimit
    {
        public static string Today => DateTime.Now.ToString("yyyy-MM-dd");

        public static int Used(MiniGameRecord r, string today) =>
            string.CompareOrdinal(today, r.dailyDate ?? "") > 0 ? 0 : r.dailyCount;

        public static int Left(MiniGameRecord r, int limit, string today) => Math.Max(0, limit - Used(r, today));

        /// <summary>Counts one session; false if none are left today.</summary>
        public static bool Consume(MiniGameRecord r, int limit, string today)
        {
            if (Left(r, limit, today) <= 0) return false;
            if (string.CompareOrdinal(today, r.dailyDate ?? "") > 0)
            {
                r.dailyDate = today;
                r.dailyCount = 0;
            }
            r.dailyCount++;
            return true;
        }
    }
}
