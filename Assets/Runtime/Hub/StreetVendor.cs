using System.Collections;
using ChildGame.UI;
using TMPro;
using UnityEngine;

namespace ChildGame.Hub
{
    /// <summary>
    /// The street vendor (secondary shop): now and then she walks across the yard for 5–7 seconds with her carrying pole.
    /// Tap her to stop her and buy food (đồng or a pair of sandals). The appearance chance is low (owner); the exact
    /// numbers are placeholders.
    /// </summary>
    public class StreetVendor : MonoBehaviour
    {
        [Tooltip("Chance she comes by soon after opening the hub.")]
        public float chanceOnHubOpen = 0.12f;
        [Tooltip("While the player stays in the hub: one roll every this many seconds…")]
        public float rollEvery = 20f;
        [Tooltip("…with this chance.")]
        public float chancePerRoll = 0.05f;
        public Vector2 walkSeconds = new Vector2(5f, 7f);

        private Transform figure;
        private bool walking, stopped;
        private float nextRoll;

        public bool Walking => walking;

        private void Start()
        {
            nextRoll = Time.time + rollEvery;
            if (Random.value < chanceOnHubOpen) StartCoroutine(AppearAfter(Random.Range(3f, 8f)));
        }

        private void Update()
        {
            if (walking || Time.time < nextRoll) return;
            nextRoll = Time.time + rollEvery;
            if (Screens.Count == 0 && Random.value < chancePerRoll) StartCoroutine(Walk());
        }

        /// <summary>Capture harness / testing: she comes by now.</summary>
        public void DebugAppear()
        {
            if (!walking) StartCoroutine(Walk());
        }

        private IEnumerator AppearAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (!walking) yield return Walk();
        }

        private IEnumerator Walk()
        {
            walking = true;
            var cam = Camera.main;
            var half = cam != null ? cam.orthographicSize * cam.aspect + 2f : 12f;
            figure = Build().transform;
            var call = Bubble(figure);
            var seconds = Random.Range(walkSeconds.x, walkSeconds.y);
            var y = -4.2f;
            for (var t = 0f; t < 1f;)
            {
                if (!stopped) t += Time.deltaTime / seconds;
                var bob = stopped ? 0f : Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 0.08f;
                figure.position = new Vector3(Mathf.Lerp(-half, half, t), y + bob, 0);
                call.SetActive(!stopped); // she calls out while walking (owner 2026-10-08), not while serving
                Step();
                yield return null;
            }
            Destroy(figure.gameObject);
            walking = false;
        }

        private void Call()
        {
            if (stopped || Screens.Count > 0) return;
            stopped = true;
            ShopScreens.Vendor();
            StartCoroutine(ResumeWhenClosed());
        }

        private IEnumerator ResumeWhenClosed()
        {
            while (Screens.Count > 0) yield return null;
            stopped = false;
        }

        /// <summary>
        /// The street vendor, drawn to match the yard painting (<see cref="YardArt.Vendor"/>): she walks with her carrying
        /// pole and two baskets, her legs stepping, the baskets swaying.
        /// </summary>
        private GameObject Build()
        {
            var go = new GameObject("StreetVendor");
            var art = new GameObject("Art").transform;
            art.SetParent(go.transform, false);
            art.localPosition = new Vector3(0, 0.62f, 0);
            art.localScale = Vector3.one * 1.25f;
            legBack = Leg(art, new Vector2(-0.05f, 0f), new Color(0.75f, 0.75f, 0.75f, 1f), 29);
            var body = new GameObject("Body").AddComponent<SpriteRenderer>();
            body.transform.SetParent(art, false);
            body.sprite = YardArt.Vendor();
            body.sortingOrder = 30;
            legFront = Leg(art, new Vector2(0.05f, 0f), Color.white, 31);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(3.6f, 2.8f);
            col.offset = new Vector2(0, 1.0f);
            go.AddComponent<Tappable>().Clicked += _ => Call();
            return go;
        }

        private Transform legFront, legBack;

        private static Transform Leg(Transform parent, Vector2 hip, Color tint, int order)
        {
            var sr = new GameObject("Leg").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = hip;
            sr.sprite = YardArt.VendorLeg();
            sr.color = tint;
            sr.sortingOrder = order;
            return sr.transform;
        }

        /// <summary>Steps while walking; stands still while serving.</summary>
        private void Step()
        {
            if (legFront == null) return;
            var swing = stopped ? 0f : Mathf.Sin(Time.time * 9f) * 22f;
            legFront.localRotation = Quaternion.Euler(0, 0, swing);
            legBack.localRotation = Quaternion.Euler(0, 0, -swing);
        }

        /// <summary>Her call, "Ai kẹo kéo, ai cà rem không?", in a speech bubble above her head.</summary>
        private static GameObject Bubble(Transform figure)
        {
            var root = new GameObject("Call");
            root.transform.SetParent(figure, false);
            root.transform.localPosition = new Vector2(0.4f, 2.75f);
            Shape(root.transform, Ui.Round, Vector2.zero, new Vector2(6.2f, 1.0f), new Color32(92, 52, 38, 255), 40);
            Shape(root.transform, Ui.Round, Vector2.zero, new Vector2(6.08f, 0.88f), Color.white, 41);
            var tail = Shape(root.transform, Ui.Round, new Vector2(-0.4f, -0.5f), new Vector2(0.34f, 0.34f), Color.white, 42);
            tail.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshPro>();
            text.transform.SetParent(root.transform, false);
            text.rectTransform.sizeDelta = new Vector2(5.6f, 0.8f);
            text.font = Ui.Bold;
            text.enableAutoSizing = true;
            text.fontSizeMin = 2f;
            text.fontSizeMax = 4f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = Ui.Brick;
            text.sortingOrder = 43;
            text.gameObject.AddComponent<LocText>().Key = "vendor.call";
            return root;
        }
        private static SpriteRenderer Shape(Transform parent, Sprite sprite, Vector2 pos, Vector2 size, Color color, int order)
        {
            var sr = new GameObject("part").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = pos;
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (sprite == Ui.Round)
            {
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.transform.localScale = Vector3.one * 0.25f;
                sr.size = size * 4f;
            }
            else sr.transform.localScale = new Vector3(size.x, size.y, 1);
            return sr;
        }
    }
}
