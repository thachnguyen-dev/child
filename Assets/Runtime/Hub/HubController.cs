using ChildGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChildGame.Hub
{
    /// <summary>
    /// The main hub (design §4): the childhood environment with one object per mini game, plus the top bar (name,
    /// common points, achievements, settings). Asks for the profile on the very first start (design §5).
    /// </summary>
    public class HubController : MonoBehaviour
    {
        [Tooltip("Generate a placeholder sky and ground. Turn off once the real hub art is in the scene.")]
        [SerializeField] private bool placeholderEnvironment = true;

        private TextMeshProUGUI nameText;
        private LocText pointsText;
        private TextMeshProUGUI coinText;

        private void Start()
        {
            Time.timeScale = 1f;
            MiniGameSession.EnsureRaycaster();
            if (placeholderEnvironment && Resources.Load<Sprite>("Art/Hub/yard") == null) BuildPlaceholderEnvironment();
            gameObject.AddComponent<StreetVendor>();
            ShopScreens.Changed += RefreshTopBar;
            EnsureObjects();
            BuildTopBar();
#if !UNITY_WEBGL
            Screens.SceneBack = () => UiScreens.Confirm("hub.quit", Application.Quit);
#endif
#if CRAZYGAMES
            // CrazyGames: land straight in the yard (owner 2026-10-10: the welcome card can be skipped). The name is the
            // CrazyGames username, or "Bạn" / "Player" for a guest; boy by default. Both can be changed in the profile.
            if (!Game.Save.profileDone)
            {
                Game.SetProfile(Game.Platform.UserName ?? Loc.Get("profile.defaultname"), Gender.Boy);
                RefreshTopBar();
            }
#endif
            if (!Game.Save.profileDone) UiScreens.Profile(() => { RefreshTopBar(); PlayIntroOnce(); });
            else PlayIntroOnce();
        }


        /// <summary>The main game story (owner 2026-10-08): plays once at the beginning; Settings can play it again.</summary>
        public static readonly string[] IntroKeys =
        {
            "main.story.1", "main.story.2", "main.story.3", "main.story.4", "main.story.5", "main.story.6", "main.story.7",
            "main.story.8", "main.story.9", "main.story.10", "main.story.11", "main.story.12", "main.story.13",
        };

        private static void PlayIntroOnce()
        {
            if (Game.Save.introSeen) return;
            Game.Save.introSeen = true;
            Game.Commit();
            PlayIntro();
        }

        /// <summary>
        /// The main story over a dark night backdrop (the bus ride, the dream); the childhood yard shows as it fades out.
        /// </summary>
        public static void PlayIntro()
        {
            var night = Ui.Canvas("IntroNight", 25);
            var backdrop = Ui.Panel(night.parent, "Night", new Color32(18, 22, 40, 255), false);
            backdrop.transform.SetAsFirstSibling();
            UiScreens.Dialogue(IntroKeys, () => GameRunner.Instance.StartCoroutine(FadeOut(backdrop, night.root.gameObject)));
        }

        private static System.Collections.IEnumerator FadeOut(UnityEngine.UI.Image backdrop, GameObject root)
        {
            for (var t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1.2f)
            {
                if (backdrop == null) yield break;
                var c = backdrop.color;
                backdrop.color = new Color(c.r, c.g, c.b, 1f - t);
                yield return null;
            }
            if (root != null) Destroy(root);
        }
        private void OnDestroy()
        {
            Screens.SceneBack = null;
            ShopScreens.Changed -= RefreshTopBar;
        }

        /// <summary>
        /// The yard (owner 2026-10-08): the owner's painting fills the screen, and each mini game is a drawn object placed
        /// in it (see <see cref="Layout"/>). Mini games without a drawing, or when the painting is missing, get a
        /// placeholder block.
        /// </summary>
        private void EnsureObjects()
        {
            var placed = FindObjectsByType<HubObject>(FindObjectsSortMode.None);
            var parent = new GameObject("YardObjects").transform;
            var yard = BuildYard(out var toWorld);
            foreach (var def in MiniGameCatalog.All)
            {
                if (def.devOnly && !Dev.Capture.ShowsTestGame) continue;
                var found = false;
                foreach (var p in placed) found |= p.definition == def;
                if (found) continue;
                var art = YardArt.For(def.id);
                if (yard && art != null && Layout.TryGetValue(def.id, out var spot))
                    HubObject.CreateYard(def, parent, art, toWorld(spot.uv), spot.scale, spot.tag);
                else HubObject.CreatePlaceholder(def, parent);
            }
        }

        /// <summary>
        /// Where each drawing sits in the painting (u, v from its bottom-left corner, 0..1), its size, and where its name
        /// tag goes (a nudge from its place just under the drawing, world units).
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, (Vector2 uv, float scale, Vector2 tag)> Layout =
            new System.Collections.Generic.Dictionary<string, (Vector2, float, Vector2)>
            {
                ["dieu"] = (new Vector2(0.862f, 0.645f), 1.5f, new Vector2(0.45f, 0f)),               // a kite hung on the post's arm
                ["toc"] = (new Vector2(0.607f, 0.375f), 1.45f, Vector2.zero),               // grandma's mat at the door
                ["xoai"] = (new Vector2(0.765f, 0.33f), 1.4f, new Vector2(-0.2f, 0f)),                 // mangoes by the wall
                ["caro"] = (new Vector2(0.44f, 0.305f), 1.4f, Vector2.zero),                // the stool by the porch
                ["oanquan"] = (new Vector2(0.655f, 0.16f), 1.3f, Vector2.zero),             // the board on the dirt
                ["mocau"] = (new Vector2(0.905f, 0.16f), 1.25f, Vector2.zero),              // a sheath on the ground
                ["ve"] = (new Vector2(0.255f, 0.40f), 1.35f, Vector2.zero),                 // a drawing under the lean-to
                ["chuon"] = (new Vector2(0.075f, 0.60f), 1.6f, Vector2.zero),               // a dragonfly on the tall plant
            };

        /// <summary>
        /// Lays the painting (Resources/Art/Hub/yard) over the screen, scaled to cover it, its bottom on the screen's bottom
        /// edge; <paramref name="toWorld"/> maps a point of the painting (0..1) to the world. False when it is missing.
        /// </summary>
        private bool BuildYard(out System.Func<Vector2, Vector2> toWorld)
        {
            toWorld = uv => Vector2.zero;
            var sprite = Resources.Load<Sprite>("Art/Hub/yard");
            if (sprite == null) return false;
            var cam = Camera.main;
            var halfH = cam != null ? cam.orthographicSize : 5.4f;
            var halfW = cam != null ? halfH * cam.aspect : 9.6f;
            var size = (Vector2)sprite.bounds.size;
            var scale = Mathf.Max(halfW * 2f / size.x, halfH * 2f / size.y);
            var yard = new GameObject("Yard").AddComponent<SpriteRenderer>();
            yard.sprite = sprite;
            yard.sortingOrder = -50;
            yard.transform.position = new Vector3(0, -halfH, 0);
            yard.transform.localScale = Vector3.one * scale;
            var w = size * scale;
            toWorld = uv => new Vector2((uv.x - 0.5f) * w.x, -halfH + uv.y * w.y);
            return true;
        }

        private static void BuildPlaceholderEnvironment()
        {
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = new Color32(150, 214, 245, 255);
            var ground = new GameObject("PlaceholderGround").AddComponent<SpriteRenderer>();
            ground.sprite = Ui.Round;
            ground.drawMode = SpriteDrawMode.Sliced;
            ground.size = new Vector2(40, 6);
            ground.transform.position = new Vector3(0, -4.6f, 0);
            ground.color = new Color32(150, 200, 100, 255);
            ground.sortingOrder = -10;
            var sun = new GameObject("PlaceholderSun").AddComponent<SpriteRenderer>();
            sun.sprite = Ui.Circle;
            sun.transform.position = new Vector3(1.3f, 3.7f, 0);
            sun.transform.localScale = Vector3.one * 1.6f;
            sun.color = Ui.Sun;
            sun.sortingOrder = -9;
        }

        private void BuildTopBar()
        {
            // The yard's UI (owner 2026-10-08): paper tags with ink borders and drawn icons, to sit on the painting.
            var safe = Ui.Canvas("HubUI", 5);
            // Name card: the child's face, name, title and points; tapping it opens the profile (with the achievements).
            var cream = new Color32(252, 244, 222, 255);
            var cardButton = Ui.Button(safe, "hub.profile", UiScreens.Profile, Color.white, 30);
            cardButton.name = "btn.hub.profile";
            PaperUi.Style((Image)cardButton.targetGraphic, cream);
            var cardLabel = cardButton.GetComponentInChildren<TextMeshProUGUI>();
            if (cardLabel != null) cardLabel.gameObject.SetActive(false);
            var bar = (RectTransform)cardButton.transform;
            bar.Place(new Vector2(0f, 1f), new Vector2(600, 150), new Vector2(20, -18));
            face = Ui.Icon(bar, "Face", PaperUi.Face(Game.Save.gender == Gender.Girl), Color.white, 110);
            ((RectTransform)face.transform).Place(new Vector2(0f, 0.5f), new Vector2(110, 110), new Vector2(22, 0));
            nameText = Ui.Text(bar, "", 44, PaperUi.InkColor, TextAlignmentOptions.Left);
            nameText.Stretch(new Vector2(0, 0.5f), Vector2.one, 0);
            nameText.margin = new Vector4(146, 6, 20, 0);
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            var points = Ui.Text(bar, "", 34, new Color32(110, 86, 70, 255), TextAlignmentOptions.Left);
            points.Stretch(Vector2.zero, new Vector2(1, 0.5f), 0);
            points.margin = new Vector4(196, 0, 20, 8);
            Ui.Icon(bar, "star", Ui.Star, Ui.StarOn, 42).Place(new Vector2(0f, 0.25f), new Vector2(42, 42), new Vector2(146, 2));
            pointsText = points.gameObject.AddComponent<LocText>();
            // The title "Trùm xóm" once every achievement is earned: a gold ribbon under the name card.
            rank = Ui.Panel(bar, "Rank", Color.white);
            PaperUi.Style(rank, new Color32(255, 214, 90, 255));
            rank.Place(new Vector2(0f, 0f), new Vector2(300, 64), new Vector2(150, -48));
            var rankText = Ui.Label(rank.transform, "rank.trumxom", 30, PaperUi.InkColor);
            rankText.Stretch(Vector2.zero, Vector2.one, 6);

            var coins = Ui.Panel(safe, "Coins", Color.white);
            PaperUi.Style(coins, cream);
            coins.Place(new Vector2(0f, 1f), new Vector2(280, 110), new Vector2(636, -18));
            Ui.Icon(coins.transform, "coin", PaperUi.Coin(), Color.white, 70).Place(new Vector2(0f, 0.5f), new Vector2(70, 70), new Vector2(20, 0));
            coinText = Ui.Text(coins.transform, "", 42, new Color32(160, 104, 20, 255), TextAlignmentOptions.Left);
            coinText.Stretch(Vector2.zero, Vector2.one);
            coinText.margin = new Vector4(100, 0, 12, 0);
            coinText.enableAutoSizing = true;
            coinText.fontSizeMin = 26;
            coinText.fontSizeMax = 42;
            PaperUi.StyleButton(Ui.Button(safe, "hub.shop", () => ShopScreens.Main(), Color.white, 36), cream, PaperUi.ShopIcon())
                .Place(new Vector2(1f, 1f), new Vector2(330, 110), new Vector2(-304, -18));
            PaperUi.StyleButton(Ui.Button(safe, "hub.settings", UiScreens.Settings, Color.white, 36), cream, PaperUi.GearIcon())
                .Place(new Vector2(1f, 1f), new Vector2(270, 110), new Vector2(-20, -18));
            RefreshTopBar();
        }

        private Image face, rank;

        public static int MaxPoints()
        {
            var n = 0;
            foreach (var d in MiniGameCatalog.All)
                if (!d.devOnly && d.hasDifficulty) n += Progress.PointsPerGame;
            return n;
        }

        private void RefreshTopBar()
        {
            nameText.text = Game.Save.playerName;
            coinText.text = Shop.Money(Game.Save.currency);
            pointsText.SetFormat("hub.points", Progress.TotalPoints(Game.Save), MaxPoints());
            rank.gameObject.SetActive(Achievements.AllDone(Game.Save));
            face.sprite = PaperUi.Face(Game.Save.gender == Gender.Girl);
        }
    }
}
