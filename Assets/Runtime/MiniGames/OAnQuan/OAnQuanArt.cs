using System.Collections.Generic;
using ChildGame.UI;
using UnityEngine;
using static ChildGame.UI.ArtShapes;

namespace ChildGame.MiniGames.OAnQuan
{
    /// <summary>
    /// Official generated art for Ô ăn quan (owner 2026-10-08: upgrade the graphics): a packed-earth yard, the board
    /// scratched in chalk on the ground (ten squares and two half-moon quan spaces), river pebbles for the dân and a big
    /// stone for each quan. Cached for the session.
    /// </summary>
    public static class OAnQuanArt
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static readonly Color Shadow = new Color(0.16f, 0.1f, 0.04f, 0.32f);

        private static bool Cached(string key, out Sprite s) => cache.TryGetValue(key, out s) && s != null;

        /// <summary>The yard: plain packed earth only (owner 2026-10-09: no decorations, so the pieces are easy to see).</summary>
        public static Sprite Yard(Rect world)
        {
            if (Cached("yard" + world, out var cached)) return cached;
            var r = new ArtRaster(world, 36f);
            var earth = new Color32(200, 162, 112, 255);
            var dark = new Color32(168, 128, 84, 255);
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var n = 0.55f * ValueNoise(p.x * 0.7f, p.y * 0.7f, 1) + 0.3f * ValueNoise(p.x * 4f, p.y * 4f, 2) + 0.15f * ValueNoise(p.x * 18f, p.y * 18f, 3);
                var c = Color.Lerp(dark, earth, n);
                c.a = 1f;
                r.px[y * r.w + x] = c;
            }
            var s = r.ToSprite("Yard");
            cache["yard" + world] = s;
            return s;
        }

        /// <summary>
        /// The board drawn in chalk on the earth, centred on the sprite's pivot: a 5 × 2 grid of squares of
        /// <paramref name="cell"/>, with a half-moon quan space at each end (radius = one square). The chalk is grainy and
        /// a little broken, as when drawn with a piece of brick or chalk on the ground.
        /// </summary>
        public static Sprite Board(float cell)
        {
            var key = "board" + cell;
            if (Cached(key, out var cached)) return cached;
            var halfW = 2.5f * cell;
            var halfH = cell;
            var r = new ArtRaster(new Rect(-halfW - cell - 0.4f, -halfH - 0.4f, (halfW + cell + 0.4f) * 2f, (halfH + 0.4f) * 2f), 60f);
            const float width = 0.055f;
            float Lines(Vector2 p)
            {
                // Distance to the nearest chalk line.
                var d = Mathf.Abs(Mathf.Max(Mathf.Abs(p.x) - halfW, Mathf.Abs(p.y) - halfH)); // outer rectangle
                if (Mathf.Abs(p.x) <= halfW) d = Mathf.Min(d, Mathf.Abs(p.y));                // middle line
                if (Mathf.Abs(p.y) <= halfH)
                    for (var i = -2; i <= 2; i++) d = Mathf.Min(d, Mathf.Abs(p.x - (i + 0.5f) * cell + 0f) + (Mathf.Abs(i + 0.5f) > 2.4f ? 10f : 0f));
                foreach (var s in new[] { -1f, 1f })
                {
                    var c = new Vector2(s * halfW, 0);
                    if ((p.x - c.x) * s >= 0f) d = Mathf.Min(d, Mathf.Abs((p - c).magnitude - cell));
                }
                // A wobble, as drawn by hand.
                return d + (ValueNoise(p.x * 3f, p.y * 3f, 8) - 0.5f) * 0.02f;
            }
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var d = Lines(p) - width * 0.5f;
                var a = Mathf.Clamp01(0.5f - d * r.ppu);
                if (a <= 0f) continue;
                // Grainy chalk: the earth shows through in specks, a few short breaks along the line.
                var grain = ValueNoise(p.x * 70f, p.y * 70f, 2);
                var breaks = ValueNoise(p.x * 2.2f + p.y * 2.2f, 0.5f, 3);
                a *= Mathf.Clamp01(0.45f + grain * 0.7f) * (breaks < 0.12f ? 0.35f : 1f);
                r.px[y * r.w + x] = new Color(0.97f, 0.95f, 0.88f, a * 0.92f);
            }
            var sprite = r.ToSprite("Board", Vector2.zero);
            cache[key] = sprite;
            return sprite;
        }

        /// <summary>A small river pebble (dân), about 0.26 units across; several shapes and tones.</summary>
        public static Sprite Pebble(int variant)
        {
            var key = "pebble" + variant;
            if (Cached(key, out var cached)) return cached;
            var rng = new System.Random(600 + variant);
            var r = new ArtRaster(new Rect(-0.2f, -0.2f, 0.4f, 0.4f), 200f);
            var rad = new Vector2(0.12f + (float)rng.NextDouble() * 0.025f, 0.095f + (float)rng.NextDouble() * 0.02f);
            var ang = (float)rng.NextDouble() * Mathf.PI;
            float Stone(Vector2 p)
            {
                var q = new Vector2(p.x * Mathf.Cos(ang) + p.y * Mathf.Sin(ang), -p.x * Mathf.Sin(ang) + p.y * Mathf.Cos(ang));
                return Ellipse(q, Vector2.zero, rad);
            }
            var tones = new Color32[]
            {
                new Color32(130, 126, 120, 255), new Color32(160, 146, 128, 255), new Color32(110, 104, 100, 255),
                new Color32(186, 178, 166, 255), new Color32(140, 118, 96, 255), new Color32(96, 96, 104, 255),
            };
            var tone = tones[variant % tones.Length];
            r.Shape(p => Stone(p - new Vector2(0.02f, -0.025f)), Shadow, Shadow, Shadow, 0f, 0.02f);
            r.Shape(Stone, tone, Color.Lerp(tone, Color.black, 0.35f), Color.Lerp(tone, Color.white, 0.55f), 0.012f, 0.06f,
                (p, c) => Color.Lerp(c, (Color)c * 0.85f, Mathf.Clamp01(ValueNoise(p.x * 60f, p.y * 60f, variant) - 0.55f) * 1.5f));
            // A glint of polish from the stream.
            r.Shape(p => Ellipse(p, new Vector2(-0.035f, 0.035f), new Vector2(0.03f, 0.018f)), new Color(1, 1, 1, 0.55f), new Color(1, 1, 1, 0f),
                new Color(1, 1, 1, 0.55f), 0f, 0.015f);
            var s = r.ToSprite("Pebble", Vector2.zero);
            cache[key] = s;
            return s;
        }

        /// <summary>The quan: a big flattish stone, darker and smoother than the dân, about 0.85 units across.</summary>
        public static Sprite QuanStone()
        {
            if (Cached("quan", out var cached)) return cached;
            var r = new ArtRaster(new Rect(-0.55f, -0.55f, 1.1f, 1.1f), 160f);
            float Stone(Vector2 p) => SmoothUnion(Ellipse(p, new Vector2(-0.04f, 0f), new Vector2(0.38f, 0.32f)),
                Ellipse(p, new Vector2(0.1f, 0.05f), new Vector2(0.3f, 0.3f)), 0.1f);
            r.Shape(p => Stone(p - new Vector2(0.05f, -0.06f)), Shadow, Shadow, Shadow, 0f, 0.05f);
            var tone = new Color32(96, 88, 104, 255);
            r.Shape(Stone, tone, new Color32(56, 50, 64, 255), new Color32(176, 168, 186, 255), 0.025f, 0.22f,
                (p, c) =>
                {
                    // Pale veins of quartz across the dark stone.
                    var vein = Mathf.Abs(Mathf.Sin(p.x * 9f + p.y * 4f + ValueNoise(p.x * 6f, p.y * 6f, 4) * 3f));
                    return Color.Lerp(c, new Color32(206, 200, 210, 255), Mathf.Clamp01(0.06f - vein) * 8f * 0.6f);
                });
            r.Shape(p => Ellipse(p, new Vector2(-0.12f, 0.12f), new Vector2(0.1f, 0.05f)), new Color(1, 1, 1, 0.4f), new Color(1, 1, 1, 0f),
                new Color(1, 1, 1, 0.4f), 0f, 0.04f);
            var s = r.ToSprite("QuanStone", Vector2.zero);
            cache["quan"] = s;
            return s;
        }
    }
}
