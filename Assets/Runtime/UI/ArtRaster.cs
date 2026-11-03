using System;
using UnityEngine;

namespace ChildGame.UI
{
    /// <summary>
    /// A small signed-distance rasteriser for generated official art (world units → pixels): shapes with soft shading
    /// from the edge inward, a light band on the upper-left edges and a warm dark outline, in the style of
    /// <see cref="HandArt"/>; anti-aliased lines; per-pixel access for textures. Used by the Nhổ tóc bạc and river art.
    /// </summary>
    public sealed class ArtRaster
    {
        public readonly int w, h;
        public readonly float ppu;
        public readonly Vector2 origin; // world position of pixel (0,0)'s corner
        public readonly Color[] px;
        /// <summary>The outline colour (the warm dark brown of the hand art by default).</summary>
        public Color OutlineColor = new Color32(70, 42, 32, 255);

        public ArtRaster(Rect world, float ppu)
        {
            this.ppu = ppu;
            origin = world.min;
            w = Mathf.CeilToInt(world.width * ppu);
            h = Mathf.CeilToInt(world.height * ppu);
            px = new Color[w * h];
        }

        public Vector2 World(int x, int y) => origin + new Vector2((x + 0.5f) / ppu, (y + 0.5f) / ppu);

        /// <summary>
        /// Draws a shape with shading from its edge inward, a light band on the upper-left edges and an outline
        /// (<paramref name="outline"/> world units; 0 = none). <paramref name="tint"/> may vary the fill per pixel.
        /// </summary>
        public void Shape(Func<Vector2, float> sdf, Color fill, Color shade, Color light, float outline, float shadeWidth,
            Func<Vector2, Color, Color> tint = null, Rect? bounds = null)
        {
            var b = bounds ?? new Rect(origin, new Vector2(w / ppu, h / ppu));
            var x0 = Mathf.Max(0, Mathf.FloorToInt((b.xMin - origin.x) * ppu) - 4);
            var x1 = Mathf.Min(w - 1, Mathf.CeilToInt((b.xMax - origin.x) * ppu) + 4);
            var y0 = Mathf.Max(0, Mathf.FloorToInt((b.yMin - origin.y) * ppu) - 4);
            var y1 = Mathf.Min(h - 1, Mathf.CeilToInt((b.yMax - origin.y) * ppu) + 4);
            var o = outline * ppu;
            for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                var p = World(x, y);
                var d = sdf(p) * ppu; // pixels, negative inside
                if (d > o + 1f) continue;
                var inner = Mathf.Clamp01(-d / (shadeWidth * ppu));
                var c = Color.Lerp(shade, fill, Mathf.SmoothStep(0f, 1f, inner));
                var band = -d < 2f || -d > shadeWidth * ppu ? 0f : Mathf.Sin((-d - 2f) / (shadeWidth * ppu - 2f) * Mathf.PI);
                var g = ArtShapes.Gradient(sdf, p, 1.5f / ppu);
                c = Color.Lerp(c, light, band * Mathf.Clamp01(Vector2.Dot(-g, new Vector2(-0.6f, 0.8f))) * 0.45f);
                if (tint != null) c = tint(p, c);
                var fillA = Mathf.Clamp01(0.5f - d);
                float a;
                if (o > 0f)
                {
                    var outerA = Mathf.Clamp01(0.5f - (d - o));
                    c = Color.Lerp(c, OutlineColor, Mathf.Clamp01(0.5f + d));
                    a = Mathf.Max(fillA, outerA);
                }
                else a = fillA;
                Blend(x, y, c, a * c.a); // the colour's own alpha counts (soft shadows)
            }
        }

        public void Blend(int x, int y, Color c, float a)
        {
            if (a <= 0f) return;
            var i = y * w + x;
            var under = px[i];
            var outA = a + under.a * (1f - a);
            if (outA <= 0f) return;
            var rgb = (new Vector3(c.r, c.g, c.b) * a + new Vector3(under.r, under.g, under.b) * under.a * (1f - a)) / outA;
            px[i] = new Color(rgb.x, rgb.y, rgb.z, outA);
        }

        /// <summary>An anti-aliased line of <paramref name="width"/> pixels, clipped to pixels already drawn when
        /// <paramref name="onlyOver"/> (keeps hair strands inside the head).</summary>
        public void Line(Vector2 a, Vector2 b, float width, Color c, float alpha, bool onlyOver)
        {
            var pa = (a - origin) * ppu;
            var pb = (b - origin) * ppu;
            var r = width * 0.5f;
            var x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, pb.x) - r - 1));
            var x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(pa.x, pb.x) + r + 1));
            var y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, pb.y) - r - 1));
            var y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(pa.y, pb.y) + r + 1));
            var ab = pb - pa;
            var len2 = Mathf.Max(ab.sqrMagnitude, 1e-4f);
            for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                var t = Mathf.Clamp01(Vector2.Dot(p - pa, ab) / len2);
                var d = Vector2.Distance(p, pa + ab * t) - r;
                var cov = Mathf.Clamp01(0.5f - d) * alpha * c.a;
                if (cov <= 0f) continue;
                var i = y * w + x;
                if (onlyOver)
                {
                    var under = px[i];
                    if (under.a <= 0f) continue;
                    px[i] = new Color(
                        Mathf.Lerp(under.r, c.r, cov), Mathf.Lerp(under.g, c.g, cov), Mathf.Lerp(under.b, c.b, cov), under.a);
                }
                else Blend(x, y, c, cov);
            }
        }

        public Sprite ToSprite(string name, FilterMode filter = FilterMode.Bilinear)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = filter, name = name };
            tex.SetPixels(px);
            tex.Apply(false, true);
            // Pivot at the world origin of the raster's centre: callers place the sprite at Center.
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
        }

        /// <summary>A sprite whose pivot is the world point <paramref name="pivot"/> (e.g. a shoulder for an arm).</summary>
        public Sprite ToSprite(string name, Vector2 pivot)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = name };
            tex.SetPixels(px);
            tex.Apply(false, true);
            var size = new Vector2(w / ppu, h / ppu);
            return Sprite.Create(tex, new Rect(0, 0, w, h), (pivot - origin) / size, ppu);
        }

        /// <summary>A sprite whose pivot is the raster's bottom-left corner (world <see cref="origin"/>).</summary>
        public Sprite ToCornerSprite(string name) => ToSprite(name, origin);

        public Vector2 Center => origin + new Vector2(w / ppu, h / ppu) * 0.5f;
    }

    /// <summary>Distance functions and noise for <see cref="ArtRaster"/> art.</summary>
    public static class ArtShapes
    {
        public static Vector2 Gradient(Func<Vector2, float> sdf, Vector2 p, float e)
        {
            var g = new Vector2(sdf(p + new Vector2(e, 0)) - sdf(p - new Vector2(e, 0)), sdf(p + new Vector2(0, e)) - sdf(p - new Vector2(0, e)));
            return g.sqrMagnitude > 1e-12f ? g.normalized : Vector2.up;
        }

        public static float Ellipse(Vector2 p, Vector2 c, Vector2 r)
        {
            // Approximate distance to an ellipse (good near the edge, which is all the shading needs).
            var q = (p - c) / r;
            var k = q.magnitude;
            return (k - 1f) * Mathf.Min(r.x, r.y) * (k < 1f ? 1f : 1f);
        }

        public static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            var pa = p - a;
            var ba = b - a;
            var h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        public static float SmoothUnion(float a, float b, float k)
        {
            var h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                var n = x * 374761393 + y * 668265263 + seed * 1442695041;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0xffffff) / (float)0xffffff;
            }
        }

        public static float ValueNoise(float x, float y, int seed)
        {
            var xi = Mathf.FloorToInt(x);
            var yi = Mathf.FloorToInt(y);
            var fx = x - xi;
            var fy = y - yi;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(Mathf.Lerp(Hash(xi, yi, seed), Hash(xi + 1, yi, seed), fx),
                Mathf.Lerp(Hash(xi, yi + 1, seed), Hash(xi + 1, yi + 1, seed), fx), fy);
        }
    }
}
