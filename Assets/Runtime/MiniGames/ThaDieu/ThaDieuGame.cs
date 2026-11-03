using System.Collections.Generic;
using ChildGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ChildGame.MiniGames.ThaDieu
{
    /// <summary>
    /// MG03 — Thả diều. Side view of an endless afternoon field. The player runs the child left or right; the kite is
    /// never controlled directly.
    /// <list type="bullet">
    /// <item>Running the right way relative to the wind raises the kite; the wrong way lowers it; standing still lets it
    /// sink slowly (owner 2026-10-07).</item>
    /// <item>"Right way" = into the wind, as in real kite flying (the kite trails downwind). One setting flips it.</item>
    /// <item>The wind is visible (streaks, leaning grass) and changes direction; it calms briefly first.</item>
    /// <item>Win: reach the target height. Loss: time runs out first. Played alone; other kites fly far away.</item>
    /// </list>
    /// ⚠ Values marked placeholder are temporary and easy to tune.
    /// </summary>
    public class ThaDieuGame : MiniGame
    {
        [Header("Placeholder tuning")]
        [Tooltip("Target kite height in metres per difficulty (design: low / higher / very high).")]
        [SerializeField] private float[] targetHeight = { 60f, 100f, 150f };
        [Tooltip("Time limit per difficulty, seconds (design: a session lasts about 3–5 minutes).")]
        [SerializeField] private float[] timeLimit = { 120f, 180f, 240f }; // owner 2026-10-08: 1 minute shorter
        [SerializeField] private float riseRate = 1.625f;        // owner 2026-10-08: ×2.5
        [SerializeField] private float wrongWayFallRate = 3.75f; // ×2.5
        [SerializeField] private float standStillFallRate = 1.25f; // ×2.5
        [SerializeField] private float runSpeed = 4.2f;
        [Tooltip("Seconds between wind changes (random in range).")]
        [SerializeField] private Vector2 windChangeEvery = new Vector2(5f, 10f); // owner: more often (placeholder range)
        [Tooltip("The calm before the wind turns around (the visible warning).")]
        [SerializeField] private float lullSeconds = 1.6f;
        [Tooltip("True: run against the wind (real kite flying). False: run with it.")]
        [SerializeField] private bool runIntoWind = true;

        private const float GroundY = -3.6f;

        private Kid kid;
        private Transform kite, kiteTail;
        private SpriteLine kiteString;
        private Transform worldRoot;
        private readonly List<Transform> streaks = new List<Transform>();
        private readonly List<Transform> grass = new List<Transform>();
        private readonly List<(Transform t, float parallax, float baseX)> scenery = new List<(Transform, float, float)>();
        private readonly List<(Transform t, float phase)> farKites = new List<(Transform, float)>();

        private float height, timeLeft;
        private float windDir = 1f;       // +1 blows to the right, -1 to the left
        private float windStrength = 1f;  // 0..1, drops during the lull
        private float nextWindChange, lullTimer;
        private float runInput;
        private float kiteSway;
        private TextMeshProUGUI timeText, heightText;
        private RectTransform meterFill, meterTarget;

        public float Height => height;
        public float Target => targetHeight[(int)Difficulty];
        /// <summary>The direction to run right now for the kite to rise (+1 right, -1 left).</summary>
        public float GoodDirection => runIntoWind ? -windDir : windDir;
        /// <summary>Overrides touch input when set (capture harness).</summary>
        public float? DebugRun { get; set; }

        // ---- Setup ----

        protected override void OnBegin()
        {
            timeLeft = timeLimit[(int)Difficulty];
            height = 4f;
            windDir = Random.value < 0.5f ? 1f : -1f;
            nextWindChange = Random.Range(windChangeEvery.x, windChangeEvery.y);
            BuildWorld();
            BuildHud();
        }

        private void BuildWorld()
        {
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = new Color32(255, 214, 160, 255); // late afternoon
            worldRoot = new GameObject("World").transform;

            // Official generated art (owner 2026-10-08): the afternoon sky with its clouds and sun (stays with the camera),
            // the distant hills, village trees and rice paddies (slow parallax), the meadow underfoot, wild grass in front.
            var halfW = cam != null ? cam.orthographicSize * cam.aspect : 9.6f;
            var skyRect = new Rect(-halfW - 1f, -3.6f, (halfW + 1f) * 2f, 9.4f);
            Follow(SceneArt.Put(worldRoot, "Sky", SceneArt.Sky(skyRect, new Color32(122, 180, 234, 255), new Color32(255, 214, 160, 255), new Vector2(halfW * 0.55f, 1.4f), 5, 12), skyRect.center, Vector2.one, -200));
            AddBand(ThaDieuArt.Far(-3.9f, -2.9f), ThaDieuArt.FarTile, -3.9f, 0.2f, -180);
            AddBand(ThaDieuArt.Meadow(-5.9f, GroundY + 0.15f), ThaDieuArt.MeadowTile, -5.9f, 1f, -150);
            // Other children's kites, far away (owner: the background shows other kites).
            for (var i = 0; i < 4; i++)
            {
                var k = SceneArt.Put(worldRoot, "FarKite", ThaDieuArt.Kite(i % 2 == 0 ? new Color32(220, 80, 80, 255) : new Color32(80, 120, 220, 255), new Color32(250, 220, 90, 255)),
                    new Vector2(-9 + i * 5.5f, 1.4f + (i % 3) * 0.9f), Vector2.one * 0.4f, -175);
                k.color = new Color(1f, 1f, 1f, 0.85f);
                scenery.Add((k.transform, 0.15f, k.transform.position.x));
                farKites.Add((k.transform, i * 1.7f));
            }
            for (var i = 0; i < 40; i++)
            {
                var g = SceneArt.Put(worldRoot, "Grass", ThaDieuArt.Tuft(i % 5), new Vector2(-10 + i * 0.5f + Random.Range(-0.1f, 0.1f), GroundY - 0.2f + Random.Range(-0.25f, 0.05f)),
                    Vector2.one * Random.Range(0.8f, 1.2f), -140);
                grass.Add(g.transform);
            }

            for (var i = 0; i < 14; i++)
            {
                var s = Shape(worldRoot, "WindStreak", Ui.Round, new Vector2(Random.Range(-10f, 10f), Random.Range(-1.5f, 4.5f)),
                    new Vector2(Random.Range(0.8f, 1.6f), 0.05f), new Color(1, 1, 1, 0.55f), -100, true);
                streaks.Add(s.transform);
            }

            kid = Kid.Create("Kid", new Color32(250, 250, 250, 255), new Color32(40, 70, 150, 255), 2.1f, Loc.PlayerGender == Gender.Girl);
            kid.transform.position = new Vector2(0, GroundY);
            kid.SetDepthOrder(10);

            kite = SceneArt.Put(null, "Kite", ThaDieuArt.Kite(new Color32(226, 64, 54, 255), new Color32(250, 212, 70, 255)), Vector2.zero, Vector2.one * 1.15f, 30).transform;
            kiteTail = new GameObject("Tail").transform;
            kiteTail.SetParent(kite, false);
            for (var i = 0; i < 6; i++)
                SceneArt.Put(kiteTail, "Bow", ThaDieuArt.Bow(i % 2 == 0 ? new Color32(255, 220, 60, 255) : new Color32(60, 150, 230, 255)), Vector2.zero, Vector2.one * 1.4f, 29);
            kiteString = SpriteLine.Create("String", 24, 0.035f, new Color32(250, 250, 250, 255), 25);
        }


        /// <summary>A strip of scenery repeated side by side, drifting at <c>parallax</c> (1 = fixed to the ground).</summary>
        private readonly List<(Transform[] copies, float tile, float parallax)> bands = new List<(Transform[], float, float)>();

        private void AddBand(Sprite sprite, float tile, float bottom, float parallax, int order)
        {
            var copies = new Transform[3];
            for (var i = 0; i < 3; i++) copies[i] = SceneArt.Put(worldRoot, "Band", sprite, new Vector2(i * tile, bottom), Vector2.one, order).transform;
            bands.Add((copies, tile, parallax));
        }

        private void UpdateBands(float camX)
        {
            foreach (var (copies, tile, parallax) in bands)
            {
                // The strip has moved camX * (1 - parallax) with the view; tile it around the camera.
                var shift = camX * (1f - parallax);
                var first = camX - Mathf.Repeat(camX - shift, tile) - tile;
                for (var i = 0; i < copies.Length; i++)
                    copies[i].position = new Vector3(first + i * tile, copies[i].position.y, 0);
            }
        }
        /// <summary>Sky, sun and field stay with the camera (the field is endless).</summary>
        private void Follow(SpriteRenderer sr) => scenery.Add((sr.transform, 0f, sr.transform.position.x));

        // ---- Loop ----

        private void Update()
        {
            if (!Running) return;
            var dt = Time.deltaTime;
            UpdateWind(dt);
            runInput = DebugRun ?? ReadInput();

            // Move the child; the camera follows (endless field).
            var p = kid.transform.position;
            p.x += runInput * runSpeed * dt;
            kid.transform.position = p;
            kid.Walking = Mathf.Abs(runInput) > 0.01f;
            kid.Face(runInput);
            var cam = Camera.main;
            if (cam != null) cam.transform.position = new Vector3(p.x, 0, cam.transform.position.z);

            // Kite height (owner rules): right way rises, wrong way falls, standing still sinks slowly.
            float rate;
            if (Mathf.Abs(runInput) < 0.01f) rate = -standStillFallRate;
            else if (Mathf.Sign(runInput) == GoodDirection) rate = riseRate * Mathf.Lerp(0.3f, 1f, windStrength);
            else rate = -wrongWayFallRate;
            height = Mathf.Max(0f, height + rate * dt);

            timeLeft -= dt;
            PlaceKite(dt);
            UpdateScenery(dt);
            RefreshHud();

            if (height >= Target) Finish(Outcome.Win);
            else if (timeLeft <= 0f)
            {
                timeLeft = 0f;
                RefreshHud();
                Finish(Outcome.Loss);
            }
        }

        private float ReadInput()
        {
            if (Time.timeScale == 0f) return 0f;
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.leftArrowKey.isPressed || k.aKey.isPressed) return -1f;
                if (k.rightArrowKey.isPressed || k.dKey.isPressed) return 1f;
            }
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.isPressed) return 0f;
            var es = EventSystem.current;
            if (es != null && es.IsPointerOverGameObject()) return 0f;
            // Hold the left or right half of the screen.
            return pointer.position.ReadValue().x < Screen.width * 0.5f ? -1f : 1f;
        }

        private void UpdateWind(float dt)
        {
            if (lullTimer > 0f)
            {
                // The calm before the turn: the wind dies down, then comes back from the other side.
                lullTimer -= dt;
                var half = lullSeconds * 0.5f;
                if (lullTimer > half) windStrength = Mathf.Clamp01((lullTimer - half) / half);
                else windStrength = Mathf.Clamp01(1f - lullTimer / half);
                if (lullTimer <= half && !turned)
                {
                    windDir = -windDir;
                    turned = true;
                }
                if (lullTimer <= 0f) windStrength = 1f;
                return;
            }
            nextWindChange -= dt;
            if (nextWindChange <= 0f)
            {
                lullTimer = lullSeconds;
                turned = false;
                nextWindChange = Random.Range(windChangeEvery.x, windChangeEvery.y);
            }
        }

        private bool turned;

        /// <summary>The kite flies downwind of the child; screen height follows its height up to the target.</summary>
        private void PlaceKite(float dt)
        {
            kiteSway += dt * (1.5f + windStrength);
            kid.ArmRaise = 115f; // the arm reaches forward and up, holding the string
            var hand = kid.HandPosition;
            var lift = Mathf.Clamp01(height / (Target * 1.05f));
            var y = Mathf.Lerp(GroundY + 0.4f, 4.3f, Mathf.Sqrt(lift));
            var x = hand.x + windDir * Mathf.Lerp(1.2f, 6.5f, lift) + Mathf.Sin(kiteSway) * 0.25f;
            if (height <= 0.01f) y = GroundY + 0.3f; // fallen on the grass
            kite.position = new Vector2(x, y + Mathf.Sin(kiteSway * 1.7f) * 0.12f * windStrength);
            kite.rotation = Quaternion.Euler(0, 0, -windDir * 12f + Mathf.Sin(kiteSway * 1.3f) * 8f);

            // Tail streams downwind.
            for (var i = 0; i < kiteTail.childCount; i++)
            {
                var t = kiteTail.GetChild(i);
                t.position = (Vector2)kite.position + new Vector2(windDir * (0.15f + i * 0.22f), -0.55f - i * 0.18f + Mathf.Sin(kiteSway * 3f + i) * 0.08f);
            }
            kiteString.Set(hand, kite.position, Mathf.Lerp(0.6f, 1.4f, lift) * (1.2f - windStrength * 0.5f));
        }

        private void UpdateScenery(float dt)
        {
            var camX = kid.transform.position.x;
            UpdateBands(camX);
            foreach (var (t, parallax, baseX) in scenery)
            {
                // Far things move slower; wrap them so the field never ends.
                var x = baseX + camX * (1f - parallax);
                var rel = Mathf.Repeat(x - camX + 16f, 32f) - 16f;
                t.position = new Vector3(camX + rel, t.position.y, 0);
            }
            foreach (var (t, phase) in farKites)
                t.rotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 0.8f + phase) * 10f);
            foreach (var g in grass)
            {
                var rel = Mathf.Repeat(g.position.x - camX + 10f, 20f) - 10f;
                g.position = new Vector3(camX + rel, g.position.y, 0);
                g.rotation = Quaternion.Euler(0, 0, -windDir * 25f * windStrength + Mathf.Sin(Time.time * 4f + g.position.x) * 4f);
            }
            foreach (var s in streaks)
            {
                var pos = s.position + new Vector3(windDir * (3f + 6f * windStrength) * dt, 0, 0);
                var rel = Mathf.Repeat(pos.x - camX + 11f, 22f) - 11f;
                s.position = new Vector3(camX + rel, pos.y, 0);
                s.GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 0.55f * windStrength);
            }
        }

        // ---- HUD ----

        private void BuildHud()
        {
            var safe = Ui.Canvas("KiteHud", 8);
            // Only the time up top: the wind shows in the streaks and the bending grass (owner 2026-10-08: no "Gió" arrow).
            var top = Ui.Panel(safe, "Top", new Color(1, 1, 1, 0.88f)).Place(new Vector2(0.5f, 1f), new Vector2(280, 110), new Vector2(0, -24));
            timeText = Ui.Text(top.transform, "", 46, Ui.Ink);
            timeText.Stretch(Vector2.zero, Vector2.one, 8);

            // Height meter on the right with the target line.
            var meter = Ui.Panel(safe, "Meter", new Color(1, 1, 1, 0.75f)).Place(new Vector2(1f, 0.5f), new Vector2(70, 600), new Vector2(-40, -20));
            meterFill = (RectTransform)Ui.Panel(meter.transform, "Fill", Ui.Leaf).transform;
            meterFill.anchorMin = Vector2.zero;
            meterFill.anchorMax = new Vector2(1f, 0f);
            meterFill.offsetMin = new Vector2(8, 8);
            meterFill.offsetMax = new Vector2(-8, 8);
            meterTarget = (RectTransform)Ui.Panel(meter.transform, "Target", Ui.Brick, false).transform;
            meterTarget.anchorMin = new Vector2(0f, 1f);
            meterTarget.anchorMax = new Vector2(1f, 1f);
            meterTarget.offsetMin = new Vector2(-12, -14);
            meterTarget.offsetMax = new Vector2(12, -8);
            heightText = Ui.Text(safe, "", 40, Ui.Ink, TextAlignmentOptions.Right);
            heightText.Place(new Vector2(1f, 0.5f), new Vector2(400, 60), new Vector2(-130, -20));

            var hintPanel = Ui.Panel(safe, "Hint", new Color(1f, 0.97f, 0.9f, 0.85f)).Place(new Vector2(0.5f, 0f), new Vector2(1100, 72), new Vector2(0, 22));
            var hint = Ui.Label(hintPanel.transform, "mg.dieu.hint", 34, Ui.Ink);
            hint.Stretch(Vector2.zero, Vector2.one, 6);
            RefreshHud();
        }

        private void RefreshHud()
        {
            if (timeText == null) return;
            var t = Mathf.CeilToInt(Mathf.Max(0, timeLeft));
            timeText.text = $"{t / 60}:{t % 60:00}";
            timeText.color = timeLeft < 30f ? Ui.Brick : Ui.Ink;
            heightText.text = Loc.Format("mg.dieu.height", Mathf.FloorToInt(height), Mathf.RoundToInt(Target));
            var fill = Mathf.Clamp01(height / Target);
            var meterHeight = ((RectTransform)meterFill.parent).rect.height - 16f;
            meterFill.offsetMax = new Vector2(-8, 8 + meterHeight * fill);
        }

        // ---- Shapes ----

        private static SpriteRenderer Shape(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size, Color color, int order, bool sliced)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            if (parent != null) sr.transform.SetParent(parent, false);
            sr.transform.position = pos;
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (sliced)
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
