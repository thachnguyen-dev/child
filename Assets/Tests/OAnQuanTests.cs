using System.Collections.Generic;
using ChildGame.MiniGames.OAnQuan;
using NUnit.Framework;

namespace ChildGame.Tests
{
    public class OAnQuanRuleTests
    {
        private static OAnQuanRules Empty()
        {
            var s = new OAnQuanRules();
            s.quan[OAnQuanRules.QuanLeft] = s.quan[OAnQuanRules.QuanRight] = true;
            return s;
        }

        [Test]
        public void NewGameLayout()
        {
            var s = OAnQuanRules.NewGame();
            Assert.AreEqual(0, s.dan[0]);
            Assert.AreEqual(5, s.dan[1]);
            Assert.AreEqual(5, s.dan[11]);
            Assert.IsTrue(s.quan[0] && s.quan[6]);
            Assert.AreEqual(10, s.Moves(0).Count);
        }

        [Test]
        public void SowingThenPickingUpTheNextPile()
        {
            var s = Empty();
            s.dan[1] = 2;  // sow into 2, 3; next (4) has pieces → pick up and continue
            s.dan[4] = 1;  // sow into 5; next (6) is a quan space with a quan → stop
            s.dan[9] = 3;
            Assert.IsTrue(s.Play(0, 1, 1));
            Assert.AreEqual(new[] { 1, 1, 0, 1 }, new[] { s.dan[2], s.dan[3], s.dan[4], s.dan[5] });
            Assert.IsTrue(s.quan[6], "a quan space is never picked up");
            Assert.AreEqual(0, s.Score(0));
        }

        [Test]
        public void EmptyThenPiecesCaptures()
        {
            var s = Empty();
            s.dan[2] = 1; // sow into 3; 4 empty; 5 has 4 → capture
            s.dan[5] = 4;
            s.dan[9] = 1;
            s.Play(0, 2, 1);
            Assert.AreEqual(4, s.capturedDan[0]);
            Assert.AreEqual(0, s.dan[5]);
        }

        [Test]
        public void CapturesChain()
        {
            var s = Empty();
            s.dan[1] = 1; // sow into 2; 3 empty; capture 4; 5 empty; capture 6 (quan + 2 dân); 7 has pieces → stop
            s.dan[4] = 3;
            s.dan[6] = 2;
            s.dan[7] = 1;
            s.Play(0, 1, 1);
            Assert.AreEqual(5, s.capturedDan[0]);
            Assert.AreEqual(1, s.capturedQuan[0]);
            Assert.AreEqual(15, s.Score(0));
            Assert.IsFalse(s.quan[6]);
            Assert.AreEqual(1, s.dan[7], "the chain stops at pieces");
        }

        [Test]
        public void ChainStopsAtTwoEmpties()
        {
            var s = Empty();
            s.dan[1] = 1; // sow into 2; 3 empty; capture 4; then 5 and 6 are both empty → the chain stops
            s.dan[4] = 2;
            s.quan[6] = false; // make 5 and 6 empty: two empties after the capture → stop
            s.dan[8] = 3;
            s.Play(0, 1, 1);
            Assert.AreEqual(2, s.capturedDan[0]);
            Assert.AreEqual(3, s.dan[8]);
        }

        [Test]
        public void TwoEmptiesEndTheTurn()
        {
            var s = Empty();
            s.dan[3] = 1; // sow into 4; 5 empty; 6 is the quan space: a quan there means "pieces" → capture it
            s.Play(0, 3, 1);
            Assert.AreEqual(1, s.capturedQuan[0], "a quan is captured through empty-then-pieces");

            var t = Empty();
            t.dan[2] = 1;  // sow into 3; 4 empty, 5 empty → turn ends
            t.dan[9] = 2;
            t.Play(0, 2, 1);
            Assert.AreEqual(0, t.Score(0));
        }

        [Test]
        public void LastPieceIntoAQuanSpaceJustEnds()
        {
            var s = Empty();
            s.dan[5] = 1; // the last piece lands in quan space 6: no capture (option b); 7 and 8 are empty → the turn ends
            s.dan[7] = 0;
            s.dan[8] = 0;
            s.dan[11] = 1;
            s.Play(0, 5, 1);
            Assert.IsTrue(s.quan[6]);
            Assert.AreEqual(1, s.dan[6]);
        }

        [Test]
        public void BuyBackOrLose()
        {
            var s = Empty();
            s.dan[9] = 2;
            s.capturedDan[0] = 7;
            var events = new List<OAnQuanRules.Event>();
            Assert.IsTrue(s.PrepareTurn(0, events));
            Assert.AreEqual(2, s.capturedDan[0]);
            Assert.AreEqual(5, events.Count);
            for (var i = 1; i <= 5; i++) Assert.AreEqual(1, s.dan[i]);

            var t = Empty();
            t.dan[9] = 2;
            t.capturedDan[0] = 4;
            Assert.IsFalse(t.PrepareTurn(0));
            Assert.IsTrue(t.GameOver);
            Assert.AreEqual(1, t.Winner);
        }

        [Test]
        public void TakingBothQuanDoesNotEndTheGame()
        {
            // Owner 2026-10-09 (design §1): the game goes on until every piece on the board is captured.
            var t = Empty();
            t.quan[0] = false;
            t.dan[3] = 1; // sow into 4; 5 empty; 6 quan → capture; 7 and 8 empty → chain stops; both quan gone
            t.dan[1] = 2;
            t.dan[9] = 3;
            t.Play(0, 3, 1);
            Assert.IsFalse(t.quan[6]);
            Assert.IsFalse(t.GameOver, "pieces are still on the board");
            Assert.AreEqual(10, t.Score(0), "only the quan so far; nothing is swept");
            Assert.AreEqual(2, t.dan[1]);
            Assert.AreEqual(3, t.dan[9]);
        }

        [Test]
        public void GameEndsWhenTheBoardIsEmpty()
        {
            var t = Empty();
            t.quan[0] = false;
            t.dan[3] = 1; // sow into 4; 5 empty; capture 6 (quan); 7 empty, 8 has 2 → capture; 9, 10 empty → stop
            t.dan[8] = 2;
            t.Play(0, 3, 1);
            Assert.AreEqual(1, t.dan[4], "the sown piece is still on the board");
            Assert.IsFalse(t.GameOver);
            t.dan[4] = 0; // once that last piece is gone too, the board is empty
            Assert.IsTrue(t.GameOver);
        }

        [Test]
        public void AStalledGameIsCalledAndSidesCollect()
        {
            var t = Empty();
            t.quan[0] = t.quan[6] = false;
            t.turnsSinceCapture = OAnQuanRules.StallTurns - 1;
            t.dan[1] = 1; // sow into 2 (now 3 pieces); 3 and 4 empty → the turn ends without a capture
            t.dan[2] = 2;
            t.dan[9] = 4;
            t.Play(0, 1, 1);
            Assert.IsTrue(t.stalled);
            Assert.IsTrue(t.GameOver);
            Assert.AreEqual(3, t.Score(0), "the 3 pieces at space 2");
            Assert.AreEqual(4, t.Score(1));
        }

        [Test]
        public void IllegalMovesAreRejected()
        {
            var s = OAnQuanRules.NewGame();
            Assert.IsFalse(s.Play(0, 7, 1), "not your side");
            Assert.IsFalse(s.Play(0, 0, 1), "quan space");
            Assert.IsFalse(s.Play(0, 1, 0), "no direction");
        }
    }

    public class OAnQuanAiTests
    {
        private static int PlayGame(Difficulty side0, Difficulty side1, int seed)
        {
            var rng = new System.Random(seed);
            var s = OAnQuanRules.NewGame();
            var side = seed % 2;
            for (var turn = 0; turn < 400 && !s.GameOver; turn++)
            {
                if (!s.PrepareTurn(side)) break;
                var m = OAnQuanAi.Choose(s, side, side == 0 ? side0 : side1, rng);
                Assert.IsTrue(s.Play(side, m.space, m.dir), "AI move must be legal");
                side = 1 - side;
            }
            return s.GameOver ? s.Winner : -1;
        }

        [Test]
        public void HarderLevelsWinMore()
        {
            int hard = 0, normal = 0, normalVsEasy = 0, easy = 0;
            for (var seed = 0; seed < 12; seed++)
            {
                var w = PlayGame(Difficulty.Hard, Difficulty.Normal, seed);
                if (w == 0) hard++;
                else if (w == 1) normal++;
                var v = PlayGame(Difficulty.Normal, Difficulty.Easy, seed);
                if (v == 0) normalVsEasy++;
                else if (v == 1) easy++;
            }
            // Same match with the sides swapped: the AI plays side 1 in the real game.
            int hard1 = 0, normal0 = 0;
            for (var seed = 0; seed < 12; seed++)
            {
                var w = PlayGame(Difficulty.Normal, Difficulty.Hard, seed);
                if (w == 1) hard1++;
                else if (w == 0) normal0++;
            }
            UnityEngine.Debug.Log($"[CHILD-TEST] O an quan swapped: Hard(side 1) {hard1}-{normal0} Normal(side 0)");
            Assert.Greater(hard1, normal0, "Hard must also win as side 1");
            int first = 0, second = 0, draws = 0;
            for (var seed = 0; seed < 12; seed++)
            {
                var w = PlayGame(Difficulty.Hard, Difficulty.Hard, seed);
                var starter = seed % 2;
                if (w == starter) first++;
                else if (w == 1 - starter) second++;
                else draws++;
            }
            UnityEngine.Debug.Log($"[CHILD-TEST] O an quan Hard vs Hard: starter {first}, second {second}, draws {draws}");
            UnityEngine.Debug.Log($"[CHILD-TEST] O an quan: Hard {hard}-{normal} Normal; Normal {normalVsEasy}-{easy} Easy");
            Assert.Greater(hard, normal);
            Assert.Greater(normalVsEasy, easy);
        }
    }
}
