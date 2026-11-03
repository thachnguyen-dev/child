using ChildGame.UI;
using UnityEngine;

namespace ChildGame.MiniGames
{
    /// <summary>
    /// A thin line drawn as a chain of sprite segments along a sagging curve (kite string, ropes). Avoids LineRenderer,
    /// whose material would need an extra shader in builds.
    /// </summary>
    public class SpriteLine : MonoBehaviour
    {
        private SpriteRenderer[] segments;
        private float width;

        public static SpriteLine Create(string name, int segmentCount, float width, Color color, int order)
        {
            var line = new GameObject(name).AddComponent<SpriteLine>();
            line.width = width;
            line.segments = new SpriteRenderer[segmentCount];
            for (var i = 0; i < segmentCount; i++)
            {
                var sr = new GameObject("seg").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(line.transform, false);
                sr.sprite = Ui.Circle;
                sr.color = color;
                sr.sortingOrder = order;
                line.segments[i] = sr;
            }
            return line;
        }

        /// <summary>Lays the line from a to b, sagging downward by <paramref name="sag"/> at the middle.</summary>
        public void Set(Vector2 a, Vector2 b, float sag)
        {
            var n = segments.Length;
            var prev = a;
            for (var i = 0; i < n; i++)
            {
                var t = (i + 1) / (float)n;
                var p = Vector2.Lerp(a, b, t) + Vector2.down * (sag * 4f * t * (1 - t));
                var mid = (prev + p) * 0.5f;
                var d = p - prev;
                var s = segments[i].transform;
                s.position = mid;
                s.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                // The circle sprite is 1 unit wide: stretch it into a capsule-ish segment.
                s.localScale = new Vector3(d.magnitude + width, width, 1);
                prev = p;
            }
        }
    }
}
