using ChildGame.UI;
using TMPro;
using UnityEngine;

namespace ChildGame.MiniGames
{
    /// <summary>
    /// ⚠ TEST-ONLY placeholder, not a real mini game: tap the moving ball enough times before time runs out. It exists
    /// to exercise the shared flow (story, instructions, result, points) until the owner's mini game list arrives.
    /// The numbers below are placeholders.
    /// </summary>
    public class PlaceholderTapGame : MiniGame
    {
        [SerializeField] private float seconds = 10f;
        [SerializeField] private int[] tapsNeeded = { 6, 10, 14 };

        private Transform ball;
        private float timeLeft;
        private int taps;
        private TextMeshProUGUI timeText, countText;

        protected override void OnBegin()
        {
            timeLeft = seconds;
            taps = 0;
            if (ball == null) BuildBall();
            ball.gameObject.SetActive(true);
            MoveBall();
            var safe = Ui.Canvas("PlaceholderHud", 8);
            timeText = Ui.Text(safe, "", 64, Ui.Ink).Place(new Vector2(0.5f, 1f), new Vector2(600, 90), new Vector2(0, -30));
            countText = Ui.Text(safe, "", 48, Ui.InkSoft).Place(new Vector2(0.5f, 1f), new Vector2(600, 70), new Vector2(0, -120));
            Refresh();
        }

        protected override void OnEnd()
        {
            ball.gameObject.SetActive(false);
            Destroy(timeText.canvas.gameObject);
        }

        private void BuildBall()
        {
            var go = new GameObject("Ball");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Ui.Circle;
            sr.color = Ui.Brick;
            go.transform.localScale = Vector3.one * 1.6f;
            go.AddComponent<CircleCollider2D>().radius = 0.6f; // a little larger than the art: forgiving for small fingers
            go.AddComponent<Tappable>().Pressed += _ => Tap();
            ball = go.transform;
        }

        private void Tap()
        {
            if (!Running || Time.timeScale == 0f) return;
            taps++;
            Refresh();
            if (taps >= Needed) Finish(true);
            else MoveBall();
        }

        private int Needed => tapsNeeded[(int)Difficulty];

        private void MoveBall()
        {
            var cam = Camera.main;
            var h = cam.orthographicSize * 0.65f;
            var w = h * cam.aspect;
            ball.position = new Vector3(Random.Range(-w, w), Random.Range(-h, h * 0.8f), 0);
        }

        private void Update()
        {
            if (!Running) return;
            timeLeft -= Time.deltaTime;
            Refresh();
            if (timeLeft <= 0) Finish(false);
        }

        private void Refresh()
        {
            if (timeText == null) return;
            timeText.text = Mathf.CeilToInt(Mathf.Max(0, timeLeft)).ToString();
            countText.text = $"{taps} / {Needed}";
        }
    }
}
