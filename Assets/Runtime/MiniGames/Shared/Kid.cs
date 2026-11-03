using System.Collections.Generic;
using ChildGame.UI;
using UnityEngine;
using static ChildGame.UI.ArtShapes;

namespace ChildGame.MiniGames
{
    /// <summary>
    /// A child seen from the side (official generated art, owner 2026-10-08), facing right by default, feet at the
    /// transform's position. Drawn in parts — body (head, face, hair, shirt, shorts), arms and legs — so walking swings
    /// the arms and legs, and an arm can be raised (holding a kite string, throwing). The far-side limbs are shaded.
    /// Supports facing, a walking bob and a shake (for fights).
    /// </summary>
    public class Kid : MonoBehaviour
    {
        private Transform body, armFront, armBack, legFront, legBack;
        private SpriteRenderer[] parts;
        private float walkPhase, shake;

        public float Height { get; private set; }
        public bool Walking { get; set; }
        /// <summary>Raises the front arm (degrees forward from hanging down); null lets it swing.</summary>
        public float? ArmRaise { get; set; }

        public static Kid Create(string name, Color shirt, Color shorts, float height = 1.7f, bool girl = false)
        {
            var go = new GameObject(name);
            var kid = go.AddComponent<Kid>();
            kid.Height = height;
            kid.body = new GameObject("Body").transform;
            kid.body.SetParent(go.transform, false);
            kid.body.localScale = Vector3.one * height;
            var art = KidArt.Get(shirt, shorts, girl);
            var shade = new Color(0.78f, 0.76f, 0.78f, 1f);
            kid.legBack = Limb(kid.body, "LegBack", art.leg, KidArt.Hip + new Vector2(-0.03f, 0), shade, out var lb);
            kid.armBack = Limb(kid.body, "ArmBack", art.arm, KidArt.Shoulder + new Vector2(-0.03f, 0), shade, out var ab);
            var torso = new GameObject("Torso").AddComponent<SpriteRenderer>();
            torso.transform.SetParent(kid.body, false);
            torso.sprite = art.body;
            kid.legFront = Limb(kid.body, "LegFront", art.leg, KidArt.Hip + new Vector2(0.03f, 0), Color.white, out var lf);
            kid.armFront = Limb(kid.body, "ArmFront", art.arm, KidArt.Shoulder + new Vector2(0.02f, 0), Color.white, out var af);
            kid.parts = new[] { lb, ab, torso, lf, af };
            return kid;
        }

        private static Transform Limb(Transform parent, string name, Sprite sprite, Vector2 pivot, Color tint, out SpriteRenderer sr)
        {
            sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = pivot;
            sr.sprite = sprite;
            sr.color = tint;
            return sr.transform;
        }

        /// <summary>Faces right (+1) or left (-1).</summary>
        public void Face(float direction)
        {
            if (Mathf.Abs(direction) > 0.01f) body.localScale = new Vector3(Mathf.Sign(direction) * Height, Height, 1);
        }

        public void Shake(float seconds) => shake = seconds;

        /// <summary>Sorting base: lower on screen (closer) draws in front.</summary>
        public void SetDepthOrder(int baseOrder)
        {
            for (var i = 0; i < parts.Length; i++) parts[i].sortingOrder = baseOrder + i;
        }

        /// <summary>Where the front hand is in the world (for a kite string, a thrown stone).</summary>
        public Vector2 HandPosition => armFront.TransformPoint(KidArt.HandLocal);

        private void Update()
        {
            var offset = Vector3.zero;
            var swing = 0f;
            if (Walking)
            {
                walkPhase += Time.deltaTime * 11f;
                offset.y = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.04f * Height;
                swing = Mathf.Sin(walkPhase);
            }
            else walkPhase = 0f;
            if (shake > 0)
            {
                shake -= Time.deltaTime;
                offset.x = Mathf.Sin(Time.time * 60f) * 0.06f;
                swing = Mathf.Sin(Time.time * 30f) * 0.6f;
            }
            body.localPosition = offset;
            legFront.localRotation = Quaternion.Euler(0, 0, swing * 28f);
            legBack.localRotation = Quaternion.Euler(0, 0, -swing * 28f);
            armBack.localRotation = Quaternion.Euler(0, 0, swing * 24f);
            armFront.localRotation = Quaternion.Euler(0, 0, ArmRaise ?? -swing * 24f);
        }
    }

    /// <summary>The kid's parts, drawn once per outfit (units: a kid 1 tall, feet at the origin, facing right).</summary>
    public static class KidArt
    {
        public static readonly Vector2 Hip = new Vector2(0f, 0.36f);
        public static readonly Vector2 Shoulder = new Vector2(0f, 0.63f);
        /// <summary>The hand, relative to the shoulder (the arm hangs straight down).</summary>
        public static readonly Vector2 HandLocal = new Vector2(0f, -0.29f);

        public struct Parts
        {
            public Sprite body, arm, leg;
        }

        private static readonly Dictionary<string, Parts> cache = new Dictionary<string, Parts>();
        private static readonly Color Skin = new Color32(236, 190, 148, 255);
        private static readonly Color SkinShade = new Color32(200, 144, 106, 255);
        private static readonly Color SkinLight = new Color32(252, 218, 186, 255);
        private static readonly Color Hair = new Color32(38, 30, 26, 255);

        public static Parts Get(Color shirt, Color shorts, bool girl)
        {
            var key = $"{ColorUtility.ToHtmlStringRGB(shirt)}{ColorUtility.ToHtmlStringRGB(shorts)}{girl}";
            if (cache.TryGetValue(key, out var p) && p.body != null) return p;
            p = new Parts { body = Body(shirt, shorts, girl), arm = Arm(shirt), leg = Leg(shorts) };
            cache[key] = p;
            return p;
        }

        private static Color Shade(Color c) => Color.Lerp(c, Color.black, 0.3f);
        private static Color Light(Color c) => Color.Lerp(c, Color.white, 0.35f);

        private static Sprite Body(Color shirt, Color shorts, bool girl)
        {
            var r = new ArtRaster(new Rect(-0.32f, 0.2f, 0.64f, 0.82f), 260f);
            var head = new Vector2(0.02f, 0.82f);
            // Neck, then shorts and shirt.
            r.Shape(p => Capsule(p, new Vector2(0.0f, 0.64f), new Vector2(0.01f, 0.72f), 0.045f), Skin, SkinShade, SkinLight, 0.012f, 0.03f);
            float Shorts(Vector2 p) => Mathf.Max(Mathf.Abs(p.x) - 0.12f, Mathf.Abs(p.y - 0.37f) - 0.07f) - 0.03f;
            r.Shape(Shorts, shorts, Shade(shorts), Light(shorts), 0.012f, 0.05f);
            float Shirt(Vector2 p) => Mathf.Max(Mathf.Abs(p.x - 0.005f) - 0.115f - (p.y - 0.5f) * 0.05f, Mathf.Abs(p.y - 0.53f) - 0.12f) - 0.035f;
            r.Shape(Shirt, shirt, Shade(shirt), Light(shirt), 0.012f, 0.06f);
            // A collar line and a button placket hint.
            r.Line(new Vector2(-0.03f, 0.665f), new Vector2(0.07f, 0.64f), 2.2f, Shade(shirt), 0.9f, true);
            // Head: face toward +x, ear, hair over the top and back.
            float Head(Vector2 p) => SmoothUnion(Ellipse(p, head, new Vector2(0.15f, 0.155f)), Ellipse(p, head + new Vector2(0.11f, -0.06f), new Vector2(0.06f, 0.06f)), 0.05f);
            r.Shape(Head, Skin, SkinShade, SkinLight, 0.012f, 0.06f);
            r.Shape(p => Ellipse(p, head + new Vector2(-0.03f, -0.01f), new Vector2(0.035f, 0.045f)), Skin, SkinShade, SkinLight, 0.01f, 0.02f);
            if (girl)
            {
                // A bob down to the jaw with a fringe, and a little ponytail at the back.
                r.Shape(p => Capsule(p, head + new Vector2(-0.16f, 0.02f), head + new Vector2(-0.24f, -0.06f), 0.045f), Hair, Shade(Hair), new Color32(110, 96, 88, 255), 0.012f, 0.03f);
                r.Shape(p => Mathf.Max(Ellipse(p, head + new Vector2(-0.015f, 0.02f), new Vector2(0.17f, 0.17f)), 0.2f * (p.x - head.x) - (p.y - head.y) - 0.0f + (p.x > head.x + 0.04f ? 0.06f : -0.08f)),
                    Hair, Shade(Hair), new Color32(110, 96, 88, 255), 0.012f, 0.05f);
            }
            else
            {
                r.Shape(p => Mathf.Max(Ellipse(p, head + new Vector2(-0.02f, 0.03f), new Vector2(0.165f, 0.155f)), 0.45f * (p.x - head.x) - (p.y - head.y) + 0.03f),
                    Hair, Shade(Hair), new Color32(110, 96, 88, 255), 0.012f, 0.05f);
            }
            // Eye, eyebrow, nose and a small smile.
            r.Shape(p => Ellipse(p, head + new Vector2(0.075f, 0.01f), new Vector2(0.016f, 0.022f)), Hair, Hair, Hair, 0f, 0.01f);
            r.Line(head + new Vector2(0.055f, 0.05f), head + new Vector2(0.1f, 0.055f), 2.2f, Hair, 0.9f, true);
            r.Line(head + new Vector2(0.09f, -0.06f), head + new Vector2(0.135f, -0.055f), 2.2f, new Color32(150, 70, 60, 255), 0.9f, true);
            r.Shape(p => Ellipse(p, head + new Vector2(0.05f, -0.03f), new Vector2(0.03f, 0.018f)), new Color32(244, 160, 140, 255),
                new Color32(244, 160, 140, 255), new Color32(244, 160, 140, 255), 0f, 0.01f);
            return r.ToSprite("KidBody", Vector2.zero);
        }

        private static Sprite Arm(Color shirt)
        {
            // Pivot at the shoulder; hangs down to the hand.
            var r = new ArtRaster(new Rect(-0.08f, -0.36f, 0.16f, 0.42f), 260f);
            r.Shape(p => SmoothUnion(Capsule(p, Vector2.zero, new Vector2(0.005f, -0.25f), 0.035f), (p - new Vector2(0.005f, -0.28f)).magnitude - 0.042f, 0.02f),
                Skin, SkinShade, SkinLight, 0.012f, 0.03f);
            r.Shape(p => Mathf.Max(Capsule(p, new Vector2(0f, 0.01f), new Vector2(0.004f, -0.09f), 0.05f), p.y - 0.04f), shirt, Shade(shirt), Light(shirt), 0.012f, 0.03f);
            return r.ToSprite("KidArm", Vector2.zero);
        }

        private static Sprite Leg(Color shorts)
        {
            // Pivot at the hip; down to a bare foot pointing forward.
            var r = new ArtRaster(new Rect(-0.08f, -0.4f, 0.22f, 0.46f), 260f);
            r.Shape(p => SmoothUnion(Capsule(p, new Vector2(0f, -0.04f), new Vector2(0.005f, -0.33f), 0.045f),
                Capsule(p, new Vector2(-0.01f, -0.34f), new Vector2(0.075f, -0.345f), 0.03f), 0.03f), Skin, SkinShade, SkinLight, 0.012f, 0.03f);
            r.Shape(p => Mathf.Max(Capsule(p, new Vector2(0f, 0.0f), new Vector2(0.0f, -0.09f), 0.06f), p.y - 0.04f), shorts, Shade(shorts), Light(shorts), 0.012f, 0.03f);
            return r.ToSprite("KidLeg", Vector2.zero);
        }
    }
}
