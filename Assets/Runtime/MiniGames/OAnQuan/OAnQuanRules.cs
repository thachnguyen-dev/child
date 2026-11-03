using System;
using System.Collections.Generic;

namespace ChildGame.MiniGames.OAnQuan
{
    /// <summary>
    /// Ô ăn quan rules (MG05, owner design + answers 2026-10-07). The 12 spaces form a ring:
    /// <code>
    ///        11 10  9  8  7        ← AI side (side 1)
    ///   0                    6     ← quan spaces
    ///         1  2  3  4  5        ← player side (side 0)
    /// </code>
    /// Direction +1 runs 1→5→6→7…; -1 runs the other way.
    /// <list type="bullet">
    /// <item>Pick up a dân space on your side and sow one piece per space in the chosen direction.</item>
    /// <item>After the last piece: next space has dân pieces → pick them up and keep sowing; next space is a quan space
    /// with pieces → the turn ends (pieces are never taken from quan spaces); next empty and the one after has pieces →
    /// capture them, and keep capturing while "empty, then pieces" repeats; two empties in a row → the turn ends.</item>
    /// <item>A quan is only captured through "empty, then pieces" (owner option b). 1 quan = 10 dân.</item>
    /// <item>No dân on your side at your turn: buy 5 back from your captured dân (one per space); if you can't, you lose.</item>
    /// <item>The game ends when every piece on the board has been captured (owner 2026-10-09: design §1, not "when both
    /// quan are taken"). Safety stops (⚠ placeholders, so a game can never run forever): after <see cref="StallTurns"/>
    /// turns in a row without a capture, or <see cref="MaxTurns"/> turns in all (pieces can circle for ever through
    /// buy-backs), the game is called and each side collects the dân left on its own row.</item>
    /// </list>
    /// </summary>
    public sealed class OAnQuanRules
    {
        public const int Spaces = 12;
        public const int QuanLeft = 0, QuanRight = 6;
        public const int QuanValue = 10;
        public const int StartDan = 5;
        private const int MaxSteps = 600;
        /// <summary>⚠ Placeholder: turns in a row without any capture before the game is called (see the class notes).</summary>
        public const int StallTurns = 12;
        /// <summary>⚠ Placeholder: the most turns a game can last (both sides together) before it is called.</summary>
        public const int MaxTurns = 200;

        public enum EventKind { Pick, Drop, Capture, Buy, EndTurn }

        public struct Event
        {
            public EventKind kind;
            public int space;
            public int dan;
            public bool quan;
            /// <summary>Capture: the empty space slapped before taking (-1 for the end-of-game sweep).</summary>
            public int slap;
        }

        public readonly int[] dan = new int[Spaces];
        public readonly bool[] quan = new bool[Spaces];
        public readonly int[] capturedDan = new int[2];
        public readonly int[] capturedQuan = new int[2];

        /// <summary>Set when a side had no dân to move and could not buy: that side lost.</summary>
        public int bankruptSide = -1;

        /// <summary>Turns played since the last capture (for the stall stop).</summary>
        public int turnsSinceCapture;

        /// <summary>Turns played in the game (both sides).</summary>
        public int turnsPlayed;

        /// <summary>Set when the game was called by a safety stop (no captures for a long time, or too many turns).</summary>
        public bool stalled;

        public static OAnQuanRules NewGame()
        {
            var r = new OAnQuanRules();
            for (var i = 0; i < Spaces; i++)
                if (!IsQuanSpace(i)) r.dan[i] = StartDan;
            r.quan[QuanLeft] = r.quan[QuanRight] = true;
            return r;
        }

        public OAnQuanRules Clone()
        {
            var c = new OAnQuanRules { bankruptSide = bankruptSide, turnsSinceCapture = turnsSinceCapture, turnsPlayed = turnsPlayed, stalled = stalled };
            Array.Copy(dan, c.dan, Spaces);
            Array.Copy(quan, c.quan, Spaces);
            Array.Copy(capturedDan, c.capturedDan, 2);
            Array.Copy(capturedQuan, c.capturedQuan, 2);
            return c;
        }

        public static bool IsQuanSpace(int i) => i == QuanLeft || i == QuanRight;
        public static int SideOf(int i) => i >= 1 && i <= 5 ? 0 : i >= 7 && i <= 11 ? 1 : -1;
        public static int Next(int i, int dir) => (i + dir + Spaces) % Spaces;

        public bool Empty(int i) => dan[i] == 0 && !quan[i];
        public int Score(int side) => capturedDan[side] + capturedQuan[side] * QuanValue;
        public bool GameOver => BoardEmpty || stalled || bankruptSide >= 0;

        public bool BoardEmpty
        {
            get
            {
                for (var i = 0; i < Spaces; i++)
                    if (!Empty(i)) return false;
                return true;
            }
        }

        /// <summary>The winning side, -1 for a draw. Only meaningful once <see cref="GameOver"/>.</summary>
        public int Winner
        {
            get
            {
                if (bankruptSide >= 0) return 1 - bankruptSide;
                var a = Score(0);
                var b = Score(1);
                return a > b ? 0 : b > a ? 1 : -1;
            }
        }

        public bool SideEmpty(int side)
        {
            for (var i = 0; i < Spaces; i++)
                if (SideOf(i) == side && dan[i] > 0) return false;
            return true;
        }

        /// <summary>Legal moves for a side: (space, direction).</summary>
        public List<(int space, int dir)> Moves(int side)
        {
            var list = new List<(int, int)>();
            for (var i = 0; i < Spaces; i++)
                if (SideOf(i) == side && dan[i] > 0)
                {
                    list.Add((i, 1));
                    list.Add((i, -1));
                }
            return list;
        }

        /// <summary>
        /// Call at the start of a side's turn. If its spaces are empty it buys dân back (events added); if it cannot,
        /// it loses (<see cref="bankruptSide"/>). Returns false when the side cannot play.
        /// </summary>
        public bool PrepareTurn(int side, List<Event> events = null)
        {
            if (GameOver) return false;
            if (!SideEmpty(side)) return true;
            if (capturedDan[side] < 5)
            {
                bankruptSide = side;
                return false;
            }
            capturedDan[side] -= 5;
            for (var i = 0; i < Spaces; i++)
                if (SideOf(i) == side)
                {
                    dan[i] = 1;
                    events?.Add(new Event { kind = EventKind.Buy, space = i, dan = 1 });
                }
            return true;
        }

        /// <summary>Plays a move for <paramref name="side"/>. Returns false if illegal.</summary>
        public bool Play(int side, int space, int dir, List<Event> events = null)
        {
            if (GameOver || SideOf(space) != side || dan[space] == 0 || (dir != 1 && dir != -1)) return false;
            var scoreBefore = Score(side);
            var pos = space;
            var hand = dan[space];
            dan[space] = 0;
            events?.Add(new Event { kind = EventKind.Pick, space = space, dan = hand });
            for (var steps = 0; steps < MaxSteps; steps++)
            {
                while (hand > 0)
                {
                    pos = Next(pos, dir);
                    dan[pos]++;
                    hand--;
                    events?.Add(new Event { kind = EventKind.Drop, space = pos, dan = 1 });
                }
                var n1 = Next(pos, dir);
                if (!Empty(n1))
                {
                    if (IsQuanSpace(n1)) break; // never pick up from a quan space
                    hand = dan[n1];
                    dan[n1] = 0;
                    pos = n1;
                    events?.Add(new Event { kind = EventKind.Pick, space = n1, dan = hand });
                    continue;
                }
                var n2 = Next(n1, dir);
                if (Empty(n2)) break;
                // Capture, and keep going while "empty, then pieces" repeats.
                while (true)
                {
                    Capture(side, n2, events, n1);
                    var n3 = Next(n2, dir);
                    if (!Empty(n3)) break;
                    var n4 = Next(n3, dir);
                    if (Empty(n4)) break;
                    n1 = n3;
                    n2 = n4;
                }
                break;
            }
            events?.Add(new Event { kind = EventKind.EndTurn });
            turnsSinceCapture = Score(side) > scoreBefore ? 0 : turnsSinceCapture + 1;
            turnsPlayed++;
            if (!BoardEmpty && (turnsSinceCapture >= StallTurns || turnsPlayed >= MaxTurns))
            {
                stalled = true;
                CollectRemaining(events);
            }
            return true;
        }

        private void Capture(int side, int space, List<Event> events, int slap = -1)
        {
            var e = new Event { kind = EventKind.Capture, space = space, dan = dan[space], quan = quan[space], slap = slap };
            capturedDan[side] += dan[space];
            if (quan[space]) capturedQuan[side]++;
            dan[space] = 0;
            quan[space] = false;
            events?.Add(e);
        }

        /// <summary>Stall stop: each side takes the dân left on its own row (pieces in quan spaces stay uncounted).</summary>
        private void CollectRemaining(List<Event> events)
        {
            for (var i = 0; i < Spaces; i++)
            {
                var side = SideOf(i);
                if (side < 0 || dan[i] == 0) continue;
                Capture(side, i, events);
            }
        }
    }
}
