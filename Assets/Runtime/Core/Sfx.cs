using System.Collections.Generic;
using UnityEngine;

namespace ChildGame
{
    /// <summary>The game's sound effects. There is no background music (owner 2026-10-09).</summary>
    public enum Snd
    {
        Tap,     // any button
        Page,    // story / instructions page turn
        Coin,    // money earned or spent
        Denied,  // can't do that
        Star,    // one result star (pitched up per star)
        Win,
        Lose,
        Whoosh,  // a throw
        Hit,     // something hit: a mango, a sandal on a head, a mo cau on a rock
        Clack,   // stones (Ô ăn quan, picking up a stone)
        Splash,  // a swim stroke, a fall into water
        Pen,     // a pen mark on paper (Caro)
        Pluck,   // a hair plucked
    }

    /// <summary>
    /// Simple sound effects, synthesised in code on first use (owner 2026-10-09: "simple SFX"; made by me, so there are no
    /// licences to track). Played through a small pool of sources at the player's sound volume, with a little pitch
    /// variation so repeated sounds don't feel mechanical.
    /// </summary>
    public static class Sfx
    {
        private const int Rate = 32000;
        private static readonly Dictionary<Snd, AudioClip> clips = new Dictionary<Snd, AudioClip>();
        private static readonly List<AudioSource> pool = new List<AudioSource>();
        private static int next;

        /// <summary>The player's sound volume, or 0 when the game portal has muted the game (CrazyGames rule).</summary>
        public static float Volume => Game.Platform.Muted ? 0f : Game.Save.sfxVolume;

        public static void Play(Snd s, float volume = 1f, float pitch = 1f, float jitter = 0.05f)
        {
            var runner = GameRunner.Instance;
            if (runner == null || Volume <= 0f) return;
            if (pool.Count == 0 || pool[0] == null)
            {
                pool.Clear();
                for (var i = 0; i < 6; i++)
                {
                    var src = runner.gameObject.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    pool.Add(src);
                }
            }
            var a = pool[next];
            next = (next + 1) % pool.Count;
            a.clip = Clip(s);
            a.volume = volume * Volume;
            a.pitch = pitch * (1f + Random.Range(-jitter, jitter));
            a.Play();
        }

        public static AudioClip Clip(Snd s)
        {
            if (clips.TryGetValue(s, out var c) && c != null) return c;
            var data = Make(s);
            c = AudioClip.Create("Sfx_" + s, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            clips[s] = c;
            return c;
        }

        // ---- Synthesis ----

        private static float[] Make(Snd s)
        {
            switch (s)
            {
                case Snd.Tap: return Knock(0.07f, 900f, new[] { 1f, 2.6f, 4.2f }, 45f, 0.6f);
                case Snd.Page: return Swish(0.14f, 2200f, 4800f, 0.35f);
                case Snd.Coin:
                {
                    var b = Buffer(0.55f);
                    Bell(b, 0f, 1760f, 7f, 0.35f);
                    Bell(b, 0.07f, 2350f, 6f, 0.4f);
                    return b;
                }
                case Snd.Denied:
                {
                    var b = Buffer(0.3f);
                    Add(b, 0f, Knock(0.12f, 230f, new[] { 1f, 2.3f }, 30f, 0.7f));
                    Add(b, 0.13f, Knock(0.12f, 200f, new[] { 1f, 2.3f }, 30f, 0.7f));
                    return b;
                }
                case Snd.Star:
                {
                    var b = Buffer(0.8f);
                    Bell(b, 0f, 1568f, 4.5f, 0.45f);
                    return b;
                }
                case Snd.Win:
                {
                    // A rising marimba arpeggio: C E G C.
                    var b = Buffer(1.3f);
                    var notes = new[] { 523.3f, 659.3f, 784f, 1046.5f };
                    for (var i = 0; i < notes.Length; i++) Marimba(b, i * 0.1f, notes[i], i == notes.Length - 1 ? 3f : 6f, 0.4f);
                    return b;
                }
                case Snd.Lose:
                {
                    var b = Buffer(1.1f);
                    Marimba(b, 0f, 392f, 6f, 0.4f);
                    Marimba(b, 0.2f, 329.6f, 6f, 0.4f);
                    Marimba(b, 0.4f, 261.6f, 3.5f, 0.45f);
                    return b;
                }
                case Snd.Whoosh: return Swish(0.26f, 500f, 1700f, 0.5f);
                case Snd.Hit:
                {
                    // A dull thunk: a low tone falling in pitch, with a soft noise burst.
                    var b = Buffer(0.18f);
                    var rng = new System.Random(3);
                    float ph = 0, lp = 0;
                    for (var i = 0; i < b.Length; i++)
                    {
                        var t = i / (float)Rate;
                        ph += 2f * Mathf.PI * Mathf.Lerp(260f, 90f, Mathf.Clamp01(t / 0.12f)) / Rate;
                        lp += ((float)rng.NextDouble() * 2f - 1f - lp) * 0.15f;
                        b[i] = (Mathf.Sin(ph) * 0.8f + lp * 1.2f * Mathf.Exp(-t * 60f)) * Mathf.Exp(-t * 22f);
                    }
                    return b;
                }
                case Snd.Clack: return Knock(0.08f, 2600f, new[] { 1f, 1.5f, 2.1f }, 70f, 0.45f);
                case Snd.Splash:
                {
                    // Noise with a quick attack and a bubbly, low-passed tail.
                    var b = Buffer(0.35f);
                    var rng = new System.Random(9);
                    float lp = 0, bp = 0, ph = 0;
                    for (var i = 0; i < b.Length; i++)
                    {
                        var t = i / (float)Rate;
                        var w = (float)rng.NextDouble() * 2f - 1f;
                        lp += (w - lp) * 0.25f;
                        bp += (lp - bp) * 0.5f;
                        ph += 2f * Mathf.PI * (500f + 900f * Mathf.Exp(-t * 12f)) / Rate;
                        var env = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 11f);
                        b[i] = (bp * 1.4f + Mathf.Sin(ph) * 0.12f * Mathf.Exp(-t * 20f)) * env;
                    }
                    return b;
                }
                case Snd.Pen:
                {
                    // A short scratch: high-passed noise with grain.
                    var b = Buffer(0.12f);
                    var rng = new System.Random(5);
                    float prev = 0;
                    for (var i = 0; i < b.Length; i++)
                    {
                        var t = i / (float)Rate;
                        var w = (float)rng.NextDouble() * 2f - 1f;
                        var hp = w - prev * 0.9f;
                        prev = w;
                        var grain = rng.NextDouble() < 0.01 ? 1.5f : 1f;
                        b[i] = hp * 0.3f * grain * Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 18f);
                    }
                    return b;
                }
                case Snd.Pluck:
                {
                    var b = Buffer(0.06f);
                    for (var i = 0; i < b.Length; i++)
                    {
                        var t = i / (float)Rate;
                        b[i] = (Mathf.Sin(2f * Mathf.PI * 3200f * t) * 0.45f + (i < 40 ? 0.4f : 0f)) * Mathf.Exp(-t * 90f);
                    }
                    return b;
                }
            }
            return Buffer(0.01f);
        }

        private static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        private static void Add(float[] into, float at, float[] src)
        {
            var o = Mathf.RoundToInt(at * Rate);
            for (var i = 0; i < src.Length && o + i < into.Length; i++) into[o + i] += src[i];
        }

        /// <summary>A struck wooden or stone object: decaying partials plus a click.</summary>
        private static float[] Knock(float seconds, float f, float[] ratios, float decay, float gain)
        {
            var b = Buffer(seconds);
            for (var i = 0; i < b.Length; i++)
            {
                var t = i / (float)Rate;
                var v = 0f;
                for (var k = 0; k < ratios.Length; k++) v += Mathf.Sin(2f * Mathf.PI * f * ratios[k] * t) * Mathf.Exp(-t * decay * (1f + k * 0.8f)) / (k + 1);
                b[i] = v * gain * Mathf.Min(1f, t / 0.0015f);
            }
            return b;
        }

        /// <summary>Air or paper: band-passed noise sweeping from <paramref name="f0"/> to <paramref name="f1"/>.</summary>
        private static float[] Swish(float seconds, float f0, float f1, float gain)
        {
            var b = Buffer(seconds);
            var rng = new System.Random(17);
            float low = 0, band = 0;
            for (var i = 0; i < b.Length; i++)
            {
                var u = i / (float)b.Length;
                var f = 2f * Mathf.Sin(Mathf.PI * Mathf.Lerp(f0, f1, Mathf.Sin(u * Mathf.PI * 0.5f)) / Rate);
                var w = (float)rng.NextDouble() * 2f - 1f;
                low += f * band;
                var high = w - low - 0.7f * band;
                band += f * high;
                b[i] = band * gain * Mathf.Sin(u * Mathf.PI);
            }
            return b;
        }

        private static void Bell(float[] b, float at, float f, float decay, float gain)
        {
            var ratios = new[] { 1f, 2.76f, 5.4f };
            var o = Mathf.RoundToInt(at * Rate);
            for (var i = o; i < b.Length; i++)
            {
                var t = (i - o) / (float)Rate;
                var v = 0f;
                for (var k = 0; k < ratios.Length; k++) v += Mathf.Sin(2f * Mathf.PI * f * ratios[k] * t) * Mathf.Exp(-t * decay * (1f + k * 1.5f)) / (1f + k * 1.6f);
                b[i] += v * gain * Mathf.Min(1f, t / 0.002f);
            }
        }

        private static void Marimba(float[] b, float at, float f, float decay, float gain)
        {
            var o = Mathf.RoundToInt(at * Rate);
            for (var i = o; i < b.Length; i++)
            {
                var t = (i - o) / (float)Rate;
                var v = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * decay)
                        + Mathf.Sin(2f * Mathf.PI * f * 3.93f * t) * Mathf.Exp(-t * decay * 4f) * 0.3f;
                b[i] += v * gain * Mathf.Min(1f, t / 0.003f);
            }
        }
    }
}
