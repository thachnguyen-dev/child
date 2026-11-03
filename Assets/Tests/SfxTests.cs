using NUnit.Framework;
using UnityEngine;

namespace ChildGame.Tests
{
    public class SfxTests
    {
        /// <summary>Every sound is audible, never clips, and is short (these are effects, not music).</summary>
        [Test]
        public void EverySoundIsAudibleAndDoesNotClip()
        {
            foreach (Snd s in System.Enum.GetValues(typeof(Snd)))
            {
                var clip = Sfx.Clip(s);
                var data = new float[clip.samples];
                clip.GetData(data, 0);
                var peak = 0f;
                foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
                Assert.That(peak, Is.InRange(0.1f, 0.99f), s + " peak");
                Assert.That(clip.length, Is.LessThan(1.5f), s + " length");
            }
        }
    }
}
