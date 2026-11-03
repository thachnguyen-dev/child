using System;
using System.Collections;
using System.IO;
using ChildGame.Hub;
using ChildGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChildGame.Dev
{
    /// <summary>
    /// Development harness: <c>-child-capture &lt;dir&gt;</c> plays through the whole flow on a throwaway save, saving a
    /// screenshot at every step, then quits. Logs <c>[CHILD-CAPTURE]</c> lines.
    /// </summary>
    public class Capture : MonoBehaviour
    {
        public static bool Enabled { get; private set; }
        /// <summary>The test-only mini game is shown only in the flow scenario, which plays it.</summary>
        public static bool ShowsTestGame => Enabled && scenario == "flow";
        private static string folder;
        private static string scenario = "flow";
        private static string difficulty = "easy";
        // Video clips for the store (tools/record-video.sh): from the shot named videoStart, every frame for videoSeconds
        // of game time at a fixed 30 fps (Time.captureFramerate), at normal speed.
        private static string videoStart;
        private static float videoSeconds = 4f;
        private static Capture instance;
        private static bool recording;
        private static string captureLang;
        /// <summary>Screenshots at this multiple of the window size (store images larger than the screen).</summary>
        private static int superSize = 1;
        /// <summary>Screenshots without any interface (store header art).</summary>
        private static bool noUi;

        public static void ReadCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-child-capture") continue;
                Enabled = true;
                folder = args[i + 1];
                SaveStore.FileName = "capture-save.json";
                SaveStore.Delete();
            }
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-child-capture-scenario") scenario = args[i + 1];
                if (args[i] == "-child-capture-difficulty") difficulty = args[i + 1];
                if (args[i] == "-child-video-start") videoStart = args[i + 1];
                if (args[i] == "-child-capture-lang") captureLang = args[i + 1];
                if (args[i] == "-child-capture-supersize") superSize = int.Parse(args[i + 1]);
                if (args[i] == "-child-capture-noui") noUi = args[i + 1] == "1";
                if (args[i] == "-child-video-seconds") videoSeconds = float.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private IEnumerator Start()
        {
            instance = this;
            if (captureLang == "en") Game.SetLanguage(Language.English); // English store videos
            Directory.CreateDirectory(folder);
            Log($"Writing to {folder}, scenario {scenario}");
            if (scenario != "shop")
            {
                // The mini game scenarios test the games, not the shop: give them what they need.
                Game.Save.owned.Add("kite");
                Game.Save.owned.Add("crayons");
                Game.Save.paper = 20;
            }
            // Only the flow scenario sees the main story; the others go straight to their game.
            if (scenario != "flow") Game.Save.introSeen = true;
            if (scenario != "flow")
            {
                switch (scenario)
                {
                    case "caro": yield return Caro(); break;
                    case "xoai": yield return Xoai(); break;
                    case "dieu": yield return Dieu(); break;
                    case "mocau": yield return MoCau(); break;
                    case "oanquan": yield return OAnQuan(); break;
                    case "chuon": yield return Chuon(); break;
                    case "toc": yield return Toc(); break;
                    case "ve": yield return Ve(1f); break;
                    case "velose": yield return Ve(0.25f); break;
                    case "shop": yield return ShopScenario(); break;
                    default: Log("Unknown scenario " + scenario); break;
                }
                while (recording) yield return null; // let a video clip finish
                Finish();
                yield break;
            }
            yield return Wait(1.5f);
            yield return Shot("01-profile");

            FindByName<TMP_InputField>("input.name").text = "Tí";
            Click("btn.profile.boy");
            yield return Wait(0.3f);
            yield return Shot("02-profile-filled");
            Click("btn.profile.start");
            yield return Wait(0.5f);
            // The main story plays once after the profile.
            yield return ReadStory("02b-main-story");
            yield return Shot("03-hub");

            OpenHub("placeholder");
            yield return Wait(0.5f);
            yield return Shot("04-difficulty");
            Click("btn.difficulty.easy");
            yield return Wait(1.2f);
            yield return Shot("05-story");
            for (var i = 0; i < 20 && Find("btn.dialogue.next") != null; i++)
            {
                Click("btn.dialogue.next");
                yield return Wait(0.2f);
            }
            yield return Shot("06-instructions");
            Click("btn.instructions.start");
            yield return Wait(1f);
            yield return Shot("07-gameplay");
            FindAnyObjectByType<MiniGame>().DebugFinish(true);
            yield return Wait(1.5f);
            yield return Shot("08-result-win");

            Click("btn.result.replay");
            yield return Wait(1.2f);
            Click("btn.instructions.start");
            yield return Wait(0.5f);
            FindAnyObjectByType<MiniGame>().DebugFinish(false);
            yield return Wait(1.5f);
            yield return Shot("09-result-lose");
            Click("btn.result.menu");
            yield return Wait(1.2f);
            yield return Shot("10-hub-after");

            Click("btn.hub.settings");
            yield return Wait(0.3f);
            Click("btn.lang.en");
            yield return Wait(0.3f);
            yield return Shot("11-settings-english");
            Click("btn.common.close");
            yield return Wait(0.3f);
            Click("btn.hub.profile");
            yield return Wait(0.3f);
            yield return Shot("12-profile");

            Finish();
        }

        private static void Finish()
        {
            Log($"Done. Points {Progress.TotalPoints(Game.Save)}, scene {SceneManager.GetActiveScene().name}");
            SaveStore.Delete();
            Application.Quit();
        }

        /// <summary>Profile → hub object → Easy → instructions shot → start. Leaves the game running.</summary>
        private IEnumerator EnterMiniGame(string id)
        {
            yield return Wait(1.5f);
            FindByName<TMP_InputField>("input.name").text = "Tí";
            Click("btn.profile.boy");
            Click("btn.profile.start");
            yield return Wait(0.5f);
            yield return Shot(id + "-01-hub");
            // Higher levels are unlocked by recording wins below them on the throwaway save.
            var level = difficulty == "hard" ? 2 : difficulty == "normal" ? 1 : 0;
            var record = Game.Save.Record(id);
            for (var d = 0; d < level; d++) record.wins[d] = true;
            OpenHub(id);
            yield return Wait(0.4f);
            Click("btn.difficulty." + difficulty);
            yield return Wait(1.2f);
            yield return ReadStory(id + "-01b-story");
            yield return Shot(id + "-02-instructions");
            if (Find("btn.story.rewatch") != null)
            {
                // Stories play once; the instructions card lists the parts seen so far to watch again.
                Click("btn.story.rewatch");
                yield return Wait(0.6f);
                yield return Shot(id + "-02b-story-list");
                Click("btn.story.part.intro");
                yield return Wait(1.6f);
                yield return Shot(id + "-02c-story-again");
                while (Find("btn.dialogue.next") != null) { Click("btn.dialogue.next"); yield return Wait(0.15f); }
                yield return Wait(0.5f);
                Click("btn.story.close");
                yield return Wait(0.4f);
            }
            Click("btn.instructions.start");
        }

        /// <summary>
        /// MG07: the whole first-time sequence (story, struggle, two catches, race) or, with the swim flag, the race only.
        /// The first catch attempt rushes (to show the fly-away), later ones creep. The race uses the AI rule at 4.5 taps/s.
        /// </summary>
        private IEnumerator Chuon()
        {
            yield return EnterMiniGame("chuon");
            var game = FindAnyObjectByType<MiniGames.ChuonChuon.ChuonChuonGame>();
            var shots = new System.Collections.Generic.HashSet<string>();
            var attempts = 0;
            var storyLines = 0;
            var nextTap = 0f;
            var t = 0f;
            while (game.Running && t < 600f)
            {
                t += Time.unscaledDeltaTime;
                if (Find("btn.dialogue.next") != null)
                {
                    Time.timeScale = 1f;
                    if (storyLines++ % 7 == 0) { yield return Wait(1.6f); yield return Shot($"chuon-03-story-{storyLines:00}"); }
                    Click("btn.dialogue.next");
                    yield return Wait(0.25f);
                    continue;
                }
                if (Find("btn.common.ok") != null)
                {
                    yield return Shot("chuon-07b-canswim");
                    Click("btn.common.ok");
                    yield return Wait(0.5f);
                    continue;
                }
                var c = game.Catch;
                if (c != null)
                {
                    Time.timeScale = 1f;
                    if (c.PickingStage)
                    {
                        yield return Wait(0.6f);
                        if (shots.Add("pick")) yield return Shot("chuon-05-pick");
                        if (game.Catch == null || game.Catch.InCloseUp) continue;
                        game.Catch.DebugPick();
                        attempts++;
                        yield return Wait(0.3f);
                        if (game.Catch == null) continue;
                        game.Catch.DebugApproachSpeed = attempts == 1 ? 6f : 0.8f;
                        if (attempts == 1) { yield return Wait(0.9f); yield return Shot("chuon-06-flyaway"); }
                        if (attempts == 2) { yield return Wait(2.5f); yield return Shot("chuon-07-approach"); }
                        // Wait for this attempt to finish (caught, or flown away and back to the pick scene).
                        while (game.Catch != null && game.Catch.InCloseUp) yield return null;
                        yield return Wait(1.8f);
                    }
                    yield return null;
                    continue;
                }
                var race = game.Race;
                if (race != null && race.Running)
                {
                    if (race.PlayerX < 1f && shots.Add("struggle" + attempts)) { yield return Wait(1.5f); yield return Shot("chuon-04-struggle-" + attempts); continue; }
                    Time.timeScale = 2f;
                    if (t >= nextTap)
                    {
                        race.Stroke(race.DebugWantsArms());
                        nextTap = t + 1f / (difficulty == "hard" ? 6f : 4.5f) / 2f;
                    }
                    if (!race.Scripted && race.PlayerX > 6f && shots.Add("racestart")) yield return Shot("chuon-08a-race-start");
                    if (!race.Scripted && race.PlayerX > race.Length - 9f && shots.Add("finish")) yield return Shot("chuon-08b-finish");
                    if (race.PlayerX > 25f && shots.Add("race")) yield return Shot("chuon-08-race");
                }
                yield return null;
            }
            Time.timeScale = 1f;
            yield return Shot("chuon-09-end");
            yield return Wait(1.2f);
            yield return Shot("chuon-10-result");
            Log($"Chuon: {attempts} catch attempts, swim flag {Game.Save.Record("chuon").HasFlag("swim")}");
        }

        /// <summary>
        /// Special 01: plays all three daily sessions (plucks, a gather swipe, a wrong pull), then checks that the start
        /// card reports no plays left. Logs the currency earned.
        /// </summary>
        private IEnumerator Toc()
        {
            yield return Wait(1.5f);
            FindByName<TMP_InputField>("input.name").text = "Tí";
            Click("btn.profile.girl");
            Click("btn.profile.start");
            yield return Wait(0.5f);
            for (var session = 1; session <= 3; session++)
            {
                OpenHub("toc");
                yield return Wait(0.4f);
                if (session == 1) yield return Shot("toc-01-start-card");
                Click("btn.special.play");
                yield return Wait(1.2f);
                if (session == 1) yield return ReadStory("toc-01b-story");
                if (session == 1) yield return Shot("toc-02-instructions");
                Click("btn.instructions.start");
                yield return Wait(0.8f);
                var game = FindAnyObjectByType<MiniGames.NhoToc.NhoTocGame>();
                if (session == 1) yield return Shot("toc-03-head");
                if (session == 1)
                {
                    game.DebugGrab(true);
                    yield return Wait(0.2f);
                    yield return Shot("toc-03b-pinch");
                    game.DebugGrab(false);
                }
                for (var round = 0; round < 3 && game.Running; round++)
                {
                    while (game.Running && game.DebugPluckOne()) yield return Wait(0.6f);
                    if (session == 1 && round == 0) yield return Shot("toc-04-plucked");
                    game.DebugSwipe();
                    yield return Wait(0.9f);
                    if (session == 1 && round == 0) yield return Shot("toc-05-new-arrangement");
                }
                if (session == 1)
                {
                    game.DebugWrongPull();
                    yield return Wait(0.3f);
                    yield return Shot("toc-06-wrong-pull");
                }
                Time.timeScale = 6f;
                while (game.Running) yield return null;
                Time.timeScale = 1f;
                yield return Wait(1.2f);
                yield return ReadStory("toc-06b-grandma-" + session);
                yield return Wait(0.6f);
                yield return Shot("toc-07-result-" + session);
                Click("btn.result.menu");
                yield return Wait(1.2f);
            }
            yield return Shot("toc-08-hub-coins");
            OpenHub("toc");
            yield return Wait(0.4f);
            yield return Shot("toc-09-no-plays-left");
            Log($"Toc: currency {Game.Save.currency}, plays left {DailyLimit.Left(Game.Save.Record("toc"), 3, DailyLimit.Today)}");
        }

        /// <summary>MG01: the judge's prompt, the capture draws the prompt on the player's sheet, then Done and the scores.</summary>
        private IEnumerator Ve(float share)
        {
            yield return EnterMiniGame("ve");
            yield return Wait(0.8f);
            yield return Shot("ve-03-judge-prompt");
            var game = FindAnyObjectByType<MiniGames.VeTranh.VeTranhGame>();
            game.DebugStart();
            yield return Wait(1f);
            yield return Shot("ve-04-empty");
            yield return game.DebugDrawPrompt(12f * share, share);
            yield return Shot("ve-05-drawn");
            yield return Wait(2f);
            Click("btn.ve.done");
            var t = 0f;
            while (Find("btn.ve.next") == null && t < 60f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return Shot("ve-06-judge-scores");
            File.WriteAllBytes(Path.Combine(folder, "ve-07-sheet.png"), game.Paper.Texture.EncodeToPNG());
            Click("btn.ve.next");
            yield return Wait(1.5f);
            yield return Shot("ve-08-result");
            Log($"Ve: prompt {game.Prompt}, coverage {game.Paper.Coverage:P1}");
        }

        /// <summary>
        /// The shop from a new save (0 đồng, 1 paper, 1 pair of sandals): locked mini games, the shop opened from a locked
        /// one, ads, a cash package, buying the kite and a snack (story), the street vendor (paid with sandals), and the hub
        /// afterwards (Trộm xoài locked again without sandals).
        /// </summary>
        private IEnumerator ShopScenario()
        {
            yield return Wait(1.5f);
            FindByName<TMP_InputField>("input.name").text = "Tí";
            Click("btn.profile.boy");
            Click("btn.profile.start");
            yield return Wait(0.6f);
            yield return Shot("shop-01-hub-locked");
            OpenHub("dieu");
            yield return Wait(0.5f);
            yield return Shot("shop-02-shop-from-locked-kite");
            for (var i = 0; i < 5; i++)
            {
                Click("btn.shop.ad");
                yield return Wait(0.8f);
            }
            Click("btn.buy.kite");
            yield return Wait(0.3f);
            Click("btn.shop.cash");
            yield return Wait(0.4f);
            yield return Shot("shop-03-cash-packages");
            Click("btn.shop.cash.5000");
            yield return Wait(0.3f);
            Screens.Back(); // closes the top window (the cash packages)
            yield return Wait(0.3f);
            Click("btn.buy.mitom");
            yield return Wait(0.5f);
            yield return Shot("shop-04-snack-story");
            for (var i = 0; i < 10 && Find("btn.dialogue.next") != null; i++)
            {
                Click("btn.dialogue.next");
                yield return Wait(0.2f);
            }
            yield return Shot("shop-05-after-buying");
            Screens.Back();
            yield return Wait(0.4f);
            var vendor = FindAnyObjectByType<StreetVendor>();
            while (vendor.Walking) yield return null; // she may already be passing by at random; wait for her to leave
            vendor.DebugAppear();
            yield return Wait(2.2f);
            yield return Shot("shop-06-vendor-walking");
            foreach (var t in FindObjectsByType<Tappable>(FindObjectsSortMode.None))
                if (t.name == "StreetVendor") t.OnPointerClick(null);
            yield return Wait(0.5f);
            yield return Shot("shop-07-vendor-stall");
            Click("btn.buysandals.carem");
            yield return Wait(0.5f);
            for (var i = 0; i < 10 && Find("btn.dialogue.next") != null; i++)
            {
                Click("btn.dialogue.next");
                yield return Wait(0.2f);
            }
            Click("btn.common.close");
            yield return Wait(1.5f);
            yield return Shot("shop-08-hub-after");
            Click("btn.hub.profile");
            yield return Wait(0.6f);
            yield return Shot("shop-09-profile");
            Click("btn.common.close");
            yield return Wait(0.4f);
            Log($"Shop: đồng {Game.Save.currency}, paper {Game.Save.paper}, sandal pairs {Game.Save.sandalPairs}, owned {string.Join(",", Game.Save.owned)}, eaten {Game.Save.snacksEaten}");
        }

        /// <summary>MG05: rock-paper-scissors, then the capture plays Hard-level moves for the player at 3x speed.</summary>
        private IEnumerator OAnQuan()
        {
            yield return EnterMiniGame("oanquan");
            yield return Wait(0.6f);
            yield return Shot("oanquan-03-rps");
            yield return PlayRps("oanquan");
            var game = FindAnyObjectByType<MiniGames.OAnQuan.OAnQuanGame>();
            yield return Wait(0.5f);
            yield return Shot("oanquan-04-board");
            string said = null;
            var shotSlap = false;
            var shotEat = false;
            var shotSweep = false;
            game.Said += s => said = s;
            Time.timeScale = 3f;
            var rng = new System.Random(3);
            var moves = 0;
            var t = 0f;
            while (game.Running && t < 600f)
            {
                t += Time.unscaledDeltaTime;
                if (game.PlayerTurn)
                {
                    var m = MiniGames.OAnQuan.OAnQuanAi.Choose(game.Rules, 0, Difficulty.Hard, rng);
                    game.DebugPlay(m.space, m.dir);
                    moves++;
                    if (moves == 1) { Time.timeScale = 1f; yield return Wait(2.2f); yield return Shot("oanquan-05-sowing"); Time.timeScale = 3f; }
                    if (moves == 6) yield return Shot("oanquan-06-midgame");
                }
                if (said == "slap" && !shotSlap)
                {
                    shotSlap = true;
                    yield return Shot("oanquan-06b-slap");
                }
                if (said == "sweep" && !shotSweep)
                {
                    shotSweep = true;
                    Time.timeScale = 1f;
                    yield return Wait(0.6f);
                    yield return Shot("oanquan-06d-sweep-notice");
                    yield return Wait(2.6f);
                    yield return Shot("oanquan-06e-swept");
                    Time.timeScale = 3f;
                }
                if (said == "eat" && !shotEat)
                {
                    shotEat = true;
                    yield return Shot("oanquan-06c-eat");
                }
                yield return null;
            }
            Time.timeScale = 1f;
            yield return Shot("oanquan-07-end");
            yield return Wait(1.2f);
            yield return Shot("oanquan-08-result");
            Log($"OAnQuan: {moves} player moves, score {game.Rules.Score(0)}-{game.Rules.Score(1)}");
        }

        /// <summary>MG06: the autopilot steers into free lanes at double speed.</summary>
        private IEnumerator MoCau()
        {
            yield return EnterMiniGame("mocau");
            var game = FindAnyObjectByType<MiniGames.MoCau.MoCauGame>();
            game.DebugSteer = 0f;
            yield return Wait(1.2f);
            yield return Shot("mocau-03-countdown");
            Time.timeScale = 2f;
            var t = 0f;
            var shot = false;
            while (game.Running && t < 200f)
            {
                t += Time.deltaTime;
                if (game.Racing) game.DebugSteer = game.DebugAutopilot();
                if (!shot && t > 14f) { shot = true; yield return Shot("mocau-04-racing"); }
                yield return null;
            }
            Time.timeScale = 1f;
            yield return Shot("mocau-05-end");
            yield return Wait(1.2f);
            yield return Shot("mocau-06-result");
            Log($"MoCau: place {game.PlayerPlace} after {t:0} s");
        }

        /// <summary>
        /// MG03: an autopilot runs into the wind, reacting 0.7 s late like a person; time runs 4  times faster to keep the
        /// capture short. Logs how long the climb took in game seconds.
        /// </summary>
        private IEnumerator Dieu()
        {
            yield return EnterMiniGame("dieu");
            var game = FindAnyObjectByType<MiniGames.ThaDieu.ThaDieuGame>();
            game.DebugRun = 0f;
            yield return Wait(0.6f);
            yield return Shot("dieu-03-start");
            Time.timeScale = 4f;
            var gameTime = 0f;
            var reactAt = 0f;
            var lastGood = game.GoodDirection;
            var shots = 0;
            while (game.Running && gameTime < 400f)
            {
                gameTime += Time.deltaTime;
                if (game.GoodDirection != lastGood)
                {
                    lastGood = game.GoodDirection;
                    reactAt = gameTime + 0.7f;
                }
                if (gameTime >= reactAt) game.DebugRun = game.GoodDirection;
                if (shots == 0 && gameTime > 12f) { shots++; yield return Shot("dieu-04-climbing"); }
                if (shots == 1 && game.Height > game.Target * 0.6f) { shots++; yield return Shot("dieu-05-high"); }
                yield return null;
            }
            Time.timeScale = 1f;
            yield return Shot("dieu-06-end");
            yield return Wait(1.2f);
            yield return ReadStory("dieu-06b-after");
            yield return Shot("dieu-07-result");
            Log($"Dieu: reached {game.Height:0}/{game.Target:0} m in {gameTime:0} game seconds");
        }

        /// <summary>
        /// MG04: two accurate stone throws, the other stones wasted, then sandals aimed at mangoes until the round ends
        /// (win, stuck sandals or a fight; each shows up in the log).
        /// </summary>
        private IEnumerator Xoai()
        {
            yield return EnterMiniGame("xoai");
            yield return Wait(0.8f);
            yield return Shot("xoai-03-start");
            var game = FindAnyObjectByType<MiniGames.TromXoai.TromXoaiGame>();
            var throws = 0;
            var stones = game.StonesLeft;
            for (var t = 0; t < 80 && game.Running; t++)
            {
                // The "out of stones" talk pauses the game: read it through.
                if (Find("btn.dialogue.next") != null)
                {
                    yield return ReadStory("xoai-05b-talk");
                    continue;
                }
                if (!game.DebugPickUp())
                {
                    yield return Wait(0.4f);
                    continue;
                }
                yield return Wait(0.3f);
                // The first stone is aimed; the other stones are wasted straight up so the sandals come into play.
                var waste = throws >= 1 && throws < stones;
                var aim = waste ? new Vector2(0.5f, 6f) : game.DebugAimAtMango(1.1f + (throws % 3) * 0.15f);
                if (aim == null) break;
                game.Throw(aim.Value);
                throws++;
                if (throws == 1)
                {
                    yield return Wait(0.45f);
                    yield return Shot("xoai-04-stone-flying");
                }
                yield return Wait(waste ? 0.5f : 1.6f);
                if (throws == 1) yield return Shot("xoai-05-mangoes-down");
                if (throws == stones + 1) yield return Shot("xoai-06-sandal");
            }
            yield return Wait(0.3f);
            yield return Shot("xoai-07-end");
            yield return Wait(2.5f);
            yield return ReadStory("xoai-07b-ending");
            yield return Shot("xoai-08-result");
            Log($"Xoai: {throws} throws, collected {game.Collected}/{game.Needed}, running {game.Running}");
        }

        /// <summary>MG02: rock-paper-scissors, then a whole game where the capture plays the Hard AI's moves for the player.</summary>
        private IEnumerator Caro()
        {
            yield return Wait(1.5f);
            FindByName<TMP_InputField>("input.name").text = "Tí";
            Click("btn.profile.girl");
            Click("btn.profile.start");
            yield return Wait(0.5f);
            yield return Shot("caro-01-hub");
            OpenHub("caro");
            yield return Wait(0.4f);
            yield return Shot("caro-01b-difficulty-locked");
            // With -child-capture-difficulty the lower levels are recorded as won so the chosen level is open.
            var level = difficulty == "hard" ? 2 : difficulty == "normal" ? 1 : 0;
            for (var d = 0; d < level; d++) Game.Save.Record("caro").wins[d] = true;
            Screens.Back();
            yield return Wait(0.2f);
            OpenHub("caro");
            yield return Wait(0.3f);
            Click("btn.difficulty." + difficulty);
            yield return Wait(1.2f);
            yield return ReadStory("caro-02a-story");
            yield return Shot("caro-02-instructions");
            Click("btn.instructions.start");
            yield return Wait(0.5f);
            yield return Shot("caro-03-rps");
            yield return PlayRps("caro");
            var game = FindAnyObjectByType<MiniGames.Caro.CaroGame>();
            var rng = new System.Random(5);
            var moves = 0;
            for (var t = 0; t < 400 && game.Running; t++)
            {
                if (game.PlayerTurn)
                {
                    var m = MiniGames.Caro.CaroAi.Choose(game.Board, MiniGames.Caro.CaroBoard.PlayerMark, Difficulty.Hard, rng);
                    game.PlayerMove(m.x, m.y);
                    moves++;
                    if (moves == 3) { yield return Wait(0.36f); yield return Shot("caro-04b-pen-writing"); }
                    if (moves == 6) yield return Shot("caro-05-midgame");
                }
                yield return Wait(0.25f);
            }
            yield return Wait(0.2f);
            yield return Shot("caro-06-end");
            yield return Wait(1.5f);
            yield return ReadStory("caro-06b-after");
            yield return Shot("caro-07-result");
            Log($"Caro finished after {moves} player moves");
        }

        /// <summary>
        /// Plays rock-paper-scissors with "rock" until someone wins, waiting for each chant; shots of the chant and the
        /// first reveal.
        /// </summary>
        private static IEnumerator PlayRps(string prefix)
        {
            for (var round = 0; round < 10; round++)
            {
                var rock = FindByName<Button>("btn.rps.rock");
                if (rock == null) yield break;
                if (!rock.interactable)
                {
                    yield return null;
                    round--;
                    continue;
                }
                rock.onClick.Invoke();
                if (round == 0)
                {
                    yield return Wait(0.6f);
                    yield return Shot(prefix + "-03b-rps-chant");
                    yield return Wait(1.6f);
                    yield return Shot(prefix + "-04-rps-reveal");
                }
                // Wait until the round is over: either the screen closed or the buttons are back for a tie.
                yield return Wait(0.5f);
                while (FindByName<Button>("btn.rps.rock") != null && !FindByName<Button>("btn.rps.rock").interactable) yield return null;
            }
        }

        /// <summary>Reads through a story sequence: each line is typed out, shot, then advanced.</summary>
        private static IEnumerator ReadStory(string prefix)
        {
            for (var n = 1; n <= 20 && Find("btn.dialogue.next") != null; n++)
            {
                yield return Wait(2.4f);
                yield return Shot($"{prefix}-{n}");
                Click("btn.dialogue.next"); // finishes the typing if it is still going
                yield return Wait(0.1f);
                if (Find("btn.dialogue.next") != null) Click("btn.dialogue.next");
                yield return Wait(0.3f);
            }
        }

        private static void OpenHub(string id)
        {
            foreach (var h in FindObjectsByType<HubObject>(FindObjectsSortMode.None))
                if (h.definition.id == id) h.Open();
        }


        /// <summary>Saves every frame as a JPEG (video/frame_00001.jpg …) for <see cref="videoSeconds"/> of game time.</summary>
        private IEnumerator Record()
        {
            recording = true;
            var dir = Path.Combine(folder, "video");
            Directory.CreateDirectory(dir);
            Time.captureFramerate = 30;
            var frames = Mathf.RoundToInt(videoSeconds * 30f);
            for (var i = 1; i <= frames; i++)
            {
                if (Time.timeScale > 1f) Time.timeScale = 1f; // never fast-forwarded (CrazyGames video rule)
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(dir, $"frame_{i:00000}.jpg"), tex.EncodeToJPG(92));
                Destroy(tex);
            }
            Time.captureFramerate = 0;
            recording = false;
            Log($"Video: {frames} frames from {videoStart}");
        }
        private static WaitForSecondsRealtime Wait(float s) => new WaitForSecondsRealtime(s);

        private static IEnumerator Shot(string name)
        {
            var hidden = new System.Collections.Generic.List<Canvas>();
            if (noUi)
                foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (c.enabled && c.isRootCanvas) { c.enabled = false; hidden.Add(c); }
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(folder, name + ".png");
            ScreenCapture.CaptureScreenshot(path, superSize);
            yield return null;
            foreach (var c in hidden) if (c != null) c.enabled = true;
            Log("Shot " + name);
            if (name == videoStart && instance != null) instance.StartCoroutine(instance.Record()); // runs alongside the scripted play
        }

        private static GameObject Find(string name)
        {
            foreach (var t in FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
                if (t.name == name && t.gameObject.activeInHierarchy) return t.gameObject;
            return null;
        }

        private static T FindByName<T>(string name) where T : Component => Find(name)?.GetComponent<T>();

        private static void Click(string name)
        {
            var button = FindByName<Button>(name);
            if (button == null) Log("MISSING button " + name);
            else button.onClick.Invoke();
        }

        private static void Log(string message) => Debug.Log("[CHILD-CAPTURE] " + message);
    }
}
