using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ChildGame.Tests
{
    public class ProgressTests
    {
        [Test]
        public void FirstWinPerDifficultyGivesOnePoint()
        {
            var save = new SaveData();
            Assert.IsTrue(Progress.RecordResult(save, "a", Difficulty.Easy, true));
            Assert.IsFalse(Progress.RecordResult(save, "a", Difficulty.Easy, true), "replay win gives nothing");
            Assert.IsTrue(Progress.RecordResult(save, "a", Difficulty.Hard, true));
            Assert.AreEqual(2, Progress.Points(save, "a"));
        }

        [Test]
        public void LossesGiveNoPointsButCountAsPlays()
        {
            var save = new SaveData();
            Assert.IsFalse(Progress.RecordResult(save, "a", Difficulty.Normal, false));
            Assert.AreEqual(0, Progress.Points(save, "a"));
            Assert.AreEqual(1, save.Record("a").plays);
            Assert.AreEqual(0, save.Record("a").totalWins);
        }

        [Test]
        public void AtMostThreePointsPerGame()
        {
            var save = new SaveData();
            for (var round = 0; round < 3; round++)
                foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
                    Progress.RecordResult(save, "a", d, true);
            Progress.RecordResult(save, "b", Difficulty.Easy, true);
            Assert.AreEqual(3, Progress.Points(save, "a"));
            Assert.AreEqual(4, Progress.TotalPoints(save));
            Assert.AreEqual(10, Progress.TotalWins(save));
        }
    }

    public class UnlockTests
    {
        [Test]
        public void DifficultiesUnlockInOrder()
        {
            var save = new SaveData();
            Assert.IsTrue(Progress.IsUnlocked(save, "a", Difficulty.Easy));
            Assert.IsFalse(Progress.IsUnlocked(save, "a", Difficulty.Normal));
            Progress.RecordResult(save, "a", Difficulty.Easy, false);
            Assert.IsFalse(Progress.IsUnlocked(save, "a", Difficulty.Normal), "a loss unlocks nothing");
            Progress.RecordResult(save, "a", Difficulty.Easy, true);
            Assert.IsTrue(Progress.IsUnlocked(save, "a", Difficulty.Normal));
            Assert.IsFalse(Progress.IsUnlocked(save, "a", Difficulty.Hard));
            Progress.RecordResult(save, "a", Difficulty.Normal, true);
            Assert.IsTrue(Progress.IsUnlocked(save, "a", Difficulty.Hard));
            Assert.IsFalse(Progress.IsUnlocked(save, "b", Difficulty.Normal), "per mini game");
        }
    }

    public class SaveTests
    {
        [Test]
        public void JsonRoundTrip()
        {
            var save = new SaveData { playerName = "Tí", gender = Gender.Girl, language = Language.English, profileDone = true };
            Progress.RecordResult(save, "a", Difficulty.Normal, true);
            save.Record("a").storySeen = true;
            save.achievements.Add("x");
            var back = SaveStore.FromJson(SaveStore.ToJson(save));
            Assert.AreEqual("Tí", back.playerName);
            Assert.AreEqual(Gender.Girl, back.gender);
            Assert.AreEqual(Language.English, back.language);
            Assert.IsTrue(back.Record("a", false).Won(Difficulty.Normal));
            Assert.IsTrue(back.Record("a", false).storySeen);
            Assert.IsTrue(back.HasAchievement("x"));
        }

        [Test]
        public void DamagedWinArraysAreRepaired()
        {
            var back = SaveStore.FromJson("{\"games\":[{\"id\":\"a\",\"wins\":[true]}]}");
            Assert.AreEqual(3, back.Record("a").wins.Length);
            Assert.IsTrue(back.Record("a").Won(Difficulty.Easy));
            Assert.IsNotNull(back.achievements);
        }
    }

    public class AchievementTests
    {
        [Test]
        public void UnlocksOnceWhenMet()
        {
            var list = new List<Achievement>
            {
                new Achievement { id = "p1", condition = AchievementCondition.TotalPoints, amount = 1 },
                new Achievement { id = "allA", condition = AchievementCondition.GamePoints, gameId = "a", amount = 3 },
                new Achievement { id = "hardA", condition = AchievementCondition.WinOnDifficulty, gameId = "a", difficulty = Difficulty.Hard },
            };
            var save = new SaveData();
            Assert.AreEqual(0, Achievements.Evaluate(save, list).Count);
            Progress.RecordResult(save, "a", Difficulty.Hard, true);
            var first = Achievements.Evaluate(save, list);
            Assert.AreEqual(2, first.Count);
            Assert.AreEqual(0, Achievements.Evaluate(save, list).Count, "never unlocked twice");
            Progress.RecordResult(save, "a", Difficulty.Easy, true);
            Progress.RecordResult(save, "a", Difficulty.Normal, true);
            Assert.AreEqual("allA", Achievements.Evaluate(save, list)[0].id);
        }

        [Test]
        public void ShippedListParses()
        {
            Assert.IsNotNull(Achievements.All);
            foreach (var a in Achievements.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(a.id));
                Assert.IsTrue(Loc.Parse(Resources.Load<TextAsset>("Localization/vi").text).ContainsKey(a.titleKey), a.titleKey);
            }
        }
    }

    public class LocTests
    {
        [Test]
        public void ParsesKeysCommentsAndEscapes()
        {
            var t = Loc.Parse("# comment\na = One\n b=Two = three \nc = x\\ny\n\nbad line");
            Assert.AreEqual("One", t["a"]);
            Assert.AreEqual("Two = three", t["b"]);
            Assert.AreEqual("x\ny", t["c"]);
            Assert.AreEqual(3, t.Count);
        }

        [Test]
        public void BothLanguagesHaveTheSameKeys()
        {
            var vi = Loc.Parse(Resources.Load<TextAsset>("Localization/vi").text);
            var en = Loc.Parse(Resources.Load<TextAsset>("Localization/en").text);
            foreach (var k in vi.Keys) Assert.IsTrue(en.ContainsKey(k), "en.txt is missing " + k);
            foreach (var k in en.Keys) Assert.IsTrue(vi.ContainsKey(k), "vi.txt is missing " + k);
        }

        [Test]
        public void GenderVariantsAndNameAreApplied()
        {
            Loc.Load(Language.Vietnamese);
            Loc.PlayerName = "Tí";
            Loc.PlayerGender = Gender.Girl;
            StringAssert.Contains("bạn gái", Loc.Get("mg.placeholder.story.2"));
            StringAssert.Contains("Tí", Loc.Get("mg.placeholder.story.1"));
            Loc.PlayerGender = Gender.Boy;
            StringAssert.Contains("bạn trai", Loc.Get("mg.placeholder.story.2"));
            Assert.AreEqual("#no.such.key", Loc.Get("no.such.key"));
        }

        [Test]
        public void EveryMiniGameHasItsTexts()
        {
            foreach (var lang in new[] { Language.Vietnamese, Language.English })
            {
                Loc.Load(lang);
                foreach (var def in MiniGameCatalog.All)
                {
                    Assert.IsTrue(Loc.Has(def.titleKey), def.id + " title");
                    Assert.IsTrue(Loc.Has(def.instructionsKey), def.id + " instructions");
                    var keys = new System.Collections.Generic.List<string>(def.storyKeys);
                    keys.AddRange(def.winKeys);
                    keys.AddRange(def.loseKeys);
                    keys.AddRange(def.completeKeys);
                    foreach (var k in keys)
                        Assert.IsTrue(Loc.Has(k) || (Loc.Has(k + "@boy") && Loc.Has(k + "@girl")), def.id + " " + k);
                }
            }
        }
    }
}
