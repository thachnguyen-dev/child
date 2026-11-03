using System.Collections;
using System.Collections.Generic;
using ChildGame.UI;
using UnityEngine;

namespace ChildGame.MiniGames.Caro
{
    /// <summary>
    /// A child's arm holding a ballpoint pen (owner 2026-10-08): it stays on screen at the edge of the board, reaches
    /// to a square, writes the mark stroke by stroke with the pen tip, and goes back to rest. The player's arm comes
    /// from below (white school shirt, blue pen); the classmate's from above (light blue shirt, red pen). A long sleeve
    /// runs off the screen behind the arm, so it never ends in the middle of the board.
    /// </summary>
    public class PenArm : MonoBehaviour
    {
        private const float Scale = 0.5f;

        private HandArt.Who who;
        private Vector2 restTip;
        private Vector2 tipOffset; // from the arm's pivot to the pen tip, in world units (rotation included)
        private Coroutine returning;

        public Vector2 Tip => (Vector2)transform.position + tipOffset;
        public Color Ink => HandArt.Ink(who);

        public static PenArm Create(HandArt.Who who, Vector2 restTip, bool fromTop)
        {
            var go = new GameObject(who + "PenArm");
            var arm = go.AddComponent<PenArm>();
            arm.who = who;
            arm.restTip = restTip;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = HandArt.Get(HandArt.Pose.Pen, who);
            sr.sortingOrder = 50;
            go.transform.localScale = Vector3.one * Scale;
            go.transform.rotation = Quaternion.Euler(0, 0, fromTop ? 180f : 0f);

            // The sleeve continues off screen beyond the sprite's bottom edge (local -y): outline, then cloth.
            var sleeveW = 2.24f;
            // It starts a little inside the drawn sleeve (hidden under it), so there is no seam.
            Box(go.transform, "SleeveOutline", new Vector2(0, -5.7f), new Vector2(sleeveW + 0.11f, 12f), new Color32(92, 52, 38, 255), 48);
            Box(go.transform, "Sleeve", new Vector2(0, -5.7f), new Vector2(sleeveW, 12.2f), HandArt.Sleeve(who), 49);

            var local = HandArt.PenTipLocal * Scale;
            arm.tipOffset = fromTop ? -local : local;
            go.transform.position = restTip - arm.tipOffset;
            return arm;
        }

        private static void Box(Transform parent, string name, Vector2 pos, Vector2 size, Color color, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = pos;
            sr.sprite = Ui.Round;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = color;
            sr.sortingOrder = order;
        }

        /// <summary>Moves the arm so the pen tip glides to <paramref name="target"/> (eased, with a slight lift).</summary>
        public IEnumerator MoveTipTo(Vector2 target, float seconds)
        {
            if (returning != null) StopCoroutine(returning);
            returning = null;
            var from = Tip;
            for (var t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                var e = Mathf.SmoothStep(0f, 1f, t);
                SetTip(Vector2.Lerp(from, target, e) + Vector2.up * (Mathf.Sin(e * Mathf.PI) * 0.15f));
                yield return null;
            }
            SetTip(target);
        }

        /// <summary>
        /// Draws along <paramref name="path"/> (world points) in <paramref name="seconds"/>, calling
        /// <paramref name="ink"/> for each small step so the line appears under the pen tip.
        /// </summary>
        public IEnumerator Trace(IReadOnlyList<Vector2> path, float seconds, System.Action<Vector2, Vector2> ink)
        {
            var total = 0f;
            for (var i = 1; i < path.Count; i++) total += Vector2.Distance(path[i - 1], path[i]);
            if (total <= 0f) yield break;
            var drawn = 0f;
            var seg = 1;
            var segStart = 0f;
            var last = path[0];
            SetTip(last);
            while (seg < path.Count)
            {
                drawn = Mathf.Min(total, drawn + total * Time.deltaTime / seconds);
                while (seg < path.Count)
                {
                    var len = Vector2.Distance(path[seg - 1], path[seg]);
                    if (segStart + len >= drawn)
                    {
                        var p = Vector2.Lerp(path[seg - 1], path[seg], len > 0 ? (drawn - segStart) / len : 1f);
                        ink(last, p);
                        last = p;
                        SetTip(p);
                        break;
                    }
                    ink(last, path[seg]);
                    last = path[seg];
                    segStart += len;
                    seg++;
                }
                if (drawn >= total) break;
                yield return null;
            }
            if (seg < path.Count) ink(last, path[path.Count - 1]);
            SetTip(path[path.Count - 1]);
        }

        /// <summary>Back to the resting place at the edge of the board (does not block).</summary>
        public void Rest() => returning = StartCoroutine(MoveTipTo(restTip, 0.35f));

        private void SetTip(Vector2 tip) => transform.position = tip - tipOffset;
    }
}
