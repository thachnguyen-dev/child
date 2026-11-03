using System;
using UnityEngine;

namespace ChildGame
{
    /// <summary>How a round ended. A draw counts as a play but never as a win.</summary>
    public enum Outcome { Win, Loss, Draw }

    /// <summary>
    /// Base class for every mini game. A mini game scene holds one MiniGame and one <see cref="MiniGameSession"/>;
    /// the session shows the story and instructions, calls <see cref="StartGame"/>, and handles the result when the
    /// game calls <see cref="Finish(Outcome)"/>. Use <c>Time.deltaTime</c> (not unscaled time): the leave prompt sets
    /// <c>Time.timeScale</c> to 0.
    /// </summary>
    public abstract class MiniGame : MonoBehaviour
    {
        public Difficulty Difficulty { get; private set; }
        public bool Running { get; private set; }

        /// <summary>Raised once per round.</summary>
        public event Action<Outcome> Finished;

        public void StartGame(Difficulty difficulty)
        {
            Difficulty = difficulty;
            Running = true;
            OnBegin();
        }

        /// <summary>Set up and start the round for <see cref="Difficulty"/>.</summary>
        protected abstract void OnBegin();

        /// <summary>Called right after the round ends, before the result screen.</summary>
        protected virtual void OnEnd() { }

        protected void Finish(bool won) => Finish(won ? Outcome.Win : Outcome.Loss);

        protected void Finish(Outcome outcome)
        {
            if (!Running) return;
            Running = false;
            OnEnd();
            Finished?.Invoke(outcome);
        }

        /// <summary>Story lines for how this round ended; null = use the definition's win/lose lines.</summary>
        public virtual System.Collections.Generic.IReadOnlyList<string> OutcomeLines => null;

        /// <summary>Currency earned this round (special mini games); added to the save when the round ends.</summary>
        public virtual int CurrencyEarned => 0;

        /// <summary>What the special result screen counts (e.g. grey hairs plucked); defaults to the currency.</summary>
        public virtual int ResultCount => CurrencyEarned;

        /// <summary>Ends the round immediately (capture harness and editor testing).</summary>
        public void DebugFinish(bool won) => Finish(won);
    }
}
