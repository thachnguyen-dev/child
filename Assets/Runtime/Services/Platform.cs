using System;

namespace ChildGame.Services
{
    /// <summary>
    /// What a game portal needs from the game (CrazyGames, docs/5-crazygames.md): gameplay start/stop events, an
    /// interstitial at natural breaks, a celebration moment, the portal's mute switch, and the player's name and language.
    /// Everywhere else it does nothing (<see cref="NoPortal"/>).
    /// </summary>
    public interface IPlatform
    {
        /// <summary>The portal has muted the game; this overrides the in-game sound volume.</summary>
        bool Muted { get; }
        /// <summary>The portal player's name, or null (not logged in / no portal).</summary>
        string UserName { get; }
        /// <summary>The player's language from the portal ("vi-VN", "en-US"…), or null.</summary>
        string Locale { get; }
        void GameplayStart();
        void GameplayStop();
        /// <summary>A big moment (three stars in a mini game, the "Trùm xóm" title).</summary>
        void Happytime();
        /// <summary>
        /// An interstitial at a natural break (end of a round). The game is paused and muted while it plays;
        /// <paramref name="done"/> always runs, whether an ad played, was skipped by the portal's frequency cap, or failed.
        /// </summary>
        void Interstitial(Action done);
    }

    public class NoPortal : IPlatform
    {
        public bool Muted => false;
        public string UserName => null;
        public string Locale => null;
        public void GameplayStart() { }
        public void GameplayStop() { }
        public void Happytime() { }
        public void Interstitial(Action done) => done?.Invoke();
    }
}
