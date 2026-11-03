using System.Collections.Generic;
using ChildGame.UI;
using UnityEngine;
using static ChildGame.UI.ArtShapes;

namespace ChildGame.MiniGames.TromXoai
{
    /// <summary>
    /// Official generated art for Trộm xoài (owner 2026-10-08: upgrade the graphics): ông Tám's yard — a mango tree with a
    /// gnarled trunk and a dense crown of long leaves, ripening mangoes, his tiled house behind a bamboo fence, a grassy
    /// yard with a dirt path, rubber flip-flops and a comic dust cloud for the fight. Cached for the session.
    /// </summary>
    public static class TromXoaiArt
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static readonly Color Shadow = new Color(0.1f, 0.12f, 0.04f, 0.3f);
        private static bool Cached(string key, out Sprite s) => cache.TryGetValue(key, out s) && s != null;

        /// <summary>The yard in front: grass with a worn dirt path along the bottom of the scene.</summary>
        public static Sprite Yard(Rect world, float pathTop, float pathBottom)
        {
            var key = "yard" + world;
            if (Cached(key, out var cached)) return cached;
            var r = new ArtRaster(world, 32f);
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var n = 0.6f * ValueNoise(p.x * 0.8f, p.y * 0.8f, 1) + 0.4f * ValueNoise(p.x * 5f, p.y * 5f, 2);
                var c = Color.Lerp(new Color32(104, 160, 70, 255), new Color32(140, 190, 90, 255), n);
                var top = pathTop + 0.12f * (ValueNoise(p.x * 1.5f, 0.5f, 3) - 0.5f);
                var bottom = pathBottom + 0.12f * (ValueNoise(p.x * 1.5f, 1.5f, 4) - 0.5f);
                if (p.y < top && p.y > bottom)
                {
                    c = Color.Lerp(new Color32(186, 154, 106, 255), new Color32(212, 182, 132, 255), ValueNoise(p.x * 3f, p.y * 3f, 5));
                    var edge = Mathf.Min(top - p.y, p.y - bottom);
                    if (edge < 0.08f) c = Color.Lerp(c, new Color32(150, 160, 90, 255), (0.08f - edge) / 0.08f * 0.6f);
                }
                // Further back (up the screen) the yard pales a little with distance.
                c = Color.Lerp(c, new Color32(170, 200, 150, 255), Mathf.Clamp01((p.y - pathTop) / (world.yMax - pathTop)) * 0.25f);
                c.a = 1f;
                r.px[y * r.w + x] = c;
            }
            var rng = new System.Random(6);
            for (var i = 0; i < (int)(world.width * 40); i++)
            {
                var p = new Vector2(Mathf.Lerp(world.xMin, world.xMax, (float)rng.NextDouble()), Mathf.Lerp(world.yMin, world.yMax, (float)rng.NextDouble()));
                if (p.y < pathTop + 0.05f && p.y > pathBottom - 0.05f)
                {
                    if (rng.NextDouble() < 0.5)
                    {
                        var rad = new Vector2(0.04f, 0.03f) * (0.7f + (float)rng.NextDouble());
                        r.Shape(q => Ellipse(q, p, rad), new Color32(170, 160, 146, 255), new Color32(120, 110, 96, 255), new Color32(220, 214, 200, 255), 0f, 0.02f, null, new Rect(p - rad * 2f, rad * 4f));
                    }
                    continue;
                }
                for (var b = 0; b < 3; b++)
                {
                    var a = Mathf.PI * 0.5f + ((float)rng.NextDouble() - 0.5f) * 1.2f;
                    r.Line(p, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.13f, 1.6f, Color.Lerp(new Color32(66, 118, 46, 255), new Color32(160, 200, 100, 255), (float)rng.NextDouble()), 0.9f, true);
                }
            }
            var s = r.ToSprite("Yard");
            cache[key] = s;
            return s;
        }

        /// <summary>
        /// The background behind the yard (pivot bottom-left at <paramref name="world"/>.min): a hazy village tree line, ông
        /// Tám's house with a tiled roof, and a bamboo fence along the back of the yard at <paramref name="fenceY"/>.
        /// </summary>
        public static Sprite Backdrop(Rect world, float fenceY, float houseX)
        {
            var key = "backdrop" + world;
            if (Cached(key, out var cached)) return cached;
            var r = new ArtRaster(world, 32f);
            // Tree line.
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var top = fenceY + 1.5f + 0.9f * ValueNoise(p.x * 0.5f, 0.5f, 1) + 0.35f * ValueNoise(p.x * 2f, 0.5f, 2);
                var a = Mathf.Clamp01((top - p.y) * r.ppu + 0.5f);
                if (a <= 0f) continue;
                var c = Color.Lerp(new Color32(108, 148, 100, 255), new Color32(150, 180, 140, 255), ValueNoise(p.x * 3f, p.y * 3f, 3));
                c = Color.Lerp(c, new Color32(190, 214, 220, 255), 0.3f); // haze
                r.px[y * r.w + x] = new Color(c.r, c.g, c.b, a);
            }
            // Ông Tám's house: whitewashed wall, a dark door and shuttered window, a curved tile roof.
            var hw = 2.2f;
            var wallTop = fenceY + 1.9f;
            r.Shape(p => Mathf.Max(Mathf.Abs(p.x - houseX) - hw, Mathf.Max(fenceY - 0.2f - p.y, p.y - wallTop)), new Color32(236, 226, 200, 255),
                new Color32(196, 184, 156, 255), new Color32(250, 244, 226, 255), 0.04f, 0.15f,
                (p, c) => (Color)c * (0.95f + 0.06f * ValueNoise(p.x * 8f, p.y * 8f, 4))); // weathered lime wash
            r.Shape(p => Mathf.Max(Mathf.Abs(p.x - (houseX - 0.6f)) - 0.38f, Mathf.Max(fenceY - 0.2f - p.y, p.y - (fenceY + 1.25f))), new Color32(110, 70, 44, 255),
                new Color32(76, 46, 30, 255), new Color32(150, 104, 70, 255), 0.03f, 0.08f,
                (p, c) => Mathf.Abs(Mathf.Repeat(p.x * 6f, 1f) - 0.5f) > 0.45f ? (Color)c * 0.8f : c); // door planks
            r.Shape(p => Mathf.Max(Mathf.Abs(p.x - (houseX + 1.1f)) - 0.45f, Mathf.Abs(p.y - (fenceY + 1.05f)) - 0.32f), new Color32(70, 120, 110, 255),
                new Color32(46, 84, 76, 255), new Color32(120, 170, 156, 255), 0.03f, 0.06f,
                (p, c) => Mathf.Abs(Mathf.Repeat(p.y * 8f, 1f) - 0.5f) > 0.4f ? (Color)c * 0.75f : c); // louvred shutters
            float Roof(Vector2 p)
            {
                var q = p - new Vector2(houseX, wallTop);
                var slope = q.y - (1.3f - Mathf.Abs(q.x) * 0.48f);
                return Mathf.Max(Mathf.Max(slope, -q.y - 0.05f), Mathf.Abs(q.x) - hw - 0.55f);
            }
            r.Shape(Roof, new Color32(186, 92, 62, 255), new Color32(126, 56, 40, 255), new Color32(226, 140, 104, 255), 0.04f, 0.2f,
                (p, c) =>
                {
                    var tiles = Mathf.Repeat((p.y - wallTop) * 6f, 1f) > 0.85f || Mathf.Abs(Mathf.Sin((p.x - houseX) * 18f)) > 0.94f;
                    return tiles ? (Color)c * 0.8f : c;
                });
            // Bamboo fence: slanted woven strips between posts.
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                if (p.y > fenceY + 0.75f || p.y < fenceY - 0.25f) continue;
                var weave = Mathf.Abs(Mathf.Repeat((p.x + p.y) * 3.2f, 1f) - 0.5f) < 0.18f || Mathf.Abs(Mathf.Repeat((p.x - p.y) * 3.2f, 1f) - 0.5f) < 0.18f;
                var post = Mathf.Abs(Mathf.Repeat(p.x, 1.6f) - 0.8f) < 0.06f;
                if (!weave && !post) continue;
                var c = post ? new Color32(150, 120, 70, 255) : new Color32(206, 178, 110, 255);
                var shade = post ? 1f : 0.85f + 0.15f * ValueNoise(p.x * 10f, p.y * 10f, 7);
                r.px[y * r.w + x] = new Color(c.r / 255f * shade, c.g / 255f * shade, c.b / 255f * shade, 1f);
            }
            var s = r.ToCornerSprite("Backdrop");
            cache[key] = s;
            return s;
        }

        /// <summary>
        /// The mango tree, in world coordinates (pivot at the world origin): a gnarled trunk with roots and two main
        /// branches, and a dense crown of long mango leaves centred on <paramref name="crown"/> with radii
        /// <paramref name="radius"/>, lit from the upper left.
        /// </summary>
        public static Sprite Tree(float trunkX, float groundY, Vector2 crown, Vector2 radius)
        {
            var key = $"tree{trunkX}{groundY}{crown}{radius}";
            if (Cached(key, out var cached)) return cached;
            var area = new Rect(crown.x - radius.x - 0.8f, groundY - 0.5f, (radius.x + 0.8f) * 2f, crown.y + radius.y + 0.8f - (groundY - 0.5f));
            var r = new ArtRaster(area, 40f);
            var bark = new Color32(116, 80, 52, 255);
            var barkDark = new Color32(76, 50, 34, 255);
            var barkLight = new Color32(160, 120, 84, 255);
            Color Bark(Vector2 p, Color c) => Color.Lerp(c, (Color)barkDark, Mathf.Clamp01(Mathf.Abs(Mathf.Sin(p.x * 22f + ValueNoise(p.x * 2f, p.y * 6f, 3) * 4f)) - 0.8f) * 2.5f);
            // Shadow on the ground, trunk with roots, main branches up into the crown.
            r.Shape(p => Ellipse(p, new Vector2(trunkX + 0.6f, groundY), new Vector2(2.8f, 0.3f)), Shadow, Shadow, Shadow, 0f, 0.2f);
            float Trunk(Vector2 p)
            {
                var w = Mathf.Lerp(0.55f, 0.36f, Mathf.InverseLerp(groundY, crown.y - 1f, p.y)) + Mathf.Max(0f, 0.5f - (p.y - groundY)) * 0.6f; // flared roots
                var bend = Mathf.Sin((p.y - groundY) * 0.8f) * 0.15f;
                return Mathf.Max(Mathf.Abs(p.x - trunkX - bend) - w, Mathf.Max(groundY - 0.05f - p.y, p.y - (crown.y - 0.6f)));
            }
            float Branches(Vector2 p) => Mathf.Min(
                Capsule(p, new Vector2(trunkX, crown.y - 1.4f), new Vector2(trunkX - 1.9f, crown.y + 0.4f), 0.22f),
                Capsule(p, new Vector2(trunkX + 0.1f, crown.y - 1.2f), new Vector2(trunkX + 2.0f, crown.y + 0.2f), 0.2f));
            r.Shape(p => SmoothUnion(Trunk(p), Branches(p), 0.25f), bark, barkDark, barkLight, 0.05f, 0.25f, Bark);
            // The crown: lumps of foliage, then thousands of long leaves, darker inside and below.
            var rng = new System.Random(21);
            var lumps = new List<(Vector2 c, float r)>();
            for (var i = 0; i < 14; i++)
            {
                var a = i / 14f * Mathf.PI * 2f;
                lumps.Add((crown + new Vector2(Mathf.Cos(a) * radius.x * 0.72f, Mathf.Sin(a) * radius.y * 0.66f), 1.0f + (float)rng.NextDouble() * 0.4f));
            }
            lumps.Add((crown, Mathf.Min(radius.x, radius.y) * 0.9f));
            float Crown(Vector2 p)
            {
                var d = 10f;
                foreach (var (c, rr) in lumps) d = SmoothUnion(d, (p - c).magnitude - rr, 0.4f);
                return d;
            }
            var leaf = new Color32(62, 128, 58, 255);
            r.Shape(Crown, leaf, new Color32(36, 84, 40, 255), new Color32(130, 186, 92, 255), 0.05f, 0.9f);
            for (var i = 0; i < 3600; i++)
            {
                var p = crown + new Vector2(((float)rng.NextDouble() * 2f - 1f) * radius.x * 1.05f, ((float)rng.NextDouble() * 2f - 1f) * radius.y * 1.05f);
                var d = Crown(p);
                if (d > -0.05f) continue;
                var depth = Mathf.Clamp01(-d / 1.2f);
                var lit = Mathf.Clamp01(Vector2.Dot((p - crown).normalized, new Vector2(-0.6f, 0.8f)) * 0.5f + 0.5f) * (1f - depth * 0.6f);
                var col = Color.Lerp(new Color32(40, 92, 44, 255), new Color32(146, 196, 96, 255), lit * (0.6f + 0.4f * (float)rng.NextDouble()));
                var a = (float)rng.NextDouble() * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f - 0.4f).normalized;
                r.Line(p - dir * 0.12f, p + dir * 0.12f, 3.6f, col, 0.95f, true);
            }
            var s = r.ToSprite("MangoTree", Vector2.zero); // pivot at the world origin: place the sprite at (0, 0)
            cache[key] = s;
            return s;
        }

        /// <summary>A ripening mango hanging by its stalk, pivot at the fruit's centre (about 0.48 × 0.62 units).</summary>
        public static Sprite Mango(int variant)
        {
            var key = "mango" + variant;
            if (Cached(key, out var cached)) return cached;
            var r = new ArtRaster(new Rect(-0.35f, -0.4f, 0.7f, 0.85f), 160f);
            var ripe = variant % 3 / 2f;
            var skin = Color.Lerp(new Color32(150, 190, 70, 255), new Color32(246, 200, 60, 255), 0.4f + ripe * 0.6f);
            r.Shape(p => Capsule(p, new Vector2(0f, 0.26f), new Vector2(0.02f, 0.4f), 0.02f), new Color32(100, 80, 44, 255), new Color32(70, 52, 30, 255),
                new Color32(140, 112, 70, 255), 0.012f, 0.02f);
            // A kidney-shaped fruit: fuller at the shoulder, a beak toward the bottom.
            float Fruit(Vector2 p) => SmoothUnion(Ellipse(p, new Vector2(0f, 0.04f), new Vector2(0.22f, 0.26f)),
                Ellipse(p, new Vector2(0.05f, -0.15f), new Vector2(0.15f, 0.16f)), 0.08f);
            r.Shape(Fruit, skin, Color.Lerp(skin, new Color32(90, 120, 40, 255), 0.45f), Color.Lerp(skin, Color.white, 0.5f), 0.02f, 0.12f,
                (p, c) => Color.Lerp(c, new Color32(232, 120, 60, 255), Mathf.Clamp01(1f - (p - new Vector2(-0.08f, 0.1f)).magnitude / 0.16f) * 0.45f * ripe)); // sun blush
            r.Shape(p => Ellipse(p, new Vector2(-0.08f, 0.12f), new Vector2(0.04f, 0.07f)), new Color(1, 1, 1, 0.5f), new Color(1, 1, 1, 0f), new Color(1, 1, 1, 0.5f), 0f, 0.03f);
            var s = r.ToSprite("Mango", Vector2.zero);
            cache[key] = s;
            return s;
        }

        /// <summary>A blue rubber flip-flop (dép lào) seen from the side, about 0.55 × 0.24 units, pivot at its centre.</summary>
        public static Sprite Sandal()
        {
            if (Cached("sandal", out var cached)) return cached;
            var r = new ArtRaster(new Rect(-0.32f, -0.15f, 0.64f, 0.3f), 200f);
            var blue = new Color32(48, 112, 196, 255);
            r.Shape(p => Mathf.Max(Mathf.Abs(p.x) - 0.26f, Mathf.Abs(p.y + 0.06f) - 0.035f) - 0.02f, blue, Color.Lerp(blue, Color.black, 0.35f),
                Color.Lerp(blue, Color.white, 0.4f), 0.015f, 0.03f);
            r.Shape(p => Mathf.Max(Mathf.Abs(p.x) - 0.25f, Mathf.Abs(p.y + 0.105f) - 0.012f), new Color32(250, 250, 250, 255), new Color32(210, 210, 210, 255), Color.white, 0f, 0.01f);
            // The thong strap arching over the front.
            r.Shape(p => Mathf.Abs((p - new Vector2(0.04f, -0.03f)).magnitude - 0.13f) - 0.022f + (p.y < -0.03f ? 1f : 0f), new Color32(236, 236, 236, 255),
                new Color32(190, 190, 190, 255), Color.white, 0.012f, 0.02f);
            var s = r.ToSprite("Sandal");
            cache["sandal"] = s;
            return s;
        }

        /// <summary>A comic fight cloud: billowing dust with stars and a flying sandal-shaped smudge, pivot at its centre.</summary>
        public static Sprite FightCloud()
        {
            if (Cached("fight", out var cached)) return cached;
            var r = new ArtRaster(new Rect(-1.4f, -1.2f, 2.8f, 2.4f), 80f);
            var rng = new System.Random(8);
            var puffs = new List<(Vector2 c, float r)>();
            for (var i = 0; i < 12; i++)
            {
                var a = i / 12f * Mathf.PI * 2f;
                puffs.Add((new Vector2(Mathf.Cos(a) * 0.8f, Mathf.Sin(a) * 0.6f), 0.4f + (float)rng.NextDouble() * 0.2f));
            }
            puffs.Add((Vector2.zero, 0.8f));
            float Cloud(Vector2 p)
            {
                var d = 10f;
                foreach (var (c, rr) in puffs) d = Mathf.Min(d, (p - c).magnitude - rr);
                return d;
            }
            r.Shape(Cloud, new Color32(246, 238, 220, 255), new Color32(200, 184, 156, 255), Color.white, 0.04f, 0.3f);
            foreach (var c in new[] { new Vector2(-0.5f, 0.6f), new Vector2(0.7f, 0.4f), new Vector2(0.2f, -0.55f) })
            {
                float Star(Vector2 p)
                {
                    var q = p - c;
                    var a = Mathf.Atan2(q.y, q.x);
                    return q.magnitude - (0.13f + 0.08f * Mathf.Cos(a * 5f));
                }
                r.Shape(Star, new Color32(255, 214, 60, 255), new Color32(220, 150, 30, 255), new Color32(255, 240, 160, 255), 0.02f, 0.05f);
            }
            // Flailing limbs poking out of the dust.
            r.Shape(p => Capsule(p, new Vector2(-0.9f, -0.1f), new Vector2(-1.25f, -0.35f), 0.07f), new Color32(236, 190, 148, 255), new Color32(200, 144, 106, 255),
                new Color32(252, 218, 186, 255), 0.02f, 0.04f);
            r.Shape(p => Capsule(p, new Vector2(0.9f, 0.2f), new Vector2(1.25f, 0.5f), 0.07f), new Color32(236, 190, 148, 255), new Color32(200, 144, 106, 255),
                new Color32(252, 218, 186, 255), 0.02f, 0.04f);
            var s = r.ToSprite("FightCloud");
            cache["fight"] = s;
            return s;
        }
    }
}
