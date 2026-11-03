using UnityEngine.SceneManagement;

namespace ChildGame
{
    /// <summary>Moves between the hub and mini game scenes and carries the chosen game and difficulty across.</summary>
    public static class SceneFlow
    {
        public const string HubScene = "Hub";

        /// <summary>The mini game being played; null when a mini game scene was opened directly in the editor.</summary>
        public static MiniGameDefinition Current { get; private set; }
        public static Difficulty CurrentDifficulty { get; private set; } = Difficulty.Normal;

        public static void Play(MiniGameDefinition game, Difficulty difficulty)
        {
            Current = game;
            CurrentDifficulty = difficulty;
            SceneManager.LoadScene(game.sceneName);
        }

        public static void Replay() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        public static void ToHub()
        {
            Current = null;
            SceneManager.LoadScene(HubScene);
        }
    }
}
