using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ChildGame.Tests
{
    public class StoryBookTests
    {
        [Test]
        public void EachPartPlaysOnce()
        {
            var r = new MiniGameRecord { id = "dieu" };
            var win = new[] { "a.win.1", "a.end" };
            var lose = new[] { "a.lose.1", "a.end" };
            Assert.AreEqual(2, StoryBook.Once(r, win).Count, "first win plays");
            Assert.AreEqual(0, StoryBook.Once(r, win).Count, "second win is silent");
            Assert.AreEqual(2, StoryBook.Once(r, lose).Count, "a different part still plays once, even sharing a line");
            Assert.AreEqual(0, StoryBook.Once(r, null).Count);
        }

        [Test]
        public void PartsListWhatWasSeen()
        {
            var def = ScriptableObject.CreateInstance<MiniGameDefinition>();
            def.storyKeys = new[] { "x.story.1" };
            def.winKeys = new[] { "x.win.1" };
            def.loseKeys = new[] { "x.lose.1" };
            def.extraStories = new List<StorySegment> { new StorySegment { titleKey = "t", keys = new[] { "x.talk.1" }, seenFlag = "talk" } };
            var r = new MiniGameRecord { id = "x" };
            Assert.IsFalse(StoryBook.AnySeen(def, r));
            r.storySeen = true;
            StoryBook.Once(r, def.winKeys);
            r.SetFlag("talk");
            var seen = StoryBook.Parts(def, r).FindAll(p => p.seen).ConvertAll(p => p.titleKey);
            CollectionAssert.AreEqual(new[] { "story.part.intro", "t", "story.part.win" }, seen);
            Object.DestroyImmediate(def);
        }
    }
}
