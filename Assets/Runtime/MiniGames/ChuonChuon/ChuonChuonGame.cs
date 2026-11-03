using System.Collections;
using ChildGame.UI;
using UnityEngine;

namespace ChildGame.MiniGames.ChuonChuon
{
    /// <summary>
    /// MG07 — Bắt chuồn chuồn + tập bơi. First time (owner design §2):
    /// go swimming → first attempt fails → catch dragonfly 1 → it bites the stomach → swimming still fails → catch
    /// dragonfly 2 → it bites the navel → swimming still fails → anh Tùng teaches them → swimming is unlocked → the race
    /// against three friends.
    /// Once swimming is unlocked, every replay goes straight to the race (owner 2026-10-07).
    /// Story: the owner's (2026-10-08); the swimming is set on a village river.
    /// </summary>
    public class ChuonChuonGame : MiniGame
    {
        public const string SwimFlag = "swim";

        /// <summary>The whole learning-to-swim story in order (for watching again; the shout and the notice included).</summary>
        public static string[] LearnStory
        {
            get
            {
                var all = new System.Collections.Generic.List<string>();
                all.AddRange(Keys("s", 1, 3));
                all.Add("mg.chuon.help");
                all.AddRange(Keys("s", 4, 9));
                all.AddRange(Keys("t", 1, 4));
                all.AddRange(Keys("b", 1, 8));
                all.AddRange(Keys("n", 1, 10));
                all.Add("mg.chuon.canswim");
                return all.ToArray();
            }
        }

        public DragonflyCatch Catch { get; private set; }
        public SwimRace Race { get; private set; }

        private string GameId => FindAnyObjectByType<MiniGameSession>()?.Definition?.id ?? "chuon";

        protected override void OnBegin()
        {
            var record = Game.Save.Record(GameId);
            StartCoroutine(record.HasFlag(SwimFlag) ? RaceOnly() : FullSequence(record));
        }

        /// <summary>The owner's story (2026-10-08), around three dives, two dragonfly hunts and anh Tùng's lesson.</summary>
        private IEnumerator FullSequence(MiniGameRecord record)
        {
            yield return Story(Keys("s", 1, 3));
            yield return Struggle("mg.chuon.help");
            yield return Story(Keys("s", 4, 9));
            yield return Story(Keys("t", 1, 4));
            yield return CatchOne();                   // bitten on the tummy
            yield return Story(Keys("b", 1, 4));
            yield return Struggle(null);
            yield return Story(Keys("b", 5, 8));
            yield return CatchOne();                   // bitten on the navel
            yield return Story(Keys("n", 1, 5));
            yield return Struggle(null);
            yield return Story(Keys("n", 6, 10));
            var noticed = false;
            UiScreens.Notice("mg.chuon.canswim", () => noticed = true);
            while (!noticed) yield return null;
            record.SetFlag(SwimFlag);
            Game.Commit();
            yield return RaceOnly();
        }

        private static string[] Keys(string part, int from, int to)
        {
            var keys = new string[to - from + 1];
            for (var i = from; i <= to; i++) keys[i - from] = $"mg.chuon.{part}.{i}";
            return keys;
        }

        private IEnumerator RaceOnly()
        {
            var finished = false;
            Race = SwimRace.Race(Difficulty, outcome =>
            {
                finished = true;
                Finish(outcome);
            });
            while (!finished) yield return null;
        }

        private IEnumerator Story(params string[] keys)
        {
            var over = false;
            UiScreens.Dialogue(keys, () => over = true);
            while (!over) yield return null;
        }

        private IEnumerator Struggle(string sayKey)
        {
            var over = false;
            Race = SwimRace.Struggle(() => over = true, sayKey);
            while (!over) yield return null;
            Race = null;
        }

        private IEnumerator CatchOne()
        {
            var over = false;
            Catch = DragonflyCatch.Begin(() => over = true);
            while (!over) yield return null;
            Catch = null;
        }
    }
}
