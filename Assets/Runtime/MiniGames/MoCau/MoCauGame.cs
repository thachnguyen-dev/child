using System.Collections;
using System.Collections.Generic;
using ChildGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ChildGame.MiniGames.MoCau
{
    /// <summary>
    /// MG06 — Đua mo cau: a top-down arcade race on areca-palm sheath sleds (mo cau) against 3 AI children, down the
    /// slope at the village entrance (official generated art: <see cref="MoCauArt"/>).
    /// <list type="bullet">
    /// <item>Everyone moves forward on their own; the player only steers left / right (owner 2026-10-07).</item>
    /// <item>Bumping another racer slows both; hitting an obstacle stops the racer for a moment.</item>
    /// <item>Win by finishing in the required place: Easy top 3, Normal top 2, Hard 1st.</item>
    /// </list>
    /// The track scrolls upward; the camera is zoomed out a little so the road ahead is readable in landscape.
    /// ⚠ Values marked placeholder are temporary and easy to tune.
    /// </summary>
    public class MoCauGame : MiniGame
    {
        [Header("Design values")]
        [SerializeField] private int[] requiredPlace = { 3, 2, 1 };

        [Header("Placeholder tuning")]
        [SerializeField] private float[] trackLength = { 380f, 480f, 580f };
        [SerializeField] private float[] obstacleRowEvery = { 12f, 9.5f, 7.5f };
        [SerializeField] private float[] aiSpeed = { 0.92f, 0.98f, 1.03f };
        [SerializeField] private float[] aiLookAhead = { 5f, 8f, 11f };
        [Tooltip("Chance that an AI racer misjudges an obstacle in its way.")]
        [SerializeField] private float[] aiCareless = { 0.35f, 0.18f, 0.06f };
        [SerializeField] private float cruiseSpeed = 8f;
        [SerializeField] private float acceleration = 6f;
        [SerializeField] private float steerSpeed = 7f;
        [SerializeField] private float obstacleStopSeconds = 1.1f;
        [SerializeField] private float bumpSlowdown = 0.7f;

        private const float HalfWidth = 4.3f;
        private const float RacerRadius = 0.5f;
        private const float CameraSize = 7f;

        private class Racer
        {
            public string name;
            public bool player;
            public Transform view;
            public float x, y, speed, top, stopTimer, bumpCooldown, targetX, wander, dustTimer;
            public int finishPlace;
            public Obstacle ignore;
            public readonly HashSet<Obstacle> judged = new HashSet<Obstacle>();
            public readonly HashSet<Obstacle> blind = new HashSet<Obstacle>();
        }

        private class Obstacle
        {
            public Rect box;
            public Transform view;
        }

        private readonly List<Racer> racers = new List<Racer>();
        private readonly List<Obstacle> obstacles = new List<Obstacle>();
        private Racer me;
        private float length;
        private bool racing;
        private int finishedCount;
        private TextMeshProUGUI placeText, bigText;
        private RectTransform progressFill;
        private readonly List<RectTransform> racerDots = new List<RectTransform>();
        private static readonly Color32[] Shirts =
        {
            new Color32(245, 245, 245, 255), new Color32(220, 70, 60, 255), new Color32(70, 160, 80, 255), new Color32(140, 90, 200, 255),
        };

        public bool Racing => racing;
        public int PlayerPlace => me.finishPlace > 0 ? me.finishPlace : LivePlace(me);
        /// <summary>Overrides touch steering when set (capture harness).</summary>
        public float? DebugSteer { get; set; }

        // ---- Setup ----

        protected override void OnBegin()
        {
            length = trackLength[(int)Difficulty];
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographicSize = CameraSize;
                cam.backgroundColor = new Color32(118, 178, 84, 255);
            }
            BuildTrack();
            BuildRacers();
            BuildHud();
            StartCoroutine(Countdown());
        }

        private void BuildTrack()
        {
            var root = new GameObject("Track").transform;
            // Official generated art (owner 2026-10-08): the slope at the village entrance, verges with trees, grass and
            // bushes, the village gate at the top, a chalk start line and a single finish line.
            var ground = MoCauArt.Ground(HalfWidth);
            for (var y = -MoCauArt.TileHeight; y < length + 30f; y += MoCauArt.TileHeight)
                Put(root, "Ground", ground, new Vector2(0, y), Vector2.one, -100);
            // The racers set off under the village gate, just past the start line.
            Put(root, "VillageGate", MoCauArt.Gate(HalfWidth * 2f + 1.4f), new Vector2(0, 4.2f), Vector2.one, 85);
            Put(root, "StartLine", MoCauArt.ChalkLine(), new Vector2(0, 1.2f), new Vector2(HalfWidth * 2f, 1f), -90);
            Put(root, "Finish", MoCauArt.ChalkLine(), new Vector2(0, length), new Vector2(HalfWidth * 2f + 0.6f, 1.8f), -90);

            // Roadside: areca palms, leafy trees and bushes on both verges.
            foreach (var side in new[] { -1f, 1f })
                for (var y = -12f; y < length + 25f; y += Random.Range(2.2f, 4.2f))
                {
                    var pick = Random.value;
                    if (pick < 0.3f)
                        Put(root, "Areca", MoCauArt.Areca(Random.Range(0, 3)), new Vector2(side * Random.Range(HalfWidth + 2.2f, HalfWidth + 9f), y),
                            Vector2.one * Random.Range(0.85f, 1.1f), 72);
                    else if (pick < 0.62f)
                        Put(root, "Tree", MoCauArt.Tree(Random.Range(0, 4)), new Vector2(side * Random.Range(HalfWidth + 2.3f, HalfWidth + 9f), y),
                            Vector2.one * Random.Range(0.8f, 1.1f), 70);
                    else
                        Put(root, "Bush", MoCauArt.Bush(Random.Range(0, 4)), new Vector2(side * Random.Range(HalfWidth + 1.1f, HalfWidth + 8f), y),
                            Vector2.one * Random.Range(0.9f, 1.4f), 68);
                }

            // Obstacle rows, each leaving a clear gap of at least 2.4 units.
            var every = obstacleRowEvery[(int)Difficulty];
            var gapCenter = 0f;
            var lastY = 0f;
            for (var y = 22f; y < length - 12f; y += every * Random.Range(0.8f, 1.2f))
            {
                // The gap may only move as far as a racer can steer before reaching it (with margin), so every row is
                // always passable.
                var maxShift = steerSpeed * 0.55f * (y - lastY) / (cruiseSpeed * 1.05f);
                gapCenter = Mathf.Clamp(gapCenter + Random.Range(-maxShift, maxShift), -HalfWidth + 1.5f, HalfWidth - 1.5f);
                lastY = y;
                var count = Random.value < 0.5f ? 1 : 2;
                for (var i = 0; i < count; i++)
                {
                    for (var tries = 0; tries < 10; tries++)
                    {
                        var kind = Random.Range(0, 4);
                        var size = kind == 3 ? new Vector2(Random.Range(1.8f, 2.6f), 0.55f) : kind == 1 ? new Vector2(1.4f, 0.9f) : new Vector2(0.95f, 0.85f);
                        var x = Random.Range(-HalfWidth + size.x * 0.5f, HalfWidth - size.x * 0.5f);
                        var box = new Rect(x - size.x * 0.5f, y - size.y * 0.5f, size.x, size.y);
                        if (box.xMax > gapCenter - 1.2f && box.xMin < gapCenter + 1.2f) continue;
                        var clash = false;
                        foreach (var o in obstacles) clash |= o.box.Overlaps(new Rect(box.x - 0.6f, box.y - 0.6f, box.width + 1.2f, box.height + 1.2f));
                        if (clash) continue;
                        obstacles.Add(new Obstacle { box = box, view = ObstacleView(root, kind, box) });
                        break;
                    }
                }
            }
        }

        /// <summary>Obstacles (owner 2026-10-08): raised dirt mounds, big rocks and bushes.</summary>
        private static Transform ObstacleView(Transform root, int kind, Rect box)
        {
            var variant = Random.Range(0, 3);
            var scale = new Vector2(box.width * 1.12f, box.height * 1.18f);
            return kind switch
            {
                0 => Put(root, "Rock", MoCauArt.Rock(variant), box.center, scale, -50).transform,
                2 => Put(root, "Bush", MoCauArt.Bush(variant), box.center, scale, -50).transform,
                _ => Put(root, "Mound", MoCauArt.Mound(variant), box.center, scale, -50).transform, // 1 = mound, 3 = long ridge
            };
        }

        private void BuildRacers()
        {
            var lanes = new List<float> { -3f, -1f, 1f, 3f };
            for (var i = 0; i < 4; i++)
            {
                var lane = lanes[Random.Range(0, lanes.Count)];
                lanes.Remove(lane);
                var r = new Racer
                {
                    name = i == 0 ? "you" : "ai" + i,
                    player = i == 0,
                    x = lane,
                    y = 0,
                    targetX = lane,
                    top = cruiseSpeed * (i == 0 ? 1f : aiSpeed[(int)Difficulty] * Random.Range(0.98f, 1.02f)),
                    wander = Random.Range(0f, 10f),
                };
                r.view = Sled(Shirts[i], i == 0).transform;
                racers.Add(r);
            }
            me = racers[0];
            PlaceViews();
        }

        // ---- Race loop ----

        private IEnumerator Countdown()
        {
            for (var n = 3; n >= 1; n--)
            {
                bigText.text = n.ToString();
                yield return new WaitForSeconds(0.8f);
            }
            bigText.text = Loc.Get("mg.mocau.go");
            racing = true;
            yield return new WaitForSeconds(0.7f);
            if (bigText.text == Loc.Get("mg.mocau.go")) bigText.text = "";
        }

        private void Update()
        {
            if (!Running) return;
            var dt = Time.deltaTime;
            if (racing)
            {
                foreach (var r in racers)
                {
                    Step(r, dt);
                    // Dust behind a sled sliding fast down the slope.
                    r.dustTimer -= dt;
                    if (r.speed > 3f && r.dustTimer <= 0f && r.finishPlace == 0)
                    {
                        r.dustTimer = 0.14f;
                        StartCoroutine(Dust(new Vector2(r.x + Random.Range(-0.25f, 0.25f), r.y - 1.3f)));
                    }
                }
                Bumps();
            }
            PlaceViews();
            RefreshHud();
        }

        private void Step(Racer r, float dt)
        {
            if (r.finishPlace > 0)
            {
                // Coast past the line.
                r.speed = Mathf.MoveTowards(r.speed, 2f, 4f * dt);
                r.y += r.speed * dt;
                return;
            }
            r.bumpCooldown -= dt;
            if (r.stopTimer > 0)
            {
                r.stopTimer -= dt;
                r.speed = 0;
            }
            else
            {
                // Mild catch-up (placeholder): AI racers far behind the player speed up a little and far ahead ease off,
                // so the pack stays close; obstacles and bumps still decide the race.
                var top = r.top;
                if (!r.player) top *= 1f + Mathf.Clamp((me.y - r.y) / 50f, -0.06f, 0.08f);
                r.speed = Mathf.MoveTowards(r.speed, top, acceleration * dt);
            }

            var steer = r.player ? PlayerSteer() : AiSteer(r, dt);
            r.x = Mathf.Clamp(r.x + steer * steerSpeed * dt * (r.stopTimer > 0 ? 0.3f : 1f), -HalfWidth + RacerRadius, HalfWidth - RacerRadius);
            r.y += r.speed * dt;

            // Obstacles stop the racer for a moment (owner rule). The same obstacle cannot stop it twice.
            foreach (var o in obstacles)
            {
                if (o == r.ignore || r.stopTimer > 0) continue;
                if (!CircleHitsBox(r.x, r.y, RacerRadius * 0.9f, o.box)) continue;
                r.stopTimer = obstacleStopSeconds;
                r.speed = 0;
                r.ignore = o;
                StartCoroutine(Wobble(r.view));
                if (r.player) Sfx.Play(Snd.Hit, 0.8f);
                break;
            }

            if (r.y >= length)
            {
                r.finishPlace = ++finishedCount;
                if (r.player) StartCoroutine(PlayerFinished());
            }
        }

        private float PlayerSteer()
        {
            if (DebugSteer.HasValue) return DebugSteer.Value;
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
            return pointer.position.ReadValue().x < Screen.width * 0.5f ? -1f : 1f;
        }

        /// <summary>AI: look ahead for obstacles in its path and slide into the nearest gap (sometimes misjudging).</summary>
        private float AiSteer(Racer r, float dt)
        {
            var look = aiLookAhead[(int)Difficulty];
            Obstacle threat = null;
            foreach (var o in obstacles)
            {
                if (o == r.ignore || o.box.yMin < r.y || o.box.yMin > r.y + look) continue;
                if (o.box.xMax < r.x - RacerRadius - 0.2f || o.box.xMin > r.x + RacerRadius + 0.2f) continue;
                if (threat == null || o.box.yMin < threat.box.yMin) threat = o;
            }
            if (threat != null && !r.judged.Contains(threat))
            {
                r.judged.Add(threat);
                if (Random.value < aiCareless[(int)Difficulty]) r.blind.Add(threat);
            }
            if (threat != null && !r.blind.Contains(threat)) r.targetX = FreeX(r, threat.box.yMin - 0.5f, threat.box.yMax + 2.5f);
            else if (threat == null)
            {
                // Drift a little, like a child wobbling on the sled.
                r.wander += dt * 0.5f;
                r.targetX = Mathf.Clamp(r.targetX + Mathf.Sin(r.wander) * 0.4f * dt, -HalfWidth + 0.8f, HalfWidth - 0.8f);
            }
            var diff = r.targetX - r.x;
            return Mathf.Abs(diff) < 0.05f ? 0f : Mathf.Clamp(diff * 2f, -0.85f, 0.85f);
        }

        /// <summary>The closest lateral position whose lane is clear of obstacles over the look-ahead.</summary>
        private float FreeX(Racer r, float fromY, float toY)
        {
            var best = r.x;
            var bestCost = float.MaxValue;
            for (var x = -HalfWidth + 0.7f; x <= HalfWidth - 0.7f; x += 0.35f)
            {
                var clear = true;
                foreach (var o in obstacles)
                {
                    if (o == r.ignore || o.box.yMax < fromY || o.box.yMin > toY) continue;
                    if (o.box.xMax > x - RacerRadius - 0.25f && o.box.xMin < x + RacerRadius + 0.25f) { clear = false; break; }
                }
                if (!clear) continue;
                var cost = Mathf.Abs(x - r.x);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = x;
                }
            }
            return best;
        }

        /// <summary>Racers that touch slow each other down and bounce apart (owner rule).</summary>
        private void Bumps()
        {
            for (var i = 0; i < racers.Count; i++)
            for (var j = i + 1; j < racers.Count; j++)
            {
                var a = racers[i];
                var b = racers[j];
                if (a.finishPlace > 0 || b.finishPlace > 0) continue;
                var d = new Vector2(b.x - a.x, (b.y - a.y) * 0.8f);
                if (d.magnitude > RacerRadius * 2f) continue;
                var push = Mathf.Sign(d.x == 0 ? Random.value - 0.5f : d.x) * 0.35f;
                a.x = Mathf.Clamp(a.x - push, -HalfWidth + RacerRadius, HalfWidth - RacerRadius);
                b.x = Mathf.Clamp(b.x + push, -HalfWidth + RacerRadius, HalfWidth - RacerRadius);
                if (a.bumpCooldown <= 0f)
                {
                    a.speed *= bumpSlowdown;
                    a.bumpCooldown = 0.6f;
                }
                if (b.bumpCooldown <= 0f)
                {
                    b.speed *= bumpSlowdown;
                    b.bumpCooldown = 0.6f;
                }
            }
        }

        private IEnumerator PlayerFinished()
        {
            racing = true;
            var place = me.finishPlace;
            var won = place <= requiredPlace[(int)Difficulty];
            bigText.text = Loc.Format("mg.mocau.finished", place);
            bigText.color = won ? Ui.Leaf : Ui.Brick;
            yield return new WaitForSeconds(1.8f);
            Finish(won ? Outcome.Win : Outcome.Loss);
        }

        private int LivePlace(Racer r)
        {
            var place = 1;
            foreach (var o in racers)
                if (o != r && (o.finishPlace > 0 || o.y > r.y)) place++;
            return place;
        }

        private void PlaceViews()
        {
            foreach (var r in racers)
            {
                r.view.position = new Vector3(r.x, r.y, 0);
                foreach (var sr in r.view.GetComponentsInChildren<SpriteRenderer>())
                    sr.sortingOrder = (r.player ? 60 : 40) + sr.transform.GetSiblingIndex();
            }
            var cam = Camera.main;
            if (cam != null) cam.transform.position = new Vector3(0, me.y + CameraSize * 0.5f, cam.transform.position.z);
        }

        private static IEnumerator Wobble(Transform t)
        {
            for (var a = 0f; a < 0.6f; a += Time.deltaTime)
            {
                t.rotation = Quaternion.Euler(0, 0, Mathf.Sin(a * 40f) * 18f * (1f - a / 0.6f));
                yield return null;
            }
            t.rotation = Quaternion.identity;
        }

        private static bool CircleHitsBox(float cx, float cy, float r, Rect box)
        {
            var nx = Mathf.Clamp(cx, box.xMin, box.xMax);
            var ny = Mathf.Clamp(cy, box.yMin, box.yMax);
            return (nx - cx) * (nx - cx) + (ny - cy) * (ny - cy) < r * r;
        }

        /// <summary>The obstacle-free steering a careful player would use (capture harness autopilot).</summary>
        public float DebugAutopilot()
        {
            Obstacle next = null;
            foreach (var o in obstacles)
                if (o != me.ignore && o.box.yMax > me.y && o.box.yMin < me.y + 10f && (next == null || o.box.yMin < next.box.yMin)) next = o;
            var target = next == null ? me.x : FreeX(me, next.box.yMin - 0.5f, next.box.yMax + 2.5f);
            var diff = target - me.x;
            return Mathf.Abs(diff) < 0.05f ? 0f : Mathf.Clamp(diff * 2f, -1f, 1f);
        }

        // ---- HUD ----

        private void BuildHud()
        {
            var safe = Ui.Canvas("RaceHud", 8);
            var panel = Ui.Panel(safe, "Place", new Color(1, 1, 1, 0.88f)).Place(new Vector2(1f, 1f), new Vector2(360, 110), new Vector2(-24, -24));
            placeText = Ui.Text(panel.transform, "", 48, Ui.Ink);
            placeText.Stretch(Vector2.zero, Vector2.one);
            var bar = Ui.Panel(safe, "Progress", new Color(1, 1, 1, 0.7f)).Place(new Vector2(1f, 0.5f), new Vector2(40, 560), new Vector2(-40, -30));
            progressFill = (RectTransform)Ui.Panel(bar.transform, "Fill", Ui.Sun).transform;
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(1, 0);
            progressFill.offsetMin = new Vector2(6, 6);
            progressFill.offsetMax = new Vector2(-6, 6);
            // One dot per racer on the bar, so the others are visible even off screen.
            for (var i = 0; i < 4; i++)
            {
                var dot = Ui.Icon(bar.transform, "Racer" + i, Ui.Circle, Shirts[i], i == 0 ? 46 : 34);
                var rt = (RectTransform)dot.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                if (i == 0) dot.gameObject.AddComponent<Outline>().effectColor = Ui.Ink;
                racerDots.Add(rt);
            }
            racerDots[0].SetAsLastSibling();
            var goalPanel = Ui.Panel(safe, "Goal", new Color(1f, 0.97f, 0.9f, 0.88f)).Place(new Vector2(0f, 0f), new Vector2(560, 70), new Vector2(24, 24));
            var goal = Ui.Label(goalPanel.transform, "mg.mocau.goal." + Difficulty.ToString().ToLowerInvariant(), 32, Ui.Ink);
            goal.Stretch(Vector2.zero, Vector2.one, 8);
            goal.alignment = TextAlignmentOptions.Center;
            bigText = Ui.Text(safe, "", 140, Ui.Ink);
            bigText.Place(new Vector2(0.5f, 0.62f), new Vector2(1400, 200));
            var hintPanel = Ui.Panel(safe, "Hint", new Color(1f, 0.97f, 0.9f, 0.88f)).Place(new Vector2(0.5f, 0f), new Vector2(520, 70), new Vector2(60, 24));
            var hint = Ui.Label(hintPanel.transform, "mg.mocau.hint", 32, Ui.Ink);
            hint.Stretch(Vector2.zero, Vector2.one, 8);
            RefreshHud();
        }

        private void RefreshHud()
        {
            if (placeText == null) return;
            placeText.text = Loc.Format("mg.mocau.place", PlayerPlace, racers.Count);
            var h = ((RectTransform)progressFill.parent).rect.height - 12f;
            progressFill.offsetMax = new Vector2(-6, 6 + h * Mathf.Clamp01(me.y / length));
            for (var i = 0; i < racerDots.Count && i < racers.Count; i++)
                racerDots[i].anchoredPosition = new Vector2(0, 6 + h * Mathf.Clamp01(racers[i].y / length));
        }

        // ---- Shapes ----

        private static SpriteRenderer Put(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 scale, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            if (parent != null) sr.transform.SetParent(parent, false);
            sr.transform.localPosition = pos;
            sr.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>A child riding a mo cau (official generated art), with a yellow marker behind the player's.</summary>
        private static GameObject Sled(Color shirt, bool player)
        {
            var go = new GameObject(player ? "PlayerSled" : "AiSled");
            if (player) Put(go.transform, "Marker", MoCauArt.Marker(), new Vector2(0, -1.6f), Vector2.one, 0);
            Put(go.transform, "Sled", MoCauArt.Sled(shirt, player && Loc.PlayerGender == Gender.Girl), Vector2.zero, Vector2.one * 1.15f, 0);
            return go;
        }

        /// <summary>A puff of dust behind a sliding sled.</summary>
        private IEnumerator Dust(Vector2 at)
        {
            var puff = Put(null, "Dust", MoCauArt.Dust(), at, Vector2.one, 30);
            var drift = new Vector2(Random.Range(-0.3f, 0.3f), -0.4f);
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.7f)
            {
                puff.transform.position = at + drift * t;
                puff.transform.localScale = Vector3.one * Mathf.Lerp(1.0f, 2.2f, t);
                puff.color = new Color(1f, 1f, 1f, 0.9f * (1f - t));
                yield return null;
            }
            Destroy(puff.gameObject);
        }
    }
}
