using System.Collections;
using System.Collections.Generic;
using ChildGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ChildGame.MiniGames.TromXoai
{
    /// <summary>
    /// MG04 — Trộm xoài. Knock mangoes down with stones (drag back and release), then with your two sandals.
    /// <list type="bullet">
    /// <item>A mango falls only on a direct hit. Stones never fall back down (owner 2026-10-07).</item>
    /// <item>A sandal can get stuck in the branches (fixed chance, every difficulty). Both stuck = loss.</item>
    /// <item>A sandal that falls on the pacing teammate starts a fight = loss. Otherwise the teammate fetches it and
    /// tosses it back.</item>
    /// <item>Win by collecting the required mangoes (3 / 5 / 8). No time limit.</item>
    /// </list>
    /// ⚠ Values marked placeholder are temporary and easy to tune; the owner may set them.
    /// </summary>
    public class TromXoaiGame : MiniGame
    {
        [Header("Design values")]
        [SerializeField] private int[] mangoesNeeded = { 3, 5, 8 };
        [Tooltip("Owner 2026-10-09: Normal and Hard get one stone fewer than the mangoes needed.")]
        [SerializeField] private int[] fewerStones = { 0, 1, 1 };

        [Header("Placeholder tuning")]
        [SerializeField] private int[] mangoesOnTree = { 9, 11, 14 };
        [SerializeField, Range(0, 1)] private float sandalStuckChance = 0.3f;
        [Tooltip("Owner 2026-10-08: a falling sandal hits the friend 60 % of the time.")]
        [SerializeField, Range(0, 1)] private float sandalHitChance = 0.6f;
        [SerializeField] private float gravity = 12f;
        [SerializeField] private float maxThrowSpeed = 17f;
        [Tooltip("Drag distance (world units) that gives the full throw speed.")]
        [SerializeField] private float fullDrag = 3f;
        [SerializeField] private float teammateSpeed = 1.6f;

        // Layout (world units; the camera shows 10.8 units of height).
        private const float GroundY = -3.4f;
        private static readonly Vector2 PlayerFeet = new Vector2(-7f, -3.3f);
        private static readonly Vector2 Hand = new Vector2(-6.75f, -1.95f);
        private static readonly Vector2 CanopyCenter = new Vector2(4.4f, 2.1f);
        private static readonly Vector2 CanopyRadius = new Vector2(4.1f, 2.6f);
        private const float TreeX = 4.4f;
        private const float TeammateMinX = 1.6f, TeammateMaxX = 7.2f, TeammateY = -2.9f;
        private const float MangoRadius = 0.14f; // half size (owner 2026-10-09)
        private const float ProjectileRadius = 0.16f;

        private enum Kind { Stone, Sandal }

        private enum SandalState { AtFeet, InHand, Flying, Stuck, OnGround, Carried, Returning }

        private class Projectile
        {
            public Kind kind;
            public int sandal = -1;
            public Transform view;
            public Vector2 pos, vel;
            public bool enteredCanopy, hitMango, decided, willHit;
            public Vector2 canopyPos;
        }

        private class Mango
        {
            public Transform view;
            public Vector2 pos;
            public bool falling;
            public float vy;
        }

        private readonly List<Transform> groundStones = new List<Transform>();
        private readonly List<Mango> mangoes = new List<Mango>();
        private readonly List<Projectile> flying = new List<Projectile>();
        private readonly SandalState[] sandalState = new SandalState[2];
        private readonly Transform[] sandalView = new Transform[2];

        private Transform world;
        private Kid player, teammate;
        private float teammateDir = 1f, teammatePause;
        private Coroutine teammateTask;
        private Kind? inHand;
        private int handSandal = -1;
        private int collected, stonesLeft;
        private bool aiming, ending;
        private Vector2 aimStart, aimNow;
        private readonly List<Transform> aimDots = new List<Transform>();
        private TextMeshProUGUI mangoText, stoneText, sandalText, hintText;

        public int Collected => collected;
        public int Needed => mangoesNeeded[(int)Difficulty];
        public bool HasThrowable => inHand.HasValue;
        public int StonesLeft => stonesLeft;

        // ---- Setup ----

        protected override void OnBegin()
        {
            // As many stones as mangoes needed (owner 2026-10-08), one fewer on Normal and Hard (owner 2026-10-09).
            stonesLeft = mangoesNeeded[(int)Difficulty] - fewerStones[(int)Difficulty];
            endKind = EndKind.Win;
            BuildWorld();
            BuildHud();
            Refresh();
        }

        private void BuildWorld()
        {
            world = new GameObject("World").transform;
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = new Color32(160, 212, 240, 255);

            // Official generated art (owner 2026-10-08): summer sky, ông Tám's house and bamboo fence behind the yard, the
            // grassy yard with its path, and the mango tree.
            var halfH = cam != null ? cam.orthographicSize : 5.4f;
            var halfW = cam != null ? halfH * cam.aspect : 9.6f;
            var skyRect = new Rect(-halfW - 0.5f, -2.6f, (halfW + 0.5f) * 2f, halfH + 3.1f);
            SceneArt.Put(world, "Sky", SceneArt.Sky(skyRect, new Color32(96, 166, 230, 255), new Color32(206, 232, 244, 255), new Vector2(-halfW * 0.4f, 4.4f), 4, 44), skyRect.center, Vector2.one, -120);
            var back = new Rect(-halfW - 0.5f, -2.2f, (halfW + 0.5f) * 2f, 4.6f);
            SceneArt.Put(world, "Backdrop", TromXoaiArt.Backdrop(back, -1.9f, -2.6f), back.min, Vector2.one, -110);
            var yard = new Rect(-halfW - 0.5f, -halfH - 0.5f, (halfW + 0.5f) * 2f, -1.75f + halfH + 0.5f);
            SceneArt.Put(world, "Yard", TromXoaiArt.Yard(yard, -3.75f, -4.7f), yard.center, Vector2.one, -100);
            SceneArt.Put(world, "MangoTree", TromXoaiArt.Tree(TreeX, GroundY + 0.5f, CanopyCenter, CanopyRadius), Vector2.zero, Vector2.one, -50);

            PlaceMangoes(mangoesOnTree[(int)Difficulty]);
            PlaceStones(stonesLeft);

            player = Kid.Create("Player", new Color32(240, 240, 240, 255), new Color32(40, 70, 150, 255), 1.7f, Loc.PlayerGender == Gender.Girl);
            player.transform.position = PlayerFeet;
            player.Face(1);
            player.SetDepthOrder(Depth(PlayerFeet.y));
            teammate = Kid.Create("Teammate", new Color32(230, 120, 60, 255), new Color32(60, 60, 60, 255), 1.6f);
            teammate.transform.position = new Vector2(Random.Range(TeammateMinX, TeammateMaxX), TeammateY);
            teammate.SetDepthOrder(Depth(TeammateY));

            for (var i = 0; i < 2; i++)
            {
                sandalView[i] = Sandal(world, $"Sandal{i}").transform;
                sandalState[i] = SandalState.AtFeet;
                sandalView[i].position = PlayerFeet + new Vector2(-0.25f + i * 0.5f, -0.05f);
            }

            for (var i = 0; i < 7; i++)
            {
                var dot = Circle(world, "Aim", Vector2.zero, 0.16f, new Color(1, 1, 1, 0.85f), 200).transform;
                dot.gameObject.SetActive(false);
                aimDots.Add(dot);
            }
        }

        private void PlaceMangoes(int count)
        {
            var spots = new List<Vector2>();
            for (var tries = 0; spots.Count < count && tries < 2000; tries++)
            {
                // Mostly on the lower and outer part of the canopy, where they hang.
                var a = Random.Range(Mathf.PI * 0.95f, Mathf.PI * 2.05f + Mathf.PI * 0.5f);
                var r = Random.Range(0.45f, 0.95f);
                var p = CanopyCenter + new Vector2(Mathf.Cos(a) * CanopyRadius.x * r, Mathf.Sin(a) * CanopyRadius.y * r);
                if (p.y < -0.2f) continue;
                var ok = true;
                foreach (var s in spots) ok &= Vector2.Distance(s, p) > 0.85f;
                if (ok) spots.Add(p);
            }
            foreach (var p in spots)
            {
                var m = new GameObject("Mango").transform;
                m.SetParent(world, false);
                m.position = p;
                SceneArt.Put(m, "Fruit", TromXoaiArt.Mango(Random.Range(0, 6)), Vector2.zero, Vector2.one * 0.525f, -39, Random.Range(-12f, 12f));
                mangoes.Add(new Mango { view = m, pos = p });
            }
        }

        private void PlaceStones(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var p = PlayerFeet + new Vector2(-1.6f + (i % 5) * 0.55f + Random.Range(-0.1f, 0.1f), -0.75f - (i / 5) * 0.38f + Random.Range(-0.08f, 0.08f));
                var s = SceneArt.Put(world, "Stone", OAnQuan.OAnQuanArt.Pebble(Random.Range(0, 6)), p, Vector2.one * 1.6f, Depth(p.y), Random.Range(0f, 360f));

                groundStones.Add(s.transform);
            }
        }

        // ---- Input: tap to pick up, drag back and release to throw ----

        private void Update()
        {
            if (!Running) return;
            player.ArmRaise = inHand.HasValue ? 165f : (float?)null; // arm up, ready to throw
            UpdateMangoes();
            if (ending)
            {
                // The round is decided: things in the air just finish falling.
                DropRemaining();
                return;
            }
            UpdateTeammate();
            UpdateProjectiles();
            if (Time.timeScale == 0f) return;

            var pointer = Pointer.current;
            if (pointer == null) return;
            var cam = Camera.main;
            var screen = pointer.position.ReadValue();
            var w = (Vector2)cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            if (pointer.press.wasPressedThisFrame && !OverUi())
            {
                if (!inHand.HasValue) TryPickUp(w);
                else
                {
                    aiming = true;
                    aimStart = aimNow = w;
                }
            }
            if (aiming)
            {
                aimNow = w;
                ShowAim();
                if (pointer.press.wasReleasedThisFrame)
                {
                    aiming = false;
                    HideAim();
                    var drag = aimStart - aimNow;
                    if (drag.magnitude > 0.35f) Throw(drag.normalized * Mathf.Clamp01(drag.magnitude / fullDrag) * maxThrowSpeed);
                }
            }
        }

        private static bool OverUi()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        private void TryPickUp(Vector2 w)
        {
            // Stones first; sandals only once the stones are used up.
            Transform best = null;
            var bestDist = 0.9f;
            foreach (var s in groundStones)
            {
                var d = Vector2.Distance(s.position, w);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = s;
                }
            }
            if (best != null)
            {
                PickUpStone(best);
                return;
            }
            if (stonesLeft > 0) return;
            for (var i = 0; i < 2; i++)
                if (sandalState[i] == SandalState.AtFeet && Vector2.Distance(sandalView[i].position, w) < 1.1f)
                {
                    PickUpSandal(i);
                    return;
                }
        }

        private void PickUpStone(Transform stone)
        {
            groundStones.Remove(stone);
            Destroy(stone.gameObject);
            inHand = Kind.Stone;
            Sfx.Play(Snd.Clack, 0.6f);
            Refresh();
        }

        private void PickUpSandal(int i)
        {
            sandalState[i] = SandalState.InHand;
            sandalView[i].position = Hand;
            handSandal = i;
            inHand = Kind.Sandal;
            Refresh();
        }

        private void ShowAim()
        {
            var drag = aimStart - aimNow;
            var power = Mathf.Clamp01(drag.magnitude / fullDrag);
            var on = drag.magnitude > 0.35f;
            // A short straight guide: direction and power only, so aiming is still a skill.
            for (var i = 0; i < aimDots.Count; i++)
            {
                aimDots[i].gameObject.SetActive(on && i < 2 + Mathf.RoundToInt(power * 5));
                aimDots[i].position = Hand + drag.normalized * (0.5f + i * 0.45f);
            }
        }

        private void HideAim()
        {
            foreach (var d in aimDots) d.gameObject.SetActive(false);
        }

        /// <summary>Throws what is in hand with an initial velocity (also used by the capture harness).</summary>
        public void Throw(Vector2 velocity)
        {
            if (!inHand.HasValue || !Running) return;
            var p = new Projectile { kind = inHand.Value, pos = Hand, vel = velocity };
            Sfx.Play(Snd.Whoosh, 0.7f);
            if (p.kind == Kind.Stone)
            {
                stonesLeft--;
                p.view = SceneArt.Put(world, "ThrownStone", OAnQuan.OAnQuanArt.Pebble(2), Hand, Vector2.one * 1.6f, 150).transform;

            }
            else
            {
                p.sandal = handSandal;
                p.view = sandalView[handSandal];
                sandalState[handSandal] = SandalState.Flying;
                handSandal = -1;
            }
            inHand = null;
            flying.Add(p);
            Refresh();
        }

        /// <summary>Picks up the next available throwable (capture harness).</summary>
        public bool DebugPickUp()
        {
            if (inHand.HasValue) return true;
            if (groundStones.Count > 0)
            {
                PickUpStone(groundStones[0]);
                return true;
            }
            if (stonesLeft > 0) return false;
            for (var i = 0; i < 2; i++)
                if (sandalState[i] == SandalState.AtFeet)
                {
                    PickUpSandal(i);
                    return true;
                }
            return false;
        }

        /// <summary>The velocity that reaches the nearest hanging mango in <paramref name="time"/> seconds (capture harness).</summary>
        public Vector2? DebugAimAtMango(float time)
        {
            foreach (var m in mangoes)
                if (!m.falling)
                    return new Vector2((m.pos.x - Hand.x) / time, (m.pos.y - Hand.y + 0.5f * gravity * time * time) / time);
            return null;
        }

        // ---- Simulation ----

        private void UpdateProjectiles()
        {
            var dt = Time.deltaTime;
            for (var i = flying.Count - 1; i >= 0; i--)
            {
                var p = flying[i];
                p.vel.y -= gravity * dt;
                p.pos += p.vel * dt;
                p.view.position = p.pos;
                p.view.Rotate(0, 0, -540f * dt);

                var removed = false;
                foreach (var m in mangoes)
                {
                    if (m.falling || Vector2.Distance(m.pos, p.pos) >= MangoRadius + ProjectileRadius) continue;
                    m.falling = true;
                    m.vy = 0;
                    Sfx.Play(Snd.Hit, 0.8f, 1.3f);
                    if (p.kind == Kind.Stone)
                    {
                        // Stones never bounce: gone the moment they hit (owner 2026-10-08).
                        RemoveStone(i, p);
                        removed = true;
                        break;
                    }
                    // A sandal is knocked back a little and, having hit a mango, can no longer get stuck.
                    p.hitMango = true;
                    p.vel = new Vector2(-p.vel.x * 0.3f, Mathf.Min(p.vel.y, 0));
                }
                if (removed) continue;

                if (p.kind == Kind.Stone)
                {
                    // Stones never come back down (owner): a falling stone can still hit a mango, but it is lost in the
                    // leaves before it drops below the canopy, so it never reaches the teammate.
                    if ((p.vel.y < 0 && p.pos.y < -0.3f) || OffScreen(p.pos)) RemoveStone(i, p);
                    continue;
                }

                if (InCanopy(p.pos))
                {
                    p.enteredCanopy = true;
                    p.canopyPos = p.pos;
                }
                // At the top of its flight the sandal's fate is decided (owner 2026-10-08): stuck in the branches (only
                // if it went into the leaves without hitting a mango), otherwise it falls, landing on the friend 60 %
                // of the time.
                if (!p.decided && p.vel.y < 0)
                {
                    p.decided = true;
                    if (p.enteredCanopy && !p.hitMango && Random.value < sandalStuckChance)
                    {
                        sandalState[p.sandal] = SandalState.Stuck;
                        p.view.position = p.canopyPos;
                        Debug.Log("[CHILD-XOAI] sandal stuck in the tree");
                        flying.RemoveAt(i);
                        CheckOutOfThrowables();
                        Refresh();
                        continue;
                    }
                    p.willHit = Random.value < sandalHitChance;
                    CancelFetch();
                    Debug.Log($"[CHILD-XOAI] sandal falls: hit roll {p.willHit}");
                }
                if (p.decided && p.vel.y < 0) Steer(p, dt);
                if (p.vel.y < 0 && p.willHit && HitsTeammate(p.pos))
                {
                    flying.RemoveAt(i);
                    endKind = EndKind.Fight;
                    Sfx.Play(Snd.Hit, 0.9f, 0.8f);
                    StartCoroutine(Fight());
                    return;
                }
                if (p.vel.y < 0 && p.pos.y <= GroundY + 0.1f)
                {
                    p.view.position = new Vector2(Mathf.Clamp(p.pos.x, -9f, 9f), GroundY + 0.1f);
                    sandalState[p.sandal] = SandalState.OnGround;
                    flying.RemoveAt(i);
                    FetchSandal(p.sandal);
                    Refresh();
                }
                else if (OffScreen(p.pos))
                {
                    // Flew out of sight: it lands at the edge of the field.
                    p.view.position = new Vector2(Mathf.Clamp(p.pos.x, -9f, 9f), GroundY + 0.1f);
                    sandalState[p.sandal] = SandalState.OnGround;
                    flying.RemoveAt(i);
                    FetchSandal(p.sandal);
                    Refresh();
                }
            }
        }

        /// <summary>
        /// Makes what the player sees match the roll, every frame while the sandal falls (its path can still change when
        /// it knocks a mango): on a hit the friend runs under the landing point, arriving in time; on a miss the friend
        /// keeps out of its way.
        /// </summary>
        private void Steer(Projectile p, float dt)
        {
            var head = TeammateY + teammate.Height * 0.7f;
            var drop = p.pos.y - head;
            var vy = p.vel.y;
            var t = drop <= 0 ? 0.02f : (vy + Mathf.Sqrt(vy * vy + 2f * gravity * drop)) / gravity;
            var land = Mathf.Clamp(p.pos.x + p.vel.x * t, -9f, 9f);
            var pos = teammate.transform.position;
            float target;
            if (p.willHit) target = land;
            else if (Mathf.Abs(land - pos.x) < 1.3f) target = land + (pos.x >= land ? 1.6f : -1.6f);
            else return;
            var need = Mathf.Abs(target - pos.x) / Mathf.Max(0.05f, t * 0.85f);
            var speed = Mathf.Clamp(need, teammateSpeed, 14f);
            pos.x = Mathf.Clamp(Mathf.MoveTowards(pos.x, target, speed * dt), -9f, 9f);
            teammate.transform.position = pos;
            teammate.Face(target - pos.x);
            teammate.Walking = Mathf.Abs(target - pos.x) > 0.02f;
            steering = true;
        }

        private bool steering;

        /// <summary>Stops a sandal fetch in progress; the sandal goes back to waiting on the ground.</summary>
        private void CancelFetch()
        {
            if (teammateTask == null) return;
            StopCoroutine(teammateTask);
            teammateTask = null;
            for (var s = 0; s < 2; s++)
                if (sandalState[s] == SandalState.Carried)
                {
                    sandalState[s] = SandalState.OnGround;
                    StartCoroutine(FetchLater(s));
                }
        }

        private void DropRemaining()
        {
            for (var i = flying.Count - 1; i >= 0; i--)
            {
                var p = flying[i];
                p.vel.y -= gravity * Time.deltaTime;
                p.pos += p.vel * Time.deltaTime;
                p.view.position = p.pos;
                if (p.pos.y > GroundY && !OffScreen(p.pos)) continue;
                if (p.kind == Kind.Stone) Destroy(p.view.gameObject);
                else p.view.position = new Vector2(Mathf.Clamp(p.pos.x, -9f, 9f), GroundY + 0.1f);
                flying.RemoveAt(i);
            }
        }

        private void RemoveStone(int index, Projectile p)
        {
            flying.RemoveAt(index);
            Destroy(p.view.gameObject);
            MaybeSandalTalk();
            CheckOutOfThrowables();
        }

        private static bool OffScreen(Vector2 p) => p.x > 11f || p.x < -11f || p.y < -6f;

        private static bool InCanopy(Vector2 p)
        {
            var d = (p - CanopyCenter) / CanopyRadius;
            return d.sqrMagnitude < 0.8f;
        }

        private bool HitsTeammate(Vector2 p)
        {
            var t = (Vector2)teammate.transform.position;
            return Mathf.Abs(p.x - t.x) < 0.45f && p.y > t.y - 0.1f && p.y < t.y + teammate.Height;
        }

        private void UpdateMangoes()
        {
            foreach (var m in mangoes)
            {
                if (!m.falling || m.view == null) continue;
                m.vy -= gravity * Time.deltaTime;
                m.pos.y += m.vy * Time.deltaTime;
                if (m.pos.y <= GroundY)
                {
                    m.pos.y = GroundY;
                    m.falling = false;
                    m.view.position = m.pos;
                    m.view.gameObject.name = "MangoOnGround";
                    m.view = null;
                    if (!ending) collected++; // once the round is decided, a landing mango no longer counts
                    Refresh();
                    if (collected >= Needed && !ending) StartCoroutine(EndAfter(0.6f, Outcome.Win));
                    continue;
                }
                m.view.position = m.pos;
            }
            mangoes.RemoveAll(m => m.view == null && !m.falling);
        }

        // ---- Teammate ----

        private void UpdateTeammate()
        {
            if (teammateTask != null) return;
            if (steering)
            {
                // Steer() moved the friend this frame (a sandal is falling).
                steering = false;
                return;
            }
            if (teammatePause > 0)
            {
                teammatePause -= Time.deltaTime;
                teammate.Walking = false;
                return;
            }
            var pos = teammate.transform.position;
            pos.x += teammateDir * teammateSpeed * Time.deltaTime;
            if (pos.x > TeammateMaxX || pos.x < TeammateMinX || Random.value < 0.004f)
            {
                teammateDir = -teammateDir;
                if (Random.value < 0.4f) teammatePause = Random.Range(0.3f, 1.2f);
            }
            pos.x = Mathf.Clamp(pos.x, TeammateMinX, TeammateMaxX);
            teammate.transform.position = pos;
            teammate.Face(teammateDir);
            teammate.Walking = true;
        }

        private void FetchSandal(int i)
        {
            if (teammateTask != null)
            {
                // Already fetching the other one: queue this one after it.
                StartCoroutine(FetchLater(i));
                return;
            }
            teammateTask = StartCoroutine(FetchRoutine(i));
        }

        private IEnumerator FetchLater(int i)
        {
            while (teammateTask != null) yield return null;
            if (Running && !ending && sandalState[i] == SandalState.OnGround) teammateTask = StartCoroutine(FetchRoutine(i));
        }

        private IEnumerator FetchRoutine(int i)
        {
            sandalState[i] = SandalState.Carried;
            yield return WalkTo(sandalView[i].position.x, 2.6f);
            teammate.Walking = false;
            yield return new WaitForSeconds(0.35f);
            // Toss it back to the player's feet.
            sandalState[i] = SandalState.Returning;
            var from = (Vector2)teammate.transform.position + new Vector2(0, 1f);
            var to = PlayerFeet + new Vector2(-0.25f + i * 0.5f, -0.05f);
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
            {
                sandalView[i].position = Vector2.Lerp(from, to, t) + new Vector2(0, Mathf.Sin(t * Mathf.PI) * 2.2f);
                yield return null;
            }
            sandalView[i].position = to;
            sandalView[i].rotation = Quaternion.identity;
            Debug.Log("[CHILD-XOAI] teammate returned a sandal");
            sandalState[i] = SandalState.AtFeet;
            teammateTask = null;
            Refresh();
        }

        private IEnumerator WalkTo(float x, float speed)
        {
            teammate.Walking = true;
            while (Mathf.Abs(teammate.transform.position.x - x) > 0.05f)
            {
                var pos = teammate.transform.position;
                var dir = Mathf.Sign(x - pos.x);
                teammate.Face(dir);
                pos.x = Mathf.MoveTowards(pos.x, x, speed * Time.deltaTime);
                teammate.transform.position = pos;
                yield return null;
            }
        }

        private IEnumerator Fight()
        {
            Debug.Log("[CHILD-XOAI] sandal hit the teammate: fight");
            ending = true;
            if (teammateTask != null) StopCoroutine(teammateTask);
            teammateTask = null;
            hintText.transform.parent.gameObject.SetActive(false); // no caption: the story tells it (owner 2026-10-08)
            teammate.Shake(0.5f);
            yield return new WaitForSeconds(0.5f);
            // The story first (once): "Á!" ... then the friend runs in and the two start scuffling (owner 2026-10-08).
            var talk = StoryBook.Once(Game.Save.Record("xoai"), FightTalk);
            Game.Commit();
            if (talk.Count > 0)
            {
                var over = false;
                UiScreens.Dialogue(talk, () => over = true);
                while (!over) yield return null;
            }
            yield return WalkTo(PlayerFeet.x + 0.9f, 7f);
            var cloud = SceneArt.Put(world, "FightCloud", TromXoaiArt.FightCloud(), PlayerFeet + new Vector2(0.45f, 0.9f), Vector2.one, 500);
            player.Shake(1.5f);
            teammate.Shake(1.5f);
            for (var t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                cloud.transform.localScale = Vector3.one * (1f + Mathf.Sin(t * 30f) * 0.08f);
                cloud.transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 17f) * 6f);
                yield return null;
            }
            Finish(Outcome.Loss);
        }

        private void CheckOutOfThrowables()
        {
            if (ending || stonesLeft > 0 || inHand.HasValue || flying.Count > 0) return;
            if (sandalState[0] == SandalState.Stuck && sandalState[1] == SandalState.Stuck)
            {
                endKind = EndKind.Stuck;
                SetHint("mg.xoai.stuck");
                StartCoroutine(EndAfter(1.2f, Outcome.Loss));
            }
        }

        private IEnumerator EndAfter(float seconds, Outcome outcome)
        {
            ending = true;
            yield return new WaitForSeconds(seconds);
            Finish(outcome);
        }

        // ---- Story (owner 2026-10-08) ----

        private enum EndKind { Win, Stuck, Fight }

        private EndKind endKind;

        /// <summary>The ending that matches how the round ended: mangoes, sandals lost in the tree, or the sandal fight.</summary>
        // The three endings and the stones-out talk (story parts; listed for watching again).
        /// <summary>Played in the yard as soon as the sandal hits the friend, before he runs in to fight.</summary>
        public static readonly string[] FightTalk = { "mg.xoai.fight.1", "mg.xoai.fight.2", "mg.xoai.fight.3", "mg.xoai.fight.4" };
        /// <summary>After the fight, before the result.</summary>
        public static readonly string[] FightEnding = { "mg.xoai.fight.5", "mg.xoai.tam", "mg.xoai.run" };
        /// <summary>The whole fight story, for watching again.</summary>
        public static readonly string[] FightStory = { "mg.xoai.fight.1", "mg.xoai.fight.2", "mg.xoai.fight.3", "mg.xoai.fight.4", "mg.xoai.fight.5", "mg.xoai.tam", "mg.xoai.run" };
        public static readonly string[] StuckEnding = { "mg.xoai.tam", "mg.xoai.run", "mg.xoai.stuck.1" };
        public static readonly string[] LootEnding = { "mg.xoai.tam", "mg.xoai.runloot" };
        public static readonly string[] SandalTalk = { "mg.xoai.talk.1", "mg.xoai.talk.2", "mg.xoai.talk.3", "mg.xoai.talk.4", "mg.xoai.talk.5" };

        public override System.Collections.Generic.IReadOnlyList<string> OutcomeLines => endKind switch
        {
            EndKind.Fight => FightEnding,
            EndKind.Stuck => StuckEnding,
            _ => LootEnding,
        };

        /// <summary>The first time the stones run out, the two friends talk it over (the game waits).</summary>
        private void MaybeSandalTalk()
        {
            if (stonesLeft > 0 || inHand.HasValue || ending) return;
            foreach (var f in flying) if (f.kind == Kind.Stone) return;
            var record = Game.Save.Record("xoai");
            if (record.HasFlag("sandaltalk")) return;
            record.SetFlag("sandaltalk");
            Game.Commit();
            Time.timeScale = 0f;
            UiScreens.Dialogue(SandalTalk, () => Time.timeScale = 1f);
        }

        /// <summary>Losing either sandal in the tree loses the pair (owner 2026-10-07): buy a new pair to play again.</summary>
        protected override void OnEnd()
        {
            if (sandalState[0] != SandalState.Stuck && sandalState[1] != SandalState.Stuck) return;
            Shop.LoseSandalPair(Game.Save);
            Game.Commit();
            Debug.Log("[CHILD-XOAI] a sandal was lost: the pair is gone");
        }

        // ---- HUD ----

        private void BuildHud()
        {
            var safe = Ui.Canvas("XoaiHud", 8);
            var panel = Ui.Panel(safe, "Counts", new Color(1, 1, 1, 0.88f)).Place(new Vector2(0.5f, 1f), new Vector2(900, 110), new Vector2(0, -24));
            var row = Ui.Row(panel.transform, "Row", 50);
            mangoText = Ui.Text(row.transform, "", 44, new Color32(200, 140, 0, 255)).Size(320, 90);
            stoneText = Ui.Text(row.transform, "", 44, Ui.InkSoft).Size(240, 90);
            sandalText = Ui.Text(row.transform, "", 44, Ui.Ink).Size(240, 90);
            // One line each, shrinking a little if a language needs more room ("Mangoes: 0/5").
            foreach (var t in new[] { mangoText, stoneText, sandalText })
            {
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.enableAutoSizing = true;
                t.fontSizeMin = 30;
                t.fontSizeMax = 44;
            }
            var hintPanel = Ui.Panel(safe, "Hint", new Color(1f, 0.97f, 0.9f, 0.85f)).Place(new Vector2(0.5f, 0f), new Vector2(1200, 76), new Vector2(0, 22));
            hintText = Ui.Text(hintPanel.transform, "", 38, Ui.Ink);
            hintText.Stretch(Vector2.zero, Vector2.one, 6);
            hintText.gameObject.AddComponent<LocText>();
        }

        private void Refresh()
        {
            if (mangoText == null) return;
            mangoText.text = Loc.Format("mg.xoai.mangoes", collected, Needed);
            stoneText.text = Loc.Format("mg.xoai.stones", stonesLeft);
            var sandals = 0;
            foreach (var s in sandalState) if (s != SandalState.Stuck) sandals++;
            sandalText.text = Loc.Format("mg.xoai.sandals", sandals);
            if (ending) return;
            SetHint(inHand.HasValue ? "mg.xoai.hint.throw" : stonesLeft > 0 ? "mg.xoai.hint.stone" : "mg.xoai.hint.sandal");
        }

        private void SetHint(string key) => hintText.GetComponent<LocText>().Key = key;

        // ---- Shapes ----

        private static int Depth(float y) => Mathf.RoundToInt(-y * 100f);

        private static SpriteRenderer Circle(Transform parent, string name, Vector2 pos, float size, Color color, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.position = pos;
            sr.transform.localScale = Vector3.one * size;
            sr.sprite = Ui.Circle;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>A blue rubber flip-flop (official generated art).</summary>
        private static SpriteRenderer Sandal(Transform parent, string name) => SceneArt.Put(parent, name, TromXoaiArt.Sandal(), Vector2.zero, Vector2.one, 400);
    }
}
