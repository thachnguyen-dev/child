using System.Collections.Generic;

namespace ChildGame.MiniGames.VeTranh
{
    /// <summary>What <see cref="CrayonCanvas.Analyze"/> measured on a sheet. Shares are of the whole sheet (0..1).</summary>
    public struct DrawingStats
    {
        public float marked, coloured, pencil;
        /// <summary>Coloured share of each crayon colour (index as <see cref="CrayonBox.Keys"/>).</summary>
        public float[] colour;
        /// <summary>How far the drawing spreads across and up the sheet.</summary>
        public float width, height;
        /// <summary>Coloured share of the upper and lower half of the sheet.</summary>
        public float upperColour, lowerColour;

        public int ColoursUsed(float minimum)
        {
            var n = 0;
            if (colour != null)
                foreach (var c in colour)
                    if (c >= minimum)
                        n++;
            return n;
        }
    }

    /// <summary>
    /// The offline judge's basic win conditions (owner, 2026-10-08: a drawing must really be drawn to win). Every
    /// check must pass:
    /// <list type="bullet">
    /// <item><b>Big enough</b>: the drawing spreads over a fair part of the sheet (scenes fill it).</item>
    /// <item><b>Drawn enough</b>: enough of the sheet carries pencil or crayon.</item>
    /// <item><b>Coloured in</b>: enough of the sheet is coloured with crayon (not for line subjects like the bicycle).</item>
    /// <item><b>Enough colours</b>: at least the number of colours the subject needs.</item>
    /// <item><b>Right colours</b>: each colour the subject needs shows (a tree needs green and brown).</item>
    /// <item><b>Whole scene</b> (Hard): colour both up top (sky) and down below (ground, water).</item>
    /// </list>
    /// ⚠ All thresholds and colour lists are placeholders.
    /// </summary>
    public static class DrawingJudge
    {
        public struct Check
        {
            public string key;
            /// <summary>Filled into the check's text (the colours needed), or null.</summary>
            public string arg;
            public bool passed;
        }

        private class Subject
        {
            public int colours;
            public string[][] needs;
            public float needShare;
            /// <summary>Marked share override for sparse line subjects (0 = the difficulty default).</summary>
            public float marked;
        }

        // Size: [difficulty] = (larger side, smaller side) the drawing must span.
        private static readonly float[,] Spread = { { 0.3f, 0f }, { 0.4f, 0.15f }, { 0.7f, 0.5f } };
        // Coloured share of the sheet per difficulty.
        private static readonly float[] ColouredShare = { 0.03f, 0.03f, 0.1f };
        // Marked share (pencil and crayon) per difficulty.
        private static readonly float[] MarkedShare = { 0.04f, 0.04f, 0.12f };
        // A colour counts as used when it covers this share of the sheet.
        private const float ColourUsed = 0.002f;
        // Hard: each half of the sheet must be at least this coloured.
        private const float HalfShare = 0.06f;

        private static readonly Dictionary<string, Subject> Subjects = new Dictionary<string, Subject>
        {
            ["fish"] = new Subject { colours = 1 },
            ["tree"] = new Subject { colours = 2, needs = new[] { new[] { "green", "lime" }, new[] { "brown" } } },
            ["sun"] = new Subject { colours = 1, needs = new[] { new[] { "yellow", "orange", "red" } } },
            ["flower"] = new Subject { colours = 2, needs = new[] { new[] { "green", "lime" } } },
            ["butterfly"] = new Subject { colours = 2 },
            ["house"] = new Subject { colours = 2 },
            ["car"] = new Subject { colours = 2 },
            ["boat"] = new Subject { colours = 2, needs = new[] { new[] { "sky", "blue" } } },
            ["person"] = new Subject { colours = 2 },
            ["bicycle"] = new Subject { colours = 0, marked = 0.015f }, // a line drawing: no colouring needed
            ["riverhouse"] = new Subject { colours = 3, needs = new[] { new[] { "sky", "blue" }, new[] { "green", "lime" } } },
            ["ricefield"] = new Subject { colours = 3, needs = new[] { new[] { "green", "lime", "yellow" }, new[] { "sky", "blue" } } },
            ["sea"] = new Subject { colours = 3, needs = new[] { new[] { "sky", "blue" } }, needShare = 0.05f },
        };

        public static List<Check> Judge(string prompt, Difficulty difficulty, DrawingStats s)
        {
            var d = (int)difficulty;
            Subjects.TryGetValue(prompt, out var subject);
            subject ??= new Subject { colours = 1 };
            var checks = new List<Check>();
            var big = System.Math.Max(s.width, s.height);
            var small = System.Math.Min(s.width, s.height);
            checks.Add(new Check { key = "ve.check.size", passed = big >= Spread[d, 0] && small >= Spread[d, 1] });
            checks.Add(new Check { key = "ve.check.drawn", passed = s.marked >= (subject.marked > 0 ? subject.marked : MarkedShare[d]) });
            if (subject.colours > 0)
                checks.Add(new Check { key = "ve.check.colour", passed = s.coloured >= ColouredShare[d] });
            if (subject.colours > 1)
                checks.Add(new Check { key = "ve.check.count", arg = subject.colours.ToString(), passed = s.ColoursUsed(ColourUsed) >= subject.colours });
            if (subject.needs != null)
            {
                var ok = true;
                var names = new List<string>();
                foreach (var group in subject.needs)
                {
                    var share = 0f;
                    foreach (var key in group) share += s.colour[System.Array.IndexOf(CrayonBox.Keys, key)];
                    if (share < System.Math.Max(ColourUsed, subject.needShare)) ok = false;
                    var alternatives = new List<string>();
                    foreach (var key in group) alternatives.Add(Loc.Get("ve.colour." + key));
                    names.Add(string.Join("/", alternatives));
                }
                checks.Add(new Check { key = "ve.check.match", arg = string.Join(", ", names), passed = ok });
            }
            if (difficulty == Difficulty.Hard)
                checks.Add(new Check { key = "ve.check.scene", passed = s.upperColour >= HalfShare && s.lowerColour >= HalfShare });
            return checks;
        }

        public static bool Passed(List<Check> checks)
        {
            foreach (var c in checks)
                if (!c.passed)
                    return false;
            return true;
        }
    }
}
