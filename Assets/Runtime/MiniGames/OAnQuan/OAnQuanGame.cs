using System.Collections;
using System.Collections.Generic;
using ChildGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChildGame.MiniGames.OAnQuan
{
    /// <summary>
    /// MG05 — Ô ăn quan against the AI, on a board drawn on the yard. Rock-paper-scissors decides who starts.
    /// The player taps a space on their (bottom) side, then picks a direction; the mover's arm sows the pieces one by
    /// one, counting out loud, and slaps ("Đập!") and takes ("Ăn!") captures (owner 2026-10-08).
    /// Rules live in <see cref="OAnQuanRules"/>; the view replays its events.
    /// </summary>
    public class OAnQuanGame : MiniGame
    {
        [Header("Placeholder tuning")]
        [Tooltip("Seconds per sown piece.")]
        [SerializeField] private float sowStep = 0.42f;
        [Tooltip("Pause on each space after a piece drops (seconds).")]
        [SerializeField] private float sowPause = 0.12f;
        [SerializeField] private float pickMove = 0.55f;
        [SerializeField] private float pickPause = 0.45f;
        [Tooltip("Reach to the empty space, then to the pieces taken (seconds).")]
        [SerializeField] private float slapMove = 0.45f;
        [SerializeField] private float slapHold = 0.5f;
        [SerializeField] private float eatHold = 0.55f;
        [Tooltip("End of the game: how long the \"both quan are taken\" notice shows before the sweep.")]
        [SerializeField] private float sweepNotice = 2.2f;
        [SerializeField] private float sweepStep = 0.25f;
        [SerializeField] private Vector2 aiThink = new Vector2(0.6f, 1.1f);

        private const float Cell = 1.9f;
        private static readonly Vector2 BoardCenter = new Vector2(0f, 0.2f);
        private const int Player = 0, Ai = 1;

        private OAnQuanRules rules;
        private readonly System.Random rng = new System.Random();
        private readonly int[] shownDan = new int[OAnQuanRules.Spaces];
        private readonly bool[] shownQuan = new bool[OAnQuanRules.Spaces];
        private readonly Transform[] spaceRoots = new Transform[OAnQuanRules.Spaces];
        private readonly TextMeshPro[] counts = new TextMeshPro[OAnQuanRules.Spaces];
        private SpriteRenderer highlight;
        private readonly ReachArm[] arms = new ReachArm[2];
        private int selected = -1;
        private bool playerTurn, busy;
        private TextMeshProUGUI turnText, playerScore, aiScore;
        private GameObject directionButtons;

        public OAnQuanRules Rules => rules;
        /// <summary>Raised when an arm says something: "slap", "eat" or a count (capture harness).</summary>
        public event System.Action<string> Said;
        public bool PlayerTurn => playerTurn && !busy && Running;

        // ---- Setup ----

        protected override void OnBegin()
        {
            rules = OAnQuanRules.NewGame();
            System.Array.Copy(rules.dan, shownDan, shownDan.Length);
            System.Array.Copy(rules.quan, shownQuan, shownQuan.Length);
            BuildBoard();
            BuildHud();
            for (var i = 0; i < OAnQuanRules.Spaces; i++) RedrawSpace(i);
            SetTurn("mg.oaq.rps");
            RockPaperScissors.Show(playerFirst => StartCoroutine(BeginTurn(playerFirst ? Player : Ai)), rng);
        }

        public static Vector2 SpaceCenter(int i)
        {
            if (i == OAnQuanRules.QuanLeft) return BoardCenter + new Vector2(-2.5f * Cell - 0.75f, 0);
            if (i == OAnQuanRules.QuanRight) return BoardCenter + new Vector2(2.5f * Cell + 0.75f, 0);
            if (i <= 5) return BoardCenter + new Vector2((i - 3) * Cell, -Cell * 0.5f);
            return BoardCenter + new Vector2((9 - i) * Cell, Cell * 0.5f);
        }

        private void BuildBoard()
        {
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = new Color32(196, 160, 112, 255); // packed earth yard
            var root = new GameObject("Board").transform;
            // Official generated art (owner 2026-10-08): the board scratched in chalk on the packed-earth yard.
            var halfH = cam != null ? cam.orthographicSize : 5.4f;
            var halfW = cam != null ? halfH * cam.aspect : 9.6f;
            var yard = new Rect(-halfW - 0.5f, -halfH - 0.5f, (halfW + 0.5f) * 2f, (halfH + 0.5f) * 2f);
            SceneArt.Put(root, "Yard", OAnQuanArt.Yard(yard), yard.center, Vector2.one, -30);
            SceneArt.Put(root, "Board", OAnQuanArt.Board(Cell), BoardCenter, Vector2.one, -18);
            for (var i = 0; i < OAnQuanRules.Spaces; i++)
            {
                spaceRoots[i] = new GameObject("Space" + i).transform;
                spaceRoots[i].SetParent(root, false);
                spaceRoots[i].position = SpaceCenter(i);
                var label = new GameObject("Count", typeof(RectTransform)).AddComponent<TextMeshPro>();
                label.transform.SetParent(root, false);
                var below = OAnQuanRules.SideOf(i) == Ai ? 1f : -1f;
                label.transform.position = OAnQuanRules.IsQuanSpace(i)
                    ? SpaceCenter(i) + new Vector2(i == 0 ? -0.3f : 0.3f, -Cell - 0.45f)
                    : SpaceCenter(i) + new Vector2(0, below * (Cell * 0.5f + 0.35f));
                label.rectTransform.sizeDelta = new Vector2(2.6f, 0.6f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.font = Ui.Bold;
                label.fontSize = 4.2f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Ui.Ink;
                label.sortingOrder = 30;
                counts[i] = label;
            }
            // Taps on the player's spaces.
            for (var i = 1; i <= 5; i++)
            {
                var space = i;
                var hit = new GameObject("Tap" + i);
                hit.transform.position = SpaceCenter(i);
                hit.AddComponent<BoxCollider2D>().size = new Vector2(Cell, Cell);
                hit.AddComponent<Tappable>().Clicked += _ => Select(space);
            }
            highlight = Box(root, "Highlight", Vector2.zero, new Vector2(Cell - 0.1f, Cell - 0.1f), new Color(1f, 0.9f, 0.3f, 0.45f), -16);
            highlight.enabled = false;

            // Two arms reach over the board (owner 2026-10-08): the player's from below, the opponent's from above.
            // At rest they wait just past the screen edge, in the gaps between the HUD panels.
            arms[Player] = ReachArm.Create(HandArt.Who.Player, new Vector2(-0.6f, -10.5f), new Vector2(-0.7f, -5.9f), false);
            arms[Ai] = ReachArm.Create(HandArt.Who.Opponent, new Vector2(2.6f, 10.5f), new Vector2(2.7f, 5.9f), true);
        }

        /// <summary>Redraws the pebbles of one space from the shown counts (deterministic scatter).</summary>
        private void RedrawSpace(int i)
        {
            var root = spaceRoots[i];
            foreach (Transform c in root) Destroy(c.gameObject);
            var n = Mathf.Min(shownDan[i], 40);
            var rand = new System.Random(i * 97 + 13);
            var radius = OAnQuanRules.IsQuanSpace(i) ? 0.8f : 0.7f;
            for (var k = 0; k < n; k++)
            {
                // Sunflower spiral keeps pebbles apart; a little jitter makes it look hand-placed.
                var a = k * 2.39996f;
                var r = radius * Mathf.Sqrt((k + 0.5f) / Mathf.Max(n, 6));
                var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r + new Vector2((float)rand.NextDouble() - 0.5f, (float)rand.NextDouble() - 0.5f) * 0.08f;
                var variant = rand.Next(6);
                SceneArt.Put(root, "Dan", OAnQuanArt.Pebble(variant), p + (OAnQuanRules.IsQuanSpace(i) ? new Vector2(i == 0 ? -0.2f : 0.2f, 0) : Vector2.zero),
                    Vector2.one, 10 + k, (float)rand.NextDouble() * 360f);
            }
            if (shownQuan[i])
                SceneArt.Put(root, "QuanStone", OAnQuanArt.QuanStone(), new Vector2(i == 0 ? 0.35f : -0.35f, 0), Vector2.one, 60);
            counts[i].text = shownQuan[i] ? Loc.Format("mg.oaq.quancount", shownDan[i]) : shownDan[i].ToString();
        }

        // ---- Turns ----

        private IEnumerator BeginTurn(int side)
        {
            if (!Running) yield break;
            if (rules.GameOver)
            {
                yield return EndGame();
                yield break;
            }
            var events = new List<OAnQuanRules.Event>();
            var canPlay = rules.PrepareTurn(side, events);
            if (events.Count > 0)
            {
                SetTurn(side == Player ? "mg.oaq.buy.you" : "mg.oaq.buy.ai");
                yield return Replay(events, side);
                yield return new WaitForSeconds(0.6f);
            }
            if (!canPlay)
            {
                SetTurn(side == Player ? "mg.oaq.bankrupt.you" : "mg.oaq.bankrupt.ai");
                yield return new WaitForSeconds(1.2f);
                yield return EndGame();
                yield break;
            }
            if (side == Player)
            {
                playerTurn = true;
                SetTurn("mg.oaq.yourturn");
            }
            else StartCoroutine(AiTurn());
        }

        private void Select(int space)
        {
            if (!PlayerTurn || Time.timeScale == 0f || rules.dan[space] == 0) return;
            selected = selected == space ? -1 : space;
            highlight.enabled = selected >= 0;
            if (selected >= 0) highlight.transform.position = SpaceCenter(selected);
            directionButtons.SetActive(selected >= 0);
        }

        /// <summary>Plays the selected space in a direction: -1 = toward the left of the screen, +1 = right.</summary>
        public void PlayerSow(int screenDirection)
        {
            if (!PlayerTurn || selected < 0) return;
            // On the player's row, +1 in ring order runs to the right.
            PlayMove(Player, selected, screenDirection);
        }

        /// <summary>Selects and plays a space (capture harness).</summary>
        public bool DebugPlay(int space, int dir)
        {
            if (!PlayerTurn || rules.dan[space] == 0) return false;
            selected = space;
            PlayMove(Player, space, dir);
            return true;
        }

        private void PlayMove(int side, int space, int dir)
        {
            playerTurn = false;
            selected = -1;
            highlight.enabled = false;
            directionButtons.SetActive(false);
            var events = new List<OAnQuanRules.Event>();
            rules.Play(side, space, dir, events);
            StartCoroutine(AfterMove(events, side));
        }

        private IEnumerator AfterMove(List<OAnQuanRules.Event> events, int side)
        {
            busy = true;
            yield return Replay(events, side);
            busy = false;
            yield return new WaitForSeconds(0.35f);
            yield return BeginTurn(1 - side);
        }

        private IEnumerator AiTurn()
        {
            SetTurn("mg.oaq.aiturn");
            var copy = rules.Clone();
            var seed = rng.Next();
            var difficulty = Difficulty;
            var think = System.Threading.Tasks.Task.Run(() => OAnQuanAi.Choose(copy, Ai, difficulty, new System.Random(seed)));
            yield return new WaitForSeconds(Random.Range(aiThink.x, aiThink.y));
            while (!think.IsCompleted) yield return null;
            if (!Running) yield break;
            var move = think.Result;
            highlight.enabled = true;
            highlight.transform.position = SpaceCenter(move.space);
            yield return new WaitForSeconds(0.4f);
            PlayMove(Ai, move.space, move.dir);
        }

        // ---- Event playback ----

        private bool sweepAnnounced;

        /// <summary>
        /// Replays the rules' events with the moving side's arm (owner 2026-10-08): it scoops a space up in a fist,
        /// sows one piece per space counting out loud, slaps the empty space ("Đập!") and takes the pieces after it
        /// ("Ăn!"). Slow enough to follow.
        /// </summary>
        private IEnumerator Replay(List<OAnQuanRules.Event> events, int side)
        {
            var arm = arms[side];
            var inHand = 0;
            var sown = 0;
            foreach (var e in events)
            {
                switch (e.kind)
                {
                    case OAnQuanRules.EventKind.Pick:
                        arm.SetPose(HandArt.Pose.Paper);
                        yield return arm.MoveTo(SpaceCenter(e.space), pickMove);
                        arm.SetPose(HandArt.Pose.Fist);
                        inHand += e.dan;
                        sown = 0;
                        shownDan[e.space] = 0;
                        RedrawSpace(e.space);
                        arm.Badge(inHand);
                        Sfx.Play(Snd.Clack, 0.6f);
                        yield return new WaitForSeconds(pickPause);
                        break;
                    case OAnQuanRules.EventKind.Drop:
                        yield return arm.MoveTo(SpaceCenter(e.space), sowStep);
                        inHand--;
                        sown++;
                        shownDan[e.space]++;
                        RedrawSpace(e.space);
                        arm.Badge(inHand);
                        arm.Say(NumberWords.Say(sown, Loc.Current), sowPause + sowStep * 0.9f);
                        Said?.Invoke("count");
                        yield return new WaitForSeconds(sowPause);
                        break;
                    case OAnQuanRules.EventKind.Capture:
                        if (e.slap < 0)
                        {
                            // End of the game (both quan taken): say so, then each side sweeps up what is left on its own
                            // row, one space at a time, so it never looks like the last mover took everything.
                            if (!sweepAnnounced)
                            {
                                sweepAnnounced = true;
                                highlight.enabled = false;
                                yield return arm.Rest();
                                SetTurn("mg.oaq.sweep");
                                Said?.Invoke("sweep");
                                yield return new WaitForSeconds(sweepNotice);
                            }
                            shownDan[e.space] = 0;
                            shownQuan[e.space] = false;
                            RedrawSpace(e.space);
                            yield return Fly(SpaceCenter(e.space), OAnQuanRules.SideOf(e.space), e.quan);
                            RefreshScores();
                            yield return new WaitForSeconds(sweepStep);
                            break;
                        }
                        arm.SetPose(HandArt.Pose.Paper);
                        yield return arm.MoveTo(SpaceCenter(e.slap), slapMove);
                        arm.Say(Loc.Get("mg.oaq.say.slap"), slapHold + 0.2f);
                        Said?.Invoke("slap");
                        StartCoroutine(Dust(SpaceCenter(e.slap)));
                        Sfx.Play(Snd.Hit, 0.8f, 0.9f);
                        yield return arm.Press(0.22f);
                        yield return new WaitForSeconds(slapHold);
                        yield return arm.MoveTo(SpaceCenter(e.space), slapMove);
                        arm.SetPose(HandArt.Pose.Fist);
                        arm.Say(Loc.Get("mg.oaq.say.eat"), eatHold + 0.4f);
                        Said?.Invoke("eat");
                        shownDan[e.space] = 0;
                        shownQuan[e.space] = false;
                        RedrawSpace(e.space);
                        yield return new WaitForSeconds(eatHold);
                        yield return Fly(SpaceCenter(e.space), side, e.quan);
                        RefreshScores();
                        break;
                    case OAnQuanRules.EventKind.Buy:
                        shownDan[e.space] = e.dan;
                        RedrawSpace(e.space);
                        RefreshScores();
                        yield return new WaitForSeconds(0.12f);
                        break;
                    case OAnQuanRules.EventKind.EndTurn:
                        highlight.enabled = false;
                        break;
                }
            }
            yield return arm.Rest();
            RefreshScores();
        }

        /// <summary>A puff of dust where a hand slaps the ground.</summary>
        private IEnumerator Dust(Vector2 at)
        {
            var ring = Disc(null, "Dust", at, 0.6f, new Color(1f, 0.95f, 0.85f, 0.6f), 45);
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.45f)
            {
                ring.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.9f, t);
                ring.color = new Color(1f, 0.95f, 0.85f, 0.6f * (1f - t));
                yield return null;
            }
            Destroy(ring.gameObject);
        }

        /// <summary>Captured pieces fly to the capturing side's tray.</summary>
        private IEnumerator Fly(Vector2 from, int side, bool quan)
        {
            var bag = SceneArt.Put(null, "Captured", quan ? OAnQuanArt.QuanStone() : OAnQuanArt.Pebble(1), from, Vector2.one * (quan ? 1f : 1.6f), 70);
            var to = new Vector2(-8f, side == Player ? -4.3f : 4.3f);
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.45f)
            {
                bag.transform.position = Vector2.Lerp(from, to, t) + new Vector2(0, Mathf.Sin(t * Mathf.PI) * 1f);
                yield return null;
            }
            Destroy(bag.gameObject);
        }

        private IEnumerator EndGame()
        {
            playerTurn = false;
            yield return new WaitForSeconds(0.4f);
            var winner = rules.Winner;
            Debug.Log($"[CHILD-OAQ] Game over: empty {rules.BoardEmpty}, stalled {rules.stalled}, bankrupt side {rules.bankruptSide}, turns {rules.turnsPlayed}, score {rules.Score(Player)}-{rules.Score(Ai)}");
            turnText.GetComponent<LocText>().SetFormat("mg.oaq.final", rules.Score(Player), rules.Score(Ai));
            yield return new WaitForSeconds(1.8f);
            Finish(winner == Player ? Outcome.Win : winner == Ai ? Outcome.Loss : Outcome.Draw);
        }

        // ---- HUD ----

        private void BuildHud()
        {
            var safe = Ui.Canvas("OaqHud", 8);
            var aiPanel = Ui.Panel(safe, "AiScore", new Color(1, 1, 1, 0.85f)).Place(new Vector2(0f, 1f), new Vector2(760, 90), new Vector2(240, -24));
            aiScore = Ui.Text(aiPanel.transform, "", 34, Ui.Brick);
            aiScore.Stretch(Vector2.zero, Vector2.one, 10);
            var playerPanel = Ui.Panel(safe, "PlayerScore", new Color(1, 1, 1, 0.85f)).Place(new Vector2(0f, 0f), new Vector2(760, 90), new Vector2(24, 24));
            playerScore = Ui.Text(playerPanel.transform, "", 34, Ui.Sky);
            playerScore.Stretch(Vector2.zero, Vector2.one, 10);
            var turnPanel = Ui.Panel(safe, "Turn", new Color(1, 1, 1, 0.85f)).Place(new Vector2(1f, 1f), new Vector2(640, 90), new Vector2(-24, -24));
            turnText = Ui.Text(turnPanel.transform, "", 34, Ui.Ink);
            turnText.Stretch(Vector2.zero, Vector2.one, 10);
            turnText.gameObject.AddComponent<LocText>();

            var row = Ui.Row(safe, "Direction", 40);
            row.Place(new Vector2(1f, 0f), new Vector2(900, 120), new Vector2(-24, 24));
            row.childAlignment = TextAnchor.MiddleRight;
            Ui.Button(row.transform, "mg.oaq.left", () => PlayerSow(-1), Ui.Leaf, 40).Size(420, 110);
            Ui.Button(row.transform, "mg.oaq.right", () => PlayerSow(1), Ui.Leaf, 40).Size(420, 110);
            directionButtons = row.gameObject;
            directionButtons.SetActive(false);
            RefreshScores();
        }

        private void RefreshScores()
        {
            if (playerScore == null) return;
            playerScore.text = Loc.Format("mg.oaq.score.you", rules.capturedQuan[Player], rules.capturedDan[Player], rules.Score(Player));
            aiScore.text = Loc.Format("mg.oaq.score.ai", rules.capturedQuan[Ai], rules.capturedDan[Ai], rules.Score(Ai));
        }

        private void SetTurn(string key) => turnText.GetComponent<LocText>().Key = key;

        // ---- Shapes ----

        private static SpriteRenderer Disc(Transform parent, string name, Vector2 pos, float size, Color color, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            if (parent != null) sr.transform.SetParent(parent, false);
            sr.transform.position = pos;
            sr.transform.localScale = Vector3.one * size;
            sr.sprite = Ui.Circle;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        private static SpriteRenderer Box(Transform parent, string name, Vector2 pos, Vector2 size, Color color, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.position = pos;
            sr.sprite = Ui.Round;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.transform.localScale = Vector3.one * 0.25f;
            sr.size = size * 4f;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
