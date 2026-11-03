using System.Collections.Generic;
using UnityEngine;

namespace ChildGame.MiniGames.Caro
{
    /// <summary>
    /// Five-in-a-row rules (MG02): a square board, one mark per cell, five or more in a row in any of the four
    /// directions wins (owner 2026-10-07: 15×15, no "blocked at both ends" rule). A full board without a winner is a draw.
    /// </summary>
    public sealed class CaroBoard
    {
        public const int Empty = 0;
        public const int PlayerMark = 1;
        public const int AiMark = 2;
        public const int WinLength = 5;

        public static readonly Vector2Int[] Directions =
        {
            new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, -1),
        };

        private readonly int[] cells;

        public int Size { get; }
        public int Count { get; private set; }
        public bool Full => Count == Size * Size;

        public CaroBoard(int size = 15)
        {
            Size = size;
            cells = new int[size * size];
        }

        /// <summary>An independent copy (the AI thinks on a background thread).</summary>
        public CaroBoard Clone()
        {
            var copy = new CaroBoard(Size) { Count = Count };
            System.Array.Copy(cells, copy.cells, cells.Length);
            return copy;
        }

        public static int Other(int mark) => mark == PlayerMark ? AiMark : PlayerMark;

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;

        public int this[int x, int y] => InBounds(x, y) ? cells[y * Size + x] : -1;

        /// <summary>Places a mark; false if the cell is occupied or outside the board.</summary>
        public bool Place(int x, int y, int mark)
        {
            if (!InBounds(x, y) || cells[y * Size + x] != Empty) return false;
            cells[y * Size + x] = mark;
            Count++;
            return true;
        }

        /// <summary>Removes a mark (used by the AI's look-ahead).</summary>
        public void Clear(int x, int y)
        {
            if (!InBounds(x, y) || cells[y * Size + x] == Empty) return;
            cells[y * Size + x] = Empty;
            Count--;
        }

        /// <summary>The cells of a winning line through (x, y), or null if the mark there has not won.</summary>
        public List<Vector2Int> WinningLine(int x, int y)
        {
            var mark = this[x, y];
            if (mark != PlayerMark && mark != AiMark) return null;
            foreach (var d in Directions)
            {
                var line = new List<Vector2Int> { new Vector2Int(x, y) };
                for (var s = 1; this[x + d.x * s, y + d.y * s] == mark; s++) line.Add(new Vector2Int(x + d.x * s, y + d.y * s));
                for (var s = 1; this[x - d.x * s, y - d.y * s] == mark; s++) line.Insert(0, new Vector2Int(x - d.x * s, y - d.y * s));
                if (line.Count >= WinLength) return line;
            }
            return null;
        }

        public bool IsWin(int x, int y) => WinningLine(x, y) != null;
    }
}
