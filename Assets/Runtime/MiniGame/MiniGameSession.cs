using ChildGame.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChildGame
{
    /// <summary>
    /// Runs the common flow around a mini game (design §6):
    /// first-time story → instructions → gameplay → win/loss → result → menu.
    /// </summary>
    public class MiniGameSession : MonoBehaviour
    {
        [Tooltip("Used when this scene is opened directly in the editor instead of from the hub.")]
        [SerializeField] private MiniGameDefinition fallbackDefinition;
        [SerializeField] private Difficulty fallbackDifficulty = Difficulty.Normal;

        private MiniGame game;
        private MiniGameDefinition definition;
        private Difficulty difficulty;

        public MiniGameDefinition Definition => definition;

        private void Start()
        {
            definition = SceneFlow.Current != null ? SceneFlow.Current : fallbackDefinition;
            difficulty = SceneFlow.Current != null ? SceneFlow.CurrentDifficulty : fallbackDifficulty;
            game = FindAnyObjectByType<MiniGame>();
            if (definition == null || game == null)
            {
                Debug.LogError("[CHILD] MiniGameSession needs a MiniGame in the scene and a definition.");
                return;
            }
            Time.timeScale = 1f;
            EnsureRaycaster();
            game.Finished += OnFinished;
            Screens.SceneBack = AskLeave;
            BuildHud();

            var record = Game.Save.Record(definition.id);
            if (!record.storySeen && definition.storyKeys != null && definition.storyKeys.Length > 0)
            {
                UiScreens.Dialogue(definition.storyKeys, () =>
                {
                    record.storySeen = true;
                    StoryBook.MarkSeen(record, definition.storyKeys);
                    Game.Commit();
                    ShowInstructions();
                });
            }
            else ShowInstructions();
        }

        private void OnDestroy()
        {
            Game.Platform.GameplayStop();
            Time.timeScale = 1f;
            if (Screens.SceneBack == AskLeave) Screens.SceneBack = null;
        }

        private void ShowInstructions() => UiScreens.Instructions(definition, difficulty, StartRound, SceneFlow.ToHub);

        private void StartRound()
        {
            // Shop requirements (docs/3-shop-design.md): sandals for Trộm xoài, one sheet of paper per play of Caro and Drawing.
            var lockReason = Shop.LockReason(Game.Save, definition);
            if (lockReason != null)
            {
                UiScreens.NeedItem(lockReason, Shop.LockItem(definition), ShowInstructions);
                return;
            }
            if (definition.paperPerPlay > 0)
            {
                if (!Shop.UsePaper(Game.Save, definition))
                {
                    UiScreens.NeedItem("need.paper", "paper", ShowInstructions);
                    return;
                }
                Game.Commit();
            }
            // Special mini games: a session counts as soon as it starts (leaving early cannot dodge the daily limit).
            if (definition.dailyLimit > 0)
            {
                if (!DailyLimit.Consume(Game.Save.Record(definition.id), definition.dailyLimit, DailyLimit.Today))
                {
                    UiScreens.Confirm("special.nomore", SceneFlow.ToHub, SceneFlow.ToHub);
                    return;
                }
                Game.Commit();
            }
            game.StartGame(difficulty);
            Game.Platform.GameplayStart(); // CrazyGames: play begins
        }

        private void BuildHud()
        {
            var safe = Ui.Canvas("MiniGameHud", 10);
            Ui.Button(safe, "hud.home", AskLeave, Ui.InkSoft, 36).Place(new Vector2(0f, 1f), new Vector2(200, 90), new Vector2(24, -24));
        }

        /// <summary>Leaving mid-round asks first; the round is frozen while the question is open.</summary>
        private void AskLeave()
        {
            if (!game.Running)
            {
                SceneFlow.ToHub();
                return;
            }
            Time.timeScale = 0f;
            Game.Platform.GameplayStop();
            UiScreens.Confirm("hud.leave", SceneFlow.ToHub, () =>
            {
                Time.timeScale = 1f;
                Game.Platform.GameplayStart();
            });
        }

        private void OnFinished(Outcome outcome)
        {
            Game.Platform.GameplayStop(); // the round is over: a break
            if (!definition.hasDifficulty)
            {
                // Special mini games: no points, no win/loss; the currency earned is added when the timer runs out.
                var earned = Mathf.Max(0, game.CurrencyEarned);
                Sfx.Play(earned > 0 ? Snd.Coin : Snd.Win, 0.8f, 1f, 0f);
                Game.Save.currency += earned;
                var rec = Game.Save.Record(definition.id);
                rec.plays++;
                Achievements.Evaluate(Game.Save);
                Game.Commit();
                var left = definition.dailyLimit > 0 ? DailyLimit.Left(rec, definition.dailyLimit, DailyLimit.Today) : -1;
                Debug.Log($"[CHILD] {definition.id}: +{earned} currency (total {Game.Save.currency}), sessions left {left}");
                // Story lines after the session (the game may choose them), then the result card.
                var specialLines = new System.Collections.Generic.List<string>();
                // Stories play once (owner 2026-10-08); they can be watched again from the instructions card.
                specialLines.AddRange(StoryBook.Once(rec, game.OutcomeLines ?? definition.winKeys));
                Game.Commit();
                void ShowSpecialResult() => UiScreens.SpecialResult(definition, game.ResultCount, earned, left, SceneFlow.Replay, SceneFlow.ToHub);
                // The end of a session is a natural break: the web portal may show an interstitial here (CrazyGames).
                GameRunner.Instance.Delay(0.6f, () => Game.Platform.Interstitial(() =>
                {
                    if (specialLines.Count > 0) UiScreens.Dialogue(specialLines, ShowSpecialResult);
                    else ShowSpecialResult();
                }));
                return;
            }
            var won = outcome == Outcome.Win;
            var pointAwarded = Progress.RecordResult(Game.Save, definition.id, difficulty, won);
            Sfx.Play(won ? Snd.Win : outcome == Outcome.Loss ? Snd.Lose : Snd.Star, 0.8f, 1f, 0f);
            if (pointAwarded) GameRunner.Instance.Delay(1.4f, () => Sfx.Play(Snd.Star, 0.7f, 1f + 0.12f * (Game.Save.Record(definition.id).Points - 1), 0f));
            var unlocked = Achievements.Evaluate(Game.Save);
            Game.Commit();
            Debug.Log($"[CHILD] {definition.id} {difficulty}: {outcome}, point {pointAwarded}, total {Progress.TotalPoints(Game.Save)}");
            var record = Game.Save.Record(definition.id);

            // Story lines for the outcome, then the closing lines when the third star was just earned.
            var lines = new System.Collections.Generic.List<string>();
            // Each story part plays once (owner 2026-10-08); the instructions card offers to watch it again.
            var outcomeLines = game.OutcomeLines ?? (outcome == Outcome.Win ? definition.winKeys : outcome == Outcome.Loss ? definition.loseKeys : null);
            lines.AddRange(StoryBook.Once(record, outcomeLines));
            if (pointAwarded && record.Points == Progress.PointsPerGame) lines.AddRange(StoryBook.Once(record, definition.completeKeys));
            Game.Commit();
            void ShowResult() => UiScreens.Result(outcome, pointAwarded, record, unlocked, SceneFlow.Replay, SceneFlow.ToHub);
            if (pointAwarded && record.Points == Progress.PointsPerGame) Game.Platform.Happytime(); // all three stars
            // The end of a round is a natural break: the web portal may show an interstitial here (CrazyGames).
            GameRunner.Instance.Delay(0.6f, () => Game.Platform.Interstitial(() =>
            {
                if (lines.Count > 0) UiScreens.Dialogue(lines, ShowResult);
                else ShowResult();
            }));
        }

        /// <summary>World objects receive taps through a Physics2DRaycaster on the main camera.</summary>
        public static void EnsureRaycaster()
        {
            Ui.EnsureEventSystem();
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<Physics2DRaycaster>() == null) cam.gameObject.AddComponent<Physics2DRaycaster>();
        }
    }
}
