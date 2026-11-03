using ChildGame.UI;
using TMPro;
using UnityEngine;

namespace ChildGame.Hub
{
    /// <summary>
    /// A symbolic object in the hub that opens a mini game (design §4). Place one per mini game in the hub scene with
    /// its art and a Collider2D; mini games without one get a generated placeholder from <see cref="HubController"/>.
    /// </summary>
    [RequireComponent(typeof(Tappable))]
    public class HubObject : MonoBehaviour
    {
        public MiniGameDefinition definition;

        private Transform stars;

        private TMPro.TextMeshPro lockNote;
        private SpriteRenderer block;
        private Color baseColor;

        private void Awake() => GetComponent<Tappable>().Clicked += _ => Open();

        private void Start()
        {
            RefreshStars();
            RefreshLock();
            ShopScreens.Changed += RefreshLock;
        }

        private void OnDestroy() => ShopScreens.Changed -= RefreshLock;

        public void Open()
        {
            if (Screens.Count > 0 || definition == null) return;
            // Locked until its item is bought: tapping opens the shop on that item (owner 2026-10-07).
            if (Shop.LockReason(Game.Save, definition) != null) ShopScreens.Main(Shop.LockItem(definition));
            else if (definition.hasDifficulty) UiScreens.Difficulty(definition);
            else UiScreens.SpecialStart(definition);
        }

        /// <summary>Locked mini games stay visible, dimmed, with a note saying what to buy.</summary>
        public void RefreshLock()
        {
            if (definition == null) return;
            var reason = Shop.LockReason(Game.Save, definition);
            if (block != null) block.color = reason != null ? Color.Lerp(baseColor, Color.gray, 0.55f) : baseColor;
            if (lockNote == null) return;
            lockNote.gameObject.SetActive(reason != null);
            if (reason != null) lockNote.GetComponent<LocText>().Key = reason;
            if (stars != null) stars.gameObject.SetActive(reason == null);
        }

        /// <summary>A coloured rounded block with the title and the three stars, until the real art exists.</summary>
        public static HubObject CreatePlaceholder(MiniGameDefinition def, Transform parent)
        {
            var go = new GameObject("Hub_" + def.id);
            go.transform.SetParent(parent, false);
            go.transform.position = def.hubPosition;
            var size = new Vector2(3.2f, 2.4f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = def.hubSprite != null ? def.hubSprite : Ui.Round;
            if (def.hubSprite == null)
            {
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = size;
            }
            sr.color = def.hubColor;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = def.hubSprite != null ? (Vector2)sr.sprite.bounds.size : size;

            var label = new GameObject("Title", typeof(RectTransform)).AddComponent<TextMeshPro>();
            label.transform.SetParent(go.transform, false);
            label.rectTransform.sizeDelta = new Vector2(3f, 1.2f);
            label.transform.localPosition = new Vector3(0, 0.35f, 0);
            label.font = Ui.Bold;
            label.fontSize = 5;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Ui.Ink;
            label.sortingOrder = 2;
            label.gameObject.AddComponent<LocText>().Key = def.titleKey;

            var hub = go.AddComponent<HubObject>();
            hub.definition = def;
            hub.block = sr;
            hub.baseColor = def.hubColor;
            var note = new GameObject("LockNote", typeof(RectTransform)).AddComponent<TextMeshPro>();
            note.transform.SetParent(go.transform, false);
            note.rectTransform.sizeDelta = new Vector2(3f, 0.9f);
            note.transform.localPosition = new Vector3(0, -0.6f, 0);
            note.font = Ui.Bold;
            note.fontSize = 2.6f;
            note.alignment = TextAlignmentOptions.Center;
            note.color = new Color32(150, 40, 30, 255);
            note.sortingOrder = 3;
            note.gameObject.AddComponent<LocText>();
            hub.lockNote = note;
            hub.stars = new GameObject("Stars").transform;
            hub.stars.SetParent(go.transform, false);
            hub.stars.localPosition = new Vector3(0, -0.6f, 0);
            return hub;
        }

        /// <summary>
        /// A drawn object in the owner's yard painting (owner 2026-10-08) with a small name tag under it: the title, then
        /// the three stars (or, while locked, what to buy). <paramref name="pivotOffset"/> moves the tap area onto the art.
        /// </summary>
        public static HubObject CreateYard(MiniGameDefinition def, Transform parent, Sprite art, Vector2 position, float scale, Vector2 tagOffset)
        {
            var go = new GameObject("Hub_" + def.id);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = art;
            sr.sortingOrder = 5;
            go.transform.localScale = Vector3.one * scale;
            // Tap area: the drawing plus the tag below it.
            var box = go.AddComponent<BoxCollider2D>();
            var b = art.bounds;
            // The tag sits just under the drawing, nudged by tagOffset (world units).
            var tagLocal = new Vector2(0f, art.bounds.min.y - 0.36f / scale) + tagOffset / scale;
            var min = Vector2.Min(b.min, tagLocal - new Vector2(1f, 0.42f) / scale);
            var max = Vector2.Max(b.max, tagLocal + new Vector2(1f, 0.42f) / scale);
            box.offset = (min + max) * 0.5f;
            box.size = max - min;

            var tag = new GameObject("Tag").transform;
            tag.SetParent(go.transform, false);
            tag.localPosition = tagLocal;
            tag.localScale = Vector3.one / scale;
            TagBox(tag, new Vector2(2.0f, 0.8f), new Color32(72, 52, 38, 220), 6);
            TagBox(tag, new Vector2(1.93f, 0.73f), new Color32(250, 242, 222, 230), 7);
            var label = new GameObject("Title", typeof(RectTransform)).AddComponent<TextMeshPro>();
            label.transform.SetParent(tag, false);
            label.rectTransform.sizeDelta = new Vector2(1.84f, 0.38f);
            label.transform.localPosition = new Vector3(0, 0.15f, 0);
            label.font = Ui.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMin = 1.8f;
            label.fontSizeMax = 2.8f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Ui.Ink;
            label.sortingOrder = 8;
            label.gameObject.AddComponent<LocText>().Key = def.titleKey;

            var hub = go.AddComponent<HubObject>();
            hub.definition = def;
            hub.block = sr;
            hub.baseColor = Color.white;
            var note = new GameObject("LockNote", typeof(RectTransform)).AddComponent<TextMeshPro>();
            note.transform.SetParent(tag, false);
            note.rectTransform.sizeDelta = new Vector2(1.84f, 0.32f);
            note.transform.localPosition = new Vector3(0, -0.17f, 0);
            note.font = Ui.Bold;
            note.enableAutoSizing = true;
            note.fontSizeMin = 1.2f;
            note.fontSizeMax = 2f;
            note.alignment = TextAlignmentOptions.Center;
            note.color = new Color32(150, 40, 30, 255);
            note.sortingOrder = 8;
            note.gameObject.AddComponent<LocText>();
            hub.lockNote = note;
            hub.stars = new GameObject("Stars").transform;
            hub.stars.SetParent(tag, false);
            hub.stars.localPosition = new Vector3(0, -0.17f, 0);
            hub.starScale = 0.26f;
            hub.starGap = 0.32f;
            return hub;
        }

        private float starScale = 0.5f, starGap = 0.6f;

        private static SpriteRenderer TagBox(Transform parent, Vector2 size, Color color, int order)
        {
            var sr = new GameObject("TagBox").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.sprite = Ui.Round;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.transform.localScale = Vector3.one * 0.25f;
            sr.size = size * 4f;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        public void RefreshStars()
        {
            if (stars == null || definition == null || !definition.hasDifficulty) return;
            foreach (Transform c in stars) Destroy(c.gameObject);
            var record = Game.Save.Record(definition.id, false);
            for (var i = 0; i < Progress.PointsPerGame; i++)
            {
                var s = new GameObject("star" + i).AddComponent<SpriteRenderer>();
                s.transform.SetParent(stars, false);
                s.transform.localPosition = new Vector3((i - 1) * starGap, 0, 0);
                s.transform.localScale = Vector3.one * starScale;
                s.sprite = Ui.Star;
                s.color = record != null && record.Won((Difficulty)i) ? Ui.StarOn : Ui.StarOff;
                s.sortingOrder = 9;
            }
        }
    }
}
