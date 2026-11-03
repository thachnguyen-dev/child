using ChildGame.MiniGames.VeTranh;
using NUnit.Framework;
using UnityEngine;

namespace ChildGame.Tests
{
    public class DrawingJudgeTests
    {
        private const int W = 1024, H = 640;

        private static CrayonCanvas Sheet() => new CrayonCanvas(W, H, 7);

        /// <summary>Draws the deskmate's childlike drawing of a subject, as the capture harness does.</summary>
        private static CrayonCanvas Reference(string prompt, int seed)
        {
            var paper = Sheet();
            foreach (var s in AiDrawing.Build(prompt, new System.Random(seed)))
            {
                paper.BeginStroke();
                for (var i = 1; i < s.points.Count; i++)
                {
                    var a = new Vector2(s.points[i - 1].x * W, s.points[i - 1].y * H);
                    var b = new Vector2(s.points[i].x * W, s.points[i].y * H);
                    if (s.tool == DrawTool.Pencil) paper.Pencil(a, b, 1f);
                    else paper.Crayon(a, b, s.color, 0.95f);
                }
            }
            return paper;
        }

        /// <summary>Back-and-forth crayon scribble filling a box (canvas fractions).</summary>
        private static void Scribble(CrayonCanvas paper, string colour, Rect box)
        {
            paper.BeginStroke();
            var c = CrayonBox.Get(colour);
            for (var y = box.yMin; y < box.yMax; y += 0.012f)
                paper.Crayon(new Vector2(box.xMin * W, y * H), new Vector2(box.xMax * W, (y + 0.006f) * H), c, 1f);
        }

        [Test]
        public void EveryReferenceDrawingPasses()
        {
            var anyFailed = "";
            for (var d = 0; d < AiDrawing.Prompts.Length; d++)
            foreach (var prompt in AiDrawing.Prompts[d])
                for (var seed = 1; seed <= 3; seed++)
                {
                    var stats = Reference(prompt, seed).Analyze();
                    var checks = DrawingJudge.Judge(prompt, (Difficulty)d, stats);
                    var failed = "";
                    foreach (var c in checks)
                        if (!c.passed)
                            failed += " " + c.key;
                    Debug.Log($"[VE-JUDGE] {prompt}#{seed}: {string.Join(",", System.Array.ConvertAll(stats.colour, x => x.ToString("P1")))} marked {stats.marked:P1} coloured {stats.coloured:P1}, spread {stats.width:P0}x{stats.height:P0}, " +
                              $"halves {stats.upperColour:P1}/{stats.lowerColour:P1}, colours {stats.ColoursUsed(0.002f)}{(failed == "" ? "" : " FAILED" + failed)}");
                    if (failed != "") anyFailed += $" {prompt}#{seed}:{failed}";
                }
            Assert.IsEmpty(anyFailed, "reference drawings that failed:" + anyFailed);
        }

        [Test]
        public void BlankSheetFails()
        {
            for (var d = 0; d < 3; d++)
                Assert.IsFalse(DrawingJudge.Passed(DrawingJudge.Judge("sun", (Difficulty)d, Sheet().Analyze())));
        }

        [Test]
        public void TinyScribbleFails()
        {
            var paper = Sheet();
            Scribble(paper, "yellow", new Rect(0.45f, 0.45f, 0.06f, 0.06f));
            Assert.IsFalse(DrawingJudge.Passed(DrawingJudge.Judge("sun", Difficulty.Easy, paper.Analyze())));
        }

        [Test]
        public void WrongColoursFail()
        {
            var paper = Sheet();
            Scribble(paper, "purple", new Rect(0.3f, 0.2f, 0.4f, 0.6f));
            var checks = DrawingJudge.Judge("tree", Difficulty.Easy, paper.Analyze());
            Assert.IsFalse(DrawingJudge.Passed(checks), "a purple blob is not a tree");
        }

        [Test]
        public void ColouredSunPasses()
        {
            var paper = Sheet();
            Scribble(paper, "yellow", new Rect(0.36f, 0.22f, 0.3f, 0.5f));
            var s = paper.Analyze();
            Debug.Log($"[VE-JUDGE] scribble sun: marked {s.marked:P1} coloured {s.coloured:P1} spread {s.width:P0}x{s.height:P0} yellow {s.colour[2]:P2}");
            Assert.IsTrue(DrawingJudge.Passed(DrawingJudge.Judge("sun", Difficulty.Easy, s)));
        }

        [Test]
        public void SceneNeedsTopAndBottom()
        {
            var paper = Sheet();
            Scribble(paper, "blue", new Rect(0.05f, 0.05f, 0.9f, 0.4f));
            Scribble(paper, "green", new Rect(0.05f, 0.1f, 0.9f, 0.3f));
            Scribble(paper, "brown", new Rect(0.3f, 0.2f, 0.2f, 0.2f));
            var checks = DrawingJudge.Judge("riverhouse", Difficulty.Hard, paper.Analyze());
            Assert.IsFalse(DrawingJudge.Passed(checks), "nothing in the upper half");
        }
    }
}
