using UnityEngine;

namespace ChildGame.UI
{
    /// <summary>
    /// Drawing sounds generated in code (official, owner 2026-10-09: simple SFX made by me): filtered-noise loops for drawing
    /// (pencil on paper, wax crayon, eraser rub).
    /// </summary>
    public static class SoundSynth
    {
        private const int Rate = 22050;

        /// <summary>Pencil: bright, gritty scratching (high-passed noise with grain clicks).</summary>
        public static AudioClip Pencil() => Noise("PencilScratch", 0.85f, 0.25f, 0.35f, 7);

        /// <summary>Wax crayon: duller, waxier drag with more low body.</summary>
        public static AudioClip Crayon() => Noise("CrayonDrag", 0.55f, 0.6f, 0.2f, 11);

        /// <summary>Eraser: soft rubbing.</summary>
        public static AudioClip Eraser() => Noise("EraserRub", 0.25f, 0.85f, 0.05f, 13);

        /// <param name="brightness">0..1: how much high frequency is kept.</param>
        /// <param name="body">0..1: amount of low-passed rumble mixed in.</param>
        /// <param name="grain">0..1: density of tiny clicks (paper tooth).</param>
        private static AudioClip Noise(string name, float brightness, float body, float grain, int seed)
        {
            var rng = new System.Random(seed);
            var n = Rate; // one second, looped
            var data = new float[n];
            float lp = 0, lp2 = 0, prev = 0;
            for (var i = 0; i < n; i++)
            {
                var white = (float)rng.NextDouble() * 2f - 1f;
                lp += (white - lp) * 0.08f;            // low band
                lp2 += (lp - lp2) * 0.3f;
                var high = white - prev * 0.95f;       // crude high-pass
                prev = white;
                var click = rng.NextDouble() < grain * 0.004 ? (float)(rng.NextDouble() * 2 - 1) * 2.5f : 0f;
                // Slow wobble, like the hand's motion changing.
                var wobble = 0.75f + 0.25f * Mathf.Sin(i / (float)Rate * Mathf.PI * 2f * 3f);
                data[i] = (high * brightness * 0.35f + lp2 * body * 1.6f + click) * wobble;
            }
            // Fade the loop seam.
            const int fade = 400;
            for (var i = 0; i < fade; i++)
            {
                var t = i / (float)fade;
                data[i] *= t;
                data[n - 1 - i] *= t;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
