using System.Collections;
using System.Collections.Generic;
using ChildGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ChildGame.MiniGames.VeTranh
{
    /// <summary>
    /// MG01 — Vẽ tranh: a drawing contest beside an AI deskmate (owner design + answers 2026-10-07, 2026-10-08).
    /// <list type="bullet">
    /// <item>The judge picks a random prompt for the difficulty (Easy objects, Normal complex objects, Hard scenes).</item>
    /// <item>The player draws with one finger: pencil, a box of 12 wax crayons, an eraser (no undo; it smudges
    /// crayon). Everything is offline.</item>
    /// <item>The AI child draws the same prompt stroke by stroke beside the player, paced by the player's progress
    /// within a minimum and maximum time. It is company, not a real rival.</item>
    /// <item>"Done" ends the drawing; the offline judge checks basic conditions (DrawingJudge) and the drawing wins only if all pass (owner, 2026-10-08).</item>
    /// </list>
    /// ⚠ Pacing, the judge thresholds and the "finished" threshold are placeholders.
    /// </summary>
    public class VeTranhGame : MiniGame
    {
        [Header("Placeholder tuning")]
        [Tooltip("Ink coverage that counts as a complete drawing, per difficulty (share of the sheet).")]
        [SerializeField] private float[] expectedCoverage = { 0.08f, 0.14f, 0.26f };
        [Tooltip("The AI's drawing time limits per difficulty (seconds): it never finishes faster or slower.")]
        [SerializeField] private Vector2[] aiTime = { new Vector2(25, 70), new Vector2(40, 110), new Vector2(60, 160) };
        [Tooltip("Minimum coverage before Done is accepted.")]
        [SerializeField] private float minimumCoverage = 0.008f;

        private const int CanvasW = 1024, CanvasH = 640;
        private const int AiW = 512, AiH = 320;

        private readonly System.Random rng = new System.Random();
        private CrayonCanvas paper, aiPaper;
        private SpriteRenderer paperView, aiView;
        private Vector2 paperSize, aiSize;
        private string prompt;
        private DrawTool tool = DrawTool.Crayon;
        private int colorIndex = 2;
        private bool drawing, active, finishing;
        private Vector2 lastCanvasPos;
        private float lastMoveTime;
        private AudioSource scratch;
        private AudioClip pencilSound, crayonSound, eraserSound;
        private readonly List<Button> crayonButtons = new List<Button>();
        private Button pencilButton, eraserButton, doneButton;
        private TextMeshProUGUI aiStatus;

        // AI drawing state: all stroke segments in order, and how much of their total length is drawn.
        private struct Segment
        {
            public int stroke;
            public Vector2 a, b;
            public float length;
        }

        private List<AiStroke> aiStrokes;
        private readonly List<Segment> aiSegments = new List<Segment>();
        private float aiTotal, aiDrawn, aiClock;
        private int aiSegment;
        private int aiLastStroke = -1;
        private Transform aiHand;

        public CrayonCanvas Paper => paper;
        public string Prompt => prompt;
        public float AiProgress => aiTotal > 0 ? aiDrawn / aiTotal : 0f;
        public bool Active => active;

        protected override void OnBegin()
        {
            prompt = AiDrawing.RandomPrompt(Difficulty, rng);
            BuildScene();
            BuildHud();
            pencilSound = SoundSynth.Pencil();
            crayonSound = SoundSynth.Crayon();
            eraserSound = SoundSynth.Eraser();
            scratch = gameObject.AddComponent<AudioSource>();
            scratch.loop = true;
            scratch.volume = 0f;
            StartCoroutine(JudgeIntro());
        }

        // ---- Scene ----

        private void BuildScene()
        {
            var cam = Camera.main;
            var halfH = cam != null ? cam.orthographicSize : 5.4f;
            var halfW = cam != null ? halfH * cam.aspect : 9.6f;
            if (cam != null) cam.backgroundColor = new Color32(196, 150, 100, 255);
            // Official generated art (owner 2026-10-08): a light wooden school desk under the sheets.
            var deskRect = new Rect(-halfW - 0.5f, -halfH - 0.5f, (halfW + 0.5f) * 2f, (halfH + 0.5f) * 2f);
            SceneArt.Put(null, "Desk", SceneArt.Desk(deskRect, new Color32(200, 150, 96, 255), 8), deskRect.center, Vector2.one, -20);

            paper = new CrayonCanvas(CanvasW, CanvasH, rng.Next());
            paperSize = new Vector2(11.2f, 11.2f * CanvasH / CanvasW);
            var paperCenter = new Vector2(-halfW + 0.45f + paperSize.x * 0.5f, -0.65f);
            Shadow(paperCenter, paperSize);
            paperView = Sheet("Paper", paper, paperCenter, paperSize, 0);

            aiPaper = new CrayonCanvas(AiW, AiH, rng.Next())
            {
                CrayonRadius = 4f, PencilRadius = 1.3f, CrayonRate = 0.08f,
            };
            aiSize = new Vector2(5f, 5f * AiH / AiW);
            var aiCenter = new Vector2(halfW - 0.45f - aiSize.x * 0.5f, halfH - 0.35f - aiSize.y * 0.5f);
            Shadow(aiCenter, aiSize);
            aiView = Sheet("AiPaper", aiPaper, aiCenter, aiSize, 0);
            aiView.transform.rotation = Quaternion.Euler(0, 0, -2.5f); // the deskmate's sheet sits a bit crooked

            aiHand = new GameObject("AiCrayonTip").transform;
            var tip = aiHand.gameObject.AddComponent<SpriteRenderer>();
            tip.sprite = Ui.Circle;
            tip.sortingOrder = 20;
            aiHand.localScale = Vector3.one * 0.22f;
            aiHand.gameObject.SetActive(false);

            aiStrokes = AiDrawing.Build(prompt, rng);
            foreach (var s in aiStrokes)
                for (var i = 1; i < s.points.Count; i++)
                {
                    var len = Vector2.Distance(s.points[i - 1], s.points[i]);
                    aiSegments.Add(new Segment { stroke = aiStrokes.IndexOf(s), a = s.points[i - 1], b = s.points[i], length = len });
                    aiTotal += len;
                }
        }

        private SpriteRenderer Sheet(string name, CrayonCanvas canvas, Vector2 center, Vector2 size, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(canvas.Texture, new Rect(0, 0, canvas.Width, canvas.Height), new Vector2(0.5f, 0.5f), canvas.Width / size.x);
            sr.transform.position = center;
            sr.sortingOrder = order;
            return sr;
        }

        private static void Shadow(Vector2 center, Vector2 size)
        {
            var sr = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            sr.sprite = Ui.Round;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.transform.localScale = Vector3.one * 0.25f;
            sr.size = (size + new Vector2(0.12f, 0.12f)) * 4f;
            sr.transform.position = center + new Vector2(0.08f, -0.1f);
            sr.color = new Color(0, 0, 0, 0.18f);
            sr.sortingOrder = -1;
        }

        // ---- Flow ----

        private IEnumerator JudgeIntro()
        {
            var over = false;
            GameObject root = null;
            var card = Ui.Modal("JudgeIntro", new Vector2(1000, 560), null, out root);
            var col = Ui.Column(card, "Column", 24, 50);
            Ui.Label(col.transform, "ve.judge.picks", 40, Ui.InkSoft).Size(0, 60);
            var big = Ui.Text(col.transform, "", 70, Ui.Ink).Size(0, 200);
            big.gameObject.AddComponent<LocText>().SetFormat("ve.prompt.big", Loc.Get("ve.prompt." + prompt));
            Ui.Button(col.transform, "ve.start", () => { Screens.Close(root); over = true; }, Ui.Leaf, 50).Size(0, 120);
            while (!over) yield return null;
            active = true;
            aiHand.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!Running) return;
            if (active && Time.timeScale > 0f) HandleInput();
            UpdateAi(Time.deltaTime);
            UpdateSound();
            paper.Flush();
            aiPaper.Flush();
        }

        private void HandleInput()
        {
            var pointer = Pointer.current;
            var cam = Camera.main;
            if (pointer == null || cam == null || finishing) return;
            var screen = pointer.position.ReadValue();
            var world = (Vector2)cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
            var local = (world - (Vector2)paperView.transform.position) / paperSize + new Vector2(0.5f, 0.5f);
            var canvasPos = new Vector2(local.x * CanvasW, local.y * CanvasH);
            if (pointer.press.wasPressedThisFrame)
            {
                var es = EventSystem.current;
                if (es != null && es.IsPointerOverGameObject()) return;
                if (local.x < 0 || local.y < 0 || local.x > 1 || local.y > 1) return;
                drawing = true;
                paper.BeginStroke();
                lastCanvasPos = canvasPos;
                lastMoveTime = Time.time;
                Apply(canvasPos, canvasPos, 0f);
                return;
            }
            if (!drawing) return;
            if (!pointer.press.isPressed)
            {
                drawing = false;
                return;
            }
            var dist = Vector2.Distance(lastCanvasPos, canvasPos);
            if (dist < 0.5f) return;
            var speed = dist / Mathf.Max(Time.deltaTime, 1e-3f);
            Apply(lastCanvasPos, canvasPos, speed);
            lastCanvasPos = canvasPos;
            lastMoveTime = Time.time;
        }

        private void Apply(Vector2 from, Vector2 to, float speed)
        {
            // Slow strokes press harder (more wax), fast scribbles lighter: no pressure sensor (owner rule).
            var pressure = Mathf.Lerp(1.05f, 0.82f, Mathf.Clamp01(speed / 1600f));
            switch (tool)
            {
                case DrawTool.Pencil: paper.Pencil(from, to, pressure); break;
                case DrawTool.Eraser: paper.Erase(from, to); break;
                default: paper.Crayon(from, to, CrayonBox.Colors[colorIndex], pressure); break;
            }
            scratchSpeed = speed;
        }

        private float scratchSpeed;

        private void UpdateSound()
        {
            var moving = drawing && Time.time - lastMoveTime < 0.08f;
            var clip = tool == DrawTool.Pencil ? pencilSound : tool == DrawTool.Eraser ? eraserSound : crayonSound;
            if (scratch.clip != clip)
            {
                scratch.clip = clip;
                scratch.Play();
            }
            if (!scratch.isPlaying) scratch.Play();
            var target = moving ? Mathf.Clamp01(scratchSpeed / 900f) * 0.6f * Sfx.Volume : 0f;
            scratch.volume = Mathf.MoveTowards(scratch.volume, target, Time.deltaTime * 4f);
        }

        /// <summary>
        /// The AI child draws at a pace tied to the player's progress, but never finishes before the minimum time and
        /// never takes longer than the maximum (owner design §4).
        /// </summary>
        private void UpdateAi(float dt)
        {
            if (!active || aiTotal <= 0f) return;
            aiClock += dt;
            var limits = aiTime[(int)Difficulty];
            float target;
            if (finishing) target = 1f;
            else
            {
                var playerProgress = Mathf.Clamp01(paper.Coverage / expectedCoverage[(int)Difficulty]);
                target = Mathf.Clamp(playerProgress, aiClock / limits.y, aiClock / limits.x);
            }
            target = Mathf.Clamp01(target);
            var maxRate = (finishing ? 6f : 1.6f) / limits.x;
            var goal = Mathf.MoveTowards(aiDrawn / aiTotal, target, maxRate * dt) * aiTotal;
            while (aiDrawn < goal - 1e-5f && aiSegment < aiSegments.Count)
            {
                var seg = aiSegments[aiSegment];
                var segStart = aiDrawn;
                var segDone = SegmentProgress;
                var remaining = seg.length * (1f - segDone);
                var step = Mathf.Min(remaining, goal - aiDrawn);
                var from = Vector2.Lerp(seg.a, seg.b, segDone);
                var to = Vector2.Lerp(seg.a, seg.b, segDone + (seg.length > 0 ? step / seg.length : 1f));
                if (seg.stroke != aiLastStroke)
                {
                    aiPaper.BeginStroke();
                    aiLastStroke = seg.stroke;
                }
                var stroke = aiStrokes[seg.stroke];
                var pa = new Vector2(from.x * AiW, from.y * AiH);
                var pb = new Vector2(to.x * AiW, to.y * AiH);
                if (stroke.tool == DrawTool.Pencil) aiPaper.Pencil(pa, pb, 1f);
                else aiPaper.Crayon(pa, pb, stroke.color, 0.95f);
                aiDrawn = segStart + step;
                SegmentProgress = seg.length > 0 ? segDone + step / seg.length : 1f;
                if (SegmentProgress >= 0.999f)
                {
                    aiSegment++;
                    SegmentProgress = 0f;
                }
                aiHand.position = aiView.transform.TransformPoint(new Vector3((to.x - 0.5f) * aiSize.x, (to.y - 0.5f) * aiSize.y, 0)) + new Vector3(0, 0, -0.1f);
                aiHand.GetComponent<SpriteRenderer>().color = stroke.tool == DrawTool.Pencil ? new Color32(80, 80, 85, 255) : stroke.color;
            }
            aiStatus.GetComponent<LocText>().Key = AiProgress >= 0.999f ? "ve.ai.done" : "ve.ai.drawing";
        }

        private float SegmentProgress { get; set; }

        public void Done()
        {
            if (!active || finishing) return;
            if (paper.Coverage < minimumCoverage)
            {
                UiScreens.Notice("ve.more");
                return;
            }
            finishing = true;
            drawing = false;
            StartCoroutine(Judge());
        }

        private IEnumerator Judge()
        {
            doneButton.interactable = false;
            // Let the deskmate finish its last strokes (quickly).
            while (AiProgress < 0.999f) yield return null;
            aiHand.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.5f);

            // The offline judge's basic conditions (owner, 2026-10-08): every check must pass to win.
            var stats = paper.Analyze();
            var checks = DrawingJudge.Judge(prompt, Difficulty, stats);
            var won = DrawingJudge.Passed(checks);
            var over = false;
            GameObject root = null;
            var card = Ui.Modal("JudgeScores", new Vector2(1180, 300 + 84 * checks.Count), null, out root);
            var col = Ui.Column(card, "Column", 14, 44);
            Ui.Label(col.transform, "ve.judge.title", 56, Ui.Ink).Size(0, 84);
            foreach (var c in checks)
            {
                var row = Ui.Row(col.transform, "Check", 22);
                row.Size(0, 70);
                Ui.Icon(row.transform, "Mark", Ui.Circle, c.passed ? Ui.Leaf : Ui.Brick, 34);
                var line = Ui.Text(row.transform, "", 40, c.passed ? Ui.Ink : Ui.Brick, TextAlignmentOptions.Left);
                line.Size(880, 70);
                line.textWrappingMode = TextWrappingModes.NoWrap; // long colour lists shrink to one line
                line.enableAutoSizing = true;
                line.fontSizeMin = 24;
                line.fontSizeMax = 40;
                if (c.arg != null) line.gameObject.AddComponent<LocText>().SetFormat(c.key, c.arg);
                else line.gameObject.AddComponent<LocText>().Key = c.key;
            }
            Ui.Label(col.transform, won ? "ve.verdict.pass" : "ve.verdict.fail", 44, won ? Ui.Leaf : Ui.Brick).Size(0, 80);
            Ui.Button(col.transform, "ve.next", () => { Screens.Close(root); over = true; }, Ui.Leaf, 48).Size(0, 110);
            Debug.Log($"[CHILD-VE] {prompt}: marked {stats.marked:P1}, coloured {stats.coloured:P1}, spread {stats.width:P0}x{stats.height:P0}, " +
                      $"halves {stats.upperColour:P1}/{stats.lowerColour:P1}, colours {stats.ColoursUsed(0.002f)} -> {(won ? "WIN" : "LOSS")}");
            while (!over) yield return null;
            Finish(won ? Outcome.Win : Outcome.Loss);
        }

        // ---- Capture harness ----

        /// <summary>Draws a childlike version of the prompt on the player's sheet, a few strokes per frame.</summary>
        public IEnumerator DebugDrawPrompt(float seconds, float share = 1f)
        {
            var strokes = AiDrawing.Build(prompt, new System.Random(rng.Next()));
            strokes = strokes.GetRange(0, Mathf.Max(1, Mathf.RoundToInt(strokes.Count * share)));
            var segments = 0;
            foreach (var s in strokes) segments += s.points.Count - 1;
            var perFrame = Mathf.Max(1, Mathf.CeilToInt(segments / (seconds * 60f)));
            var n = 0;
            foreach (var s in strokes)
            {
                paper.BeginStroke();
                tool = s.tool;
                for (var i = 1; i < s.points.Count; i++)
                {
                    var a = new Vector2(s.points[i - 1].x * CanvasW, s.points[i - 1].y * CanvasH);
                    var b = new Vector2(s.points[i].x * CanvasW, s.points[i].y * CanvasH);
                    if (s.tool == DrawTool.Pencil) paper.Pencil(a, b, 1f);
                    else paper.Crayon(a, b, s.color, 0.95f);
                    if (++n % perFrame == 0) yield return null;
                }
            }
            tool = DrawTool.Crayon;
        }

        public void DebugStart()
        {
            if (Find("btn.ve.start") is Button b) b.onClick.Invoke();
        }

        private static Object Find(string name)
        {
            foreach (var t in FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
                if (t.name == name) return t.GetComponent<Button>();
            return null;
        }

        // ---- HUD: prompt card, crayon box, tools ----

        private void BuildHud()
        {
            var safe = Ui.Canvas("DrawHud", 8);
            var promptCard = Ui.Panel(safe, "Prompt", new Color(1, 1, 1, 0.92f)).Place(new Vector2(0f, 1f), new Vector2(860, 90), new Vector2(240, -24));
            var promptText = Ui.Text(promptCard.transform, "", 40, Ui.Ink);
            promptText.Stretch(Vector2.zero, Vector2.one, 8);
            promptText.gameObject.AddComponent<LocText>().SetFormat("ve.prompt.line", Loc.Get("ve.prompt." + prompt));

            aiStatus = Ui.Text(safe, "", 30, Ui.Ink);
            aiStatus.Place(new Vector2(1f, 1f), new Vector2(560, 50), new Vector2(-40, -380));
            aiStatus.gameObject.AddComponent<LocText>().Key = "ve.ai.drawing";

            var box = Ui.Panel(safe, "CrayonBox", Color.white).Place(new Vector2(1f, 0f), new Vector2(660, 590), new Vector2(-24, 24));
            box.sprite = SceneArt.CrayonBox(); // the open cardboard box of crayons (generated art)
            box.type = Image.Type.Simple;
            for (var i = 0; i < CrayonBox.Colors.Length; i++)
            {
                var index = i;
                var b = CrayonStick(box.transform, CrayonBox.Colors[i], () => SelectCrayon(index));
                ((RectTransform)b.transform).Place(new Vector2(0f, 1f), new Vector2(86, 170), new Vector2(26 + (i % 6) * 104, -22 - (i / 6) * 186));
                b.name = "btn.crayon." + CrayonBox.Keys[i];
                crayonButtons.Add(b);
            }
            pencilButton = Ui.Button(box.transform, "ve.tool.pencil", () => SelectTool(DrawTool.Pencil), Ui.InkSoft, 36);
            pencilButton.Place(new Vector2(0f, 0f), new Vector2(290, 90), new Vector2(26, 130));
            eraserButton = Ui.Button(box.transform, "ve.tool.eraser", () => SelectTool(DrawTool.Eraser), Ui.InkSoft, 36);
            eraserButton.Place(new Vector2(1f, 0f), new Vector2(290, 90), new Vector2(-26, 130));
            doneButton = Ui.Button(box.transform, "ve.done", Done, Ui.Leaf, 46);
            doneButton.Place(new Vector2(0.5f, 0f), new Vector2(608, 96), new Vector2(0, 20));
            RefreshTools();
        }

        private void SelectCrayon(int index)
        {
            colorIndex = index;
            SelectTool(DrawTool.Crayon);
        }

        private void SelectTool(DrawTool t)
        {
            tool = t;
            if (drawing) paper.BeginStroke();
            RefreshTools();
        }

        private void RefreshTools()
        {
            for (var i = 0; i < crayonButtons.Count; i++)
            {
                var picked = tool == DrawTool.Crayon && i == colorIndex;
                var rt = (RectTransform)crayonButtons[i].transform;
                // The picked crayon is lifted out of the box.
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -22 - (i / 6) * 186 + (picked ? 16 : 0));
                crayonButtons[i].transform.localScale = Vector3.one * (picked ? 1.08f : 1f);
            }
            ((Image)pencilButton.targetGraphic).color = tool == DrawTool.Pencil ? Ui.Sky : Ui.InkSoft;
            ((Image)eraserButton.targetGraphic).color = tool == DrawTool.Eraser ? Ui.Brick : Ui.InkSoft;
        }

        private static Sprite crayonSprite, wrapperSprite;

        /// <summary>A wax crayon drawn in code: rounded body, cone tip, paper wrapper band.</summary>
        private static Button CrayonStick(Transform parent, Color color, System.Action onClick)
        {
            crayonSprite ??= MakeCrayonSprite(false);
            wrapperSprite ??= MakeCrayonSprite(true);
            var rt = Ui.Rect(parent, "Crayon");
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = crayonSprite;
            img.color = color;
            var wrapper = Ui.Rect(rt, "Wrapper").gameObject.AddComponent<Image>();
            wrapper.sprite = wrapperSprite;
            wrapper.color = new Color(1, 1, 1, 0.85f);
            wrapper.raycastTarget = false;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick());
            if (color.r > 0.95f && color.g > 0.95f) rt.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.35f);
            return b;
        }

        private static Sprite MakeCrayonSprite(bool wrapper)
        {
            const int w = 64, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var u = (x + 0.5f) / w;
                var v = (y + 0.5f) / h;
                var halfWidth = v < 0.78f ? 0.36f : Mathf.Lerp(0.36f, 0.1f, (v - 0.78f) / 0.2f); // body, then the tip cone
                var inside = Mathf.Abs(u - 0.5f) < halfWidth && v > 0.02f && v < 0.98f;
                byte a = 0;
                if (inside)
                {
                    if (!wrapper) a = 255;
                    else if (v > 0.12f && v < 0.66f) a = 255; // paper wrapper band
                }
                var shade = (byte)(wrapper ? 255 : 255 - (int)(Mathf.Abs(u - 0.42f) * 120)); // simple round shading
                if (wrapper && (Mathf.Abs(v - 0.2f) < 0.012f || Mathf.Abs(v - 0.58f) < 0.012f)) shade = 150;
                px[y * w + x] = new Color32(shade, shade, shade, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100);
        }
    }
}
