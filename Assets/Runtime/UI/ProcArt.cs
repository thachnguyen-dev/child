using System.Collections.Generic;
using UnityEngine;

namespace ChildGame.UI
{
    /// <summary>
    /// Temporary art drawn in code (owner 2026-10-07: use self-made assets until the real ones arrive).
    /// Strokes are rasterised with anti-aliasing into white-on-transparent sprites, tinted by the renderer.
    /// </summary>
    public static class ProcArt
    {
        /// <summary>Rasterises polylines (in 0..1 texture space) with the given thickness (also 0..1).</summary>
        public static Sprite Strokes(int size, IEnumerable<Vector2[]> lines, float thickness, float pixelsPerUnit)
        {
            var px = new float[size * size];
            var r = thickness * size * 0.5f;
            foreach (var line in lines)
            {
                for (var i = 0; i < line.Length - 1; i++)
                {
                    var a = line[i] * size;
                    var b = line[i + 1] * size;
                    var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - r - 1));
                    var maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + r + 1));
                    var minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - r - 1));
                    var maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + r + 1));
                    for (var y = minY; y <= maxY; y++)
                    for (var x = minX; x <= maxX; x++)
                    {
                        var d = DistanceToSegment(new Vector2(x + 0.5f, y + 0.5f), a, b);
                        var alpha = Mathf.Clamp01(r - d + 0.5f);
                        var idx = y * size + x;
                        if (alpha > px[idx]) px[idx] = alpha;
                    }
                }
            }
            var colors = new Color32[size * size];
            for (var i = 0; i < colors.Length; i++) colors[i] = new Color32(255, 255, 255, (byte)(px[i] * 255));
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>A gently curved stroke from a to b (hand-drawn feel).</summary>
        public static Vector2[] Curve(Vector2 a, Vector2 b, float bend, int steps = 12)
        {
            var pts = new Vector2[steps + 1];
            var normal = new Vector2(-(b - a).y, (b - a).x).normalized;
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                pts[i] = Vector2.Lerp(a, b, t) + normal * (Mathf.Sin(t * Mathf.PI) * bend);
            }
            return pts;
        }

        public const float PenXThickness = 0.11f, PenOThickness = 0.1f;

        /// <summary>The X's two strokes in 0..1 sprite space, in drawing order (also traced by the pen in Caro).</summary>
        public static Vector2[][] PenXStrokes() => new[]
        {
            Curve(new Vector2(0.18f, 0.80f), new Vector2(0.84f, 0.17f), 0.025f),
            Curve(new Vector2(0.20f, 0.20f), new Vector2(0.80f, 0.84f), -0.03f),
        };

        /// <summary>The O's single loop in 0..1 sprite space: slightly lopsided, the end overshoots the start.</summary>
        public static Vector2[][] PenOStrokes()
        {
            const int steps = 48;
            var pts = new Vector2[steps + 1];
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps * Mathf.PI * 2.15f + 1.9f;
                var wobble = 1f + 0.05f * Mathf.Sin(t * 3f);
                pts[i] = new Vector2(0.5f + Mathf.Cos(t) * 0.31f * wobble, 0.5f + Mathf.Sin(t) * 0.34f * wobble);
            }
            return new[] { pts };
        }

        /// <summary>A pen-drawn X: two slightly curved crossing strokes.</summary>
        public static Sprite PenX(int size, float ppu) => Strokes(size, PenXStrokes(), PenXThickness, ppu);

        /// <summary>A pen-drawn O: a slightly lopsided loop whose end overshoots its start.</summary>
        public static Sprite PenO(int size, float ppu) => Strokes(size, PenOStrokes(), PenOThickness, ppu);

        /// <summary>
        /// Squared notebook paper: <paramref name="cells"/> × <paramref name="cells"/> squares with thin blue lines, every
        /// cell <paramref name="cellPixels"/> pixels. One sprite unit = one cell.
        /// </summary>
        public static Sprite GridPaper(int cells, int cellPixels, Color paper, Color line)
        {
            var size = cells * cellPixels;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            var rng = new System.Random(7);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var onLine = x % cellPixels == 0 || y % cellPixels == 0 || x == size - 1 || y == size - 1;
                // A little paper grain.
                var grain = (float)rng.NextDouble() * 0.025f;
                var c = onLine ? line : paper;
                px[y * size + x] = new Color(c.r - grain, c.g - grain, c.b - grain, 1f);
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), cellPixels);
        }
    }
}
