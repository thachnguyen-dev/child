using ChildGame.MiniGames.Caro;
using NUnit.Framework;
using UnityEngine;

namespace ChildGame.Tests
{
    public class CaroRuleTests
    {
        private static CaroBoard With(int mark, params (int x, int y)[] cells)
        {
            var b = new CaroBoard();
            foreach (var c in cells) b.Place(c.x, c.y, mark);
            return b;
        }

        [Test]
        public void FiveInARowWinsInEveryDirection()
        {
            Assert.IsTrue(With(1, (0, 0), (1, 0), (2, 0), (3, 0), (4, 0)).IsWin(2, 0), "horizontal");
            Assert.IsTrue(With(1, (5, 3), (5, 4), (5, 5), (5, 6), (5, 7)).IsWin(5, 7), "vertical");
            Assert.IsTrue(With(1, (1, 1), (2, 2), (3, 3), (4, 4), (5, 5)).IsWin(3, 3), "diagonal");
            Assert.IsTrue(With(1, (9, 1), (8, 2), (7, 3), (6, 4), (5, 5)).IsWin(9, 1), "anti-diagonal");
        }

        [Test]
        public void FourOrAGapIsNotAWin()
        {
            Assert.IsFalse(With(1, (0, 0), (1, 0), (2, 0), (3, 0)).IsWin(3, 0));
            Assert.IsFalse(With(1, (0, 0), (1, 0), (3, 0), (4, 0), (5, 0)).IsWin(4, 0), "gap breaks the line");
        }

        [Test]
        public void SixInARowWinsAndBlockedEndsStillWin()
        {
            Assert.IsTrue(With(1, (0, 7), (1, 7), (2, 7), (3, 7), (4, 7), (5, 7)).IsWin(5, 7), "overline counts");
            var b = With(1, (2, 0), (3, 0), (4, 0), (5, 0), (6, 0));
            b.Place(1, 0, 2);
            b.Place(7, 0, 2);
            Assert.IsTrue(b.IsWin(4, 0), "no blocked-ends rule");
        }

        [Test]
        public void OccupiedCellsAndFullBoard()
        {
            var b = new CaroBoard(3);
            Assert.IsTrue(b.Place(1, 1, 1));
            Assert.IsFalse(b.Place(1, 1, 2), "occupied");
            Assert.IsFalse(b.Place(3, 0, 2), "outside");
            for (var y = 0; y < 3; y++)
            for (var x = 0; x < 3; x++)
                b.Place(x, y, 2);
            Assert.IsTrue(b.Full);
        }
    }

    public class CaroAiTests
    {
        [Test]
        public void NormalAndHardTakeAWin()
        {
            foreach (var d in new[] { Difficulty.Normal, Difficulty.Hard })
            {
                var b = new CaroBoard();
                for (var x = 3; x < 7; x++) b.Place(x, 7, CaroBoard.AiMark);
                b.Place(2, 7, CaroBoard.PlayerMark);
                b.Place(5, 5, CaroBoard.PlayerMark);
                Assert.AreEqual(new Vector2Int(7, 7), CaroAi.Choose(b, CaroBoard.AiMark, d, new System.Random(1)), d.ToString());
            }
        }

        [Test]
        public void NormalAndHardBlockAFour()
        {
            foreach (var d in new[] { Difficulty.Normal, Difficulty.Hard })
            {
                var b = new CaroBoard();
                for (var y = 4; y < 8; y++) b.Place(7, y, CaroBoard.PlayerMark);
                b.Place(7, 3, CaroBoard.AiMark);
                b.Place(2, 2, CaroBoard.AiMark);
                Assert.AreEqual(new Vector2Int(7, 8), CaroAi.Choose(b, CaroBoard.AiMark, d, new System.Random(2)), d.ToString());
            }
        }

        [Test]
        public void HardBlocksAnOpenThree()
        {
            var b = new CaroBoard();
            b.Place(6, 7, CaroBoard.PlayerMark);
            b.Place(7, 7, CaroBoard.PlayerMark);
            b.Place(8, 7, CaroBoard.PlayerMark);
            b.Place(7, 8, CaroBoard.AiMark);
            b.Place(8, 8, CaroBoard.AiMark);
            var move = CaroAi.Choose(b, CaroBoard.AiMark, Difficulty.Hard, new System.Random(3));
            Assert.That(move == new Vector2Int(5, 7) || move == new Vector2Int(9, 7), $"blocked at {move}");
        }

        [Test]
        public void StrongerLevelsBeatWeakerOnes()
        {
            // Hard (as O) against Easy (as X) over several seeded games: Hard should win clearly more.
            var hardWins = 0;
            var easyWins = 0;
            for (var seed = 0; seed < 20; seed++)
            {
                var rng = new System.Random(seed);
                var b = new CaroBoard();
                var mark = seed % 2 == 0 ? CaroBoard.PlayerMark : CaroBoard.AiMark;
                while (!b.Full)
                {
                    var d = mark == CaroBoard.AiMark ? Difficulty.Hard : Difficulty.Easy;
                    var m = CaroAi.Choose(b, mark, d, rng);
                    Assert.IsTrue(b.Place(m.x, m.y, mark), $"AI chose an illegal cell {m}");
                    if (b.IsWin(m.x, m.y))
                    {
                        if (mark == CaroBoard.AiMark) hardWins++;
                        else easyWins++;
                        break;
                    }
                    mark = CaroBoard.Other(mark);
                }
            }
            Assert.Greater(hardWins, easyWins * 2, $"hard {hardWins} vs easy {easyWins}");
        }
    }

    public class CaroLadderTests
    {
        /// <summary>Plays <paramref name="games"/> seeded games, alternating who starts. Returns (strong wins, weak wins).</summary>
        public static (int strong, int weak) Match(Difficulty strong, Difficulty weak, int games)
        {
            int s = 0, w = 0;
            for (var seed = 0; seed < games; seed++)
            {
                var rng = new System.Random(seed * 31 + 7);
                var b = new CaroBoard();
                var mark = seed % 2 == 0 ? CaroBoard.PlayerMark : CaroBoard.AiMark;
                while (!b.Full)
                {
                    var m = CaroAi.Choose(b, mark, mark == CaroBoard.AiMark ? strong : weak, rng);
                    b.Place(m.x, m.y, mark);
                    if (b.IsWin(m.x, m.y))
                    {
                        if (mark == CaroBoard.AiMark) s++;
                        else w++;
                        break;
                    }
                    mark = CaroBoard.Other(mark);
                }
            }
            return (s, w);
        }

        [Test]
        public void EachLevelIsStrongerThanTheOneBelow()
        {
            var normalVsEasy = Match(Difficulty.Normal, Difficulty.Easy, 20);
            var hardVsNormal = Match(Difficulty.Hard, Difficulty.Normal, 20);
            UnityEngine.Debug.Log($"[CHILD-TEST] Normal vs Easy {normalVsEasy}, Hard vs Normal {hardVsNormal}");
            Assert.Greater(normalVsEasy.strong, normalVsEasy.weak * 2, $"normal vs easy {normalVsEasy}");
            Assert.Greater(hardVsNormal.strong, hardVsNormal.weak, $"hard vs normal {hardVsNormal}");
        }
    }

    public class RockPaperScissorsTests
    {
        [Test]
        public void Rules()
        {
            Assert.AreEqual(1, RockPaperScissors.Compare(RockPaperScissors.Hand.Paper, RockPaperScissors.Hand.Rock));
            Assert.AreEqual(1, RockPaperScissors.Compare(RockPaperScissors.Hand.Scissors, RockPaperScissors.Hand.Paper));
            Assert.AreEqual(1, RockPaperScissors.Compare(RockPaperScissors.Hand.Rock, RockPaperScissors.Hand.Scissors));
            Assert.AreEqual(-1, RockPaperScissors.Compare(RockPaperScissors.Hand.Rock, RockPaperScissors.Hand.Paper));
            Assert.AreEqual(0, RockPaperScissors.Compare(RockPaperScissors.Hand.Scissors, RockPaperScissors.Hand.Scissors));
        }
    }
}
