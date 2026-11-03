using System.IO;
using ChildGame.MiniGames.VeTranh;
using UnityEngine;

namespace ChildGame.EditorTools
{
    /// <summary>
    /// Renders a test sheet of the crayon engine to <c>logs/crayon-sheet.png</c> (and a 3× zoom crop) so the wax look can
    /// be judged without running the game:
    /// <c>Unity.exe -batchmode -quit -projectPath src -executeMethod ChildGame.EditorTools.CrayonSheet.Render</c>
    /// </summary>
    public static class CrayonSheet
    {
        private static readonly Color Red = new Color32(214, 40, 40, 255);
        private static readonly Color Yellow = new Color32(250, 210, 40, 255);
        private static readonly Color Blue = new Color32(40, 90, 200, 255);
        private static readonly Color Green = new Color32(60, 160, 70, 255);
        private static readonly Color Brown = new Color32(130, 80, 40, 255);

        public static void Render()
        {
            var c = new CrayonCanvas(1024, 640, 7);

            // Row 1: light vs hard zigzags.
            Zigzag(c, new Vector2(40, 560), 200, 50, Red, 0.72f);
            Zigzag(c, new Vector2(280, 560), 200, 50, Red, 1.05f);
            // Build-up: 1, 2, 4, 8 passes of the same hatch.
            for (var k = 0; k < 4; k++)
            {
                var passes = 1 << k;
                for (var p = 0; p < passes; p++) Hatch(c, new Rect(530 + k * 120, 470, 100, 120), Blue, 0.85f, 7f, p * 2.3f);
            }
            // Row 2: a coloured-in sun: yellow fill, blue over half of it (green where they overlap), pencil outline.
            Hatch(c, new Rect(40, 250, 240, 170), Yellow, 0.9f, 6f, 0);
            Hatch(c, new Rect(160, 250, 240, 170), Blue, 0.85f, 6f, 1.3f);
            Circle(c, new Vector2(520, 330), 85, Yellow, 0.95f, true);
            ScribbleFill(c, new Vector2(520, 330), 80, Yellow);
            Rays(c, new Vector2(520, 330), 100, 150, new Color32(240, 140, 30, 255));
            // Pencil: a plain line, and a line that crosses a green wax patch (wax resists graphite).
            Hatch(c, new Rect(700, 250, 280, 160), Green, 0.95f, 5f, 0);
            for (var k = 0; k < 3; k++)
            {
                c.BeginStroke();
                Line(c, new Vector2(660, 300 + k * 40), new Vector2(1000, 330 + k * 40), (a, b) => c.Pencil(a, b, 0.9f + k * 0.08f));
            }
            // Row 3: eraser across pencil (lifts it) and across wax (smudges it).
            c.BeginStroke();
            Line(c, new Vector2(40, 140), new Vector2(400, 150), (a, b) => c.Pencil(a, b, 1f));
            Line(c, new Vector2(40, 110), new Vector2(400, 120), (a, b) => c.Pencil(a, b, 1f));
            Line(c, new Vector2(200, 80), new Vector2(240, 190), (a, b) => c.Erase(a, b));
            Hatch(c, new Rect(460, 40, 240, 150), Red, 0.95f, 6f, 0);
            Hatch(c, new Rect(560, 40, 240, 150), Brown, 0.9f, 6f, 0.7f);
            for (var k = 0; k < 4; k++) Line(c, new Vector2(470, 70 + k * 20), new Vector2(880, 90 + k * 20), (a, b) => c.Erase(a, b));
            c.Flush();

            var root = Path.GetFullPath("../logs");
            Directory.CreateDirectory(root);
            File.WriteAllBytes(Path.Combine(root, "crayon-sheet.png"), c.Texture.EncodeToPNG());
            File.WriteAllBytes(Path.Combine(root, "crayon-zoom.png"), Zoom(c.Texture, new RectInt(100, 230, 340, 220), 3).EncodeToPNG());
            Debug.Log($"[CHILD-CRAYON] Sheet written to {root} (coverage {c.Coverage:P0}, colours {c.ColorsUsed})");
        }

        private delegate void Seg(Vector2 a, Vector2 b);

        private static void Line(CrayonCanvas c, Vector2 a, Vector2 b, Seg seg)
        {
            const int n = 30;
            var prev = a;
            for (var i = 1; i <= n; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)n) + new Vector2(0, Mathf.Sin(i * 0.7f) * 1.2f);
                seg(prev, p);
                prev = p;
            }
        }

        private static void Zigzag(CrayonCanvas c, Vector2 start, float width, float height, Color col, float pressure)
        {
            c.BeginStroke();
            var prev = start;
            for (var i = 1; i <= 16; i++)
            {
                var p = start + new Vector2(width * i / 16f, (i % 2 == 0 ? 0 : -height));
                Line(c, prev, p, (a, b) => c.Crayon(a, b, col, pressure));
                prev = p;
            }
        }

        /// <summary>Back-and-forth colouring like a child fills an area (diagonal strokes, slightly uneven).</summary>
        private static void Hatch(CrayonCanvas c, Rect r, Color col, float pressure, float spacing, float phase)
        {
            c.BeginStroke();
            var up = true;
            for (var x = r.xMin - r.height + phase; x < r.xMax; x += spacing)
            {
                var a = new Vector2(x, r.yMin);
                var b = new Vector2(x + r.height * 0.9f, r.yMax);
                ClipToRect(ref a, ref b, r);
                if (a == b) continue;
                if (up) Line(c, a, b, (p, q) => c.Crayon(p, q, col, pressure));
                else Line(c, b, a, (p, q) => c.Crayon(p, q, col, pressure));
                up = !up;
            }
        }

        private static void ClipToRect(ref Vector2 a, ref Vector2 b, Rect r)
        {
            var d = b - a;
            float t0 = 0, t1 = 1;
            if (a.x < r.xMin) t0 = Mathf.Max(t0, (r.xMin - a.x) / d.x);
            if (b.x > r.xMax) t1 = Mathf.Min(t1, (r.xMax - a.x) / d.x);
            if (t0 >= t1) { b = a; return; }
            var na = a + d * t0;
            var nb = a + d * t1;
            a = na;
            b = nb;
        }

        private static void Circle(CrayonCanvas c, Vector2 center, float radius, Color col, float pressure, bool wobble)
        {
            c.BeginStroke();
            var prev = center + new Vector2(radius, 0);
            for (var i = 1; i <= 40; i++)
            {
                var ang = i / 40f * Mathf.PI * 2.08f;
                var rr = radius * (wobble ? 1f + Mathf.Sin(ang * 3f) * 0.04f : 1f);
                var p = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rr;
                c.Crayon(prev, p, col, pressure);
                prev = p;
            }
        }

        private static void ScribbleFill(CrayonCanvas c, Vector2 center, float radius, Color col)
        {
            c.BeginStroke();
            var prev = center + new Vector2(-radius, 0);
            for (var i = 0; i < 70; i++)
            {
                var y = -radius + i * radius * 2f / 70f;
                var half = Mathf.Sqrt(Mathf.Max(0, radius * radius - y * y)) * 0.95f;
                var p = center + new Vector2(i % 2 == 0 ? half : -half, y);
                c.Crayon(prev, p, col, 0.9f);
                prev = p;
            }
        }

        private static void Rays(CrayonCanvas c, Vector2 center, float r0, float r1, Color col)
        {
            for (var i = 0; i < 10; i++)
            {
                c.BeginStroke();
                var ang = i / 10f * Mathf.PI * 2f;
                var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Line(c, center + d * r0, center + d * r1, (a, b) => c.Crayon(a, b, col, 1f));
            }
        }

        private static Texture2D Zoom(Texture2D src, RectInt r, int scale)
        {
            var dst = new Texture2D(r.width * scale, r.height * scale, TextureFormat.RGBA32, false);
            for (var y = 0; y < dst.height; y++)
            for (var x = 0; x < dst.width; x++)
                dst.SetPixel(x, y, src.GetPixel(r.x + x / scale, r.y + y / scale));
            dst.Apply();
            return dst;
        }
    }
}
