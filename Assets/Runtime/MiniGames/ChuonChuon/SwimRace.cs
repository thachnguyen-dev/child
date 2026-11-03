using System;
using System.Collections;
using System.Collections.Generic;
using ChildGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace ChildGame.MiniGames.ChuonChuon
{
    /// <summary>
    /// MG07 swimming race, side view (owner design §6–§14). Four swimmers, one lane each (the player's lane is the
    /// lowest). Every tap is one stroke:
    /// <list type="bullet">
    /// <item>Tap in front of the swimmer → arms: rise, almost no forward movement.</item>
    /// <item>Tap behind → legs: move forward, but sink a little.</item>
    /// </list>
    /// Oxygen drains while the head is under water and recovers above; empty = drowned = loss. The AI friends use the
    /// same rules with their own tap rate. Reach the finish first to win.
    /// ⚠ All numbers are placeholders.
    /// </summary>
    public class SwimRace : MonoBehaviour
    {
        // Stroke physics.
        public float armLift = 1.7f, armForward = 0.25f;
        public float legForward = 1.15f, legSink = 0.6f;
        public float forwardDrag = 1.0f, verticalDrag = 2.6f, sinkPull = 0.35f;
        public float headUnder = 0.45f, maxDepth = 2.2f;
        public float oxygenDrain = 0.22f, oxygenRecover = 0.5f;
        public float[] distance = { 70f, 100f, 130f };
        public float[] aiTapRate = { 3.0f, 3.8f, 4.6f };
        public float[] aiMistakes = { 0.2f, 0.1f, 0.04f };

        private class Swimmer
        {
            public bool player;
            public float x, depth, vx, vd, oxygen = 1f, nextTap, armAnim, legAnim;
            public int lane;
            public Transform view, arm, farArm, leg, farLeg, splash, handSplash, waterline;
            public readonly List<Transform> bubbles = new List<Transform>();
        }



        private const float PlayerScreenX = -4f;  // the player sits left of centre; tapping right of them is "in front"

        private readonly List<Swimmer> swimmers = new List<Swimmer>();
        private Swimmer me;
        private Difficulty difficulty;
        private float length;
        private bool running, scripted;
        private Action<Outcome> done;
        private GameObject hud;
        private TextMeshProUGUI bigText;
        private RectTransform oxygenFill, progressFill;
        private readonly List<RectTransform> dots = new List<RectTransform>();
        private Transform world, bankLayer;
        private SpriteRenderer sky;

        public bool Running => running;
        public bool Scripted => scripted;
        public float Length => length;
        public float PlayerDepth => me.depth;
        public float PlayerOxygen => me.oxygen;
        public float PlayerX => me.x;

        /// <summary>The real race against three friends.</summary>
        public static SwimRace Race(Difficulty difficulty, Action<Outcome> done) => Create(difficulty, false, done);

        /// <summary>The first attempts before swimming is learned: the child alone, sinking no matter what.</summary>
        public static SwimRace Struggle(Action done, string sayKey = null) => Create(Difficulty.Easy, true, _ => done(), sayKey);

        private static SwimRace Create(Difficulty difficulty, bool scripted, Action<Outcome> done, string sayKey = null)
        {
            var race = new GameObject("SwimRace").AddComponent<SwimRace>();
            race.difficulty = difficulty;
            race.scripted = scripted;
            race.sayKey = sayKey;
            race.done = done;
            race.length = race.distance[(int)difficulty];
            race.Build();
            race.StartCoroutine(scripted ? race.StruggleRoutine() : race.CountIn());
            return race;
        }

        // ---- Setup ----

        /// <summary>
        /// Rows across the river: the player's row is nearest (lowest on screen); the friends' rows are farther away,
        /// higher up and a little smaller. No lane lines: it is one river (owner 2026-10-08).
        /// </summary>
        private static float Surface(int lane) => -3.3f + lane * 1.35f;
        private static float RowScale(int lane) => 1f - lane * 0.08f;
        private static int RowOrder(int lane) => 100 - lane * 20;
        private const float FrontDepth = 1.9f;

        private void Build()
        {
            world = new GameObject("RiverWorld").transform;
            world.SetParent(transform, false);
            var cam = Camera.main;
            var halfW = 9.6f;
            if (cam != null)
            {
                cam.orthographicSize = 5.4f;
                cam.backgroundColor = new Color32(176, 216, 240, 255);
                halfW = cam.orthographicSize * cam.aspect;
            }
            // Sky (follows the camera), the far bank (slow parallax), the river, and the water in front of each row.
            sky = Put(transform, "Sky", RiverArt.Sky(halfW * 2f + 2f), Vector2.zero, -60);
            bankLayer = new GameObject("FarBank").transform;
            bankLayer.SetParent(transform, false);
            var bankSprite = RiverArt.Bank();
            for (var x = -RiverArt.BankTile * 2f; x < length * 0.7f + RiverArt.BankTile * 3f; x += RiverArt.BankTile)
                Put(bankLayer, "Bank", bankSprite, new Vector2(x, RiverArt.FarWater - 0.6f), -40);
            var lanes = scripted ? 1 : 4;
            var waterSprite = RiverArt.Water();
            for (var x = -RiverArt.WaterTile * 2f; x < length + 40f; x += RiverArt.WaterTile)
            {
                Put(world, "Water", waterSprite, new Vector2(x, RiverArt.WaterBottom), -50 + 5);
                for (var lane = 0; lane < lanes; lane++)
                {
                    var depth = FrontDepth * RowScale(lane);
                    Put(world, "WaterFront", RiverArt.Overlay(Surface(lane), depth), new Vector2(x, Surface(lane) - depth), RowOrder(lane) + 8);
                }
            }
            if (!scripted)
                for (var lane = 0; lane < lanes; lane++)
                {
                    // The finish: a bamboo stake with a red scarf in each row.
                    var s = Put(world, "Stake", RiverArt.Stake(), new Vector2(length + 0.4f, Surface(lane) - 0.1f), RowOrder(lane) + 1);
                    s.transform.localScale = Vector3.one * RowScale(lane);
                }

            var wear = new Color32[] { new Color32(40, 96, 200, 255), new Color32(214, 64, 56, 255), new Color32(56, 150, 80, 255), new Color32(140, 84, 190, 255) };
            for (var lane = 0; lane < lanes; lane++)
            {
                var s = new Swimmer { player = lane == 0, lane = lane, nextTap = Random.Range(0f, 0.4f) };
                // The player's look follows their profile; the friends are boys (placeholder cast).
                var girl = lane == 0 && Loc.PlayerGender == Gender.Girl;
                SwimmerView(s, RiverArt.Swimmer(wear[lane], girl), RowOrder(lane));
                s.waterline = Put(world, "Waterline", RiverArt.Waterline(), Vector2.zero, RowOrder(lane) + 9).transform;
                s.waterline.localScale = Vector3.one * RowScale(lane);
                for (var b = 0; b < 3; b++)
                {
                    var bubble = Put(world, "Bubble", RiverArt.Bubble(), Vector2.zero, RowOrder(lane) + 9);
                    bubble.gameObject.SetActive(false);
                    s.bubbles.Add(bubble.transform);
                }
                swimmers.Add(s);
            }
            me = swimmers[0];
            BuildHud();
            Place();
        }

        private void SwimmerView(Swimmer s, RiverArt.SwimmerSprites art, int order)
        {
            var root = new GameObject("Swimmer").transform;
            root.SetParent(world, false);
            root.localScale = Vector3.one * (1.35f * RowScale(s.lane));
            var shade = new Color(0.78f, 0.78f, 0.8f, 1f); // the far-side limbs, in the body's shadow
            s.farArm = Limb(root, "FarArm", art.arm, new Vector2(-0.2f, 0.06f), shade, order + 0);
            s.farLeg = Limb(root, "FarLeg", art.leg, new Vector2(-1.1f, 0.0f), shade, order + 0);
            Put(root, "Body", art.body, Vector2.zero, order + 2).transform.localPosition = Vector3.zero;
            s.leg = Limb(root, "Leg", art.leg, new Vector2(-1.1f, -0.05f), Color.white, order + 3);
            s.arm = Limb(root, "Arm", art.arm, new Vector2(-0.22f, 0.02f), Color.white, order + 3);
            var splash = Put(root, "Splash", RiverArt.Splash(), Vector2.zero, order + 9);
            splash.transform.localPosition = new Vector2(-2.0f, 0.02f);
            splash.gameObject.SetActive(false);
            s.splash = splash.transform;
            var handSplash = Put(root, "HandSplash", RiverArt.Splash(), Vector2.zero, order + 9);
            handSplash.transform.localPosition = new Vector2(0.75f, 0.02f);
            handSplash.transform.localScale = Vector3.one * 0.7f;
            handSplash.gameObject.SetActive(false);
            s.handSplash = handSplash.transform;
            s.view = root;
        }

        private static Transform Limb(Transform root, string name, Sprite sprite, Vector2 pivot, Color tint, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root, false);
            sr.transform.localPosition = pivot;
            sr.sprite = sprite;
            sr.color = tint;
            sr.sortingOrder = order;
            return sr.transform;
        }

        private static SpriteRenderer Put(Transform parent, string name, Sprite sprite, Vector2 pos, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = pos;
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>A wake ring spreading on the surface behind a stroke.</summary>
        private IEnumerator Wake(Swimmer s, Vector2 at)
        {
            var ring = Put(world, "Wake", RiverArt.Ring(), at, RowOrder(s.lane) + 9);
            var scale = RowScale(s.lane);
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
            {
                ring.transform.localScale = Vector3.one * (scale * Mathf.Lerp(0.4f, 1.6f, t));
                ring.color = new Color(1f, 1f, 1f, 1f - t);
                yield return null;
            }
            Destroy(ring.gameObject);
        }

        // ---- Race ----

        private IEnumerator CountIn()
        {
            for (var n = 3; n >= 1; n--)
            {
                bigText.text = n.ToString();
                yield return new WaitForSeconds(0.7f);
            }
            bigText.text = Loc.Get("mg.mocau.go");
            running = true;
            yield return new WaitForSeconds(0.6f);
            bigText.text = "";
        }

        /// <summary>
        /// The child jumps in ("Bùm!"), splashes about kicking (does not know the strokes yet) and sinks; nothing is at
        /// stake. <see cref="sayKey"/>, if set, is what the child shouts while going under.
        /// </summary>
        private IEnumerator StruggleRoutine()
        {
            bigText.text = "";
            // The jump: from above the water, down with a big splash.
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.45f)
            {
                jumpLift = Mathf.Lerp(3.2f, 0f, t * t);
                yield return null;
            }
            jumpLift = 0f;
            StartCoroutine(BigSplash(new Vector2(me.x - 0.6f, Surface(0))));
            Sfx.Play(Snd.Splash, 1f, 0.7f, 0f);
            StartCoroutine(Wake(me, new Vector2(me.x - 0.6f, Surface(0))));
            running = true;
            if (!string.IsNullOrEmpty(sayKey)) Say(Loc.Get(sayKey), 2.8f);
            for (var t = 0f; t < 3.2f; t += 0.3f)
            {
                Stroke(me, false);
                yield return new WaitForSeconds(0.3f);
            }
            running = false;
            yield return new WaitForSeconds(0.9f);
            Finish(Outcome.Loss);
        }

        private float jumpLift;
        private Transform hudSafe;
        private string sayKey;

        private IEnumerator BigSplash(Vector2 at)
        {
            var s = Put(world, "BigSplash", RiverArt.Splash(), at, RowOrder(0) + 10);
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.7f)
            {
                s.transform.localScale = new Vector3(Mathf.Lerp(1.2f, 2.6f, t), Mathf.Lerp(1.6f, 2.4f, Mathf.Sqrt(t)), 1f);
                s.color = new Color(1f, 1f, 1f, 1f - t * t);
                yield return null;
            }
            Destroy(s.gameObject);
        }

        /// <summary>A speech bubble over the player's head for a moment.</summary>
        private void Say(string text, float seconds)
        {
            var panel = Ui.Panel(hudSafe, "Say", Color.white).Place(new Vector2(0.5f, 0.5f), new Vector2(980, 120), new Vector2(-180, 120));
            var t = Ui.Text(panel.transform, text, 46, Ui.Sky);
            t.Stretch(Vector2.zero, Vector2.one, 12);
            Destroy(panel.gameObject, seconds);
        }

        private void Update()
        {
            if (running)
            {
                if (!scripted && Time.timeScale > 0f) ReadTaps();
                var dt = Time.deltaTime;
                foreach (var s in swimmers)
                {
                    if (!s.player && !scripted) AiThink(s, dt);
                    Simulate(s, dt);
                }
                if (!scripted) CheckEnd();
            }
            Place();
            RefreshHud();
        }

        private void ReadTaps()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var playerScreen = cam.WorldToScreenPoint(new Vector3(me.x, 0, 0)).x;
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame) Stroke(me, true);
                if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) Stroke(me, false);
            }
            var es = EventSystem.current;
            var touch = Touchscreen.current;
            if (touch != null)
            {
                // Every new finger counts: fast alternating taps with two thumbs are allowed.
                foreach (var t in touch.touches)
                {
                    if (!t.press.wasPressedThisFrame) continue;
                    if (es != null && es.IsPointerOverGameObject(t.touchId.ReadValue())) continue;
                    Stroke(me, t.position.ReadValue().x > playerScreen);
                }
                return;
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && (es == null || !es.IsPointerOverGameObject()))
                Stroke(me, mouse.position.ReadValue().x > playerScreen);
        }

        /// <summary>One stroke: arms (rise) or legs (forward and sink). Public for the capture harness.</summary>
        public void Stroke(bool arms)
        {
            if (!scripted) Stroke(me, arms);
        }

        private void Stroke(Swimmer s, bool arms)
        {
            if (!running) return;
            if (arms)
            {
                s.vd -= armLift;
                s.vx += armForward;
                s.armAnim = 1f;
            }
            else
            {
                s.vx += legForward;
                s.vd += legSink;
                s.legAnim = 1f;
            }
            if (s.player) Sfx.Play(Snd.Splash, arms ? 0.6f : 0.4f, arms ? 1f : 1.3f);
            // A ring spreads on the water where the hand or the feet hit it.
            var scale = RowScale(s.lane) * 1.35f;
            StartCoroutine(Wake(s, new Vector2(s.x + (arms ? 0.8f : -2.0f) * scale, Surface(s.lane))));
        }

        private void Simulate(Swimmer s, float dt)
        {
            s.vx *= Mathf.Exp(-forwardDrag * dt);
            s.vd *= Mathf.Exp(-verticalDrag * dt);
            s.vd += sinkPull * dt;
            s.x += s.vx * dt;
            s.depth += s.vd * dt;
            if (s.depth < 0f)
            {
                s.depth = 0f;
                s.vd = Mathf.Max(0f, s.vd);
            }
            if (s.depth > maxDepth)
            {
                s.depth = maxDepth;
                s.vd = Mathf.Min(0f, s.vd);
            }
            if (s.depth > headUnder) s.oxygen -= oxygenDrain * dt;
            else s.oxygen += oxygenRecover * dt;
            s.oxygen = Mathf.Clamp01(s.oxygen);
            s.armAnim = Mathf.Max(0, s.armAnim - dt * 4f);
            s.legAnim = Mathf.Max(0, s.legAnim - dt * 5f);
        }

        /// <summary>AI friends tap at their own rate, choosing arms when too deep or short of breath, legs otherwise.</summary>
        private void AiThink(Swimmer s, float dt)
        {
            s.nextTap -= dt;
            if (s.nextTap > 0f) return;
            var rate = aiTapRate[(int)difficulty];
            s.nextTap = 1f / rate * Random.Range(0.75f, 1.25f);
            var wantArms = s.depth > 0.25f || s.oxygen < 0.45f;
            if (Random.value < aiMistakes[(int)difficulty]) wantArms = !wantArms;
            Stroke(s, wantArms);
        }

        private void CheckEnd()
        {
            if (me.oxygen <= 0f)
            {
                StartCoroutine(End(Outcome.Loss, "mg.chuon.drowned"));
                return;
            }
            if (me.x >= length)
            {
                StartCoroutine(End(Outcome.Win, "mg.chuon.first"));
                return;
            }
            foreach (var s in swimmers)
                if (!s.player && s.x >= length)
                {
                    StartCoroutine(End(Outcome.Loss, "mg.chuon.friendfirst"));
                    return;
                }
        }

        private IEnumerator End(Outcome outcome, string key)
        {
            Debug.Log($"[CHILD-SWIM] {key} at x {me.x:0.0}/{length}, oxygen {me.oxygen:0.00}");
            running = false;
            bigText.text = Loc.Get(key);
            bigText.color = outcome == Outcome.Win ? Ui.Leaf : Ui.Brick;
            yield return new WaitForSeconds(1.8f);
            Finish(outcome);
        }

        private void Finish(Outcome outcome)
        {
            Destroy(hud);
            done?.Invoke(outcome);
            Destroy(gameObject);
        }

        /// <summary>Capture autopilot: the same choice rule as the AI.</summary>
        public bool DebugWantsArms() => me.depth > 0.25f || me.oxygen < 0.45f;

        // ---- View ----

        private void Place()
        {
            foreach (var s in swimmers)
            {
                var surface = Surface(s.lane);
                var scale = RowScale(s.lane);
                // At the surface the back and head ride above the water; sinking takes them under.
                var y = surface + (0.12f - s.depth * 0.6f) * scale + (s.player ? jumpLift : 0f);
                s.view.position = new Vector3(s.x, y + Mathf.Sin(Time.time * 3f + s.lane) * 0.04f * scale, 0);
                s.view.rotation = Quaternion.Euler(0, 0, Mathf.Clamp(-s.vd * 8f, -20f, 20f));
                // Arms windmill in turn; legs flutter, harder right after a kick.
                var armAngle = Mathf.Lerp(-20f, 200f, s.armAnim);
                s.arm.localRotation = Quaternion.Euler(0, 0, armAngle);
                s.farArm.localRotation = Quaternion.Euler(0, 0, armAngle + 180f);
                var kick = Mathf.Sin(Time.time * 16f + s.lane * 1.7f) * (3f + 14f * s.legAnim);
                s.leg.localRotation = Quaternion.Euler(0, 0, kick);
                s.farLeg.localRotation = Quaternion.Euler(0, 0, -kick);
                s.splash.gameObject.SetActive(s.legAnim > 0.3f);
                // The water laps around the body while it is at the surface.
                s.waterline.position = new Vector3(s.x - 0.55f * scale * 1.35f, surface, 0);
                s.waterline.gameObject.SetActive(s.depth < headUnder && (!s.player || jumpLift <= 0f));
                s.handSplash.gameObject.SetActive(s.armAnim > 0.04f && s.armAnim < 0.2f);
                var under = s.depth > headUnder;
                for (var b = 0; b < s.bubbles.Count; b++)
                {
                    s.bubbles[b].gameObject.SetActive(under);
                    if (!under) continue;
                    var phase = Mathf.Repeat(Time.time * 0.8f + b * 0.33f, 1f);
                    s.bubbles[b].position = new Vector3(s.x + (0.45f + b * 0.12f) * scale, y + phase * (surface - y), 0);
                }
            }
            var cam = Camera.main;
            if (cam == null) return;
            var camX = me.x - PlayerScreenX;
            cam.transform.position = new Vector3(camX, 0, cam.transform.position.z);
            sky.transform.position = new Vector3(camX, 0, 0);
            bankLayer.position = new Vector3(camX * 0.6f, 0, 0); // the far bank drifts by slower
        }

        private void BuildHud()
        {
            var safe = Ui.Canvas("SwimHud", 8);
            hud = safe.root.gameObject;
            hudSafe = safe;
            bigText = Ui.Text(safe, "", 110, Ui.Ink);
            bigText.Place(new Vector2(0.5f, 0.62f), new Vector2(1500, 180));
            if (scripted) return;
            var o2 = Ui.Panel(safe, "Oxygen", new Color(1, 1, 1, 0.85f)).Place(new Vector2(0f, 1f), new Vector2(560, 90), new Vector2(230, -24));
            Ui.Label(o2.transform, "mg.chuon.oxygen", 32, Ui.Ink, TextAlignmentOptions.Left).Place(new Vector2(0f, 0.5f), new Vector2(180, 70), new Vector2(20, 0));
            var bar = Ui.Panel(o2.transform, "Bar", new Color(0.85f, 0.85f, 0.85f, 1f)).Place(new Vector2(1f, 0.5f), new Vector2(330, 40), new Vector2(-20, 0));
            oxygenFill = (RectTransform)Ui.Panel(bar.transform, "Fill", Ui.Sky).transform;
            oxygenFill.anchorMin = Vector2.zero;
            oxygenFill.anchorMax = new Vector2(1, 1);
            oxygenFill.offsetMin = oxygenFill.offsetMax = Vector2.zero;

            var track = Ui.Panel(safe, "Progress", new Color(1, 1, 1, 0.75f)).Place(new Vector2(1f, 1f), new Vector2(760, 36), new Vector2(-40, -50));
            progressFill = (RectTransform)track.transform;
            var colors = new Color[] { new Color32(30, 90, 200, 255), new Color32(220, 70, 60, 255), new Color32(60, 160, 80, 255), new Color32(150, 90, 200, 255) };
            for (var i = 0; i < swimmers.Count; i++)
            {
                var dot = Ui.Icon(track.transform, "Dot", Ui.Circle, colors[i], i == 0 ? 44 : 32);
                var rt = (RectTransform)dot.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                dots.Add(rt);
            }
            dots[0].SetAsLastSibling();
            var hintPanel = Ui.Panel(safe, "Hint", new Color(1f, 0.97f, 0.9f, 0.85f)).Place(new Vector2(0.5f, 0f), new Vector2(1000, 70), new Vector2(150, 24));
            var hint = Ui.Label(hintPanel.transform, "mg.chuon.swimhint", 30, Ui.Ink);
            hint.Stretch(Vector2.zero, Vector2.one, 6);
        }

        private void RefreshHud()
        {
            if (oxygenFill == null) return;
            oxygenFill.anchorMax = new Vector2(Mathf.Max(0.001f, me.oxygen), 1);
            oxygenFill.GetComponent<UnityEngine.UI.Image>().color = me.oxygen < 0.3f ? Ui.Brick : Ui.Sky;
            var w = progressFill.rect.width;
            for (var i = 0; i < swimmers.Count && i < dots.Count; i++)
                dots[i].anchoredPosition = new Vector2(Mathf.Clamp01(swimmers[i].x / length) * w, 0);
        }

        private void OnDestroy()
        {
            if (hud != null) Destroy(hud);
        }
    }
}
