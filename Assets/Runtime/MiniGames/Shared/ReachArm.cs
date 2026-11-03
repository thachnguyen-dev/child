using System.Collections;
using ChildGame.UI;
using TMPro;
using UnityEngine;

namespace ChildGame.MiniGames
{
    /// <summary>
    /// A child's arm reaching over a board from off screen (Ô ăn quan, owner 2026-10-08). It pivots around a shoulder
    /// below (player) or above (opponent) the screen, so it leans toward where the hand goes, and swaps hand poses:
    /// an open palm to slap and scoop, a fist to carry pieces. A long sleeve runs off screen behind it. The arm also
    /// has a speech bubble for what the child says while playing, and a small badge for the pieces in hand.
    /// </summary>
    public class ReachArm : MonoBehaviour
    {
        private const float DefaultScale = 0.45f;
        private float scale = DefaultScale;
        /// <summary>The middle of the palm / fist in the hand sprite (sprite units from the bottom-centre pivot).</summary>
        private static readonly Vector2 HandLocal = new Vector2(0f, 4.05f);

        private HandArt.Who who;
        private bool fromTop;
        private Vector2 shoulder, rest;
        private SpriteRenderer sprite, sleeve;
        private HandArt.Pose pose;

        /// <summary>The point that follows the target: the pinch for Pinch, the pen tip for Pen, else the palm.</summary>
        private Vector2 Anchor => pose == HandArt.Pose.Pinch ? HandArt.PinchLocal : pose == HandArt.Pose.Pen ? HandArt.PenTipLocal : HandLocal;
        private Transform bubble;
        private TextMeshPro bubbleText, badgeText;
        private GameObject badge;
        private Coroutine bubbleHide;
        private Vector2 hand;

        public Vector2 Hand => hand;

        public static ReachArm Create(HandArt.Who who, Vector2 shoulder, Vector2 rest, bool fromTop, float? scale = null)
        {
            var go = new GameObject(who + "ReachArm");
            var arm = go.AddComponent<ReachArm>();
            arm.who = who;
            arm.fromTop = fromTop;
            arm.shoulder = shoulder;
            arm.rest = rest;
            arm.sprite = go.AddComponent<SpriteRenderer>();
            arm.sprite.sortingOrder = 90; // above the pebbles and quan stones
            arm.scale = scale ?? DefaultScale;
            go.transform.localScale = Vector3.one * arm.scale;
            // The sleeve runs on off screen: the sprite's own bottom row (outline, cloth and shading) stretched down.
            arm.sleeve = new GameObject("Sleeve").AddComponent<SpriteRenderer>();
            arm.sleeve.transform.SetParent(go.transform, false);
            arm.sleeve.transform.localPosition = new Vector3(0, 0.02f, 0);
            arm.sleeve.transform.localScale = new Vector3(1f, 600f, 1f);
            arm.sleeve.sortingOrder = 89;
            arm.SetPose(HandArt.Pose.Paper);
            arm.BuildBubble();
            arm.SetHand(rest);
            return arm;
        }

        private static SpriteRenderer Box(Transform parent, string name, Vector2 pos, Vector2 size, Color color, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = pos;
            sr.sprite = Ui.Round;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        public void SetPose(HandArt.Pose pose)
        {
            this.pose = pose;
            var s = HandArt.Get(pose, who);
            sprite.sprite = s;
            var key = s.GetInstanceID();
            if (!sleeveSlices.TryGetValue(key, out var slice))
            {
                var r = s.rect;
                slice = Sprite.Create(s.texture, new Rect(r.x, r.y, r.width, 2f), new Vector2(0.5f, 1f), s.pixelsPerUnit);
                sleeveSlices[key] = slice;
            }
            sleeve.sprite = slice;
            SetHand(hand); // keep the new pose's anchor on the same spot
        }

        private static readonly System.Collections.Generic.Dictionary<int, Sprite> sleeveSlices = new System.Collections.Generic.Dictionary<int, Sprite>();

        /// <summary>Places the hand (palm centre) at <paramref name="p"/>, the arm leaning from the shoulder.</summary>
        public void SetHand(Vector2 p)
        {
            hand = p;
            var toHand = p - shoulder;
            // Sprite "up" is fingers; the arm points from the shoulder to the hand.
            var angle = Mathf.Atan2(toHand.y, toHand.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, angle);
            transform.position = (Vector3)(p - (Vector2)(transform.rotation * (Anchor * transform.localScale.x)));
            PlaceBubble();
        }

        /// <summary>Glides the hand to <paramref name="target"/> with a slight lift off the ground.</summary>
        public IEnumerator MoveTo(Vector2 target, float seconds)
        {
            var from = hand;
            var lift = fromTop ? -0.2f : 0.2f;
            for (var t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                var e = Mathf.SmoothStep(0f, 1f, t);
                SetHand(Vector2.Lerp(from, target, e) + Vector2.up * (Mathf.Sin(e * Mathf.PI) * lift));
                yield return null;
            }
            SetHand(target);
        }

        /// <summary>A quick press down onto the ground (the hand shrinks a little as it lowers, then lifts).</summary>
        public IEnumerator Press(float seconds)
        {
            for (var t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                var k = 1f - 0.1f * Mathf.Sin(t * Mathf.PI);
                transform.localScale = Vector3.one * (scale * k);
                SetHand(hand);
                yield return null;
            }
            transform.localScale = Vector3.one * scale;
            SetHand(hand);
        }

        public IEnumerator Rest(float seconds = 0.45f)
        {
            SetPose(HandArt.Pose.Paper);
            Badge(0);
            yield return MoveTo(rest, seconds);
        }

        // ---- Speech bubble and pieces-in-hand badge ----

        private void BuildBubble()
        {
            bubble = new GameObject(who + "Bubble").transform;
            var tail = Box(bubble, "Tail", new Vector2(0, fromTop ? 0.42f : -0.42f), new Vector2(0.4f, 0.4f), new Color32(92, 52, 38, 255), 99);
            tail.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            Box(bubble, "Edge", Vector2.zero, new Vector2(2.36f, 0.96f), new Color32(92, 52, 38, 255), 100);
            Box(bubble, "Fill", Vector2.zero, new Vector2(2.26f, 0.86f), Color.white, 101);
            var tailFill = Box(bubble, "TailFill", new Vector2(0, fromTop ? 0.4f : -0.4f), new Vector2(0.3f, 0.3f), Color.white, 102);
            tailFill.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            bubbleText = Label(bubble, 4.4f, who == HandArt.Who.Player ? Ui.Sky : Ui.Brick, 103);
            bubble.gameObject.SetActive(false);

            badge = new GameObject(who + "InHand");
            badge.transform.SetParent(bubble.parent, false);
            Box(badge.transform, "Edge", Vector2.zero, new Vector2(0.78f, 0.62f), new Color32(92, 52, 38, 255), 96);
            Box(badge.transform, "Fill", Vector2.zero, new Vector2(0.7f, 0.54f), new Color32(255, 248, 231, 255), 97);
            badgeText = Label(badge.transform, 3.4f, Ui.Ink, 98);
            badge.SetActive(false);
        }

        private static TextMeshPro Label(Transform parent, float size, Color color, int order)
        {
            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshPro>();
            t.transform.SetParent(parent, false);
            t.rectTransform.sizeDelta = new Vector2(2.2f, 0.8f);
            t.font = Ui.Bold;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.color = color;
            t.sortingOrder = order;
            return t;
        }

        /// <summary>Shows a short line in the bubble beside the hand for <paramref name="seconds"/>.</summary>
        public void Say(string text, float seconds)
        {
            bubbleText.text = text;
            bubble.gameObject.SetActive(true);
            PlaceBubble();
            if (bubbleHide != null) StopCoroutine(bubbleHide);
            bubbleHide = StartCoroutine(HideBubble(seconds));
        }

        private IEnumerator HideBubble(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            bubble.gameObject.SetActive(false);
            bubbleHide = null;
        }

        /// <summary>The number of pieces in the hand (0 hides the badge).</summary>
        public void Badge(int n)
        {
            badge.SetActive(n > 0);
            badgeText.text = n.ToString();
            PlaceBubble();
        }

        private void PlaceBubble()
        {
            if (bubble == null) return;
            // The bubble floats on the far side of the hand from the arm; the badge sits on the wrist side.
            var away = fromTop ? -1f : 1f;
            var p = hand + new Vector2(0, away * 1.75f);
            var cam = Camera.main;
            if (cam != null)
            {
                var halfH = cam.orthographicSize;
                var halfW = halfH * cam.aspect;
                p.x = Mathf.Clamp(p.x, -halfW + 1.3f, halfW - 1.3f);
                p.y = Mathf.Clamp(p.y, -halfH + 0.6f, halfH - 0.6f);
            }
            bubble.position = p;
            badge.transform.position = hand + new Vector2(0.95f, -away * 0.95f);
        }

        private void OnDestroy()
        {
            if (bubble != null) Destroy(bubble.gameObject);
            if (badge != null) Destroy(badge);
        }
    }
}
