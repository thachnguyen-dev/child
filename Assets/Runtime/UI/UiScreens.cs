using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChildGame.UI
{
    /// <summary>The shared screens of the game flow (design §5, §6). Placeholder look; layouts are code-built.</summary>
    public static class UiScreens
    {
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        // ---- First start: name, gender, language (design §5) ----

        public static void Profile(Action done)
        {
            var card = Ui.Modal("ProfileScreen", new Vector2(1180, 900), null, out var root);
            var col = Ui.Column(card, "Column", 34, 56);

            Ui.Label(col.transform, "game.title", 64, new Color32(196, 92, 52, 255)).Size(0, 84); // the game's official name
            Ui.Label(col.transform, "profile.title", 48, Ui.Ink).Size(0, 64);
            LanguageRow(col.transform);

            Ui.Label(col.transform, "profile.name", 40, Ui.InkSoft).Size(0, 50);
            var input = Ui.Input(col.transform, "input.name", "profile.name.placeholder", 12).Size(0, 110);
            // Editing later: start from the current name. A game portal's logged-in player starts with their portal name
            // (CrazyGames account integration).
            var startName = Game.Save.profileDone ? Game.Save.playerName : Game.Platform.UserName;
            if (!string.IsNullOrEmpty(startName)) input.text = startName.Length > 12 ? startName.Substring(0, 12) : startName;

            Ui.Label(col.transform, "profile.gender", 40, Ui.InkSoft).Size(0, 50);
            var genderRow = Ui.Row(col.transform, "Gender", 40).Size(0, 120);
            Gender? gender = Game.Save.profileDone ? Game.Save.gender : (Gender?)null;
            Button start = null;
            Button boy = null, girl = null;

            void Refresh()
            {
                ((Image)boy.targetGraphic).color = gender == Gender.Boy ? Ui.Sky : Ui.StarOff;
                ((Image)girl.targetGraphic).color = gender == Gender.Girl ? Ui.Brick : Ui.StarOff;
                start.interactable = gender.HasValue && input.text.Trim().Length > 0;
            }

            boy = Ui.Button(genderRow.transform, "profile.boy", () => { gender = Gender.Boy; Refresh(); }, Ui.StarOff).Size(360, 120);
            girl = Ui.Button(genderRow.transform, "profile.girl", () => { gender = Gender.Girl; Refresh(); }, Ui.StarOff).Size(360, 120);

            start = Ui.Button(col.transform, "profile.start", () =>
            {
                Game.SetProfile(input.text, gender ?? Gender.Boy);
                Screens.Close(root);
                done?.Invoke();
            }, Ui.Leaf, 54).Size(0, 130);
            input.onValueChanged.AddListener(_ => Refresh());
            Refresh();
        }

        /// <summary>Tiếng Việt / English buttons; the current language is highlighted.</summary>
        private static void LanguageRow(Transform parent)
        {
            var row = Ui.Row(parent, "Language", 30).Size(0, 90);
            var vi = Ui.Button(row.transform, "lang.vi", null, Ui.StarOff, 38).Size(300, 90);
            var en = Ui.Button(row.transform, "lang.en", null, Ui.StarOff, 38).Size(300, 90);

            void Refresh()
            {
                ((Image)vi.targetGraphic).color = Loc.Current == Language.Vietnamese ? Ui.Sun : Ui.StarOff;
                ((Image)en.targetGraphic).color = Loc.Current == Language.English ? Ui.Sun : Ui.StarOff;
            }

            vi.onClick.AddListener(() => { Game.SetLanguage(Language.Vietnamese); Refresh(); });
            en.onClick.AddListener(() => { Game.SetLanguage(Language.English); Refresh(); });
            Refresh();
        }

        // ---- Difficulty choice from the hub ----

        public static void Difficulty(MiniGameDefinition game)
        {
            GameObject root = null;
            var card = Ui.Modal("DifficultyScreen", new Vector2(1000, 820), () => Screens.Close(root), out root);
            var col = Ui.Column(card, "Column", 30, 50);
            Ui.Label(col.transform, game.titleKey, 68, Ui.Ink).Size(0, 110);
            var record = Game.Save.Record(game.id, false);
            Ui.Stars(col.transform, record, 70).Size(0, 80);
            if (game.paperPerPlay > 0)
            {
                var paperLine = Ui.Text(col.transform, "", 30, Ui.InkSoft).Size(0, 40);
                paperLine.gameObject.AddComponent<LocText>().SetFormat("difficulty.paper", Game.Save.paper);
            }

            foreach (ChildGame.Difficulty d in Enum.GetValues(typeof(ChildGame.Difficulty)))
            {
                var unlocked = Progress.IsUnlocked(Game.Save, game.id, d);
                var button = Ui.Button(col.transform, Ui.DifficultyKey(d), () =>
                {
                    if (!Shop.HasPaperFor(Game.Save, game))
                    {
                        NeedItem("need.paper", "paper", null);
                        return;
                    }
                    Screens.Close(root);
                    SceneFlow.Play(game, d);
                }, unlocked ? Ui.DifficultyColor(d) : Ui.StarOff, 50).Size(0, 120);
                button.interactable = unlocked;
                if (!unlocked)
                    Ui.Label(button.transform, "difficulty.locked." + d.ToString().ToLowerInvariant(), 28, Ui.InkSoft, TextAlignmentOptions.Right, false)
                        .Stretch(Vector2.zero, Vector2.one, 24);
                if (record != null && record.Won(d))
                    Ui.Icon(button.transform, "won", Ui.Star, Ui.StarOn, 64).Place(new Vector2(1f, 0.5f), new Vector2(64, 64), new Vector2(-30, 0));
            }
            Ui.Button(col.transform, "common.close", () => Screens.Close(root), Ui.InkSoft, 40).Size(0, 90);
        }

        // ---- First-time story / dialogue (design §13) ----

        /// <summary>
        /// A story sequence. Each line key may have a companion <c>&lt;key&gt;.who</c> naming the speaker (shown as a name tag;
        /// <c>@you</c> is the player, tagged "Bạn" in the player's colour). <c>.who = @scene</c> shows
        /// the line as a full-screen scene card (e.g. "Giờ nghỉ giải lao"); no <c>.who</c> means narration. Text types
        /// itself out; a tap shows the rest of the line, the next tap moves on.
        /// </summary>
        public static void Dialogue(IReadOnlyList<string> lineKeys, Action done)
        {
            var safe = Ui.Canvas("DialogueScreen", 30);
            var root = safe.root.gameObject;
            var index = 0;

            void Finish()
            {
                Screens.Close(root);
                done?.Invoke();
            }

            Screens.Push(root, Finish);

            // Tapping anywhere advances.
            var catcher = Ui.Panel(safe.parent, "Tap", new Color(0, 0, 0, 0.35f), false);
            catcher.transform.SetAsFirstSibling();
            var advance = catcher.gameObject.AddComponent<Button>();
            advance.transition = Selectable.Transition.None;
            advance.name = "btn.dialogue.next";

            // Scene card: big centred title on a darker veil.
            var card = Ui.Panel(safe, "SceneCard", new Color(0.12f, 0.1f, 0.1f, 0.78f), false);
            card.raycastTarget = false;
            var cardText = Ui.Text(card.transform, "", 92, Ui.Cream);
            cardText.Stretch(Vector2.zero, Vector2.one, 60);
            cardText.fontStyle = FontStyles.Italic;

            var box = Ui.Panel(safe, "Box", Ui.Cream).Stretch(new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.36f));
            box.raycastTarget = false;
            var tag = Ui.Panel(box.transform, "Speaker", Ui.Brick);
            tag.raycastTarget = false;
            tag.Place(new Vector2(0f, 1f), new Vector2(440, 76), new Vector2(40, 40));
            var tagText = Ui.Text(tag.transform, "", 38, Color.white);
            tagText.Stretch(Vector2.zero, Vector2.one, 6);
            var text = Ui.Text(box.transform, "", 50, Ui.Ink, TextAlignmentOptions.TopLeft, false);
            text.Stretch(Vector2.zero, Vector2.one, 50);
            text.margin = new Vector4(0, 20, 0, 0);
            var hint = Ui.Label(box.transform, "dialogue.next", 32, Ui.InkSoft, TextAlignmentOptions.BottomRight);
            hint.Stretch(Vector2.zero, Vector2.one, 30);

            Ui.Button(safe, "dialogue.skip", Finish, Ui.InkSoft, 36).Place(new Vector2(1f, 1f), new Vector2(240, 90), new Vector2(-30, -30));

            Coroutine typing = null;
            TextMeshProUGUI target = text;
            // Long lines are split into pages so nothing runs under the "tap to continue" hint (owner 2026-10-08):
            // a tap finishes the page, the next shows the next page, then the next line.
            text.overflowMode = TextOverflowModes.Page;
            cardText.overflowMode = TextOverflowModes.Page;
            text.margin = new Vector4(0, 20, 0, 46);

            int PageEnd(TextMeshProUGUI t)
            {
                t.ForceMeshUpdate();
                var info = t.textInfo;
                var page = Mathf.Clamp(t.pageToDisplay, 1, Mathf.Max(1, info.pageCount));
                return info.pageCount <= 1 ? t.text.Length : info.pageInfo[page - 1].lastCharacterIndex + 1;
            }

            void TypePage(TextMeshProUGUI t)
            {
                t.ForceMeshUpdate();
                var info = t.textInfo;
                var first = info.pageCount <= 1 ? 0 : info.pageInfo[Mathf.Clamp(t.pageToDisplay, 1, info.pageCount) - 1].firstCharacterIndex;
                t.maxVisibleCharacters = first;
                if (typing != null) GameRunner.Instance.StopCoroutine(typing);
                typing = GameRunner.Instance.StartCoroutine(TypeOut(t, first, PageEnd(t)));
            }

            void Show()
            {
                var key = lineKeys[index];
                var who = Loc.Raw(key + ".who");
                var scene = who == "@scene";
                card.gameObject.SetActive(scene);
                box.gameObject.SetActive(!scene);
                target = scene ? cardText : text;
                target.text = Loc.Get(key);
                if (!scene)
                {
                    tag.gameObject.SetActive(!string.IsNullOrEmpty(who));
                    if (!string.IsNullOrEmpty(who))
                    {
                        // "@you" is the player: the tag reads "Bạn" / "You" (owner 2026-10-08: never the name).
                        var you = who.StartsWith("@you");
                        tagText.text = you ? Loc.Get("story.you") + who.Substring(4) : Loc.Get(key + ".who");
                        tag.color = you || who.StartsWith("{name}") ? Ui.Sky : Ui.Brick;
                    }
                    // Narration (no speaker) reads in a softer, slanted voice.
                    text.fontStyle = string.IsNullOrEmpty(who) ? FontStyles.Italic : FontStyles.Normal;
                    text.color = string.IsNullOrEmpty(who) ? Ui.InkSoft : Ui.Ink;
                }
                target.pageToDisplay = 1;
                TypePage(target);
            }

            advance.onClick.AddListener(() =>
            {
                // A tap finishes the page; the next shows the next page of a long line, then the next line.
                var end = PageEnd(target);
                if (target.maxVisibleCharacters < end)
                {
                    if (typing != null) GameRunner.Instance.StopCoroutine(typing);
                    target.maxVisibleCharacters = end;
                    return;
                }
                Sfx.Play(Snd.Page, 0.5f);
                if (target.pageToDisplay < target.textInfo.pageCount)
                {
                    target.pageToDisplay++;
                    TypePage(target);
                    return;
                }
                index++;
                if (index >= lineKeys.Count) Finish();
                else Show();
            });
            Show();
        }

        private static System.Collections.IEnumerator TypeOut(TextMeshProUGUI t, int from, int to)
        {
            const float charsPerSecond = 45f;
            var shown = (float)from;
            while (t != null && t.maxVisibleCharacters < to)
            {
                shown += Time.unscaledDeltaTime * charsPerSecond;
                t.maxVisibleCharacters = Mathf.Min(to, Mathf.FloorToInt(shown));
                yield return null;
            }
        }


        /// <summary>
        /// Long text in a fixed area is split into parts instead of running under the buttons (owner 2026-10-08): one
        /// paragraph per page, "‹ 1/2 ›" arrows under it when there is more than one, and the text shrinks a little if a
        /// single paragraph would still not fit.
        /// </summary>
        public static void Paginate(TextMeshProUGUI body, Transform parent, Vector2 anchor, string text)
        {
            var parts = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var page = 0;
            body.enableAutoSizing = true;
            body.fontSizeMax = body.fontSize;
            body.fontSizeMin = body.fontSize * 0.75f;
            body.overflowMode = TextOverflowModes.Truncate;
            var row = Ui.Row(parent, "Pages", 30);
            row.Place(anchor, new Vector2(420, 70));
            var prev = Ui.Button(row.transform, "page.prev", null, Ui.InkSoft, 40).Size(110, 66);
            var label = Ui.Text(row.transform, "", 34, Ui.InkSoft).Size(120, 66);
            var next = Ui.Button(row.transform, "page.next", null, Ui.Leaf, 40).Size(110, 66);
            void Show()
            {
                body.text = parts.Length > 0 ? parts[page] : "";
                row.gameObject.SetActive(parts.Length > 1);
                label.text = $"{page + 1}/{parts.Length}";
                prev.interactable = page > 0;
                next.interactable = page < parts.Length - 1;
            }
            prev.onClick.AddListener(() => { page = Mathf.Max(0, page - 1); Show(); });
            next.onClick.AddListener(() => { page = Mathf.Min(parts.Length - 1, page + 1); Show(); });
            Show();
        }
        // ---- Instructions before each round ----

        public static void Instructions(MiniGameDefinition game, ChildGame.Difficulty difficulty, Action onStart, Action onLeave)
        {
            GameObject root = null;
            var card = Ui.Modal("InstructionsScreen", new Vector2(1300, 860), () => { Screens.Close(root); onLeave(); }, out root);
            Ui.Label(card, game.titleKey, 64, Ui.Ink).Place(new Vector2(0.5f, 1f), new Vector2(1200, 110), new Vector2(0, -40));
            if (game.hasDifficulty)
            {
                var pill = Ui.Panel(card, "Difficulty", Ui.DifficultyColor(difficulty)).Place(new Vector2(0.5f, 1f), new Vector2(320, 70), new Vector2(0, -160));
                Ui.Label(pill.transform, Ui.DifficultyKey(difficulty), 36, Color.white).Stretch(Vector2.zero, Vector2.one);
            }
            Ui.Label(card, "instructions.title", 40, Ui.InkSoft).Place(new Vector2(0.5f, 1f), new Vector2(1200, 60), new Vector2(0, -260));
            // Each paragraph is its own page (owner 2026-10-08: long
            // instructions are split into parts so the buttons never cover them).
            var body = Ui.Text(card, "", 42, Ui.Ink, TextAlignmentOptions.Top, false);
            body.Stretch(new Vector2(0, 0.29f), new Vector2(1, 0.6f), 0);
            body.margin = new Vector4(70, 0, 70, 0); // padding at the sides only, so the page holds more lines
            Paginate(body, card, new Vector2(0.5f, 0.235f), Loc.Get(game.instructionsKey));

            Ui.Button(card, "common.back", () => { Screens.Close(root); onLeave(); }, Ui.InkSoft, 42)
                .Place(new Vector2(0f, 0f), new Vector2(380, 120), new Vector2(60, 50));
            Ui.Button(card, "instructions.start", () => { Screens.Close(root); onStart(); }, Ui.Leaf, 52)
                .Place(new Vector2(1f, 0f), new Vector2(520, 130), new Vector2(-60, 45));
            // Stories play once (owner 2026-10-08): the parts seen so far can be watched again from here.
            if (StoryBook.AnySeen(game, Game.Save.Record(game.id)))
                Ui.Button(card, "story.rewatch", () => StoryList(game), Ui.Sky, 34)
                    .Place(new Vector2(1f, 1f), new Vector2(320, 84), new Vector2(-36, -36));
        }

        /// <summary>The story parts of a mini game seen so far; tapping one plays it again.</summary>
        public static void StoryList(MiniGameDefinition game)
        {
            GameObject root = null;
            var parts = StoryBook.Parts(game, Game.Save.Record(game.id));
            var seen = parts.FindAll(p => p.seen);
            var card = Ui.Modal("StoryListScreen", new Vector2(900, 300 + 112 * seen.Count), () => Screens.Close(root), out root, 30);
            var col = Ui.Column(card, "Column", 16, 44);
            Ui.Label(col.transform, "story.rewatch", 54, Ui.Ink).Size(0, 84);
            foreach (var p in seen)
            {
                var keys = p.keys;
                Ui.Button(col.transform, p.titleKey, () =>
                {
                    Screens.Close(root);
                    Dialogue(keys, () => StoryList(game));
                }, Ui.Leaf, 40).Size(0, 96);
            }
            Ui.Button(col.transform, "story.close", () => Screens.Close(root), Ui.InkSoft, 40).Size(0, 96);
        }

        // ---- Result (design §6, §7) ----

        public static void Result(Outcome outcome, bool pointAwarded, MiniGameRecord record, IReadOnlyList<Achievement> newAchievements,
            Action onReplay, Action onMenu)
        {
            GameObject root = null;
            var card = Ui.Modal("ResultScreen", new Vector2(1100, 860), () => { Screens.Close(root); onMenu(); }, out root);
            var col = Ui.Column(card, "Column", 26, 50);
            var won = outcome == Outcome.Win;
            Ui.Label(col.transform, outcome == Outcome.Win ? "result.win" : outcome == Outcome.Draw ? "result.draw" : "result.lose", 90,
                outcome == Outcome.Win ? Ui.Leaf : outcome == Outcome.Draw ? Ui.Sky : Ui.Brick).Size(0, 130);
            Ui.Stars(col.transform, record, 90).Size(0, 100);
            if (pointAwarded) Ui.Label(col.transform, "result.point", 48, Ui.Ink).Size(0, 70);
            else if (won) Ui.Label(col.transform, "result.nopoint", 38, Ui.InkSoft, TextAlignmentOptions.Center, false).Size(0, 60);
            foreach (var a in newAchievements)
            {
                var line = Ui.Text(col.transform, "", 38, Ui.Ink).Size(0, 56);
                line.gameObject.AddComponent<LocText>().SetFormat("result.achievement", Loc.Get(a.titleKey));
            }
            if (newAchievements.Count > 0 && Achievements.AllDone(Game.Save))
            {
                // The last achievement: the player becomes "Trùm xóm" (owner 2026-10-08).
                Ui.Label(col.transform, "result.rank", 42, new Color32(200, 140, 0, 255)).Size(0, 64);
            }
            var row = Ui.Row(col.transform, "Buttons", 40).Size(0, 140);
            Ui.Button(row.transform, "result.menu", () => { Screens.Close(root); onMenu(); }, Ui.InkSoft, 44).Size(420, 130);
            Ui.Button(row.transform, "result.replay", () => { Screens.Close(root); onReplay(); }, Ui.Leaf, 48).Size(420, 130);
        }

        // ---- Special mini games (no difficulty, currency, daily limit) ----

        public static void SpecialStart(MiniGameDefinition game)
        {
            GameObject root = null;
            var card = Ui.Modal("SpecialScreen", new Vector2(1000, 640), () => Screens.Close(root), out root);
            var col = Ui.Column(card, "Column", 30, 50);
            Ui.Label(col.transform, game.titleKey, 68, Ui.Ink).Size(0, 110);
            Ui.Label(col.transform, "special.reward", 38, Ui.InkSoft, TextAlignmentOptions.Center, false).Size(0, 60);
            var left = game.dailyLimit > 0 ? DailyLimit.Left(Game.Save.Record(game.id), game.dailyLimit, DailyLimit.Today) : 1;
            if (game.dailyLimit > 0)
            {
                var info = Ui.Text(col.transform, "", 42, left > 0 ? Ui.Ink : Ui.Brick).Size(0, 70);
                info.gameObject.AddComponent<LocText>().SetFormat(left > 0 ? "special.left" : "special.none", left, game.dailyLimit);
            }
            var play = Ui.Button(col.transform, "special.play", () => { Screens.Close(root); SceneFlow.Play(game, ChildGame.Difficulty.Easy); }, Ui.Leaf, 50).Size(0, 120);
            play.interactable = left > 0;
            if (left <= 0) ((Image)play.targetGraphic).color = Ui.StarOff;
            Ui.Button(col.transform, "common.close", () => Screens.Close(root), Ui.InkSoft, 40).Size(0, 90);
        }

        public static void SpecialResult(MiniGameDefinition game, int count, int earned, int sessionsLeft, Action onReplay, Action onMenu)
        {
            GameObject root = null;
            var card = Ui.Modal("SpecialResult", new Vector2(1000, 760), () => { Screens.Close(root); onMenu(); }, out root);
            var col = Ui.Column(card, "Column", 26, 50);
            Ui.Label(col.transform, "special.done", 76, Ui.Leaf).Size(0, 120);
            var countText = Ui.Text(col.transform, "", 46, Ui.Ink).Size(0, 70);
            countText.gameObject.AddComponent<LocText>().SetFormat(game.id == "toc" ? "special.toc.count" : "special.count", count);
            var coins = Ui.Row(col.transform, "Coins", 20).Size(0, 90);
            Ui.Icon(coins.transform, "coin", Ui.Circle, Ui.StarOn, 64);
            var amount = Ui.Text(coins.transform, "", 60, new Color32(200, 140, 0, 255)).Size(420, 90);
            amount.textWrappingMode = TextWrappingModes.NoWrap;
            amount.gameObject.AddComponent<LocText>().SetFormat("special.earned", Shop.Money(earned));
            if (sessionsLeft >= 0)
            {
                var left = Ui.Text(col.transform, "", 34, Ui.InkSoft).Size(0, 50);
                left.gameObject.AddComponent<LocText>().SetFormat(sessionsLeft > 0 ? "special.left" : "special.none", sessionsLeft, game.dailyLimit);
            }
            var row = Ui.Row(col.transform, "Buttons", 40).Size(0, 140);
            Ui.Button(row.transform, "result.menu", () => { Screens.Close(root); onMenu(); }, Ui.InkSoft, 44).Size(420, 130);
            if (sessionsLeft != 0)
                Ui.Button(row.transform, "result.replay", () => { Screens.Close(root); onReplay(); }, Ui.Leaf, 48).Size(420, 130);
        }

        // ---- Settings ----

        public static void Settings()
        {
            GameObject root = null;
            var card = Ui.Modal("SettingsScreen", new Vector2(1000, 800), () => Screens.Close(root), out root);
            var col = Ui.Column(card, "Column", 28, 56);
            Ui.Label(col.transform, "settings.title", 64, Ui.Ink).Size(0, 100);
            Ui.Label(col.transform, "settings.language", 38, Ui.InkSoft).Size(0, 50);
            LanguageRow(col.transform);
            Ui.Label(col.transform, "settings.sfx", 38, Ui.InkSoft).Size(0, 50);
            Ui.Slider(col.transform, "slider.sfx", Game.Save.sfxVolume, v =>
            {
                Game.Save.sfxVolume = v;
            }).Size(0, 50);
#if UNITY_ANDROID && !UNITY_EDITOR
            // Development builds only: LevelPlay's ad diagnostics.
            if (Debug.isDebugBuild) Ui.Button(col.transform, "settings.adtest", Services.LevelPlayAds.LaunchTestSuite, Ui.InkSoft, 36).Size(0, 90);
#endif
            Ui.Button(col.transform, "settings.story", () => { Game.Commit(); Screens.Close(root); Hub.HubController.PlayIntro(); }, Ui.Sky, 40).Size(0, 100);
            Ui.Button(col.transform, "common.close", () => { Game.Commit(); Screens.Close(root); }, Ui.Leaf, 44).Size(0, 110);
        }

        // ---- Achievements (design §8) ----

        /// <summary>
        /// The player's profile (owner 2026-10-08, opened from the name card): name, boy / girl, points and money, the title
        /// "Trùm xóm" once every achievement is earned, and the achievements with what each needs.
        /// </summary>
        public static void Profile()
        {
            GameObject root = null;
            var card = Ui.Modal("ProfileScreen", new Vector2(1300, 980), () => Screens.Close(root), out root);
            var save = Game.Save;
            var name = Ui.Text(card, save.playerName, 64, Ui.Ink, TextAlignmentOptions.Left);
            name.Place(new Vector2(0f, 1f), new Vector2(700, 90), new Vector2(60, -36));
            Ui.Label(card, save.gender == Gender.Girl ? "profile.girl" : "profile.boy", 36, Ui.InkSoft, TextAlignmentOptions.Left)
                .Place(new Vector2(0f, 1f), new Vector2(400, 50), new Vector2(64, -126));
            // Change name / boy-girl (the web portal version starts without the welcome card, owner 2026-10-10).
            Ui.Button(card, "profile.edit", () => { Screens.Close(root); Profile(SceneFlow.ToHub); }, Ui.Sky, 30)
                .Place(new Vector2(0f, 1f), new Vector2(380, 64), new Vector2(290, -118));
            var points = Ui.Text(card, "", 38, Ui.Ink, TextAlignmentOptions.Right);
            points.Place(new Vector2(1f, 1f), new Vector2(520, 56), new Vector2(-60, -40));
            points.gameObject.AddComponent<LocText>().SetFormat("profile.points", Progress.TotalPoints(save), Hub.HubController.MaxPoints());
            var money = Ui.Text(card, "", 38, new Color32(200, 140, 0, 255), TextAlignmentOptions.Right);
            money.Place(new Vector2(1f, 1f), new Vector2(520, 56), new Vector2(-60, -100));
            money.gameObject.AddComponent<LocText>().SetFormat("profile.money", Shop.Money(save.currency));

            // The title: "Trùm xóm" once everything is earned.
            var all = Achievements.AllDone(save);
            var rank = Ui.Panel(card, "Rank", all ? new Color32(255, 214, 90, 255) : new Color32(240, 232, 214, 255)).Place(new Vector2(0.5f, 1f), new Vector2(1180, 80), new Vector2(0, -180));
            var rankText = Ui.Text(rank.transform, "", all ? 40 : 30, all ? Ui.Ink : Ui.InkSoft);
            rankText.Stretch(Vector2.zero, Vector2.one, 8);
            if (all) rankText.gameObject.AddComponent<LocText>().SetFormat("profile.rank", Loc.Get("rank.trumxom"));
            else rankText.gameObject.AddComponent<LocText>().Key = "profile.rank.none";

            var head = Ui.Text(card, "", 40, Ui.Ink);
            head.Place(new Vector2(0.5f, 1f), new Vector2(1100, 60), new Vector2(0, -280));
            head.gameObject.AddComponent<LocText>().SetFormat("profile.achievements", Achievements.Earned(save), Achievements.All.Count);
            var col = Ui.Column(card, "List", 10, 0);
            col.childAlignment = TextAnchor.UpperCenter;
            col.Stretch(new Vector2(0, 0.16f), new Vector2(1, 0.7f), 50);
            if (Achievements.All.Count == 0)
                Ui.Label(col.transform, "achievements.none", 40, Ui.InkSoft, TextAlignmentOptions.Center, false).Size(0, 80);
            foreach (var a in Achievements.All)
            {
                var done = save.HasAchievement(a.id);
                var row = Ui.Panel(col.transform, "row." + a.id, done ? new Color32(255, 248, 226, 255) : new Color(1, 1, 1, 0.45f)).Size(0, 70);
                Ui.Icon(row.transform, "star", Ui.Star, done ? Ui.StarOn : Ui.StarOff, 48).Place(new Vector2(0f, 0.5f), new Vector2(48, 48), new Vector2(20, 0));
                Ui.Label(row.transform, a.titleKey, 32, done ? Ui.Ink : Ui.InkSoft, TextAlignmentOptions.Left).Place(new Vector2(0f, 0.5f), new Vector2(620, 60), new Vector2(86, 0));
                Ui.Label(row.transform, a.descriptionKey, 26, Ui.InkSoft, TextAlignmentOptions.Right, false).Place(new Vector2(1f, 0.5f), new Vector2(440, 60), new Vector2(-24, 0));
            }
            Ui.Button(card, "common.close", () => Screens.Close(root), Ui.Leaf, 44).Place(new Vector2(0.5f, 0f), new Vector2(420, 110), new Vector2(0, 34));
        }

        // ---- Missing item: go to the shop ----

        /// <summary>
        /// Explains what is missing (paper, sandals, an item) with a button to the shop on that item. <paramref name="afterward"/>
        /// runs once every screen is closed again.
        /// </summary>
        public static void NeedItem(string messageKey, string itemId, Action afterward)
        {
            GameObject root = null;
            void Close()
            {
                Screens.Close(root);
                if (afterward != null) GameRunner.Instance.StartCoroutine(WhenNoScreens(afterward));
            }
            var card = Ui.Modal("NeedItem", new Vector2(980, 520), Close, out root, 35);
            var label = Ui.Text(card, "", 44, Ui.Ink);
            label.Stretch(new Vector2(0, 0.38f), Vector2.one, 50);
            label.gameObject.AddComponent<LocText>().SetFormat(messageKey, Game.Save.paper);
            Ui.Button(card, "need.toshop", () => { Close(); ShopScreens.Main(itemId); }, Ui.Leaf, 42)
                .Place(new Vector2(1f, 0f), new Vector2(440, 120), new Vector2(-60, 45));
            Ui.Button(card, "common.close", Close, Ui.InkSoft, 42)
                .Place(new Vector2(0f, 0f), new Vector2(300, 120), new Vector2(60, 45));
        }

        private static System.Collections.IEnumerator WhenNoScreens(Action action)
        {
            yield return null;
            while (Screens.Count > 0) yield return null;
            action();
        }

        // ---- A message with one OK button ----

        public static void Notice(string messageKey, Action closed = null)
        {
            GameObject root = null;
            void Close()
            {
                Screens.Close(root);
                closed?.Invoke();
            }
            var card = Ui.Modal("NoticeScreen", new Vector2(860, 440), Close, out root, 40);
            Ui.Label(card, messageKey, 46, Ui.Ink).Stretch(new Vector2(0, 0.38f), Vector2.one, 50);
            Ui.Button(card, "common.ok", Close, Ui.Leaf, 44)
                .Place(new Vector2(0.5f, 0f), new Vector2(360, 120), new Vector2(0, 45));
        }

        // ---- Yes / no ----

        public static void Confirm(string messageKey, Action onYes, Action onNo = null)
        {
            GameObject root = null;
            void No()
            {
                Screens.Close(root);
                onNo?.Invoke();
            }

            var card = Ui.Modal("ConfirmScreen", new Vector2(900, 520), No, out root, 40);
            Ui.Label(card, messageKey, 48, Ui.Ink).Stretch(new Vector2(0, 0.4f), Vector2.one, 50);
            Ui.Button(card, "common.no", No, Ui.InkSoft, 44)
                .Place(new Vector2(0f, 0f), new Vector2(340, 120), new Vector2(70, 50));
            Ui.Button(card, "common.yes", () => { Screens.Close(root); onYes(); }, Ui.Brick, 44)
                .Place(new Vector2(1f, 0f), new Vector2(340, 120), new Vector2(-70, 50));
        }
    }
}
