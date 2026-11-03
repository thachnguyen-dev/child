using System.Collections.Generic;
using ChildGame.UI;
using UnityEngine;
using static ChildGame.UI.ArtShapes;

namespace ChildGame.MiniGames.ThaDieu
{
    /// <summary>
    /// Official generated art for Thả diều (owner 2026-10-08: upgrade the graphics): an afternoon countryside — hazy
    /// hills, a village tree line and rice paddies in the distance (a seamless strip for the endless field), a meadow with
    /// wild grass and flowers, grass tufts that bend in the wind, and a paper kite on a bamboo frame with a tail of paper
    /// bows. Cached for the session.
    /// </summary>
    public static class ThaDieuArt
    {
        public const float FarTile = 32f, MeadowTile = 20f;
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static bool Cached(string key, out Sprite s) => cache.TryGetValue(key, out s) && s != null;

        /// <summary>Periodic noise along x (repeats every <paramref name="period"/> cells), so strips tile without seams.</summary>
        private static float PNoise(float x, float y, int period, int seed)
        {
            var xi = Mathf.FloorToInt(x);
            var yi = Mathf.FloorToInt(y);
            var fx = x - xi;
            var fy = y - yi;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            int W(int v) => ((v % period) + period) % period;
            return Mathf.Lerp(Mathf.Lerp(Hash(W(xi), yi, seed), Hash(W(xi + 1), yi, seed), fx),
                Mathf.Lerp(Hash(W(xi), yi + 1, seed), Hash(W(xi + 1), yi + 1, seed), fx), fy);
        }

        /// <summary>Noise seamless over <paramref name="tile"/>: cellsPerUnit × tile must be a whole number.</summary>
        private static float TNoise(float x, float y, float cellsPerUnit, float tile, int seed) =>
            PNoise(x * cellsPerUnit, y * cellsPerUnit, Mathf.RoundToInt(tile * cellsPerUnit), seed);

        /// <summary>
        /// The distant countryside, one tile <see cref="FarTile"/> wide (pivot bottom-left at y = <paramref name="bottom"/>):
        /// two ranges of hazy hills, a dark line of village trees and bamboo, then rice paddies in strips of green and gold
        /// down to the meadow.
        /// </summary>
        public static Sprite Far(float bottom, float horizon)
        {
            var key = $"far{bottom}{horizon}";
            if (Cached(key, out var cached)) return cached;
            var r = new ArtRaster(new Rect(0f, bottom, FarTile, horizon + 3.6f - bottom), 40f);
            var haze = new Color32(214, 214, 196, 255);
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                Color c = new Color(0, 0, 0, 0);
                // Back hills (bluish, hazy), then nearer hills (greener).
                var back = horizon + 1.6f + 1.4f * TNoise(p.x, 0.5f, 0.125f, FarTile, 1) + 0.4f * TNoise(p.x, 0.5f, 0.4375f, FarTile, 2);
                var front = horizon + 0.7f + 0.9f * TNoise(p.x, 0.5f, 0.1875f, FarTile, 3) + 0.2f * TNoise(p.x, 0.5f, 0.8125f, FarTile, 4);
                // Each layer blends in over its edge (anti-aliased ridgelines).
                float Cover(float edge) => Mathf.Clamp01((edge - p.y) * r.ppu + 0.5f);
                c = Color.Lerp(c, Color.Lerp(new Color32(150, 170, 170, 255), haze, 0.35f), Cover(back));
                c = Color.Lerp(c, Color.Lerp(new Color32(118, 150, 104, 255), haze, 0.25f), Cover(front));
                // Village tree line: lumpy crowns along the horizon.
                var trees = horizon + 0.25f + 0.35f * TNoise(p.x, 0.5f, 1.5625f, FarTile, 5) + 0.2f * TNoise(p.x, 0.5f, 4f, FarTile, 6);
                c = Color.Lerp(c, Color.Lerp(new Color32(70, 108, 62, 255), new Color32(96, 132, 76, 255), TNoise(p.x, p.y, 3f, FarTile, 7)), Cover(trees));
                // Rice paddies: strips running across, alternating young green and ripening gold, darker bunds between.
                if (p.y < horizon)
                {
                    var depth = (horizon - p.y) / (horizon - bottom);
                    var stripe = Mathf.Repeat((horizon - p.y) / Mathf.Lerp(0.1f, 0.5f, depth) + TNoise(p.x, 0.5f, 0.3125f, FarTile, 8) * 1.5f, 1f);
                    var field = Mathf.FloorToInt((horizon - p.y) / Mathf.Lerp(0.1f, 0.5f, depth) + TNoise(p.x, 0.5f, 0.3125f, FarTile, 8) * 1.5f);
                    var gold = Hash(field, Mathf.FloorToInt(p.x / 6.4f) % 5, 9) > 0.65f;
                    c = gold ? new Color32(206, 190, 96, 255) : new Color32(124, 170, 80, 255);
                    c = Color.Lerp(c, haze, (1f - depth) * 0.3f);
                    if (stripe < 0.06f) c = (Color)c * 0.82f; // bund
                    c *= 0.95f + 0.08f * TNoise(p.x, p.y, 6f, FarTile, 10);
                }
                c.a = Mathf.Clamp01(c.a);
                r.px[y * r.w + x] = c;
            }
            var s = r.ToCornerSprite("FarCountry");
            cache[key] = s;
            return s;
        }

        /// <summary>
        /// The meadow the child runs on, one tile <see cref="MeadowTile"/> wide (pivot bottom-left at y =
        /// <paramref name="bottom"/>, its grassy top edge at <paramref name="top"/>): mown grass, clover and wild flowers.
        /// </summary>
        public static Sprite Meadow(float bottom, float top)
        {
            var key = $"meadow{bottom}{top}";
            if (Cached(key, out var cached)) return cached;
            var r = new ArtRaster(new Rect(0f, bottom, MeadowTile, top + 0.3f - bottom), 30f);
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var edge = top + 0.08f * TNoise(p.x, 0.5f, 4f, MeadowTile, 1) + 0.06f * TNoise(p.x, 0.5f, 12f, MeadowTile, 2);
                if (p.y > edge) continue;
                var n = 0.6f * TNoise(p.x, p.y, 0.7f, MeadowTile, 3) + 0.4f * TNoise(p.x, p.y, 5f, MeadowTile, 4);
                var c = Color.Lerp(new Color32(96, 150, 62, 255), new Color32(134, 182, 82, 255), n);
                if (p.y > edge - 0.1f) c = Color.Lerp(c, new Color32(160, 200, 100, 255), 0.35f); // sunlit edge
                c.a = 1f;
                r.px[y * r.w + x] = c;
            }
            var rng = new System.Random(3);
            foreach (var o in new[] { -MeadowTile, 0f, MeadowTile })
            {
                var frng = new System.Random(4);
                for (var i = 0; i < 700; i++)
                {
                    var p = new Vector2((float)frng.NextDouble() * MeadowTile + o, Mathf.Lerp(bottom, top, (float)frng.NextDouble()));
                    var tone = (float)frng.NextDouble();
                    for (var b = 0; b < 3; b++)
                    {
                        var a = Mathf.PI * 0.5f + ((float)frng.NextDouble() - 0.5f) * 1.2f;
                        r.Line(p, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.14f, 1.6f,
                            Color.Lerp(new Color32(70, 120, 48, 255), new Color32(160, 200, 100, 255), tone), 0.9f, true);
                    }
                    if (frng.NextDouble() < 0.07)
                    {
                        var petal = frng.NextDouble() < 0.4 ? new Color32(250, 250, 240, 255) : frng.NextDouble() < 0.5 ? new Color32(250, 210, 60, 255) : new Color32(220, 120, 200, 255);
                        r.Shape(q => (q - p).magnitude - 0.055f, petal, Color.Lerp(petal, Color.black, 0.2f), Color.white, 0f, 0.03f, null,
                            new Rect(p - Vector2.one * 0.1f, Vector2.one * 0.2f));
                    }
                }
            }
            var s = r.ToCornerSprite("Meadow");
            cache[key] = s;
            return s;
        }

        /// <summary>A tuft of tall wild grass, pivot at its base (it bends in the wind by rotating).</summary>
        public static Sprite Tuft(int variant)
        {
            var key = "tuft" + variant;
            if (Cached(key, out var cached)) return cached;
            var rng = new System.Random(80 + variant);
            var r = new ArtRaster(new Rect(-0.35f, 0f, 0.7f, 0.75f), 90f);
            for (var b = 0; b < 7; b++)
            {
                var lean = ((float)rng.NextDouble() - 0.5f) * 0.5f;
                var h = 0.35f + (float)rng.NextDouble() * 0.35f;
                var prev = new Vector2(((float)rng.NextDouble() - 0.5f) * 0.12f, 0f);
                var col = Color.Lerp(new Color32(70, 124, 50, 255), new Color32(150, 196, 92, 255), (float)rng.NextDouble());
                for (var i = 1; i <= 10; i++)
                {
                    var t = i / 10f;
                    var p = new Vector2(prev.x + lean * 0.1f * (1f + t), t * h);
                    r.Line(prev, p, Mathf.Lerp(4.5f, 1f, t), col, 1f, false);
                    prev = p;
                }
            }
            var s = r.ToSprite("Tuft", Vector2.zero);
            cache[key] = s;
            return s;
        }

        /// <summary>
        /// A paper kite (diều giấy) on a bamboo cross frame, seen face on, pivot at the crossing of the sticks (the bridle
        /// point): two-tone paper panels, the frame sticks, a little paper rim; about 1 unit across.
        /// </summary>
        public static Sprite Kite(Color paper, Color second)
        {
            var key = "kite" + ColorUtility.ToHtmlStringRGB(paper) + ColorUtility.ToHtmlStringRGB(second);
            if (Cached(key, out var cached)) return cached;
            var r = new ArtRaster(new Rect(-0.55f, -0.75f, 1.1f, 1.25f), 140f);
            float Diamond(Vector2 p) => (Mathf.Abs(p.x) / 0.46f + Mathf.Abs(p.y - (p.y > 0 ? 0 : 0)) / (p.y > 0 ? 0.4f : 0.66f) - 1f) * 0.3f;
            r.Shape(Diamond, paper, Color.Lerp(paper, Color.black, 0.25f), Color.Lerp(paper, Color.white, 0.4f), 0.02f, 0.06f,
                (p, c) =>
                {
                    // Opposite quarters in the second colour; paper fibres.
                    var quarter = (p.x > 0) == (p.y > 0);
                    var col = quarter ? (Color)c : Color.Lerp(c, second, 0.85f);
                    return col * (0.96f + 0.05f * ValueNoise(p.x * 40f, p.y * 40f, 2));
                });
            // The bamboo sticks: the spine and the bowed cross spar.
            var bamboo = new Color32(150, 112, 60, 255);
            r.Line(new Vector2(0, 0.4f), new Vector2(0, -0.66f), 3.2f, bamboo, 1f, true);
            for (var i = 0; i < 12; i++)
            {
                var t0 = i / 12f * 2f - 1f;
                var t1 = (i + 1) / 12f * 2f - 1f;
                r.Line(new Vector2(t0 * 0.46f, 0.04f * (1f - t0 * t0)), new Vector2(t1 * 0.46f, 0.04f * (1f - t1 * t1)), 3f, bamboo, 1f, true);
            }
            var s = r.ToSprite("Kite", Vector2.zero);
            cache[key] = s;
            return s;
        }

        /// <summary>A paper bow for the kite's tail, about 0.22 units wide.</summary>
        public static Sprite Bow(Color color)
        {
            var key = "bow" + ColorUtility.ToHtmlStringRGB(color);
            if (Cached(key, out var cached)) return cached;
            var r = new ArtRaster(new Rect(-0.15f, -0.09f, 0.3f, 0.18f), 160f);
            float Wings(Vector2 p) => Mathf.Min(Ellipse(p, new Vector2(-0.065f, 0f), new Vector2(0.07f, 0.055f)), Ellipse(p, new Vector2(0.065f, 0f), new Vector2(0.07f, 0.055f)));
            r.Shape(Wings, color, Color.Lerp(color, Color.black, 0.3f), Color.Lerp(color, Color.white, 0.4f), 0.012f, 0.03f);
            r.Shape(p => (p).magnitude - 0.025f, Color.Lerp(color, Color.black, 0.2f), Color.Lerp(color, Color.black, 0.4f), color, 0.01f, 0.01f);
            var s = r.ToSprite("Bow", Vector2.zero);
            cache["bow" + ColorUtility.ToHtmlStringRGB(color)] = s;
            return s;
        }
    }
}
