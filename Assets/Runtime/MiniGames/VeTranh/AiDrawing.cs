using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChildGame.MiniGames.VeTranh
{
    /// <summary>The 12-colour wax crayon box (owner: 12 basic colours).</summary>
    public static class CrayonBox
    {
        public static readonly Color32[] Colors =
        {
            new Color32(214, 40, 40, 255),   // red
            new Color32(240, 130, 30, 255),  // orange
            new Color32(250, 210, 40, 255),  // yellow
            new Color32(160, 200, 50, 255),  // yellow-green
            new Color32(50, 150, 65, 255),   // green
            new Color32(90, 180, 230, 255),  // sky blue
            new Color32(40, 85, 195, 255),   // blue
            new Color32(130, 70, 170, 255),  // purple
            new Color32(240, 130, 170, 255), // pink
            new Color32(130, 80, 40, 255),   // brown
            new Color32(35, 35, 40, 255),    // black
            new Color32(250, 250, 248, 255), // white
        };

        public static readonly string[] Keys =
        {
            "red", "orange", "yellow", "lime", "green", "sky", "blue", "purple", "pink", "brown", "black", "white",
        };

        public static Color32 Get(string key) => Colors[Array.IndexOf(Keys, key)];
    }

    public enum DrawTool { Pencil, Crayon, Eraser }

    /// <summary>One stroke of the AI child's drawing, in normalised canvas coordinates (0..1).</summary>
    public class AiStroke
    {
        public DrawTool tool;
        public Color32 color;
        public List<Vector2> points = new List<Vector2>();
        public float Length
        {
            get
            {
                var l = 0f;
                for (var i = 1; i < points.Count; i++) l += Vector2.Distance(points[i - 1], points[i]);
                return l;
            }
        }
    }

    /// <summary>
    /// Drawing prompts and the AI child's childlike drawings of them: wobbly pencil outlines, then crayon scribbles
    /// filling the shapes (MG01). Each drawing is built from simple shapes with random jitter, so no two are the same.
    /// Prompts: Easy = single simple objects, Normal = more complex objects, Hard = whole scenes (owner design §7).
    /// </summary>
    public static class AiDrawing
    {
        public static readonly string[][] Prompts =
        {
            new[] { "fish", "tree", "sun", "flower", "butterfly" },
            new[] { "house", "car", "boat", "person", "bicycle" },
            new[] { "riverhouse", "ricefield", "sea" },
        };

        public static string RandomPrompt(Difficulty d, System.Random rng)
        {
            var list = Prompts[(int)d];
            return list[rng.Next(list.Length)];
        }

        public static List<AiStroke> Build(string prompt, System.Random rng)
        {
            var b = new Builder(rng);
            switch (prompt)
            {
                case "fish": Fish(b, new Vector2(0.5f, 0.5f), 1f); break;
                case "tree": Tree(b, new Vector2(0.5f, 0.12f), 1f); break;
                case "sun": Sun(b, new Vector2(0.5f, 0.52f), 1f); break;
                case "flower": Flower(b, new Vector2(0.5f, 0.1f), 1f); break;
                case "butterfly": Butterfly(b, new Vector2(0.5f, 0.5f), 1f); break;
                case "house": House(b, new Vector2(0.5f, 0.12f), 1f); break;
                case "car": Car(b, new Vector2(0.5f, 0.2f), 1f); break;
                case "boat": Water(b, 0.3f); Boat(b, new Vector2(0.5f, 0.3f), 1f); break;
                case "person": Person(b, new Vector2(0.5f, 0.08f), 1f); break;
                case "bicycle": Bicycle(b, new Vector2(0.5f, 0.3f), 1f); break;
                case "riverhouse":
                    Sky(b, 0.55f);
                    Sun(b, new Vector2(0.85f, 0.8f), 0.45f);
                    Water(b, 0.22f);
                    House(b, new Vector2(0.32f, 0.24f), 0.62f);
                    Tree(b, new Vector2(0.72f, 0.24f), 0.6f);
                    Grass(b, 0.22f, 0.3f);
                    break;
                case "ricefield":
                    Sky(b, 0.5f);
                    Sun(b, new Vector2(0.2f, 0.78f), 0.45f);
                    Cloud(b, new Vector2(0.62f, 0.8f), 0.6f);
                    Field(b);
                    Bird(b, new Vector2(0.45f, 0.72f));
                    Bird(b, new Vector2(0.55f, 0.66f));
                    Tree(b, new Vector2(0.85f, 0.48f), 0.45f);
                    break;
                default: // sea
                    Sky(b, 0.45f);
                    Sun(b, new Vector2(0.78f, 0.78f), 0.45f);
                    Cloud(b, new Vector2(0.3f, 0.8f), 0.55f);
                    Water(b, 0.45f);
                    Boat(b, new Vector2(0.42f, 0.42f), 0.7f);
                    Bird(b, new Vector2(0.6f, 0.65f));
                    break;
            }
            return b.Strokes;
        }

        // ---- Subjects (positions are the base point of each subject, scale 1 = the full sheet) ----

        private static void Fish(Builder b, Vector2 c, float s)
        {
            var body = b.Ellipse(c, 0.26f * s, 0.16f * s);
            var tail = new List<Vector2> { c + new Vector2(0.22f, 0) * s, c + new Vector2(0.38f, 0.13f) * s, c + new Vector2(0.38f, -0.13f) * s };
            b.Outline(body);
            b.Outline(tail, true);
            b.Fill(body, "orange");
            b.Fill(tail, "red");
            b.Dot(c + new Vector2(-0.14f, 0.04f) * s, 0.02f * s, "black");
            b.Line(c + new Vector2(-0.02f, 0.13f) * s, c + new Vector2(-0.02f, -0.13f) * s, DrawTool.Pencil, "black");
            b.Fill(b.Ellipse(c + new Vector2(0.02f, 0.12f) * s, 0.1f * s, 0.05f * s), "yellow");
        }

        private static void Tree(Builder b, Vector2 foot, float s)
        {
            var trunk = b.Rect(foot + new Vector2(-0.05f, 0) * s, foot + new Vector2(0.05f, 0.35f) * s);
            b.Outline(trunk, true);
            b.Fill(trunk, "brown");
            var crown = b.Blob(foot + new Vector2(0, 0.55f) * s, 0.22f * s, 0.24f * s, 7);
            b.Outline(crown);
            b.Fill(crown, "green");
            b.Fill(b.Ellipse(foot + new Vector2(-0.06f, 0.6f) * s, 0.08f * s, 0.07f * s), "lime");
        }

        private static void Sun(Builder b, Vector2 c, float s)
        {
            var disc = b.Ellipse(c, 0.17f * s, 0.17f * s * 1.6f);
            b.Outline(disc, false, "orange");
            b.Fill(disc, "yellow");
            for (var i = 0; i < 10; i++)
            {
                var a = i / 10f * Mathf.PI * 2f + (float)b.Rng.NextDouble() * 0.2f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 1.6f);
                b.Line(c + d * 0.21f * s, c + d * 0.31f * s, DrawTool.Crayon, "orange");
            }
            b.Dot(c + new Vector2(-0.05f, 0.05f) * s, 0.015f * s, "black");
            b.Dot(c + new Vector2(0.05f, 0.05f) * s, 0.015f * s, "black");
            b.Arc(c + new Vector2(0, -0.01f) * s, 0.07f * s, 200f, 340f, "red");
        }

        private static void Flower(Builder b, Vector2 foot, float s)
        {
            b.Line(foot, foot + new Vector2(0, 0.5f) * s, DrawTool.Crayon, "green");
            var leaf = b.Ellipse(foot + new Vector2(0.07f, 0.2f) * s, 0.07f * s, 0.04f * s);
            b.Fill(leaf, "green");
            var center = foot + new Vector2(0, 0.62f) * s;
            for (var i = 0; i < 6; i++)
            {
                var a = i / 6f * Mathf.PI * 2f;
                var petal = b.Ellipse(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 1.6f) * 0.11f * s, 0.07f * s, 0.1f * s);
                b.Outline(petal);
                b.Fill(petal, "pink");
            }
            var mid = b.Ellipse(center, 0.06f * s, 0.09f * s);
            b.Fill(mid, "yellow");
        }

        private static void Butterfly(Builder b, Vector2 c, float s)
        {
            foreach (var side in new[] { -1f, 1f })
            {
                var top = b.Ellipse(c + new Vector2(side * 0.15f, 0.12f) * s, 0.14f * s, 0.2f * s);
                var bottom = b.Ellipse(c + new Vector2(side * 0.12f, -0.14f) * s, 0.1f * s, 0.14f * s);
                b.Outline(top);
                b.Outline(bottom);
                b.Fill(top, "sky");
                b.Fill(bottom, "purple");
                b.Dot(c + new Vector2(side * 0.16f, 0.14f) * s, 0.03f * s, "yellow");
            }
            var body = b.Ellipse(c, 0.025f * s, 0.25f * s);
            b.Fill(body, "black");
            b.Line(c + new Vector2(0, 0.24f) * s, c + new Vector2(-0.06f, 0.36f) * s, DrawTool.Pencil, "black");
            b.Line(c + new Vector2(0, 0.24f) * s, c + new Vector2(0.06f, 0.36f) * s, DrawTool.Pencil, "black");
        }

        private static void House(Builder b, Vector2 foot, float s)
        {
            var wall = b.Rect(foot + new Vector2(-0.22f, 0) * s, foot + new Vector2(0.22f, 0.42f) * s);
            var roof = new List<Vector2> { foot + new Vector2(-0.28f, 0.42f) * s, foot + new Vector2(0, 0.74f) * s, foot + new Vector2(0.28f, 0.42f) * s };
            var door = b.Rect(foot + new Vector2(-0.05f, 0) * s, foot + new Vector2(0.06f, 0.22f) * s);
            var window = b.Rect(foot + new Vector2(0.1f, 0.22f) * s, foot + new Vector2(0.18f, 0.32f) * s);
            b.Outline(wall, true);
            b.Outline(roof, true);
            b.Fill(wall, "yellow");
            b.Fill(roof, "red");
            b.Outline(door, true);
            b.Fill(door, "brown");
            b.Outline(window, true);
            b.Fill(window, "sky");
        }

        private static void Car(Builder b, Vector2 foot, float s)
        {
            var body = b.Rect(foot + new Vector2(-0.32f, 0.08f) * s, foot + new Vector2(0.32f, 0.28f) * s);
            var cabin = new List<Vector2> { foot + new Vector2(-0.18f, 0.28f) * s, foot + new Vector2(-0.1f, 0.46f) * s, foot + new Vector2(0.14f, 0.46f) * s, foot + new Vector2(0.22f, 0.28f) * s };
            b.Outline(body, true);
            b.Outline(cabin, true);
            b.Fill(body, "blue");
            b.Fill(cabin, "sky");
            foreach (var x in new[] { -0.2f, 0.2f })
            {
                var wheel = b.Ellipse(foot + new Vector2(x, 0.08f) * s, 0.07f * s, 0.11f * s);
                b.Outline(wheel);
                b.Fill(wheel, "black");
            }
        }

        private static void Boat(Builder b, Vector2 foot, float s)
        {
            var hull = new List<Vector2> { foot + new Vector2(-0.3f, 0.1f) * s, foot + new Vector2(0.3f, 0.1f) * s, foot + new Vector2(0.2f, -0.04f) * s, foot + new Vector2(-0.2f, -0.04f) * s };
            var sail = new List<Vector2> { foot + new Vector2(0.02f, 0.14f) * s, foot + new Vector2(0.02f, 0.6f) * s, foot + new Vector2(0.24f, 0.14f) * s };
            b.Outline(hull, true);
            b.Fill(hull, "brown");
            b.Line(foot + new Vector2(0, 0.1f) * s, foot + new Vector2(0, 0.62f) * s, DrawTool.Pencil, "black");
            b.Outline(sail, true);
            b.Fill(sail, "red");
        }

        private static void Person(Builder b, Vector2 foot, float s)
        {
            var head = b.Ellipse(foot + new Vector2(0, 0.74f) * s, 0.09f * s, 0.14f * s);
            b.Outline(head);
            b.Fill(head, "orange");
            var shirt = b.Rect(foot + new Vector2(-0.1f, 0.32f) * s, foot + new Vector2(0.1f, 0.6f) * s);
            b.Outline(shirt, true);
            b.Fill(shirt, "red");
            b.Line(foot + new Vector2(-0.1f, 0.55f) * s, foot + new Vector2(-0.22f, 0.4f) * s, DrawTool.Crayon, "orange");
            b.Line(foot + new Vector2(0.1f, 0.55f) * s, foot + new Vector2(0.22f, 0.4f) * s, DrawTool.Crayon, "orange");
            b.Line(foot + new Vector2(-0.05f, 0.32f) * s, foot + new Vector2(-0.07f, 0f) * s, DrawTool.Crayon, "blue");
            b.Line(foot + new Vector2(0.05f, 0.32f) * s, foot + new Vector2(0.07f, 0f) * s, DrawTool.Crayon, "blue");
            b.Dot(foot + new Vector2(-0.03f, 0.77f) * s, 0.012f * s, "black");
            b.Dot(foot + new Vector2(0.03f, 0.77f) * s, 0.012f * s, "black");
            b.Arc(foot + new Vector2(0, 0.73f) * s, 0.04f * s, 200f, 340f, "red");
            b.Fill(b.Ellipse(foot + new Vector2(0, 0.86f) * s, 0.09f * s, 0.05f * s), "black");
        }

        private static void Bicycle(Builder b, Vector2 foot, float s)
        {
            foreach (var x in new[] { -0.2f, 0.2f })
                b.Outline(b.Ellipse(foot + new Vector2(x, 0.1f) * s, 0.13f * s, 0.2f * s), false, "black");
            var a = foot + new Vector2(-0.2f, 0.1f) * s;
            var c = foot + new Vector2(0.2f, 0.1f) * s;
            var seat = foot + new Vector2(-0.05f, 0.38f) * s;
            var bar = foot + new Vector2(0.14f, 0.4f) * s;
            b.Line(a, seat, DrawTool.Crayon, "red");
            b.Line(seat, bar, DrawTool.Crayon, "red");
            b.Line(bar, c, DrawTool.Crayon, "red");
            b.Line(a, foot + new Vector2(0.03f, 0.1f) * s, DrawTool.Crayon, "red");
            b.Line(foot + new Vector2(0.03f, 0.1f) * s, seat, DrawTool.Crayon, "red");
            b.Line(bar, bar + new Vector2(0.05f, 0.08f) * s, DrawTool.Pencil, "black");
        }

        // ---- Scene parts ----

        private static void Sky(Builder b, float from) => b.Fill(b.Rect(new Vector2(0.02f, from), new Vector2(0.98f, 0.97f)), "sky", 0.03f);
        private static void Water(Builder b, float top) => b.Fill(b.Rect(new Vector2(0.02f, 0.03f), new Vector2(0.98f, top)), "blue", 0.03f);
        private static void Grass(Builder b, float bottom, float top) => b.Fill(b.Rect(new Vector2(0.02f, bottom), new Vector2(0.98f, top)), "green", 0.025f);

        private static void Field(Builder b)
        {
            b.Fill(b.Rect(new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.5f)), "lime", 0.025f);
            for (var y = 0.08f; y < 0.48f; y += 0.08f)
                b.Line(new Vector2(0.04f, y), new Vector2(0.96f, y + 0.02f), DrawTool.Crayon, "green");
        }

        private static void Cloud(Builder b, Vector2 c, float s)
        {
            var blob = b.Blob(c, 0.18f * s, 0.1f * s, 6);
            b.Outline(blob, false, "blue");
            b.Fill(blob, "white");
        }

        private static void Bird(Builder b, Vector2 c)
        {
            b.Arc(c + new Vector2(-0.025f, 0), 0.025f, 20f, 160f, "black");
            b.Arc(c + new Vector2(0.025f, 0), 0.025f, 20f, 160f, "black");
        }

        /// <summary>Accumulates strokes with a child's wobble.</summary>
        private class Builder
        {
            public readonly List<AiStroke> Strokes = new List<AiStroke>();
            public readonly System.Random Rng;

            public Builder(System.Random rng) => Rng = rng;

            private float J(float amount) => ((float)Rng.NextDouble() * 2f - 1f) * amount;

            public List<Vector2> Ellipse(Vector2 c, float rx, float ry)
            {
                var pts = new List<Vector2>();
                var start = J(0.5f);
                const int n = 28;
                for (var i = 0; i <= n + 2; i++) // overshoot: children close circles past the start
                {
                    var a = start + i / (float)n * Mathf.PI * 2f;
                    pts.Add(c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry) * (1f + J(0.05f)));
                }
                return pts;
            }

            public List<Vector2> Blob(Vector2 c, float rx, float ry, int lobes)
            {
                var pts = new List<Vector2>();
                const int n = 36;
                for (var i = 0; i <= n; i++)
                {
                    var a = i / (float)n * Mathf.PI * 2f;
                    var r = 1f + 0.18f * Mathf.Abs(Mathf.Sin(a * lobes * 0.5f));
                    pts.Add(c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry) * r);
                }
                return pts;
            }

            public List<Vector2> Rect(Vector2 min, Vector2 max)
            {
                return new List<Vector2>
                {
                    min + new Vector2(J(0.006f), J(0.006f)), new Vector2(max.x + J(0.006f), min.y + J(0.006f)),
                    max + new Vector2(J(0.006f), J(0.006f)), new Vector2(min.x + J(0.006f), max.y + J(0.006f)),
                };
            }

            public void Outline(List<Vector2> shape, bool closed = false, string color = "black")
            {
                var s = new AiStroke { tool = color == "black" ? DrawTool.Pencil : DrawTool.Crayon, color = CrayonBox.Get(color) };
                for (var i = 0; i < shape.Count; i++)
                {
                    var p = shape[i];
                    s.points.Add(p + new Vector2(J(0.004f), J(0.006f)));
                    // Straight edges get intermediate wobbly points.
                    if (shape.Count < 8)
                    {
                        var q = shape[(i + 1) % shape.Count];
                        if (!closed && i == shape.Count - 1) break;
                        for (var k = 1; k < 6; k++) s.points.Add(Vector2.Lerp(p, q, k / 6f) + new Vector2(J(0.004f), J(0.006f)));
                    }
                }
                if (closed && shape.Count >= 8) s.points.Add(shape[0]);
                if (closed && shape.Count < 8) s.points.Add(shape[0]);
                Strokes.Add(s);
            }

            public void Line(Vector2 a, Vector2 b, DrawTool tool, string color)
            {
                var s = new AiStroke { tool = tool, color = CrayonBox.Get(color) };
                for (var k = 0; k <= 8; k++) s.points.Add(Vector2.Lerp(a, b, k / 8f) + new Vector2(J(0.004f), J(0.005f)));
                Strokes.Add(s);
            }

            public void Arc(Vector2 c, float r, float fromDeg, float toDeg, string color)
            {
                var s = new AiStroke { tool = DrawTool.Crayon, color = CrayonBox.Get(color) };
                for (var k = 0; k <= 10; k++)
                {
                    var a = Mathf.Lerp(fromDeg, toDeg, k / 10f) * Mathf.Deg2Rad;
                    s.points.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 1.6f) * r);
                }
                Strokes.Add(s);
            }

            public void Dot(Vector2 c, float r, string color) => Fill(Ellipse(c, r, r * 1.6f), color, 0.008f);

            /// <summary>Back-and-forth scribble inside a closed shape, a little outside the lines like a child.</summary>
            public void Fill(List<Vector2> shape, string color, float spacing = 0.013f)
            {
                // Each shape is coloured at its own angle, like a child turning the crayon: work in a rotated frame.
                // (The sheet is 1.6× wider than tall, so x is scaled to keep angles true.)
                var angle = J(0.6f) + (Rng.NextDouble() < 0.35 ? 0.8f : 0f);
                var cos = Mathf.Cos(angle);
                var sin = Mathf.Sin(angle);
                var pivot = shape[0];
                Vector2 ToFrame(Vector2 p)
                {
                    var d = new Vector2((p.x - pivot.x) * 1.6f, p.y - pivot.y);
                    return new Vector2(d.x * cos + d.y * sin, -d.x * sin + d.y * cos);
                }
                Vector2 FromFrame(Vector2 q)
                {
                    var d = new Vector2(q.x * cos - q.y * sin, q.x * sin + q.y * cos);
                    return new Vector2(d.x / 1.6f + pivot.x, d.y + pivot.y);
                }
                var local = new List<Vector2>();
                foreach (var p in shape) local.Add(ToFrame(p));
                var min = local[0];
                var max = local[0];
                foreach (var p in local)
                {
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
                var s = new AiStroke { tool = DrawTool.Crayon, color = CrayonBox.Get(color) };
                var left = true;
                for (var y = min.y + spacing * 0.5f; y < max.y; y += spacing * (0.8f + (float)Rng.NextDouble() * 0.4f))
                {
                    var xs = Crossings(local, y);
                    if (xs.Count < 2) continue;
                    var x0 = xs[0] - 0.008f + J(0.008f);
                    var x1 = xs[xs.Count - 1] + 0.008f + J(0.008f);
                    s.points.Add(FromFrame(new Vector2(left ? x0 : x1, y + J(0.003f))));
                    s.points.Add(FromFrame(new Vector2(left ? x1 : x0, y + spacing * 0.5f + J(0.003f))));
                    left = !left;
                }
                if (s.points.Count > 1) Strokes.Add(s);
            }

            private static List<float> Crossings(List<Vector2> poly, float y)
            {
                var xs = new List<float>();
                for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                {
                    var a = poly[i];
                    var b = poly[j];
                    if ((a.y > y) == (b.y > y)) continue;
                    xs.Add(a.x + (y - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                xs.Sort();
                return xs;
            }
        }
    }
}
