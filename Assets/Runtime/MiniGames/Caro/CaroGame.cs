using System.Collections;
using ChildGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChildGame.MiniGames.Caro
{
    /// <summary>
    /// MG02 — cờ caro on the classroom desk: five in a row against the AI on a 15×15 squared-paper board.
    /// Rock-paper-scissors decides who moves first. The player is X (blue pen), the AI is O (red pen).
    /// </summary>
    public class CaroGame : MiniGame
    {
        [SerializeField] private int boardSize = 15;
        [Tooltip("World size of one cell.")]
        [SerializeField] private float cell = 0.66f;
        [Tooltip("The AI 'thinks' for a moment so its moves feel natural (placeholder range, seconds).")]
        [SerializeField] private Vector2 aiDelay = new Vector2(0.45f, 0.9f);

        public static readonly Color PlayerInk = new Color32(40, 80, 200, 255);
        public static readonly Color AiInk = new Color32(205, 45, 45, 255);

        private CaroBoard board;
        private Transform boardRoot;
        private Sprite xSprite, oSprite;
        private SpriteRenderer lastMoveMark;
        private TextMeshProUGUI turnText;
        private bool playerTurn;
        private PenArm playerArm, aiArm;
        private readonly System.Random rng = new System.Random();

        public CaroBoard Board => board;
        public bool PlayerTurn => playerTurn && Running;

        protected override void OnBegin()
        {
            board = new CaroBoard(boardSize);
            BuildBoard();
            BuildHud();
            SetTurnText("mg.caro.rps");
            RockPaperScissors.Show(playerFirst =>
            {
                if (playerFirst) BeginPlayerTurn();
                else StartCoroutine(AiTurn());
            }, rng, "mg.caro.opponent");
        }

        // ---- Turns ----

        private void BeginPlayerTurn()
        {
            playerTurn = true;
            SetTurnText("mg.caro.yourturn");
        }

        /// <summary>Places the player's mark (touch input or the capture harness). False if not allowed.</summary>
        public bool PlayerMove(int x, int y)
        {
            if (!PlayerTurn || Time.timeScale == 0f || !board.Place(x, y, CaroBoard.PlayerMark)) return false;
            playerTurn = false;
            StartCoroutine(PlayerWrites(x, y));
            return true;
        }

        private IEnumerator PlayerWrites(int x, int y)
        {
            yield return WriteMark(playerArm, x, y, CaroBoard.PlayerMark);
            if (CheckEnd(x, y)) yield break;
            playerArm.Rest();
            StartCoroutine(AiTurn());
        }

        private IEnumerator AiTurn()
        {
            SetTurnText("mg.caro.aiturn");
            // Think on a worker thread (Hard searches for forced wins) while the "thinking" pause plays.
            var copy = board.Clone();
            var seed = rng.Next();
            var difficulty = Difficulty;
            var think = System.Threading.Tasks.Task.Run(() => CaroAi.Choose(copy, CaroBoard.AiMark, difficulty, new System.Random(seed)));
            yield return new WaitForSeconds(Random.Range(aiDelay.x, aiDelay.y));
            while (!think.IsCompleted) yield return null;
            if (!Running) yield break;
            var move = think.Result;
            board.Place(move.x, move.y, CaroBoard.AiMark);
            yield return WriteMark(aiArm, move.x, move.y, CaroBoard.AiMark);
            if (CheckEnd(move.x, move.y)) yield break;
            aiArm.Rest();
            BeginPlayerTurn();
        }

        private bool CheckEnd(int x, int y)
        {
            var line = board.WinningLine(x, y);
            if (line != null)
            {
                var playerWon = board[x, y] == CaroBoard.PlayerMark;
                SetTurnText(playerWon ? "mg.caro.youwin" : "mg.caro.aiwin");
                StartCoroutine(StrikeThrough(line[0], line[line.Count - 1], playerWon ? playerArm : aiArm,
                    () => Finish(playerWon ? Outcome.Win : Outcome.Loss)));
                return true;
            }
            if (!board.Full) return false;
            SetTurnText("mg.caro.draw");
            Finish(Outcome.Draw);
            return true;
        }

        // ---- Board view ----

        private Vector3 CellCenter(int x, int y)
        {
            var half = (boardSize - 1) * 0.5f;
            return boardRoot.position + new Vector3((x - half) * cell, (y - half) * cell, 0);
        }

        private bool CellAt(Vector3 world, out int x, out int y)
        {
            var local = world - boardRoot.position;
            var half = boardSize * 0.5f;
            x = Mathf.FloorToInt(local.x / cell + half);
            y = Mathf.FloorToInt(local.y / cell + half);
            return board.InBounds(x, y);
        }

        private void BuildBoard()
        {
            if (boardRoot != null) Destroy(boardRoot.gameObject);
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = new Color32(176, 122, 76, 255); // wooden desk
            boardRoot = new GameObject("Board").transform;
            // Board on the right, room for the turn panel on the left.
            boardRoot.position = new Vector3(2.6f, -0.15f, 0);

            // Official generated art (owner 2026-10-08): the classroom desk, a sheet torn from a squared notebook, a ruler
            // and an eraser lying beside it.
            var halfH = cam != null ? cam.orthographicSize : 5.4f;
            var halfW = cam != null ? halfH * cam.aspect : 9.6f;
            var deskRect = new Rect(-halfW - 0.5f, -halfH - 0.5f, (halfW + 0.5f) * 2f, (halfH + 0.5f) * 2f);
            SceneArt.Put(boardRoot, "Desk", SceneArt.Desk(deskRect, new Color32(176, 118, 70, 255), 3), deskRect.center - (Vector2)boardRoot.position, Vector2.one, -10);
            SceneArt.Put(boardRoot, "Notebook", SceneArt.NotebookGrid(boardSize, cell), Vector2.zero, Vector2.one, -1);
            SceneArt.Put(boardRoot, "Ruler", SceneArt.Ruler(), new Vector2(-halfW - 2.6f + 3.4f, -halfH + 0.9f) - (Vector2)boardRoot.position, Vector2.one * 0.9f, -5, 8f);
            SceneArt.Put(boardRoot, "Eraser", SceneArt.Eraser(), new Vector2(-halfW + 1.2f, halfH - 1.9f) - (Vector2)boardRoot.position, Vector2.one, -5, -20f);

            var box = boardRoot.gameObject.AddComponent<BoxCollider2D>();
            box.size = Vector2.one * boardSize * cell;
            boardRoot.gameObject.AddComponent<Tappable>().Clicked += OnBoardTap;

            xSprite ??= ProcArt.PenX(96, 96);
            oSprite ??= ProcArt.PenO(96, 96);

            lastMoveMark = new GameObject("LastMove").AddComponent<SpriteRenderer>();
            lastMoveMark.transform.SetParent(boardRoot, false);
            lastMoveMark.sprite = Ui.Circle;
            lastMoveMark.color = new Color(1f, 0.85f, 0.2f, 0.45f);
            lastMoveMark.transform.localScale = Vector3.one * cell * 0.95f;
            lastMoveMark.sortingOrder = 0;
            lastMoveMark.enabled = false;

            // The two writing arms rest at the board's edges: the player's below right, the classmate's above left.
            var halfBoard = boardSize * cell * 0.5f;
            var c = (Vector2)boardRoot.position;
            playerArm = PenArm.Create(HandArt.Who.Player, c + new Vector2(halfBoard + 1.15f, -2.3f), false);
            aiArm = PenArm.Create(HandArt.Who.Opponent, c + new Vector2(-halfBoard - 0.75f, 2.5f), true);
        }

        private void OnBoardTap(PointerEventData e)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var world = cam.ScreenToWorldPoint(new Vector3(e.position.x, e.position.y, -cam.transform.position.z));
            if (CellAt(world, out var x, out var y)) PlayerMove(x, y);
        }

        private void ShowMark(int x, int y, int mark, float rotation)
        {
            var sr = new GameObject(mark == CaroBoard.PlayerMark ? "X" : "O").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(boardRoot, false);
            sr.transform.position = CellCenter(x, y);
            sr.transform.rotation = Quaternion.Euler(0, 0, rotation);
            sr.sprite = mark == CaroBoard.PlayerMark ? xSprite : oSprite;
            sr.color = mark == CaroBoard.PlayerMark ? PlayerInk : AiInk;
            sr.sortingOrder = 1;
            sr.transform.localScale = Vector3.one * cell * 0.92f;
            Sfx.Play(Snd.Pen, 0.7f);
            lastMoveMark.enabled = true;
            lastMoveMark.transform.position = CellCenter(x, y);
        }

        /// <summary>
        /// The arm reaches to the square and writes the mark with its pen, stroke by stroke (owner 2026-10-08). Ink dots
        /// follow the tip along the same paths as the finished mark, which then replaces them.
        /// </summary>
        private IEnumerator WriteMark(PenArm arm, int x, int y, int mark)
        {
            var rotation = Random.Range(-8f, 8f);
            var strokes = mark == CaroBoard.PlayerMark ? ProcArt.PenXStrokes() : ProcArt.PenOStrokes();
            var thickness = mark == CaroBoard.PlayerMark ? ProcArt.PenXThickness : ProcArt.PenOThickness;
            var size = cell * 0.92f;
            var center = (Vector2)CellCenter(x, y);
            var rot = Quaternion.Euler(0, 0, rotation);
            var dots = new System.Collections.Generic.List<GameObject>();
            var dotSize = thickness * size;
            void Ink(Vector2 a, Vector2 b)
            {
                var n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / (dotSize * 0.3f)));
                for (var i = 1; i <= n; i++)
                {
                    var dot = new GameObject("ink").AddComponent<SpriteRenderer>();
                    dot.sprite = Ui.Circle;
                    dot.color = arm.Ink;
                    dot.sortingOrder = 1;
                    dot.transform.position = Vector2.Lerp(a, b, i / (float)n);
                    dot.transform.localScale = Vector3.one * dotSize;
                    dots.Add(dot.gameObject);
                }
            }
            var first = true;
            foreach (var stroke in strokes)
            {
                var path = new System.Collections.Generic.List<Vector2>();
                foreach (var p in stroke) path.Add(center + (Vector2)(rot * ((p - new Vector2(0.5f, 0.5f)) * size)));
                yield return arm.MoveTipTo(path[0], first ? 0.28f : 0.09f);
                first = false;
                yield return arm.Trace(path, mark == CaroBoard.PlayerMark ? 0.14f : 0.3f, Ink);
            }
            foreach (var d in dots) Destroy(d);
            ShowMark(x, y, mark, rotation);
        }

        /// <summary>The winner's pen draws a line through the five, then the result is reported.</summary>
        private IEnumerator StrikeThrough(Vector2Int from, Vector2Int to, PenArm arm, System.Action done)
        {
            var sr = new GameObject("WinLine").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(boardRoot, false);
            sr.sprite = Ui.Round;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.color = arm.Ink;
            sr.sortingOrder = 3;
            Vector2 a = CellCenter(from.x, from.y);
            Vector2 b = CellCenter(to.x, to.y);
            var dir = (b - a).normalized;
            a -= dir * cell * 0.4f;
            b += dir * cell * 0.4f;
            var width = cell * 0.16f;
            sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            // The rounded sprite has 0.24-unit corners; scale it down so a thin line keeps round ends.
            const float shrink = 0.2f;
            sr.transform.localScale = Vector3.one * shrink;
            sr.enabled = false;
            yield return arm.MoveTipTo(a, 0.25f);
            sr.enabled = true;
            yield return arm.Trace(new[] { a, b }, 0.45f, (p, q) =>
            {
                var len = Mathf.Max(width, Vector2.Distance(a, q));
                sr.size = new Vector2(len, width) / shrink;
                sr.transform.position = a + dir * (len * 0.5f);
            });
            arm.Rest();
            yield return new WaitForSeconds(0.6f);
            done();
        }

        // ---- HUD ----

        private void BuildHud()
        {
            var safe = Ui.Canvas("CaroHud", 8);
            var panel = Ui.Panel(safe, "TurnPanel", new Color(1, 1, 1, 0.9f)).Place(new Vector2(0f, 0.5f), new Vector2(520, 440), new Vector2(40, -20));
            var col = Ui.Column(panel.transform, "Column", 20, 30);
            var you = Ui.Text(col.transform, "", 44, PlayerInk).Size(0, 70);
            you.gameObject.AddComponent<LocText>().SetFormat("mg.caro.you", Game.Save.playerName);
            Ui.Label(col.transform, "mg.caro.ai", 44, AiInk).Size(0, 70);
            turnText = Ui.Text(col.transform, "", 46, Ui.Ink).Size(0, 160);
        }

        private void SetTurnText(string key)
        {
            if (turnText == null) return;
            var loc = turnText.GetComponent<LocText>();
            if (loc == null) loc = turnText.gameObject.AddComponent<LocText>();
            loc.Key = key;
        }
    }
}
