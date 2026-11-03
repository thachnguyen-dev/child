using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChildGame.MiniGames.Caro
{
    /// <summary>
    /// The caro opponent (MG02). Difficulty only changes how well it chooses (owner rule), never the rules:
    /// <list type="bullet">
    /// <item>Easy: rough window scoring, picks loosely among many cells, sometimes misses wins and blocks.</item>
    /// <item>Normal: recognises caro shapes (fours, open threes, double threats), plays with a little variety.</item>
    /// <item>Hard: shape scoring plus a search for forced wins (a chain of fours the player must keep blocking), and
    /// one reply of look-ahead.</item>
    /// </list>
    /// </summary>
    public static class CaroAi
    {
        public const int FiveScore = 1_000_000;

        private struct Candidate
        {
            public Vector2Int cell;
            public float score;
            public int attack;
            public int defense;
        }

        public static Vector2Int Choose(CaroBoard board, int me, Difficulty difficulty, System.Random rng)
        {
            var opp = CaroBoard.Other(me);
            var c = board.Size / 2;
            if (board.Count == 0)
                return difficulty == Difficulty.Easy ? new Vector2Int(c + rng.Next(-2, 3), c + rng.Next(-2, 3)) : new Vector2Int(c, c);

            var easy = difficulty == Difficulty.Easy;
            var list = Score(board, me, difficulty);
            list.Sort((a, b) => b.score.CompareTo(a.score));

            // A win, then blocking a five, come first. Easy can miss either.
            foreach (var x in list)
                if (x.attack >= FiveScore && (!easy || rng.NextDouble() < 0.6)) return x.cell;
            foreach (var x in list)
                if (x.defense >= FiveScore && (!easy || rng.NextDouble() < 0.5)) return x.cell;

            switch (difficulty)
            {
                case Difficulty.Easy: return PickWeighted(list, Math.Min(10, list.Count), 0.75, rng);
                case Difficulty.Normal: return PickWeighted(list, Math.Min(3, list.Count), 0.3, rng);
            }

            // Hard: a forced win by continuous fours.
            if (FindForcedWin(board, me, 8, out var winMove)) return winMove;
            return LookAhead(board, me, opp, list);
        }

        /// <summary>Of the best candidates, prefer the one that leaves the player the weakest best threat.</summary>
        private static Vector2Int LookAhead(CaroBoard board, int me, int opp, List<Candidate> list)
        {
            var best = list[0].cell;
            var bestValue = float.MinValue;
            var n = Math.Min(8, list.Count);
            for (var i = 0; i < n; i++)
            {
                var cand = list[i];
                board.Place(cand.cell.x, cand.cell.y, me);
                var reply = 0;
                foreach (var cell in Nearby(board))
                    reply = Math.Max(reply, Shape(board, cell.x, cell.y, opp));
                // A player forced win after this move is as bad as it gets.
                if (reply < FiveScore && FindForcedWin(board, opp, 4, out _)) reply = FiveScore / 2;
                board.Clear(cand.cell.x, cand.cell.y);
                var value = cand.score - 0.8f * reply;
                if (value > bestValue)
                {
                    bestValue = value;
                    best = cand.cell;
                }
            }
            return best;
        }

        /// <summary>
        /// Victory by continuous fours: <paramref name="me"/> keeps making fours, each one has a single blocking cell the
        /// opponent must take, until a five or an unstoppable shape (open four, double four) appears.
        /// </summary>
        public static bool FindForcedWin(CaroBoard board, int me, int depth, out Vector2Int move)
        {
            move = default;
            var opp = CaroBoard.Other(me);
            foreach (var cell in Nearby(board))
                if (Shape(board, cell.x, cell.y, me) >= FiveScore)
                {
                    move = cell;
                    return true;
                }
            if (depth <= 0) return false;
            foreach (var cell in Nearby(board))
            {
                var info = Classify(board, cell.x, cell.y, me);
                if (info.fours == 0 && info.openFours == 0) continue;
                board.Place(cell.x, cell.y, me);
                var won = false;
                if (info.openFours > 0 || info.fours >= 2) won = !OpponentHasFive(board, opp);
                else if (FiveCells(board, me, out var block) == 1)
                {
                    // The opponent must block; if the block makes their own five, this line fails.
                    if (Shape(board, block.x, block.y, opp) < FiveScore)
                    {
                        board.Place(block.x, block.y, opp);
                        won = FindForcedWin(board, me, depth - 1, out _);
                        board.Clear(block.x, block.y);
                    }
                }
                board.Clear(cell.x, cell.y);
                if (won)
                {
                    move = cell;
                    return true;
                }
            }
            return false;
        }

        private static bool OpponentHasFive(CaroBoard board, int opp)
        {
            foreach (var cell in Nearby(board))
                if (Shape(board, cell.x, cell.y, opp) >= FiveScore) return true;
            return false;
        }

        /// <summary>Number of cells where <paramref name="mark"/> would complete five (one of them in <paramref name="cell"/>).</summary>
        private static int FiveCells(CaroBoard board, int mark, out Vector2Int cell)
        {
            cell = default;
            var n = 0;
            foreach (var c in Nearby(board))
                if (Shape(board, c.x, c.y, mark) >= FiveScore)
                {
                    cell = c;
                    n++;
                }
            return n;
        }

        private static Vector2Int PickWeighted(List<Candidate> list, int top, double falloff, System.Random rng)
        {
            // Rank-based: rank 0 has weight 1, rank k has weight falloff^k.
            var total = 0.0;
            for (var i = 0; i < top; i++) total += Math.Pow(falloff, i);
            var roll = rng.NextDouble() * total;
            for (var i = 0; i < top; i++)
            {
                roll -= Math.Pow(falloff, i);
                if (roll <= 0) return list[i].cell;
            }
            return list[0].cell;
        }

        private static List<Candidate> Score(CaroBoard board, int me, Difficulty difficulty)
        {
            var opp = CaroBoard.Other(me);
            var easy = difficulty == Difficulty.Easy;
            var defenseWeight = easy ? 0.4f : difficulty == Difficulty.Normal ? 0.8f : 0.9f;
            var list = new List<Candidate>();
            foreach (var cell in Nearby(board))
            {
                var attack = easy ? Windows(board, cell.x, cell.y, me) : Shape(board, cell.x, cell.y, me);
                var defense = easy ? Windows(board, cell.x, cell.y, opp) : Shape(board, cell.x, cell.y, opp);
                list.Add(new Candidate { cell = cell, attack = attack, defense = defense, score = attack + defense * defenseWeight });
            }
            return list;
        }

        /// <summary>Empty cells within two of any mark: the only ones worth considering.</summary>
        private static List<Vector2Int> Nearby(CaroBoard board)
        {
            var list = new List<Vector2Int>();
            for (var y = 0; y < board.Size; y++)
            for (var x = 0; x < board.Size; x++)
            {
                if (board[x, y] != CaroBoard.Empty) continue;
                var near = false;
                for (var dy = -2; dy <= 2 && !near; dy++)
                for (var dx = -2; dx <= 2 && !near; dx++)
                {
                    var v = board[x + dx, y + dy];
                    near = v == CaroBoard.PlayerMark || v == CaroBoard.AiMark;
                }
                if (near) list.Add(new Vector2Int(x, y));
            }
            return list;
        }

        // ---- Easy: window scoring ----

        private static readonly int[] WindowWeight = { 0, 1, 12, 150, 3000, FiveScore };

        /// <summary>Sum over every five-cell window through the cell without an opposing mark, by own marks inside.</summary>
        public static int Windows(CaroBoard board, int x, int y, int mark)
        {
            var total = 0;
            foreach (var d in CaroBoard.Directions)
                for (var start = -4; start <= 0; start++)
                {
                    var own = 0;
                    var blocked = false;
                    for (var k = 0; k < 5 && !blocked; k++)
                    {
                        var s = start + k;
                        var v = s == 0 ? mark : board[x + d.x * s, y + d.y * s];
                        if (v == mark) own++;
                        else if (v != CaroBoard.Empty) blocked = true;
                    }
                    if (blocked) continue;
                    if (own == 5) return FiveScore;
                    total += WindowWeight[own];
                }
            return total;
        }

        // ---- Normal / Hard: shape scoring ----

        public struct ShapeInfo
        {
            public bool five;
            public int openFours, fours, openThrees, threes, openTwos, twos;
        }

        /// <summary>The caro shapes <paramref name="mark"/> would make in each direction by playing (x, y).</summary>
        public static ShapeInfo Classify(CaroBoard board, int x, int y, int mark)
        {
            var info = new ShapeInfo();
            var line = new char[11];
            foreach (var d in CaroBoard.Directions)
            {
                for (var s = -5; s <= 5; s++)
                {
                    var v = s == 0 ? mark : board[x + d.x * s, y + d.y * s];
                    line[s + 5] = v == mark ? 'x' : v == CaroBoard.Empty ? '_' : 'o';
                }
                var str = new string(line);
                if (Has(str, "xxxxx")) { info.five = true; continue; }
                if (Has(str, "_xxxx_")) { info.openFours++; continue; }
                if (Has(str, "xxxx_", "_xxxx", "xxx_x", "x_xxx", "xx_xx")) { info.fours++; continue; }
                if (Has(str, "_xxx__", "__xxx_", "_x_xx_", "_xx_x_")) { info.openThrees++; continue; }
                if (Has(str, "xxx__", "__xxx", "_xxx_", "x_xx_", "_x_xx", "xx_x_", "_xx_x", "x__xx", "xx__x", "x_x_x")) { info.threes++; continue; }
                if (Has(str, "__xx__", "_x_x_", "_x__x_")) { info.openTwos++; continue; }
                if (Has(str, "xx___", "___xx", "_xx__", "__xx_", "x_x__", "__x_x")) info.twos++;
            }
            return info;
        }

        /// <summary>True if any pattern occurs in the line at a position that covers the centre (index 5).</summary>
        private static bool Has(string line, params string[] patterns)
        {
            foreach (var p in patterns)
            {
                var from = 0;
                while (true)
                {
                    var i = line.IndexOf(p, from, StringComparison.Ordinal);
                    if (i < 0) break;
                    if (i <= 5 && i + p.Length > 5 && p[5 - i] == 'x') return true;
                    from = i + 1;
                }
            }
            return false;
        }

        /// <summary>A score for (x, y) from its shapes; unstoppable combinations rank just below a five.</summary>
        public static int Shape(CaroBoard board, int x, int y, int mark)
        {
            var s = Classify(board, x, y, mark);
            if (s.five) return FiveScore;
            if (s.openFours > 0 || s.fours >= 2) return 200_000;
            if (s.fours > 0 && s.openThrees > 0) return 100_000;
            if (s.openThrees >= 2) return 50_000;
            return s.fours * 6_000 + s.openThrees * 4_000 + s.threes * 400 + s.openTwos * 250 + s.twos * 40 + 1;
        }
    }
}
