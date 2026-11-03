using System.Collections;
using System.Collections.Generic;
using ChildGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ChildGame.MiniGames.NhoToc
{
    /// <summary>
    /// Special 01 — Nhổ tóc bạc. Top-down view of grandma's head; only the child's hands are visible.
    /// <list type="bullet">
    /// <item>Touch a grey hair, hold and drag to pull it out: +1 currency.</item>
    /// <item>Swipe across the hair to gather it: the whole head of hair is replaced by a new arrangement with at least one
    /// grey hair (owner design §9–§11).</item>
    /// <item>Pulling on black hair costs a little time (placeholder 2 s).</item>
    /// <item>30-second sessions, 3 per day, no win/loss, no points (handled by the session and <see cref="DailyLimit"/>).</item>
    /// </list>
    /// The hair is a generated texture (thousands of drawn strands, no simulation), grey hairs are separate strand
    /// sprites. ⚠ Numbers marked placeholder are temporary.
    /// </summary>
    public class NhoTocGame : MiniGame
    {
        [SerializeField] private float sessionSeconds = 30f;
        [Header("Placeholder tuning")]
        [SerializeField] private int maxGreyPerLayout = 4;
        [SerializeField] private float wrongPullPenalty = 2f;
        [SerializeField] private float pullToPluck = 0.9f;
        [SerializeField] private float swipeDistance = 2.2f;
        [SerializeField] private float swipeMaxSeconds = 0.6f;

        private static readonly Vector2 HeadCenter = new Vector2(0f, 0.1f);
        private static readonly Vector2 HeadRadius = new Vector2(3.5f, 3.9f);
        private static readonly Vector2 Bun = new Vector2(0f, -3.6f);
        private const float GreyLength = 0.85f;

        private class Grey
        {
            public Transform view;
            public Vector2 root, dir;
            public bool gone;
        }

        private SpriteRenderer hair;
        private readonly List<Sprite> layouts = new List<Sprite>();
        private int layoutIndex;
        private readonly List<Grey> greys = new List<Grey>();
        private Sprite greySprite, pluckedSprite;
        private System.Threading.Tasks.Task<(Color[] px, int w, int h)> nextHair;
        private ReachArm rightArm, leftArm;
        private float timeLeft;
        private int plucked;
        private bool refreshing;
        private TextMeshProUGUI timeText, countText, bubbleText;
        private GameObject bubble;

        // Gesture state.
        private bool pressing, swiped;
        private Vector2 pressStart, lastPos;
        private float pressTime, stillTime;
        private Grey held;

        [Tooltip("đồng per grey hair (owner 2026-10-07: 200; 2026-10-08: 100).")]
        [SerializeField] private int dongPerHair = 100;

        public override int CurrencyEarned => plucked * dongPerHair;
        /// <summary>Grandma's reward lines (the definition's win lines) only when a hair was plucked.</summary>
        public override IReadOnlyList<string> OutcomeLines => plucked > 0 ? null : System.Array.Empty<string>();
        public override int ResultCount => plucked;
        public int Plucked => plucked;
        public int GreyCount { get { var n = 0; foreach (var g in greys) if (!g.gone) n++; return n; } }

        protected override void OnBegin()
        {
            timeLeft = sessionSeconds;
            plucked = 0;
            BuildScene();
            BuildHud();
            NewArrangement();
        }

        // ---- Scene ----

        private void BuildScene()
        {
            var cam = Camera.main;
            var halfH = cam != null ? cam.orthographicSize : 5.4f;
            var halfW = cam != null ? halfH * cam.aspect : 9.6f;
            if (cam != null) cam.backgroundColor = new Color32(214, 190, 140, 255);
            // Official generated art (owner 2026-10-08): the mat, grandma's shoulders and ears, her hair and bun.
            var matRect = new Rect(-halfW - 0.4f, -halfH - 0.4f, (halfW + 0.4f) * 2f, (halfH + 0.4f) * 2f);
            Place("Mat", NhoTocArt.Mat(matRect, Random.Range(0, 9999)), matRect.center, Vector3.one, -100);
            var bodyRect = new Rect(-7.2f, -halfH - 0.6f, 14.4f, -2f - (-halfH - 0.6f));
            Place("Body", NhoTocArt.Body(bodyRect, HeadCenter, HeadRadius), bodyRect.center, Vector3.one, -60);
            var ear = NhoTocArt.Ear();
            foreach (var side in new[] { -1f, 1f })
                Place("Ear", ear, HeadCenter + new Vector2(side * (HeadRadius.x - 0.08f), -0.45f), new Vector3(side, 1f, 1f), -52);
            hair = Place("Hair", HairSprite(Random.Range(0, 99999)), HeadCenter, new Vector3(HeadRadius.x, HeadRadius.y, 1f), -50);
            layouts.Add(hair.sprite);
            QueueHair();
            Place("Bun", NhoTocArt.Bun(new Vector2(2.1f, 1.7f)), Bun, Vector3.one, -45);
            greySprite = NhoTocArt.GreyHair(GreyLength, false);
            pluckedSprite = NhoTocArt.GreyHair(GreyLength, true);

            // The child's arms reach in from below: the left hand holds the hair aside, the right one plucks.
            leftArm = ReachArm.Create(HandArt.Who.Player, new Vector2(-7f, -11f), LeftRest, false);
            rightArm = ReachArm.Create(HandArt.Who.Player, new Vector2(7.5f, -11f), RightRest, false);
            rightArm.SetPose(HandArt.Pose.Pinch);
        }

        private static readonly Vector2 LeftRest = new Vector2(-3.8f, -1.1f);
        private static readonly Vector2 RightRest = new Vector2(4.6f, -2.2f);

        private SpriteRenderer Place(string name, Sprite sprite, Vector2 pos, Vector3 scale, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.transform.position = pos;
            sr.transform.localScale = scale;
            sr.sortingOrder = order;
            return sr;
        }

        private const int HairSize = 640;

        private static Sprite HairSprite(int seed)
        {
            var px = NhoTocArt.HairPixels(HairSize, seed, out var w, out var h);
            return NhoTocArt.HairSprite(px, w, h);
        }

        /// <summary>Renders another hair arrangement on a worker thread (up to three in all).</summary>
        private void QueueHair()
        {
            if (layouts.Count >= 3) return;
            var seed = Random.Range(0, 99999);
            nextHair = System.Threading.Tasks.Task.Run(() =>
            {
                var px = NhoTocArt.HairPixels(HairSize, seed, out var w, out var h);
                return (px, w, h);
            });
        }

        private void CollectHair()
        {
            if (nextHair == null || !nextHair.IsCompleted) return;
            if (!nextHair.IsFaulted)
            {
                var (px, w, h) = nextHair.Result;
                layouts.Add(NhoTocArt.HairSprite(px, w, h));
            }
            nextHair = null;
            QueueHair();
        }

        // ---- Arrangements ----

        private void NewArrangement()
        {
            layoutIndex = (layoutIndex + 1) % layouts.Count;
            hair.sprite = layouts[layoutIndex];
            hair.transform.localScale = new Vector3(HeadRadius.x * (Random.value < 0.5f ? -1f : 1f), HeadRadius.y, 1);
            hair.transform.rotation = Quaternion.Euler(0, 0, Random.Range(-4f, 4f));
            foreach (var g in greys) if (g.view != null) Destroy(g.view.gameObject);
            greys.Clear();
            // Design rule: at least one grey hair every time; the maximum is configurable.
            var count = Random.Range(1, maxGreyPerLayout + 1);
            for (var tries = 0; greys.Count < count && tries < 200; tries++)
            {
                var local = new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(-0.75f, 0.8f));
                if (local.sqrMagnitude > 0.6f || Mathf.Abs(local.x) < 0.08f) continue;
                var root = HeadCenter + Vector2.Scale(local, HeadRadius);
                var ok = true;
                foreach (var o in greys) ok &= Vector2.Distance(o.root, root) > 1.2f;
                if (!ok) continue;
                var dir = NhoTocArt.Flow(local);
                var view = new GameObject("GreyHair").AddComponent<SpriteRenderer>();
                view.sprite = greySprite;
                view.color = Color.white;
                view.sortingOrder = -40;
                view.transform.position = root + dir * GreyLength * 0.5f;
                view.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                greys.Add(new Grey { view = view.transform, root = root, dir = dir });
            }
        }

        private IEnumerator Gather(Vector2 from, Vector2 to)
        {
            refreshing = true;
            // The left hand sweeps across the head, then a new arrangement appears.
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
            {
                leftArm.SetHand(Vector2.Lerp(from, to, Mathf.SmoothStep(0, 1, t)));
                yield return null;
            }
            NewArrangement();
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
            {
                leftArm.SetHand(Vector2.Lerp(to, LeftRest, t));
                yield return null;
            }
            leftArm.SetHand(LeftRest);
            refreshing = false;
        }

        // ---- Input ----

        private void Update()
        {
            CollectHair();
            if (!Running) return;
            timeLeft -= Time.deltaTime;
            RefreshHud();
            if (timeLeft <= 0f)
            {
                timeLeft = 0f;
                RefreshHud();
                if (held != null) Release();
                Finish(Outcome.Win); // no win/loss in this game: the session simply ends
                return;
            }
            if (debugHold) return;
            if (!pressing && Time.timeScale > 0f) rightArm.SetHand(Vector2.Lerp(rightArm.Hand, RightRest, 1f - Mathf.Exp(-Time.deltaTime * 6f)));
            if (Time.timeScale == 0f || refreshing) return;
            var pointer = Pointer.current;
            var cam = Camera.main;
            if (pointer == null || cam == null) return;
            var screen = pointer.position.ReadValue();
            var w = (Vector2)cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));

            if (pointer.press.wasPressedThisFrame)
            {
                var es = EventSystem.current;
                if (es != null && es.IsPointerOverGameObject()) return;
                Press(w);
            }
            else if (pressing && pointer.press.isPressed) Drag(w);
            else if (pressing && !pointer.press.isPressed) Up(w);
        }

        private void Press(Vector2 w)
        {
            pressing = true;
            swiped = false;
            pressStart = lastPos = w;
            pressTime = Time.time;
            stillTime = 0f;
            held = FindGrey(w);
            rightArm.SetHand(w);
        }

        private void Drag(Vector2 w)
        {
            if (Vector2.Distance(w, lastPos) < 0.03f) stillTime += Time.deltaTime;
            lastPos = w;
            rightArm.SetHand(w);
            if (held != null)
            {
                // The hair stretches from its root to the fingers; far enough = out it comes.
                var d = w - held.root;
                var len = Mathf.Max(GreyLength, d.magnitude);
                held.view.position = held.root + d.normalized * len * 0.5f;
                held.view.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                held.view.localScale = new Vector3(len / GreyLength, 1, 1);
                if (d.magnitude >= pullToPluck) Pluck(held);
                return;
            }
            if (!swiped && Vector2.Distance(w, pressStart) >= swipeDistance && Time.time - pressTime <= swipeMaxSeconds)
            {
                swiped = true;
                StartCoroutine(Gather(pressStart, w));
            }
        }

        private void Up(Vector2 w)
        {
            pressing = false;
            if (held != null)
            {
                Release();
                return;
            }
            if (swiped) return;
            // A deliberate pull (held still a moment, then dragged a little) on black hair is a mistake.
            var dist = Vector2.Distance(w, pressStart);
            if (stillTime >= 0.12f && dist >= 0.45f && dist < swipeDistance && OnHead(pressStart)) WrongPull(pressStart);
        }

        private Grey FindGrey(Vector2 w)
        {
            Grey best = null;
            var bestDist = 0.32f; // finger-sized tolerance
            foreach (var g in greys)
            {
                if (g.gone) continue;
                var tip = g.root + g.dir * GreyLength;
                var d = DistanceToSegment(w, g.root, tip);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = g;
                }
            }
            return best;
        }

        private void Release()
        {
            // Not pulled far enough: it springs back.
            var g = held;
            held = null;
            if (g == null || g.gone) return;
            g.view.position = g.root + g.dir * GreyLength * 0.5f;
            g.view.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(g.dir.y, g.dir.x) * Mathf.Rad2Deg);
            g.view.localScale = Vector3.one;
        }

        private void Pluck(Grey g)
        {
            g.gone = true;
            held = null;
            plucked++;
            Sfx.Play(Snd.Pluck, 0.7f, 1f, 0.1f);
            var view = g.view.GetComponent<SpriteRenderer>();
            view.sprite = pluckedSprite; // out with its little white root
            g.view.localScale = Vector3.one;
            StartCoroutine(FlyOff(g.view));
            Popup("+" + Shop.Money(dongPerHair), lastPos, new Color32(200, 140, 0, 255));
        }

        private void WrongPull(Vector2 at)
        {
            timeLeft = Mathf.Max(0f, timeLeft - wrongPullPenalty);
            Popup($"-{wrongPullPenalty:0}s", at, Ui.Brick);
            StartCoroutine(Ouch());
        }

        /// <summary>Capture harness: pulls the first remaining grey hair (true if there was one).</summary>
        public bool DebugPluckOne()
        {
            foreach (var g in greys)
                if (!g.gone)
                {
                    lastPos = g.root + g.dir * pullToPluck;
                    Pluck(g);
                    return true;
                }
            return false;
        }

        /// <summary>Capture harness: pinch the first grey hair and pull it partway (true), or let go (false).</summary>
        private bool debugHold;

        public void DebugGrab(bool on)
        {
            if (!on)
            {
                pressing = false;
                debugHold = false;
                Release();
                return;
            }
            foreach (var g in greys)
                if (!g.gone)
                {
                    pressing = true;
                    debugHold = true;
                    held = g;
                    var w = g.root + g.dir * (pullToPluck * 0.97f); // just short of plucking
                    rightArm.SetHand(w);
                    lastPos = w;
                    Drag(w);
                    return;
                }
        }

        /// <summary>Capture harness: a gather swipe, or a wrong pull on black hair.</summary>
        public void DebugSwipe() => StartCoroutine(Gather(new Vector2(-3f, 1f), new Vector2(3f, -1f)));
        public void DebugWrongPull() => WrongPull(new Vector2(-1.5f, 1.8f));
        public bool Refreshing => refreshing;

        private static bool OnHead(Vector2 p)
        {
            var d = (p - HeadCenter) / HeadRadius;
            return d.sqrMagnitude < 1f;
        }

        private IEnumerator FlyOff(Transform t)
        {
            var from = (Vector2)t.position;
            var to = new Vector2(7.5f, 4.5f);
            for (var a = 0f; a < 1f; a += Time.deltaTime / 0.45f)
            {
                t.position = Vector2.Lerp(from, to, a) + new Vector2(0, Mathf.Sin(a * Mathf.PI) * 1.2f);
                t.Rotate(0, 0, 720f * Time.deltaTime);
                yield return null;
            }
            Destroy(t.gameObject);
        }

        /// <summary>Grandma flinches: the view jolts for a moment.</summary>
        private IEnumerator Flinch()
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            var home = cam.transform.position;
            for (var t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                var k = 1f - t / 0.35f;
                cam.transform.position = home + new Vector3(Mathf.Sin(t * 70f) * 0.09f * k, Mathf.Cos(t * 55f) * 0.05f * k, 0f);
                yield return null;
            }
            cam.transform.position = home;
        }

        private IEnumerator Ouch()
        {
            bubble.SetActive(true);
            StartCoroutine(Flinch());
            bubbleText.GetComponent<LocText>().Key = "toc.ouch";
            yield return new WaitForSeconds(1.2f);
            bubble.SetActive(false);
        }

        private void Popup(string text, Vector2 at, Color color)
        {
            var t = new GameObject("Popup", typeof(RectTransform)).AddComponent<TextMeshPro>();
            t.font = Ui.Bold;
            t.fontSize = 6;
            t.text = text;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.sortingOrder = 100;
            t.outlineWidth = 0.22f; // reads on black hair and on the mat
            t.outlineColor = new Color32(70, 42, 32, 255);
            t.transform.position = at + new Vector2(0, 0.6f);
            StartCoroutine(Rise(t));
        }

        private static IEnumerator Rise(TextMeshPro t)
        {
            for (var a = 0f; a < 1f; a += Time.deltaTime / 0.8f)
            {
                t.transform.position += Vector3.up * Time.deltaTime * 1.2f;
                t.alpha = 1f - a;
                yield return null;
            }
            Destroy(t.gameObject);
        }

        // ---- HUD and shapes ----

        private void BuildHud()
        {
            var safe = Ui.Canvas("TocHud", 8);
            var panel = Ui.Panel(safe, "Top", new Color(1, 1, 1, 0.88f)).Place(new Vector2(1f, 1f), new Vector2(520, 110), new Vector2(-24, -24));
            timeText = Ui.Text(panel.transform, "", 50, Ui.Ink);
            timeText.Place(new Vector2(0f, 0.5f), new Vector2(200, 90), new Vector2(20, 0));
            countText = Ui.Text(panel.transform, "", 44, new Color32(200, 140, 0, 255));
            countText.Place(new Vector2(1f, 0.5f), new Vector2(290, 90), new Vector2(-20, 0));
            bubble = Ui.Panel(safe, "Bubble", Color.white).Place(new Vector2(0.5f, 0f), new Vector2(760, 100), new Vector2(0, 40)).gameObject;
            bubbleText = Ui.Text(bubble.transform, "", 40, Ui.Brick);
            bubbleText.Stretch(Vector2.zero, Vector2.one, 10);
            bubbleText.gameObject.AddComponent<LocText>();
            bubble.SetActive(false);
            var hintPanel = Ui.Panel(safe, "Hint", new Color(1f, 0.97f, 0.9f, 0.85f)).Place(new Vector2(0f, 1f), new Vector2(520, 110), new Vector2(24, -128));
            var hint = Ui.Label(hintPanel.transform, "toc.hint", 30, Ui.Ink);
            hint.Stretch(Vector2.zero, Vector2.one, 12); // under the back button, clear of the arms
            hint.alignment = TextAlignmentOptions.Left;
        }

        private void RefreshHud()
        {
            if (timeText == null) return;
            timeText.text = Mathf.CeilToInt(timeLeft).ToString();
            timeText.color = timeLeft < 8f ? Ui.Brick : Ui.Ink;
            countText.text = Loc.Format("toc.count", plucked);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
