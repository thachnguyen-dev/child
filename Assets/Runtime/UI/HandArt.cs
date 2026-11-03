using System.Collections.Generic;
using UnityEngine;

namespace ChildGame.UI
{
    /// <summary>
    /// Drawn hands for rock-paper-scissors (official art, owner 2026-10-08): a child's forearm and hand pointing up, in
    /// a fist (búa), open hand (bao) or V (kéo), with a shirt sleeve. Built from signed-distance shapes (palm, each finger,
    /// thumb, nails) layered back to front; every layer gets a dark outline, so overlapping fingers stay readable, plus
    /// soft rim shading and a highlight. Anti-aliased, rendered once and cached.
    /// </summary>
    public static class HandArt
    {
        public enum Pose { Fist, Paper, Scissors, Pen, Pinch }

        /// <summary>Where the thumb and index fingertips meet in the Pinch sprite (pixels, like <see cref="PenTipPixel"/>).</summary>
        public static readonly Vector2 PinchPixel = new Vector2(214, 702);

        /// <summary>The pinch point relative to the sprite pivot, in sprite units.</summary>
        public static Vector2 PinchLocal => new Vector2((PinchPixel.x - W * 0.5f) / 100f, PinchPixel.y / 100f);

        /// <summary>Where the pen tip is in the Pen sprite (pixels; the sprite is 512×832, pivot at the bottom centre).</summary>
        public static readonly Vector2 PenTipPixel = new Vector2(150, 800);

        /// <summary>The pen tip relative to the sprite pivot, in sprite units (100 px = 1 unit).</summary>
        public static Vector2 PenTipLocal => new Vector2((PenTipPixel.x - W * 0.5f) / 100f, PenTipPixel.y / 100f);

        /// <summary>Ballpoint ink: the player writes in blue, the opponent in red (like the X and O on the board).</summary>
        public static Color Ink(Who who) => who == Who.Player ? new Color32(40, 80, 200, 255) : new Color32(205, 45, 45, 255);

        private const int W = 512, H = 832;
        private const float Outline = 5.5f;

        private static readonly Color OutlineColor = new Color32(92, 52, 38, 255);
        private static readonly Color Skin = new Color32(246, 202, 166, 255);
        private static readonly Color SkinShade = new Color32(222, 162, 128, 255);
        private static readonly Color SkinLight = new Color32(255, 226, 200, 255);
        private static readonly Color Nail = new Color32(255, 228, 220, 255);

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>Whose arm: the player (white school shirt, blue cuff) or the opponent (light blue shirt).</summary>
        public enum Who { Player, Opponent }

        public static Color Sleeve(Who who) => who == Who.Player ? new Color32(250, 250, 246, 255) : new Color32(150, 200, 236, 255);
        public static Color Cuff(Who who) => who == Who.Player ? new Color32(70, 110, 190, 255) : new Color32(90, 140, 200, 255);

        public static string ResourceName(Pose pose, Who who, bool palm = false) =>
            $"Art/Hands/hand_{who.ToString().ToLowerInvariant()}_{pose.ToString().ToLowerInvariant()}{(palm ? "_palm" : "")}";

        /// <summary>Poses that also come seen from the palm side (an opponent facing you in rock-paper-scissors).</summary>
        public static bool HasPalm(Pose pose) => pose == Pose.Fist || pose == Pose.Paper || pose == Pose.Scissors;

        /// <summary>
        /// The hand sprite (pivot at the bottom centre, fingers up). Loads the pre-rendered image from Resources (made by
        /// the editor baker); renders it on the spot if the image is missing.
        /// </summary>
        public static Sprite Get(Pose pose, Who who, bool palm = false)
        {
            palm &= HasPalm(pose);
            var key = ResourceName(pose, who, palm);
            if (cache.TryGetValue(key, out var s)) return s;
            s = Resources.Load<Sprite>(key);
            if (s == null) s = Sprite.Create(RenderTexture(pose, who, palm), new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 100f);
            cache[key] = s;
            return s;
        }

        /// <summary>Renders the hand at full resolution (used by the editor baker).</summary>
        public static Texture2D RenderTexture(Pose pose, Who who, bool palm = false)
        {
            penInk = Ink(who);
            palm &= HasPalm(pose);
            return Render(Parts(pose, Sleeve(who), Cuff(who), palm), palm);
        }

        private static Color penInk;

        // ---- Shapes ----

        private abstract class Part
        {
            public Color fill, shade, light;
            public bool nail;
            /// <summary>A crease or line on the skin: drawn as a soft dark line, no outline.</summary>
            public bool crease;
            public abstract float Sdf(Vector2 p);
        }

        private class Capsule : Part
        {
            public Vector2 a, b;
            public float r;
            public override float Sdf(Vector2 p)
            {
                var pa = p - a;
                var ba = b - a;
                var h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
                return (pa - ba * h).magnitude - r;
            }
        }

        private class RoundBox : Part
        {
            public Vector2 c, half;
            public float r, angle;
            public override float Sdf(Vector2 p)
            {
                var d = p - c;
                var cs = Mathf.Cos(-angle * Mathf.Deg2Rad);
                var sn = Mathf.Sin(-angle * Mathf.Deg2Rad);
                d = new Vector2(d.x * cs - d.y * sn, d.x * sn + d.y * cs);
                var q = new Vector2(Mathf.Abs(d.x), Mathf.Abs(d.y)) - half + new Vector2(r, r);
                return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
            }
        }

        /// <summary>
        /// Several shapes merged into one silhouette with a smooth blend (palm + fingers + thumb): one outline around the
        /// whole, soft webbing where fingers meet the palm.
        /// </summary>
        private class Merge : Part
        {
            public List<Part> shapes = new List<Part>();
            public float k = 14f;
            public override float Sdf(Vector2 p)
            {
                var d = shapes[0].Sdf(p);
                for (var i = 1; i < shapes.Count; i++)
                {
                    var b = shapes[i].Sdf(p);
                    var h = Mathf.Clamp01(0.5f + 0.5f * (b - d) / k);
                    d = Mathf.Lerp(b, d, h) - k * h * (1f - h);
                }
                return d;
            }
        }

        private static T Skinned<T>(T part) where T : Part
        {
            part.fill = Skin;
            part.shade = SkinShade;
            part.light = SkinLight;
            return part;
        }

        private static Capsule Finger(Vector2 a, Vector2 b, float r) => Skinned(new Capsule { a = a, b = b, r = r });

        /// <summary>A fingernail near the tip of a straight finger from a to b.</summary>
        private static Part NailOf(Vector2 a, Vector2 b, float r)
        {
            var dir = (b - a).normalized;
            var tip = b - dir * (r * 0.55f);
            var nail = new RoundBox
            {
                c = tip, half = new Vector2(r * 0.55f, r * 0.62f), r = r * 0.45f,
                angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f,
                fill = Nail, shade = new Color32(240, 196, 186, 255), light = Color.white, nail = true,
            };
            return nail;
        }

        private static readonly Color CreaseColor = new Color32(188, 124, 92, 255);

        /// <summary>A line on the skin from a to b (a crease, a tendon), <paramref name="width"/> pixels.</summary>
        private static Part Crease(Vector2 a, Vector2 b, float width) => new Capsule { a = a, b = b, r = width * 0.5f, fill = CreaseColor, crease = true };

        /// <summary>A curved crease through three points (palm lines).</summary>
        private static void CreaseArc(List<Part> parts, Vector2 a, Vector2 m, Vector2 b, float width)
        {
            var prev = a;
            for (var i = 1; i <= 10; i++)
            {
                var t = i / 10f;
                var p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * m + t * t * b;
                parts.Add(Crease(prev, p, width * Mathf.Lerp(1f, 0.6f, Mathf.Abs(t - 0.4f))));
                prev = p;
            }
        }

        /// <summary>Short creases across a finger from a (base) to b (tip) at two joints (fractions along it).</summary>
        private static void JointCreases(List<Part> parts, Vector2 a, Vector2 b, float r, float first, float second)
        {
            var dir = (b - a).normalized;
            var across = new Vector2(-dir.y, dir.x);
            foreach (var t in new[] { first, second })
            {
                var c = Vector2.Lerp(a, b, t);
                parts.Add(Crease(c - across * r * 0.55f, c + across * r * 0.55f, 3f));
            }
        }

        private static List<Part> Parts(Pose pose, Color sleeve, Color cuff, bool palm)
        {
            var parts = new List<Part>();
            // Forearm and sleeve: the sleeve runs off the bottom of the sprite.
            // (The pen pose bends the wrist and brings its own forearm.)
            if (pose != Pose.Pen && pose != Pose.Pinch) parts.Add(Skinned(new Capsule { a = new Vector2(256, -60), b = new Vector2(256, 300), r = 72 }));
            parts.Add(new RoundBox { c = new Vector2(256, 60), half = new Vector2(112, 150), r = 30, fill = sleeve, shade = Color.Lerp(sleeve, Color.black, 0.18f), light = Color.Lerp(sleeve, Color.white, 0.35f) });
            parts.Add(new RoundBox { c = new Vector2(256, 200), half = new Vector2(116, 24), r = 20, fill = cuff, shade = Color.Lerp(cuff, Color.black, 0.2f), light = Color.Lerp(cuff, Color.white, 0.3f) });

            switch (pose)
            {
                case Pose.Paper:
                {
                    var hand = Skinned(new Merge());
                    hand.shapes.Add(new RoundBox { c = new Vector2(262, 410), half = new Vector2(100, 100), r = 62 });
                    var digits = new[]
                    {
                        (new Vector2(194, 420), new Vector2(150, 682), 26f), // index
                        (new Vector2(244, 430), new Vector2(240, 730), 27f), // middle
                        (new Vector2(292, 425), new Vector2(330, 708), 26f), // ring
                        (new Vector2(334, 412), new Vector2(404, 628), 23f), // little
                        (new Vector2(196, 380), new Vector2(92, 520), 30f),  // thumb, out to the side
                    };
                    foreach (var (a, b, r) in digits) hand.shapes.Add(new Capsule { a = a, b = b, r = r });
                    parts.Add(hand);
                    if (!palm)
                    {
                        // The back of the hand: nails on every finger, faint knuckle lines.
                        foreach (var (a, b, r) in digits) parts.Add(NailOf(a, b, r));
                        foreach (var (a, b, r) in digits) JointCreases(parts, a, b, r, 0.5f, 0.18f);
                    }
                    else
                    {
                        // The palm: no nails; the palm's three main lines and the creases at each finger joint.
                        foreach (var (a, b, r) in digits) JointCreases(parts, a, b, r, 0.75f, 0.42f);
                        CreaseArc(parts, new Vector2(330, 470), new Vector2(250, 482), new Vector2(178, 452), 5f);  // heart line
                        CreaseArc(parts, new Vector2(320, 430), new Vector2(250, 432), new Vector2(190, 405), 5f);  // head line
                        CreaseArc(parts, new Vector2(212, 420), new Vector2(222, 360), new Vector2(262, 318), 5f);  // life line
                    }
                    break;
                }
                case Pose.Scissors:
                {
                    // Palm with index and middle up in a V, as one shape.
                    var hand = Skinned(new Merge());
                    hand.shapes.Add(new RoundBox { c = new Vector2(258, 405), half = new Vector2(104, 100), r = 58 });
                    var i0 = new Vector2(212, 430);
                    var i1 = new Vector2(150, 712);
                    var m0 = new Vector2(262, 436);
                    var m1 = new Vector2(320, 726);
                    hand.shapes.Add(new Capsule { a = i0, b = i1, r = 27 });
                    hand.shapes.Add(new Capsule { a = m0, b = m1, r = 28 });
                    if (!palm)
                    {
                        // Seen from the back: the thumb is tucked along the side of the hand, holding the curled fingers.
                        hand.shapes.Add(new Capsule { a = new Vector2(178, 360), b = new Vector2(168, 452), r = 26 });
                    }
                    parts.Add(hand);
                    if (!palm)
                    {
                        parts.Add(NailOf(i0, i1, 27));
                        parts.Add(NailOf(m0, m1, 28));
                        JointCreases(parts, i0, i1, 27, 0.5f, 0.18f);
                        JointCreases(parts, m0, m1, 28, 0.5f, 0.18f);
                        // Ring and little fingers curled: from the back, only their knuckles show at the top right.
                        parts.Add(Skinned(new RoundBox { c = new Vector2(316, 492), half = new Vector2(28, 26), r = 24 }));
                        parts.Add(Skinned(new RoundBox { c = new Vector2(352, 470), half = new Vector2(24, 24), r = 21 }));
                    }
                    else
                    {
                        JointCreases(parts, i0, i1, 27, 0.75f, 0.42f);
                        JointCreases(parts, m0, m1, 28, 0.75f, 0.42f);
                        // From the palm side: ring and little fingers curled in, the thumb folded over them.
                        parts.Add(Skinned(new RoundBox { c = new Vector2(318, 480), half = new Vector2(30, 34), r = 26 }));
                        parts.Add(Skinned(new RoundBox { c = new Vector2(352, 458), half = new Vector2(26, 30), r = 22 }));
                        parts.Add(Finger(new Vector2(180, 384), new Vector2(304, 440), 28));
                        CreaseArc(parts, new Vector2(196, 370), new Vector2(230, 330), new Vector2(270, 316), 5f); // life line
                    }
                    break;
                }
                case Pose.Pen:
                {
                    // A child's pen grip seen from above: palm, curled middle/ring/little fingers, a ballpoint pen pointing
                    // up-left, held between the thumb and the index finger (both over the pen).
                    // The hand sits low; the wrist bends up into it.
                    var hand = Skinned(new Merge());
                    hand.shapes.Add(new Capsule { a = new Vector2(256, 150), b = new Vector2(292, 470), r = 68 });
                    hand.shapes.Add(new RoundBox { c = new Vector2(300, 530), half = new Vector2(92, 88), r = 56, angle = -15 });
                    parts.Add(hand);
                    // Middle, ring and little fingers curled under, showing as knuckles on the right.
                    parts.Add(Skinned(new RoundBox { c = new Vector2(382, 482), half = new Vector2(24, 28), r = 20, angle = -30 }));
                    parts.Add(Skinned(new RoundBox { c = new Vector2(388, 522), half = new Vector2(27, 31), r = 23, angle = -30 }));
                    parts.Add(Skinned(new RoundBox { c = new Vector2(374, 564), half = new Vector2(29, 33), r = 25, angle = -30 }));
                    // The pen: a long clear barrel resting across the hand and sticking out past it, coloured cap end and
                    // grip, metal cone, ink tip.
                    var back = new Vector2(440, 314);
                    var tip = PenTipPixel;
                    var dir = (tip - back).normalized;
                    var ink = penInk;
                    parts.Add(new Capsule { a = back, b = tip - dir * 70f, r = 17, fill = new Color32(236, 240, 244, 255), shade = new Color32(190, 198, 208, 255), light = Color.white });
                    parts.Add(new Capsule { a = back - dir * 8f, b = back + dir * 46f, r = 18, fill = ink, shade = Color.Lerp(ink, Color.black, 0.3f), light = Color.Lerp(ink, Color.white, 0.4f) });
                    parts.Add(new Capsule { a = tip - dir * 132f, b = tip - dir * 72f, r = 18.5f, fill = Color.Lerp(ink, Color.white, 0.25f), shade = ink, light = Color.Lerp(ink, Color.white, 0.55f) });
                    parts.Add(new Capsule { a = tip - dir * 70f, b = tip - dir * 10f, r = 11, fill = new Color32(200, 204, 210, 255), shade = new Color32(150, 156, 164, 255), light = Color.white });
                    parts.Add(new Capsule { a = tip - dir * 12f, b = tip, r = 5, fill = ink, shade = ink, light = ink });
                    // Thumb and index finger pinch the grip.
                    // Thumb and index finger pinch the grip just above the cone; short, bent toward it.
                    var grip = tip - dir * 150f;
                    var t0 = new Vector2(222, 516);
                    var t1 = grip + new Vector2(-24, -14);
                    parts.Add(Finger(t0, t1, 25));
                    parts.Add(NailOf(t0, t1, 25));
                    var i0 = new Vector2(316, 598);
                    var i1 = grip + new Vector2(24, 16);
                    parts.Add(Finger(i0, i1, 23));
                    parts.Add(NailOf(i0, i1, 23));
                    // Sleeve and cuff again on top, so the bent wrist disappears neatly into the sleeve.
                    parts.Add(new RoundBox { c = new Vector2(256, 60), half = new Vector2(112, 150), r = 30, fill = sleeve, shade = Color.Lerp(sleeve, Color.black, 0.18f), light = Color.Lerp(sleeve, Color.white, 0.35f) });
                    parts.Add(new RoundBox { c = new Vector2(256, 200), half = new Vector2(116, 24), r = 20, fill = cuff, shade = Color.Lerp(cuff, Color.black, 0.2f), light = Color.Lerp(cuff, Color.white, 0.3f) });
                    break;
                }
                case Pose.Pinch:
                {
                    // Thumb and index fingertips pressed together, as when plucking a hair; the other fingers curled. Same
                    // bent wrist and hand as the pen grip, without the pen.
                    var hand = Skinned(new Merge());
                    hand.shapes.Add(new Capsule { a = new Vector2(256, 150), b = new Vector2(292, 470), r = 68 });
                    hand.shapes.Add(new RoundBox { c = new Vector2(300, 530), half = new Vector2(92, 88), r = 56, angle = -15 });
                    parts.Add(hand);
                    parts.Add(Skinned(new RoundBox { c = new Vector2(382, 482), half = new Vector2(24, 28), r = 20, angle = -30 }));
                    parts.Add(Skinned(new RoundBox { c = new Vector2(388, 522), half = new Vector2(27, 31), r = 23, angle = -30 }));
                    parts.Add(Skinned(new RoundBox { c = new Vector2(374, 564), half = new Vector2(29, 33), r = 25, angle = -30 }));
                    var p = PinchPixel;
                    // Index finger: from the top of the palm, reaching up-left to the pinch, nail facing the viewer.
                    var i0 = new Vector2(316, 598);
                    var i1 = p + new Vector2(16, 10);
                    parts.Add(Finger(i0, i1, 23));
                    parts.Add(NailOf(i0, i1, 23));
                    // Thumb: its fleshy base grows out of the lower left of the hand (part of the hand's silhouette), then
                    // the end joint bends up to meet the index tip (drawn over it).
                    var t0 = new Vector2(176, 566);
                    var t1 = p + new Vector2(-4, -16);
                    hand.shapes.Add(new Capsule { a = new Vector2(250, 420), b = t0, r = 34 });
                    parts.Add(Finger(t0, t1, 25));
                    parts.Add(NailOf(t0, t1, 25));
                    parts.Add(new RoundBox { c = new Vector2(256, 60), half = new Vector2(112, 150), r = 30, fill = sleeve, shade = Color.Lerp(sleeve, Color.black, 0.18f), light = Color.Lerp(sleeve, Color.white, 0.35f) });
                    parts.Add(new RoundBox { c = new Vector2(256, 200), half = new Vector2(116, 24), r = 20, fill = cuff, shade = Color.Lerp(cuff, Color.black, 0.2f), light = Color.Lerp(cuff, Color.white, 0.3f) });
                    break;
                }
                default: // Fist
                {
                    if (!palm)
                    {
                        // The back of a fist: the flat back of the hand, the four knuckles in a ridge along the top, the
                        // thumb tucked along the side; the curled fingers and their nails face away.
                        var hand = Skinned(new Merge { k = 10f });
                        hand.shapes.Add(new RoundBox { c = new Vector2(262, 404), half = new Vector2(104, 104), r = 60 });
                        hand.shapes.Add(new Capsule { a = new Vector2(166, 360), b = new Vector2(160, 446), r = 30 }); // thumb
                        var xs = new[] { 186f, 234f, 282f, 326f };
                        for (var k = 0; k < 4; k++) hand.shapes.Add(new Capsule { a = new Vector2(xs[k], 470), b = new Vector2(xs[k], 486 - (k == 3 ? 10 : 0)), r = 26 });
                        parts.Add(hand);
                        // Knuckle dimples and the tendons running back to the wrist.
                        for (var k = 0; k < 4; k++)
                        {
                            var top = new Vector2(xs[k], 486 - (k == 3 ? 10 : 0));
                            parts.Add(Crease(top + new Vector2(-8, -4), top + new Vector2(8, -4), 3f));
                            parts.Add(Crease(top + new Vector2(0, -30), new Vector2(Mathf.Lerp(xs[k], 256f, 0.5f), 330), 2.5f));
                        }
                        parts.Add(Crease(new Vector2(204, 498), new Vector2(206, 520), 3f)); // gaps between the knuckles
                        parts.Add(Crease(new Vector2(258, 498), new Vector2(258, 522), 3f));
                        parts.Add(Crease(new Vector2(304, 492), new Vector2(304, 514), 3f));
                    }
                    else
                    {
                        // From the palm side: the curled fingers' rolls across the top, the thumb wrapped across them.
                        parts.Add(Skinned(new RoundBox { c = new Vector2(258, 400), half = new Vector2(108, 100), r = 56 }));
                        var xs = new[] { 182f, 230f, 278f, 324f };
                        for (var k = 0; k < 4; k++)
                            parts.Add(Skinned(new RoundBox { c = new Vector2(xs[k], 478 - (k == 3 ? 10 : 0)), half = new Vector2(30, 46), r = 28 }));
                        parts.Add(Finger(new Vector2(168, 378), new Vector2(270, 414), 29));
                        CreaseArc(parts, new Vector2(200, 368), new Vector2(232, 330), new Vector2(272, 314), 5f); // life line
                    }
                    break;
                }
            }
            return parts;
        }

        // ---- Rendering ----

        private static Texture2D Render(List<Part> parts, bool mirror = false)
        {
            var px = new Color[W * H];
            for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                var col = new Color(0, 0, 0, 0);
                foreach (var part in parts)
                {
                    var d = part.Sdf(p);
                    if (part.crease)
                    {
                        // A soft line on whatever skin is already drawn here.
                        var ca = Mathf.Clamp01(0.5f - d) * 0.6f;
                        if (ca > 0f && col.a > 0f) col = new Color(Mathf.Lerp(col.r, part.fill.r, ca), Mathf.Lerp(col.g, part.fill.g, ca), Mathf.Lerp(col.b, part.fill.b, ca), col.a);
                        continue;
                    }
                    if (d > Outline + 1f) continue;
                    // Fill with soft rim shading (darker toward the edge, lighter in a spot toward the upper left).
                    var inner = Mathf.Clamp01(-d / 26f);
                    var c = Color.Lerp(part.shade, part.fill, Mathf.SmoothStep(0f, 1f, inner));
                    // A soft highlight band just inside the edges that face the light (upper left). Only near the edge,
                    // where the distance-field normal is smooth, so no crease shows across flat areas.
                    var band = -d < 5f || -d > 30f ? 0f : Mathf.Sin((-d - 5f) / 25f * Mathf.PI);
                    var lightSpot = band * Mathf.Clamp01(Vector2.Dot(-Gradient(part, p), new Vector2(-0.6f, 0.8f)));
                    c = Color.Lerp(c, part.light, lightSpot * (part.nail ? 0.6f : 0.35f));
                    // Outline band around the shape, anti-aliased on both sides.
                    var fillA = Mathf.Clamp01(0.5f - d);
                    var outerA = Mathf.Clamp01(0.5f - (d - Outline));
                    var outlineMix = Mathf.Clamp01(0.5f + d); // 0 inside, 1 in the band
                    var layer = Color.Lerp(c, part.nail ? Color.Lerp(OutlineColor, Nail, 0.35f) : OutlineColor, outlineMix);
                    var a = Mathf.Max(fillA, outerA);
                    col = Color.Lerp(col, new Color(layer.r, layer.g, layer.b, 1f), a);
                    col.a = Mathf.Max(col.a, a);
                }
                // The palm view is the right hand seen from the front: mirrored, so the thumb is on the other side.
                px[y * W + (mirror ? W - 1 - x : x)] = col;
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Approximate outward normal of a part at p (numerical gradient of its distance field).</summary>
        private static Vector2 Gradient(Part part, Vector2 p)
        {
            const float e = 1.5f;
            var dx = part.Sdf(p + new Vector2(e, 0)) - part.Sdf(p - new Vector2(e, 0));
            var dy = part.Sdf(p + new Vector2(0, e)) - part.Sdf(p - new Vector2(0, e));
            var g = new Vector2(dx, dy);
            return g.sqrMagnitude > 1e-6f ? g.normalized : Vector2.up;
        }
    }
}
