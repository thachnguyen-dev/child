using NUnit.Framework;
using UnityEngine;

namespace ChildGame.Tests
{
    public class ShopTests
    {
        private static MiniGameDefinition Def(string unlock = "", int paper = 0, bool sandals = false)
        {
            var d = ScriptableObject.CreateInstance<MiniGameDefinition>();
            d.id = "x";
            d.unlockItem = unlock;
            d.paperPerPlay = paper;
            d.needsSandals = sandals;
            return d;
        }

        [Test]
        public void NewGameStartingKit()
        {
            var s = new SaveData();
            Assert.AreEqual(0, s.currency);
            Assert.AreEqual(1, s.paper);
            Assert.AreEqual(1, s.sandalPairs);
        }

        [Test]
        public void OldSavesGetTheStartingKit()
        {
            var back = SaveStore.FromJson("{\"version\":1,\"currency\":50}");
            Assert.AreEqual(1, back.paper);
            Assert.AreEqual(1, back.sandalPairs);
            Assert.AreEqual(50, back.currency);
            Assert.IsNotNull(back.owned);
        }

        [Test]
        public void BuyingCostsDongAndRespectsLimits()
        {
            var s = new SaveData { currency = 0 };
            var kite = Shop.Find("kite");
            Assert.IsFalse(Shop.Buy(s, kite), "no money");
            s.currency = kite.price;
            Assert.IsTrue(Shop.Buy(s, kite));
            Assert.AreEqual(0, s.currency);
            Assert.IsTrue(Shop.Owns(s, "kite"));
            s.currency = 100000;
            Assert.IsFalse(Shop.Buy(s, kite), "bought once");
            Assert.IsFalse(Shop.Buy(s, Shop.Find("sandals")), "already one pair (the maximum)");
            var paper = s.paper;
            Assert.IsTrue(Shop.Buy(s, Shop.Find("paper")));
            Assert.AreEqual(paper + 1, s.paper, "one sheet at a time");
        }

        [Test]
        public void LocksAndPaper()
        {
            var s = new SaveData();
            var drawing = Def("crayons", 1);
            Assert.AreEqual("lock.crayons", Shop.LockReason(s, drawing));
            s.owned.Add("crayons");
            Assert.IsNull(Shop.LockReason(s, drawing));
            Assert.IsTrue(Shop.UsePaper(s, drawing));
            Assert.AreEqual(0, s.paper);
            Assert.IsFalse(Shop.UsePaper(s, drawing), "no paper left");

            var mango = Def(sandals: true);
            Assert.IsNull(Shop.LockReason(s, mango));
            Shop.LoseSandalPair(s);
            Assert.AreEqual("lock.sandals", Shop.LockReason(s, mango));
            Assert.AreEqual("sandals", Shop.LockItem(mango));
        }

        [Test]
        public void VendorTakesDongOrSandals()
        {
            var s = new SaveData { currency = 0 };
            var food = Shop.Vendor[0];
            Assert.IsFalse(Shop.Buy(s, food), "no money");
            Assert.IsTrue(Shop.Buy(s, food, true), "a pair of sandals");
            Assert.AreEqual(0, s.sandalPairs);
            Assert.IsFalse(Shop.Buy(s, food, true), "no sandals left");
            Assert.AreEqual(1, s.snacksEaten);
        }

        [Test]
        public void AdsGiveDongUpToTheDailyLimit()
        {
            var s = new SaveData();
            for (var i = 0; i < Shop.AdsPerDay; i++) Assert.IsTrue(Shop.RewardAd(s, "2026-10-07"));
            Assert.IsFalse(Shop.RewardAd(s, "2026-10-07"));
            Assert.AreEqual(Shop.AdsPerDay * Shop.AdReward, s.currency);
            Assert.AreEqual(Shop.AdsPerDay, Shop.AdsLeft(s, "2026-10-08"));
        }
    }
}
