using NUnit.Framework;

namespace ChildGame.Tests
{
    public class DailyLimitTests
    {
        [Test]
        public void ThreeSessionsADayThenResetNextDay()
        {
            var r = new MiniGameRecord { id = "toc" };
            Assert.AreEqual(3, DailyLimit.Left(r, 3, "2026-10-07"));
            Assert.IsTrue(DailyLimit.Consume(r, 3, "2026-10-07"));
            Assert.IsTrue(DailyLimit.Consume(r, 3, "2026-10-07"));
            Assert.IsTrue(DailyLimit.Consume(r, 3, "2026-10-07"));
            Assert.IsFalse(DailyLimit.Consume(r, 3, "2026-10-07"), "fourth session the same day");
            Assert.AreEqual(0, DailyLimit.Left(r, 3, "2026-10-07"));
            Assert.AreEqual(3, DailyLimit.Left(r, 3, "2026-10-08"), "next day resets");
            Assert.IsTrue(DailyLimit.Consume(r, 3, "2026-10-08"));
            Assert.AreEqual(2, DailyLimit.Left(r, 3, "2026-10-08"));
        }

        [Test]
        public void ClockGoingBackDoesNotReset()
        {
            var r = new MiniGameRecord { id = "toc" };
            for (var i = 0; i < 3; i++) DailyLimit.Consume(r, 3, "2026-10-08");
            Assert.AreEqual(0, DailyLimit.Left(r, 3, "2026-10-07"), "an earlier date keeps the stored count");
        }

        [Test]
        public void SaveKeepsCurrencyAndDailyCount()
        {
            var s = new SaveData { currency = 17 };
            DailyLimit.Consume(s.Record("toc"), 3, "2026-10-07");
            var back = SaveStore.FromJson(SaveStore.ToJson(s));
            Assert.AreEqual(17, back.currency);
            Assert.AreEqual(2, DailyLimit.Left(back.Record("toc"), 3, "2026-10-07"));
        }
    }
}
