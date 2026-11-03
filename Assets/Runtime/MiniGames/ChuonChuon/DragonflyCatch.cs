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
    /// MG07 dragonfly catching, in two stages (owner design §3):
    /// <list type="number">
    /// <item>Stage 1: several dragonflies perch on reed tips; tap one to try for it.</item>
    /// <item>Stage 2: close-up of that dragonfly and the child's hand. Drag anywhere to move the hand (it moves by the
    /// finger's movement, so the finger never covers the dragonfly) and pinch the <b>tail</b>.</item>
    /// </list>
    /// It flies away if the hand moves too fast for how close it is, touches anything but the tail, the phone shakes,
    /// or it gets loud (<see cref="Disturbance"/>). Then the player picks another one. Its wings flutter harder as it gets
    /// nervous. ⚠ Speeds and sizes are placeholders.
    /// </summary>
    public class DragonflyCatch : MonoBehaviour
    {
        [Tooltip("Hand speed (units/s) allowed right next to the dragonfly, and far away.")]
        public Vector2 allowedSpeed = new Vector2(1.1f, 7f);
        [Tooltip("Distance over which the allowed speed rises from near to far.")]
        public float calmDistance = 4f;
        [Tooltip("How close (units) the pinch must get to the tail tip to catch it.")]
        public float tailReach = 0.45f;

        private Action done;
        private Transform root;
        private Disturbance disturbance;
        private readonly List<(Transform t, Vector2 perch, float phase)> perched = new List<(Transform, Vector2, float)>();
        private TextMeshProUGUI caption;
        private GameObject hud;

        // Stage 2 state.
        private Transform fly, armRoot;
        private List<Transform> wings;
        private Vector2 pinch, lastPinch, tailTip, anchor;
        private Vector2[] bodyPoints;
        private float speed, nervous, grace;
        private bool closeUp, attemptOver;
        private Vector2? dragFrom;

        /// <summary>Capture harness: when set, the hand moves itself toward the tail at this speed.</summary>
        public float? DebugApproachSpeed { get; set; }
        public bool InCloseUp => closeUp && !attemptOver;
        public bool PickingStage => !closeUp;
        public int Remaining => perched.Count;

        public static DragonflyCatch Begin(Action caught)
        {
            var go = new GameObject("DragonflyCatch");
            var c = go.AddComponent<DragonflyCatch>();
            c.done = caught;
            c.disturbance = go.AddComponent<Disturbance>();
            c.disturbance.enabled = false;
            c.BuildHud();
            c.ShowStage1();
            return c;
        }

        // ---- Stage 1: pick a dragonfly ----

        private void ShowStage1()
        {
            closeUp = false;
            disturbance.enabled = false;
            if (root != null) Destroy(root.gameObject);
            root = new GameObject("Stage1").transform;
            root.SetParent(transform, false);
            SetCaption("mg.chuon.pick");
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0, 0, -10);
                cam.orthographicSize = 5.4f;
                cam.backgroundColor = new Color32(170, 215, 240, 255);
            }
            // Official generated art (owner 2026-10-08): a pond under the sky, the far bank, reeds in the water, the
            // grassy near bank in front.
            var halfW = cam != null ? cam.orthographicSize * cam.aspect : 9.6f;
            var sky = new Rect(-halfW - 0.5f, -1.6f, (halfW + 0.5f) * 2f, 7.3f);
            SceneArt.Put(root, "Sky", SceneArt.Sky(sky, new Color32(110, 172, 226, 255), new Color32(206, 232, 240, 255), new Vector2(halfW * 0.5f, 4.2f), 4, 31), sky.center, Vector2.one, -60);
            for (var x = -RiverArt.BankTile * 1.5f; x < halfW + 1f; x += RiverArt.BankTile)
                SceneArt.Put(root, "FarBank", RiverArt.Bank(), new Vector2(x, -2.0f), Vector2.one, -40);
            for (var x = -RiverArt.WaterTile * 2f; x < halfW + 1f; x += RiverArt.WaterTile)
                SceneArt.Put(root, "Pond", RiverArt.Water(), new Vector2(x, -1.4f - (RiverArt.FarWater - RiverArt.WaterBottom)), Vector2.one, -45);
            SceneArt.Put(root, "NearBank", DragonflyArt.NearBank(halfW * 2f + 2f), new Vector2(-halfW - 1f, -4.35f), Vector2.one, -10);
            if (perched.Count < 2) perched.Clear();
            var count = perched.Count == 0 ? 5 : perched.Count;
            var keep = new List<Vector2>();
            foreach (var p in perched) keep.Add(p.perch);
            perched.Clear();
            for (var i = 0; i < count; i++)
            {
                var x = keep.Count > i ? keep[i].x : -7f + i * 3.5f + Random.Range(-0.6f, 0.6f);
                var h = keep.Count > i ? keep[i].y : Random.Range(0.2f, 2.4f);
                // A reed with a seed head; the dragonfly sits on its tip.
                SceneArt.Put(root, "Reed", DragonflyArt.Reed(h + 3.9f, i), new Vector2(x, -3.9f), Vector2.one, -30);
                var f = DragonflyView.Create(root, Palette(i), 0.55f, out _);
                f.position = new Vector2(x - 0.15f, h + 0.18f);
                var col = f.gameObject.AddComponent<CircleCollider2D>();
                col.radius = 1.6f; // generous: easy to tap with a finger
                var index = i;
                f.gameObject.AddComponent<Tappable>().Clicked += _ => Pick(index);
                perched.Add((f, new Vector2(x, h), Random.Range(0f, 6f)));
            }
        }

        private static Color Palette(int i) => i % 3 == 0 ? new Color32(220, 70, 50, 255) : i % 3 == 1 ? new Color32(60, 140, 210, 255) : new Color32(240, 170, 40, 255);

        private void Pick(int index)
        {
            if (closeUp || Screens.Count > 0 || index >= perched.Count) return;
            StartCoroutine(ShowStage2(index));
        }

        /// <summary>Capture harness: picks the first dragonfly.</summary>
        public void DebugPick() => Pick(0);

        // ---- Stage 2: the close-up ----

        private IEnumerator ShowStage2(int index)
        {
            closeUp = true;
            attemptOver = false;
            var color = Palette(index);
            perched.RemoveAt(index);
            Destroy(root.gameObject);
            root = new GameObject("Stage2").transform;
            root.SetParent(transform, false);
            SetCaption("mg.chuon.approach");
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = new Color32(190, 225, 245, 255);

            // A big blurred-looking reed stem and the dragonfly on its tip, tail toward the hand.
            var halfW = cam != null ? cam.orthographicSize * cam.aspect : 9.6f;
            var view = new Rect(-halfW - 0.5f, -5.9f, (halfW + 0.5f) * 2f, 11.8f);
            SceneArt.Put(root, "Background", DragonflyArt.Bokeh(view), view.center, Vector2.one, -60);
            SceneArt.Put(root, "Stem", DragonflyArt.Stem(6.4f), new Vector2(-2.55f, -6.3f), new Vector2(1.6f, 1f), -20);
            fly = DragonflyView.Create(root, color, 2.4f, out wings);
            fly.position = new Vector2(-2.2f, 0.1f);
            tailTip = fly.TransformPoint(DragonflyView.TailTip);
            bodyPoints = new[]
            {
                (Vector2)fly.TransformPoint(DragonflyView.Head), (Vector2)fly.TransformPoint(DragonflyView.Thorax),
                (Vector2)fly.TransformPoint(DragonflyView.WingTipUp), (Vector2)fly.TransformPoint(DragonflyView.WingTipDown),
            };

            anchor = new Vector2(11f, -7f);
            pinch = lastPinch = new Vector2(6.5f, -3.6f);
            armRoot = Hand(root);
            UpdateArm();
            speed = nervous = 0f;
            grace = 1f;
            dragFrom = null;
            disturbance.enabled = true;
            yield break;
        }

        private void Update()
        {
            if (closeUp) UpdateCloseUp();
            else
                foreach (var (t, perch, phase) in perched)
                {
                    // Perched dragonflies bob a little.
                    if (t == null) continue;
                    t.position = new Vector2(perch.x - 0.15f, perch.y + 0.18f + Mathf.Sin(Time.time * 2f + phase) * 0.03f);
                }
        }

        private void UpdateCloseUp()
        {
            if (fly == null) return;
            var dt = Mathf.Max(Time.deltaTime, 1e-4f);
            FlutterWings(Mathf.Lerp(6f, 40f, Mathf.Clamp01(nervous)));
            if (attemptOver || Time.timeScale == 0f) return;

            MoveHand();
            var moved = Vector2.Distance(pinch, lastPinch) / dt;
            speed = Mathf.Lerp(speed, moved, 0.35f);
            lastPinch = pinch;
            UpdateArm();

            grace -= dt;
            var dist = Vector2.Distance(pinch, tailTip);
            var allowed = Mathf.Lerp(allowedSpeed.x, allowedSpeed.y, Mathf.Clamp01((dist - 0.6f) / calmDistance));
            nervous = Mathf.Lerp(nervous, speed / allowed, 0.2f);

            if (nervous > 1f)
            {
                StartCoroutine(FlyAway("mg.chuon.fail.fast"));
                return;
            }
            foreach (var p in bodyPoints)
                if (Vector2.Distance(pinch, p) < 0.55f)
                {
                    StartCoroutine(FlyAway("mg.chuon.fail.touch"));
                    return;
                }
            if (grace <= 0f && disturbance.Disturbed)
            {
                StartCoroutine(FlyAway(disturbance.Reason == "sound" ? "mg.chuon.fail.sound" : "mg.chuon.fail.shake"));
                return;
            }
            if (dist < tailReach) StartCoroutine(Caught());
        }

        private void MoveHand()
        {
            if (DebugApproachSpeed.HasValue)
            {
                pinch = Vector2.MoveTowards(pinch, tailTip, DebugApproachSpeed.Value * Time.deltaTime);
                return;
            }
            var pointer = Pointer.current;
            var cam = Camera.main;
            if (pointer == null || cam == null) return;
            if (!pointer.press.isPressed)
            {
                dragFrom = null;
                return;
            }
            var es = EventSystem.current;
            if (dragFrom == null && es != null && es.IsPointerOverGameObject()) return;
            var screen = pointer.position.ReadValue();
            var world = (Vector2)cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
            if (dragFrom.HasValue) pinch += world - dragFrom.Value;
            dragFrom = world;
            pinch.x = Mathf.Clamp(pinch.x, -9f, 9f);
            pinch.y = Mathf.Clamp(pinch.y, -5f, 5f);
        }

        private IEnumerator FlyAway(string reasonKey)
        {
            attemptOver = true;
            disturbance.enabled = false;
            SetCaption(reasonKey);
            var start = (Vector2)fly.position;
            var dir = new Vector2(-1f, 0.8f).normalized;
            for (var t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                fly.position = start + dir * (t * t * 22f) + new Vector2(0, Mathf.Sin(t * 30f) * 0.1f);
                FlutterWings(60f);
                yield return null;
            }
            yield return new WaitForSeconds(0.8f);
            ShowStage1();
        }

        private IEnumerator Caught()
        {
            attemptOver = true;
            disturbance.enabled = false;
            SetCaption("mg.chuon.caught");
            for (var t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                FlutterWings(30f);
                yield return null;
            }
            Destroy(hud);
            done?.Invoke();
            Destroy(gameObject);
        }

        private void FlutterWings(float rate)
        {
            if (wings == null) return;
            for (var i = 0; i < wings.Count; i++)
            {
                var w = wings[i];
                if (w == null) continue;
                var s = 0.7f + 0.3f * Mathf.Sin(Time.time * rate + i);
                w.localScale = new Vector3(1f, s, 1f); // the wing beats: it narrows and widens
            }
        }

        // ---- Hand and HUD ----

        /// <summary>The child's hand reaching in from the lower right, thumb and finger ready to pinch (official hand art).</summary>
        private Transform Hand(Transform parent)
        {
            hand = ReachArm.Create(HandArt.Who.Player, anchor + new Vector2(3f, -4f), pinch, false, 0.9f);
            hand.transform.SetParent(parent, true);
            hand.SetPose(HandArt.Pose.Pinch);
            return hand.transform;
        }

        private ReachArm hand;

        private void UpdateArm()
        {
            if (hand != null) hand.SetHand(pinch);
        }

        private void BuildHud()
        {
            var safe = Ui.Canvas("CatchHud", 8);
            hud = safe.root.gameObject;
            var panel = Ui.Panel(safe, "Caption", new Color(1, 1, 1, 0.88f)).Place(new Vector2(0.5f, 1f), new Vector2(1100, 100), new Vector2(0, -24));
            caption = Ui.Text(panel.transform, "", 40, Ui.Ink);
            caption.Stretch(Vector2.zero, Vector2.one, 10);
            caption.gameObject.AddComponent<LocText>();
        }

        private void SetCaption(string key) => caption.GetComponent<LocText>().Key = key;

        private void OnDestroy()
        {
            if (hud != null) Destroy(hud);
        }
    }

    /// <summary>A dragonfly (official generated art: <see cref="DragonflyArt"/>), facing left with the tail to the right. Local units: body ≈ 3 long.</summary>
    public static class DragonflyView
    {
        public static readonly Vector2 Head = new Vector2(-1.25f, 0f);
        public static readonly Vector2 Thorax = new Vector2(-0.7f, 0f);
        public static readonly Vector2 TailTip = new Vector2(1.75f, -0.05f);
        public static readonly Vector2 WingTipUp = new Vector2(-0.6f, 1.1f);
        public static readonly Vector2 WingTipDown = new Vector2(-0.6f, -1.1f);

        public static Transform Create(Transform parent, Color body, float scale, out List<Transform> wings)
        {
            var root = new GameObject("Dragonfly").transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * scale;
            wings = new List<Transform>();
            // Official generated art (owner 2026-10-08): four veined wings rooted at the thorax, then the body.
            var wing = DragonflyArt.Wing();
            foreach (var (pos, angle) in new[] { (new Vector2(-0.75f, 0.55f), 80f), (new Vector2(-0.45f, 0.5f), 65f), (new Vector2(-0.75f, -0.55f), -80f), (new Vector2(-0.45f, -0.5f), -65f) })
            {
                var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var w = new GameObject("Wing").AddComponent<SpriteRenderer>();
                w.transform.SetParent(root, false);
                w.transform.localPosition = pos - dir * 0.575f; // the wing's root, on the thorax
                w.transform.localRotation = Quaternion.Euler(0, 0, angle);
                w.sprite = wing;
                w.sortingOrder = 21;
                wings.Add(w.transform);
            }
            var b = new GameObject("Body").AddComponent<SpriteRenderer>();
            b.transform.SetParent(root, false);
            b.sprite = DragonflyArt.Body(body);
            b.sortingOrder = 23;
            return root;
        }
    }
}
