using System.Collections.Generic;
using ChildGame.MiniGames.OAnQuan;
using NUnit.Framework;

namespace ChildGame.Tests
{
    /// <summary>
    /// Plays thousands of random games and replays every event on a shadow board, checking each capture against the rules
    /// (owner report 2026-10-09: "after two captures the AI took everything").
    /// </summary>
    public class OAnQuanFuzzTests
    {
        [Test]
        public void EveryCaptureFollowsTheRules()
        {
            var rng = new System.Random(1234);
            int empty = 0, stalled = 0, bankrupt = 0, turns = 0;
            for (var game = 0; game < 3000; game++)
            {
                var r = OAnQuanRules.NewGame();
                var side = game % 2;
                var turn = 0;
                for (; turn < 1000 && !r.GameOver; turn++, side = 1 - side)
                {
                    if (!r.PrepareTurn(side)) break;
                    var moves = r.Moves(side);
                    var (space, dir) = moves[rng.Next(moves.Count)];
                    var before = r.Clone();
                    var events = new List<OAnQuanRules.Event>();
                    Assert.IsTrue(r.Play(side, space, dir, events));
                    Check(before, side, dir, events, r);
                }
                Assert.IsTrue(r.GameOver, "every game ends");
                turns += turn;
                if (r.bankruptSide >= 0) bankrupt++;
                else if (r.stalled) stalled++;
                else empty++;
            }
            // How games end under the owner's rule (design §1), for the release notes.
            UnityEngine.Debug.Log($"[OAQ-FUZZ] board emptied {empty}, stalled {stalled}, bankrupt {bankrupt}, average {turns / 3000f:0.0} turns");
        }

        private static void Check(OAnQuanRules board, int side, int dir, List<OAnQuanRules.Event> events, OAnQuanRules after)
        {
            var dan = (int[])board.dan.Clone();
            var quan = (bool[])board.quan.Clone();
            bool Empty(int i) => dan[i] == 0 && !quan[i];
            var lastCapture = -1;
            var ended = false;
            foreach (var e in events)
            {
                switch (e.kind)
                {
                    case OAnQuanRules.EventKind.Pick:
                        dan[e.space] = 0;
                        lastCapture = -1;
                        break;
                    case OAnQuanRules.EventKind.Drop:
                        dan[e.space]++;
                        lastCapture = -1;
                        break;
                    case OAnQuanRules.EventKind.EndTurn:
                        if (lastCapture >= 0)
                        {
                            // A chain stops only at "pieces" or "two empties".
                            var n3 = OAnQuanRules.Next(lastCapture, dir);
                            Assert.IsFalse(Empty(n3) && !Empty(OAnQuanRules.Next(n3, dir)), "the chain stopped too early");
                        }
                        ended = true;
                        break;
                    case OAnQuanRules.EventKind.Capture:
                        if (e.slap < 0)
                        {
                            // The stall-stop sweep: only after the move, only when the game was called, only a side's own row.
                            Assert.IsTrue(ended, "sweep only after the move");
                            Assert.IsTrue(after.stalled, "sweep only when the game stalled");
                            Assert.GreaterOrEqual(OAnQuanRules.SideOf(e.space), 0, "never sweeps a quan space");
                        }
                        else
                        {
                            Assert.IsFalse(ended);
                            Assert.IsTrue(Empty(e.slap), "the slapped space is empty");
                            Assert.AreEqual(OAnQuanRules.Next(e.slap, dir), e.space, "takes the space right after the slap");
                            Assert.IsFalse(Empty(e.space), "takes a space with pieces");
                            if (lastCapture >= 0) Assert.AreEqual(OAnQuanRules.Next(lastCapture, dir), e.slap, "a chain continues from the last capture");
                            lastCapture = e.space;
                        }
                        Assert.AreEqual(dan[e.space], e.dan);
                        dan[e.space] = 0;
                        quan[e.space] = false;
                        break;
                }
            }
            CollectionAssert.AreEqual(dan, after.dan);
            CollectionAssert.AreEqual(quan, after.quan);
        }
    }
}
