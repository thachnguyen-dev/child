using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace ChildGame.MiniGames.VeTranh
{
    /// <summary>
    /// A sheet of drawing paper that takes wax crayon, pencil and eraser (MG01). The owner's key requirement is that
    /// colouring must feel like real wax crayons, not digital fills. The model follows the physically-inspired crayon
    /// simulations of Rudolf, Mould and Neufeld (paper as a height field, deposition from contact and pressure,
    /// smearing of deposited wax), simplified to run in real time on a phone:
    /// <list type="bullet">
    /// <item><b>Paper tooth</b>: a generated height field (fine grain, mid bumps, fibres). Wax only catches where the
    /// crayon tip reaches the paper surface, so a light pass is speckled and the paper shows through.</item>
    /// <item><b>Build-up</b>: deposited wax fills the valleys, so more passes give denser colour; wax saturates, so heavy
    /// layers stop taking more (burnishing).</item>
    /// <item><b>Worn tip</b>: a blunt round tip with streaks along the stroke direction and a slightly ragged edge.</item>
    /// <item><b>Layering</b>: colours mix subtractively (absorbance averaging, a cheap stand-in for Kubelka–Munk): yellow
    /// over blue goes green, the newest layer dominates. Thick wax gets a faint sheen.</item>
    /// <item><b>Pencil</b>: thin graphite that catches on the grain; wax resists it.</item>
    /// <item><b>Eraser</b>: lifts graphite, but only smears and smudges wax (owner rule).</item>
    /// </list>
    /// Coordinates are canvas pixels (0..Width, 0..Height).
    /// </summary>
    public sealed class CrayonCanvas
    {
        public readonly int Width, Height;
        public Texture2D Texture { get; }

        // Placeholder tuning (feel).
        public float CrayonRadius = 7.5f;
        public float PencilRadius = 2.3f;
        public float EraserRadius = 13f;
        public float CrayonRate = 0.075f;
        public float PencilRate = 0.38f;

        private readonly float[] tooth, wax, absR, absG, absB, graphite;
        private readonly Color32 paper = new Color32(251, 249, 242, 255);
        private int coveredCount;
        private RectInt dirty;
        private bool hasDirty;
        private readonly float[] stripes = new float[64];
        private System.Random rng;
        private readonly HashSet<Color32> colorsUsed = new HashSet<Color32>();

        /// <summary>Share of the sheet with visible marks (0..1).</summary>
        public float Coverage => coveredCount / (float)(Width * Height);
        public int ColorsUsed => colorsUsed.Count;

        public CrayonCanvas(int width, int height, int seed)
        {
            Width = width;
            Height = height;
            rng = new System.Random(seed);
            var n = width * height;
            tooth = new float[n];
            wax = new float[n];
            absR = new float[n];
            absG = new float[n];
            absB = new float[n];
            graphite = new float[n];
            MakeTooth(seed);
            Texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            MarkDirty(0, 0, width - 1, height - 1);
            Flush();
        }

        // ---- Paper ----

        private void MakeTooth(int seed)
        {
            // Several rotated octaves hide the noise grid; fibres are long, thin and run roughly one way.
            var fine = ValueNoise(seed, 1.7f, 1f, 25f);
            var fine2 = ValueNoise(seed + 3, 2.6f, 1f, -40f);
            var mid = ValueNoise(seed + 1, 6.5f, 1f, 10f);
            var fibre = ValueNoise(seed + 2, 3f, 5f, 8f);
            var min = float.MaxValue;
            var max = float.MinValue;
            for (var i = 0; i < tooth.Length; i++)
            {
                var v = 0.32f * fine[i] + 0.26f * fine2[i] + 0.24f * mid[i] + 0.18f * fibre[i];
                tooth[i] = v;
                if (v < min) min = v;
                if (v > max) max = v;
            }
            for (var i = 0; i < tooth.Length; i++)
            {
                var t = (tooth[i] - min) / (max - min);
                tooth[i] = Mathf.SmoothStep(0f, 1f, t); // a little contrast: clear peaks and valleys
            }
        }

        /// <summary>
        /// Smooth value noise with the given cell size, rotated by <paramref name="angle"/> degrees (stretch &gt; 1
        /// elongates it along the rotated x axis).
        /// </summary>
        private float[] ValueNoise(int seed, float cell, float stretch, float angle)
        {
            var r = new System.Random(seed);
            var cos = Mathf.Cos(angle * Mathf.Deg2Rad);
            var sin = Mathf.Sin(angle * Mathf.Deg2Rad);
            var span = 3 * (Width + Height);
            var gw = Mathf.CeilToInt(span / (cell * stretch)) + 3;
            var gh = Mathf.CeilToInt(span / cell) + 3;
            var grid = new float[gw * gh];
            for (var i = 0; i < grid.Length; i++) grid[i] = (float)r.NextDouble();
            var outp = new float[Width * Height];
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    // Rotate into the noise grid, offset so coordinates stay positive.
                    var rx = x * cos - y * sin + Width + Height;
                    var ry = x * sin + y * cos + Width + Height;
                    var gx = rx / (cell * stretch);
                    var gy = ry / cell;
                    var x0 = (int)gx;
                    var y0 = (int)gy;
                    var fx = gx - x0;
                    var fy = gy - y0;
                    fx = fx * fx * fx * (fx * (fx * 6 - 15) + 10);
                    fy = fy * fy * fy * (fy * (fy * 6 - 15) + 10);
                    var a = grid[y0 * gw + x0];
                    var b = grid[y0 * gw + x0 + 1];
                    var c = grid[(y0 + 1) * gw + x0];
                    var d = grid[(y0 + 1) * gw + x0 + 1];
                    outp[y * Width + x] = Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
                }
            }
            return outp;
        }

        // ---- Tools ----

        /// <summary>Starts a new stroke: a fresh pattern of streaks for the worn crayon tip.</summary>
        public void BeginStroke()
        {
            // Streaks: a smoothed random profile across the tip (the worn crayon face is uneven).
            var raw = new float[stripes.Length];
            for (var i = 0; i < raw.Length; i++) raw[i] = (float)rng.NextDouble();
            for (var i = 0; i < stripes.Length; i++)
            {
                var a = raw[Mathf.Max(0, i - 2)] + raw[Mathf.Max(0, i - 1)] + raw[i] + raw[Mathf.Min(63, i + 1)] + raw[Mathf.Min(63, i + 2)];
                stripes[i] = a / 5f;
            }
            strokeDistance = 0f;
            strokePhase = (float)rng.NextDouble() * 10f;
        }

        private float strokeDistance, strokePhase;

        /// <summary>Wax crayon from a to b. <paramref name="pressure"/> ≈ 0.6 (light) … 1.1 (hard).</summary>
        public void Crayon(Vector2 a, Vector2 b, Color color, float pressure)
        {
            colorsUsed.Add(color);
            var ar = -Mathf.Log(Mathf.Max(color.r, 0.03f));
            var ag = -Mathf.Log(Mathf.Max(color.g, 0.03f));
            var ab = -Mathf.Log(Mathf.Max(color.b, 0.03f));
            var dir = b - a;
            var len = dir.magnitude;
            var normal = len > 1e-4f ? new Vector2(-dir.y, dir.x) / len : Vector2.up;
            var forward = len > 1e-4f ? dir / len : Vector2.right;
            // Wax is dragged a little past each bump of the paper: sample the tooth behind the pixel along the stroke.
            var back1 = -Mathf.RoundToInt(forward.x * 1.6f) - Mathf.RoundToInt(forward.y * 1.6f) * Width;
            var back2 = -Mathf.RoundToInt(forward.x * 3.2f) - Mathf.RoundToInt(forward.y * 3.2f) * Width;
            var back3 = -Mathf.RoundToInt(forward.x * 4.8f) - Mathf.RoundToInt(forward.y * 4.8f) * Width;
            var n = tooth.Length;
            var steps = Mathf.Max(1, Mathf.CeilToInt(len / 1.6f));
            var r = CrayonRadius;
            for (var s = 0; s <= steps; s++)
            {
                var c = Vector2.Lerp(a, b, s / (float)steps);
                // A hand never presses evenly: pressure drifts along the stroke.
                var along = strokeDistance + len * s / steps;
                var press = pressure * (0.86f + 0.1f * Mathf.Sin(along * 0.045f + strokePhase) + 0.06f * Mathf.Sin(along * 0.17f + strokePhase * 2f));
                ForEach(c, r, (i, px, py, d) =>
                {
                    // Blunt tip profile with a ragged edge, streaked along the stroke.
                    var offset = Vector2.Dot(new Vector2(px - c.x, py - c.y), normal) / r; // -1..1 across the stroke
                    var s0 = stripes[Mathf.Clamp((int)((offset * 0.5f + 0.5f) * 63f), 0, 63)];
                    var stripe = 0.5f + 0.75f * s0 * s0;
                    var profile = 1f - Mathf.Pow(d, 5f);
                    var t1 = tooth[Mathf.Clamp(i + back1, 0, n - 1)];
                    var t2 = tooth[Mathf.Clamp(i + back2, 0, n - 1)];
                    var t3 = tooth[Mathf.Clamp(i + back3, 0, n - 1)];
                    var dragged = Mathf.Max(tooth[i], Mathf.Max(t1 * 0.93f, Mathf.Max(t2 * 0.86f, t3 * 0.78f)));
                    var surface = dragged + wax[i] * 0.55f; // wax fills the valleys: later passes reach deeper
                    var contact = press * profile * stripe - (1f - surface) * 0.82f;
                    if (contact <= 0f) return;
                    var dep = contact * CrayonRate * (1f - Mathf.Clamp01(wax[i] / 1.7f)); // saturation = burnishing
                    if (dep <= 0f) return;
                    var w = wax[i];
                    var oldWeight = w * 0.55f; // the newest layer dominates
                    var total = oldWeight + dep;
                    absR[i] = (absR[i] * oldWeight + ar * dep) / total;
                    absG[i] = (absG[i] * oldWeight + ag * dep) / total;
                    absB[i] = (absB[i] * oldWeight + ab * dep) / total;
                    var was = IsCovered(i);
                    wax[i] = w + dep;
                    Track(i, was);
                });
            }
            strokeDistance += len;
            // Now and then a tiny crumb of wax drops beside the stroke.
            if (len > 3f && rng.NextDouble() < 0.04 * pressure)
            {
                var p = b + normal * (float)(rng.NextDouble() * 2 - 1) * r * 1.4f;
                ForEach(p, 1.4f, (i, px, py, d) =>
                {
                    var was = IsCovered(i);
                    absR[i] = ar;
                    absG[i] = ag;
                    absB[i] = ab;
                    wax[i] = Mathf.Max(wax[i], 0.9f);
                    Track(i, was);
                });
            }
        }

        /// <summary>Pencil from a to b: thin graphite that catches on the grain and skips over wax.</summary>
        public void Pencil(Vector2 a, Vector2 b, float pressure)
        {
            var len = Vector2.Distance(a, b);
            var steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.8f));
            for (var s = 0; s <= steps; s++)
            {
                var c = Vector2.Lerp(a, b, s / (float)steps);
                ForEach(c, PencilRadius, (i, px, py, d) =>
                {
                    var contact = pressure * (1f - d * d) - (1f - tooth[i]) * 0.55f;
                    if (contact <= 0f) return;
                    var dep = contact * PencilRate * (1f - Mathf.Clamp01(wax[i] * 0.7f)) * (1f - graphite[i] * 0.6f);
                    var was = IsCovered(i);
                    graphite[i] = Mathf.Min(1.2f, graphite[i] + dep);
                    Track(i, was);
                });
            }
        }

        /// <summary>Eraser from a to b: lifts pencil; smears crayon along the rub instead of removing it.</summary>
        public void Erase(Vector2 a, Vector2 b)
        {
            var dir = b - a;
            var len = dir.magnitude;
            if (len < 0.5f) return;
            dir /= len;
            var steps = Mathf.Max(1, Mathf.CeilToInt(len / 3f));
            var r = EraserRadius;
            for (var s = 0; s <= steps; s++)
            {
                var c = Vector2.Lerp(a, b, s / (float)steps);
                ForEach(c, r, (i, px, py, d) =>
                {
                    var profile = 1f - d * d;
                    var was = IsCovered(i);
                    graphite[i] *= 1f - 0.4f * profile;
                    // Pull wax from behind the eraser into this pixel: a smudge in the rubbing direction.
                    var sx = Mathf.Clamp(Mathf.RoundToInt(px - dir.x * 5f), 0, Width - 1);
                    var sy = Mathf.Clamp(Mathf.RoundToInt(py - dir.y * 5f), 0, Height - 1);
                    var j = sy * Width + sx;
                    var k = 0.3f * profile;
                    var src = wax[j];
                    if (src > 0.01f)
                    {
                        var mixW = wax[i] + src * k;
                        absR[i] = (absR[i] * wax[i] + absR[j] * src * k) / mixW;
                        absG[i] = (absG[i] * wax[i] + absG[j] * src * k) / mixW;
                        absB[i] = (absB[i] * wax[i] + absB[j] * src * k) / mixW;
                    }
                    var target = Mathf.Lerp(wax[i], src, k) * (1f - 0.012f * profile); // spread, slightly lifted
                    wax[i] = target;
                    Track(i, was);
                });
            }
        }

        private delegate void PixelOp(int index, int x, int y, float normalizedDistance);

        private void ForEach(Vector2 c, float r, PixelOp op)
        {
            var x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - r));
            var x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(c.x + r));
            var y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - r));
            var y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(c.y + r));
            if (x0 > x1 || y0 > y1) return;
            for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                var dx = x + 0.5f - c.x;
                var dy = y + 0.5f - c.y;
                var d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                if (d > 1f) continue;
                op(y * Width + x, x, y, d);
            }
            MarkDirty(x0, y0, x1, y1);
        }

        private bool IsCovered(int i) => wax[i] > 0.06f || graphite[i] > 0.06f;

        private void Track(int i, bool was)
        {
            var now = IsCovered(i);
            if (was != now) coveredCount += now ? 1 : -1;
        }

        private void MarkDirty(int x0, int y0, int x1, int y1)
        {
            if (!hasDirty)
            {
                dirty = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
                hasDirty = true;
                return;
            }
            var minX = Mathf.Min(dirty.xMin, x0);
            var minY = Mathf.Min(dirty.yMin, y0);
            var maxX = Mathf.Max(dirty.xMax - 1, x1);
            var maxY = Mathf.Max(dirty.yMax - 1, y1);
            dirty = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        // ---- Judging ----

        /// <summary>
        /// Measures the sheet for the offline judge: how much is marked, coloured and pencilled, which crayon colours
        /// show (layered wax is matched to the nearest crayon, so yellow over blue counts as green), how far the drawing
        /// spreads and how the colour splits between the upper and lower half. Samples every other pixel.
        /// </summary>
        public DrawingStats Analyze()
        {
            const int step = 2;
            var stats = new DrawingStats { colour = new float[CrayonBox.Colors.Length] };
            var xs = new List<int>();
            var ys = new List<int>();
            int samples = 0, marked = 0, coloured = 0, pencilled = 0, upper = 0, lower = 0;
            var counts = new int[CrayonBox.Colors.Length];
            for (var y = 0; y < Height; y += step)
            for (var x = 0; x < Width; x += step)
            {
                samples++;
                var i = y * Width + x;
                if (!IsCovered(i)) continue;
                marked++;
                xs.Add(x);
                ys.Add(y);
                if (graphite[i] > 0.15f) pencilled++;
                if (wax[i] < SolidWax) continue;
                coloured++;
                if (y >= Height / 2) upper++;
                else lower++;
                counts[NearestCrayon(Mathf.Exp(-absR[i]), Mathf.Exp(-absG[i]), Mathf.Exp(-absB[i]))]++;
            }
            stats.marked = marked / (float)samples;
            stats.coloured = coloured / (float)samples;
            stats.pencil = pencilled / (float)samples;
            stats.upperColour = upper / (float)samples * 2f;
            stats.lowerColour = lower / (float)samples * 2f;
            for (var c = 0; c < counts.Length; c++) stats.colour[c] = counts[c] / (float)samples;
            if (xs.Count > 0)
            {
                // Ignore the outermost 2% of marks, so a stray dot does not stretch the drawing.
                xs.Sort();
                ys.Sort();
                var lo = xs.Count / 50;
                var hi = xs.Count - 1 - lo;
                stats.width = (xs[hi] - xs[lo]) / (float)Width;
                stats.height = (ys[hi] - ys[lo]) / (float)Height;
            }
            return stats;
        }

        /// <summary>Wax this thick reads as coloured in, not a stray speck.</summary>
        private const float SolidWax = 0.2f;

        private static int NearestCrayon(float r, float g, float b)
        {
            var best = 0;
            var bestD = float.MaxValue;
            for (var c = 0; c < CrayonBox.Colors.Length; c++)
            {
                Color k = CrayonBox.Colors[c];
                var d = (k.r - r) * (k.r - r) + (k.g - g) * (k.g - g) + (k.b - b) * (k.b - b);
                if (d < bestD)
                {
                    bestD = d;
                    best = c;
                }
            }
            return best;
        }

        // ---- Look ----

        /// <summary>Recomposes the changed area and uploads the texture. Call once per frame.</summary>
        public void Flush()
        {
            if (!hasDirty) return;
            hasDirty = false;
            var data = Texture.GetPixelData<Color32>(0);
            var graphiteColor = new Vector3(0.27f, 0.27f, 0.3f);
            for (var y = dirty.yMin; y < dirty.yMax; y++)
            for (var x = dirty.xMin; x < dirty.xMax; x++)
            {
                var i = y * Width + x;
                var t = tooth[i];
                // Paper: a hint of its grain.
                var shade = 0.955f + 0.045f * t;
                var col = new Vector3(paper.r / 255f * shade, paper.g / 255f * shade, paper.b / 255f * shade);
                var w = wax[i];
                if (w > 0.001f)
                {
                    var cover = 1f - Mathf.Exp(-3.3f * w);
                    var wc = new Vector3(Mathf.Exp(-absR[i]), Mathf.Exp(-absG[i]), Mathf.Exp(-absB[i]));
                    // Waxy look: slightly darker in the grain valleys, a faint sheen on thick wax at the peaks.
                    wc *= 0.93f + 0.07f * t;
                    var sheen = Mathf.Clamp01(w - 0.9f) * 0.10f * t;
                    col = Vector3.Lerp(col, wc, cover) + new Vector3(sheen, sheen, sheen);
                }
                var g = graphite[i];
                if (g > 0.001f)
                {
                    var gk = Mathf.Clamp01(g * 1.2f) * 0.85f;
                    col = Vector3.Lerp(col, graphiteColor, gk) + Vector3.one * (Mathf.Clamp01(g - 0.8f) * 0.06f);
                }
                data[i] = new Color32((byte)(Mathf.Clamp01(col.x) * 255), (byte)(Mathf.Clamp01(col.y) * 255), (byte)(Mathf.Clamp01(col.z) * 255), 255);
            }
            Texture.Apply(false);
        }
    }
}
