using System;
using System.Collections.Generic;

namespace ChildGame.MiniGames.OAnQuan
{
    /// <summary>
    /// The Ô ăn quan opponent. Easy: often random, otherwise greedy. Normal: best immediate gain. Hard: alpha-beta
    /// search 4 plies deep on score difference (plus pieces kept on its side).
    /// </summary>
    public static class OAnQuanAi
    {
        public static (int space, int dir) Choose(OAnQuanRules state, int side, Difficulty difficulty, Random rng)
        {
            var moves = state.Moves(side);
            if (moves.Count == 0) return (-1, 1);
            switch (difficulty)
            {
                case Difficulty.Easy:
                    return rng.NextDouble() < 0.6 ? moves[rng.Next(moves.Count)] : Greedy(state, side, moves, rng);
                case Difficulty.Normal:
                    return Best(state, side, moves, rng, 1);
                default:
                    return Best(state, side, moves, rng, 4);
            }
        }

        /// <summary>The move with the best search value, looking <paramref name="replies"/> further plies ahead.</summary>
        private static (int, int) Best(OAnQuanRules state, int side, List<(int space, int dir)> moves, Random rng, int replies)
        {
            var best = moves[0];
            var bestValue = double.MinValue;
            Shuffle(moves, rng);
            foreach (var m in moves)
            {
                var s = state.Clone();
                s.Play(side, m.space, m.dir);
                var v = -Search(s, 1 - side, replies, double.MinValue / 2, double.MaxValue / 2);
                if (v > bestValue)
                {
                    bestValue = v;
                    best = m;
                }
            }
            return best;
        }

        private static (int, int) Greedy(OAnQuanRules state, int side, List<(int space, int dir)> moves, Random rng)
        {
            Shuffle(moves, rng);
            var best = moves[0];
            var bestGain = int.MinValue;
            foreach (var m in moves)
            {
                var s = state.Clone();
                s.Play(side, m.space, m.dir);
                var gain = s.Score(side) - state.Score(side);
                if (gain > bestGain)
                {
                    bestGain = gain;
                    best = m;
                }
            }
            return best;
        }

        /// <summary>Negamax with alpha-beta: value of the position for <paramref name="side"/> (to move).</summary>
        private static double Search(OAnQuanRules state, int side, int depth, double alpha, double beta)
        {
            if (state.GameOver || depth == 0) return Evaluate(state, side);
            var probe = state.Clone();
            if (!probe.PrepareTurn(side)) return Evaluate(probe, side);
            var best = double.MinValue / 2;
            foreach (var m in probe.Moves(side))
            {
                var s = probe.Clone();
                s.Play(side, m.space, m.dir);
                var v = -Search(s, 1 - side, depth - 1, -beta, -alpha);
                if (v > best) best = v;
                if (v > alpha) alpha = v;
                if (alpha >= beta) break;
            }
            return best;
        }

        private static double Evaluate(OAnQuanRules s, int side)
        {
            if (s.GameOver)
            {
                var w = s.Winner;
                return w == side ? 1000 : w == 1 - side ? -1000 : 0;
            }
            double own = 0, theirs = 0;
            for (var i = 0; i < OAnQuanRules.Spaces; i++)
            {
                if (OAnQuanRules.SideOf(i) == side) own += s.dan[i];
                else if (OAnQuanRules.SideOf(i) == 1 - side) theirs += s.dan[i];
            }
            return s.Score(side) - s.Score(1 - side) + 0.3 * (own - theirs);
        }

        private static void Shuffle<T>(List<T> list, Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
