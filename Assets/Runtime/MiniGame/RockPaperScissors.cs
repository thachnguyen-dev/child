using System;
using System.Collections;
using ChildGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChildGame
{
    /// <summary>
    /// Oẳn tù tì: the shared sub-game that decides who moves first in turn-based mini games (owner 2026-10-07).
    /// Two real arms face each other on screen (owner 2026-10-08): the opponent's from the top, the player's from the
    /// bottom. The player picks a hand, both fists pump through the chant, then both hands open at once. A tie is played
    /// again; the winner moves first.
    /// </summary>
    public static class RockPaperScissors
    {
        public enum Hand { Rock = 0, Paper = 1, Scissors = 2 }

        /// <summary>1 if <paramref name="a"/> beats <paramref name="b"/>, -1 if it loses, 0 for a tie.</summary>
        public static int Compare(Hand a, Hand b)
        {
            if (a == b) return 0;
            return ((int)a - (int)b + 3) % 3 == 1 ? 1 : -1;
        }

        public static string HandKey(Hand h) => "rps." + h.ToString().ToLowerInvariant();

        public static HandArt.Pose PoseOf(Hand h) => h == Hand.Rock ? HandArt.Pose.Fist : h == Hand.Paper ? HandArt.Pose.Paper : HandArt.Pose.Scissors;

        /// <summary>Shows the sub-game; <paramref name="done"/> receives true when the player moves first.</summary>
        public static void Show(Action<bool> done, System.Random rng = null, string opponentKey = "rps.opponent")
        {
            rng ??= new System.Random();
            var safe = Ui.Canvas("RockPaperScissors", 25);
            var root = safe.root.gameObject;
            Screens.Push(root, SceneFlow.ToHub);

            // A soft table-top backdrop over the game.
            var backdrop = Ui.Panel(safe.parent, "Backdrop", new Color32(214, 236, 248, 255), false);
            backdrop.transform.SetAsFirstSibling();

            // The two arms. Sprites are 512×832 with the pivot at the wrist end (bottom centre).
            var opponent = Arm(safe, HandArt.Who.Opponent, new Vector2(0.56f, 1f), 180f);
            var player = Arm(safe, HandArt.Who.Player, new Vector2(0.4f, 0f), 0f);
            Ui.Label(safe, opponentKey, 32, Ui.InkSoft, TextAlignmentOptions.Left).Place(new Vector2(0.56f, 1f), new Vector2(400, 50), new Vector2(340, -40));
            Ui.Text(safe, Game.Save.playerName, 32, Ui.InkSoft, TextAlignmentOptions.Left).Place(new Vector2(0.4f, 0f), new Vector2(400, 50), new Vector2(340, 40));

            var title = Ui.Label(safe, "rps.title", 64, Ui.Ink, TextAlignmentOptions.Left);
            title.Place(new Vector2(0f, 0.5f), new Vector2(560, 100), new Vector2(60, 150));
            var chant = Ui.Label(safe, "rps.choose", 44, Ui.InkSoft, TextAlignmentOptions.Left);
            chant.Place(new Vector2(0f, 0.5f), new Vector2(560, 160), new Vector2(60, 10));
            var verdict = Ui.Text(safe, "", 52, Ui.Leaf, TextAlignmentOptions.Left);
            verdict.Place(new Vector2(0f, 0.5f), new Vector2(560, 160), new Vector2(60, -150));

            // Choices on the right, each showing the hand shape.
            var buttons = new Button[3];
            var column = Ui.Column(safe, "Choices", 22, 0);
            column.Place(new Vector2(1f, 0.5f), new Vector2(360, 690), new Vector2(-50, 0));
            foreach (Hand h in Enum.GetValues(typeof(Hand)))
            {
                var hand = h;
                var b = Ui.Button(column.transform, HandKey(h), null, Ui.Sun, 40).Size(0, 215);
                var label = b.GetComponentInChildren<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Right;
                label.margin = new Vector4(0, 0, 26, 0);
                var icon = Ui.Rect(b.transform, "Icon").gameObject.AddComponent<Image>();
                icon.sprite = HandArt.Get(PoseOf(h), HandArt.Who.Player);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                ((RectTransform)icon.transform).Place(new Vector2(0f, 0f), new Vector2(150, 244), new Vector2(18, 0));
                b.onClick.AddListener(() => GameRunner.Instance.StartCoroutine(Play(hand)));
                buttons[(int)h] = b;
            }

            IEnumerator Play(Hand pick)
            {
                foreach (var b in buttons) b.interactable = false;
                verdict.text = "";
                var ai = (Hand)rng.Next(3);
                // "Oẳn – tù – tì!": both fists pump three times, then "Ra cái gì, ra cái này!" and both hands open.
                var beats = new[] { "rps.chant.1", "rps.chant.2", "rps.chant.3" };
                foreach (var beat in beats)
                {
                    chant.GetComponent<LocText>().Key = beat;
                    yield return Pump(player, opponent, 0.42f);
                }
                chant.GetComponent<LocText>().Key = "rps.chant.4";
                yield return Pump(player, opponent, 0.5f);
                if (root == null) yield break;
                SetPose(player, PoseOf(pick), HandArt.Who.Player);
                SetPose(opponent, PoseOf(ai), HandArt.Who.Opponent);
                yield return Pop(player.transform, opponent.transform);
                var result = Compare(pick, ai);
                chant.GetComponent<LocText>().SetFormat("rps.reveal", Loc.Get(HandKey(pick)), Loc.Get(HandKey(ai)));
                verdict.text = Loc.Get(result > 0 ? "rps.youfirst" : result < 0 ? "rps.aifirst" : "rps.tie");
                verdict.color = result > 0 ? Ui.Leaf : result < 0 ? Ui.Brick : Ui.Sky;
                yield return new WaitForSeconds(result == 0 ? 1.3f : 1.7f);
                if (root == null) yield break;
                if (result == 0)
                {
                    SetPose(player, HandArt.Pose.Fist, HandArt.Who.Player);
                    SetPose(opponent, HandArt.Pose.Fist, HandArt.Who.Opponent);
                    chant.GetComponent<LocText>().Key = "rps.choose";
                    verdict.text = "";
                    foreach (var b in buttons) b.interactable = true;
                    yield break;
                }
                Screens.Close(root);
                done?.Invoke(result > 0);
            }
        }

        private static Image Arm(RectTransform parent, HandArt.Who who, Vector2 anchor, float rotation)
        {
            var img = Ui.Rect(parent, who + "Arm").gameObject.AddComponent<Image>();
            img.sprite = HandArt.Get(HandArt.Pose.Fist, who, who == HandArt.Who.Opponent);
            img.raycastTarget = false;
            var rt = (RectTransform)img.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(512, 832) * 0.86f;
            // The sleeve runs off the screen edge.
            rt.anchoredPosition = new Vector2(0, rotation == 0f ? -40f : 40f);
            rt.localRotation = Quaternion.Euler(0, 0, rotation);
            return img;
        }

        /// <summary>You see the back of your own hand, and the palm of the opponent's hand facing you.</summary>
        private static void SetPose(Image arm, HandArt.Pose pose, HandArt.Who who) => arm.sprite = HandArt.Get(pose, who, who == HandArt.Who.Opponent);

        /// <summary>One beat of the chant: both arms swing in toward each other and back.</summary>
        private static IEnumerator Pump(Image player, Image opponent, float seconds)
        {
            var p = (RectTransform)player.transform;
            var o = (RectTransform)opponent.transform;
            var p0 = p.anchoredPosition;
            var o0 = o.anchoredPosition;
            for (var t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds)
            {
                if (p == null) yield break;
                var lift = Mathf.Sin(t * Mathf.PI) * 70f;
                p.anchoredPosition = p0 + new Vector2(0, lift);
                o.anchoredPosition = o0 - new Vector2(0, lift);
                yield return null;
            }
            if (p == null) yield break;
            p.anchoredPosition = p0;
            o.anchoredPosition = o0;
        }

        /// <summary>The reveal: both hands pop out a little.</summary>
        private static IEnumerator Pop(Transform a, Transform b)
        {
            for (var t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.25f)
            {
                if (a == null) yield break;
                var s = 1f + Mathf.Sin(t * Mathf.PI) * 0.1f;
                a.localScale = b.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (a == null) yield break;
            a.localScale = b.localScale = Vector3.one;
        }
    }
}
