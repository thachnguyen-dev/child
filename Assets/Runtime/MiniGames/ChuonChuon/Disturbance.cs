using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace ChildGame.MiniGames.ChuonChuon
{
    /// <summary>
    /// Real-world disturbances that scare the dragonfly (owner 2026-10-07):
    /// <list type="bullet">
    /// <item>Shaking the phone: the accelerometer's change from the slowly tracked gravity vector.</item>
    /// <item>Loud sound: microphone loudness above the calibrated room level. Android only, and only when the player
    /// grants microphone permission; otherwise sound is ignored (the web build has no microphone).</item>
    /// </list>
    /// ⚠ Thresholds are placeholders.
    /// </summary>
    public class Disturbance : MonoBehaviour
    {
        public float shakeThreshold = 0.28f;     // in g, after removing gravity
        public float loudFactor = 4f;            // × the room level
        public float loudFloor = 0.06f;          // minimum RMS that counts as loud

        private Vector3 gravity;
        private bool hasGravity;
        private AudioClip mic;
        private string micDevice;
        private float roomLevel = -1f, calibrateUntil;
        private readonly float[] samples = new float[512];

        public float Shake { get; private set; }
        public float Loudness { get; private set; }
        public bool MicActive => mic != null;
        /// <summary>True on a frame where the phone shook or it got loud.</summary>
        public bool Disturbed { get; private set; }
        public string Reason { get; private set; }

        private static bool micAsked;

        private void OnEnable()
        {
            if (Accelerometer.current != null) InputSystem.EnableDevice(Accelerometer.current);
            hasGravity = false;
            StartMicrophone();
        }

        private void OnDisable()
        {
#if !UNITY_WEBGL
            if (mic != null && Microphone.IsRecording(micDevice)) Microphone.End(micDevice);
#endif
            mic = null;
        }

        private void StartMicrophone()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                if (micAsked) return; // asked once; a refusal means sound is ignored
                micAsked = true;
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => { if (isActiveAndEnabled) StartMicrophone(); };
                Permission.RequestUserPermission(Permission.Microphone, callbacks);
                return;
            }
            if (Microphone.devices.Length == 0) return;
            micDevice = Microphone.devices[0];
            mic = Microphone.Start(micDevice, true, 1, 16000);
            roomLevel = -1f;
            calibrateUntil = Time.unscaledTime + 1f;
#endif
        }

        private void Update()
        {
            Disturbed = false;
            Reason = null;

            var acc = Accelerometer.current;
            if (acc != null && acc.enabled)
            {
                var a = acc.acceleration.ReadValue();
                if (!hasGravity)
                {
                    gravity = a;
                    hasGravity = true;
                }
                gravity = Vector3.Lerp(gravity, a, 0.08f);
                Shake = (a - gravity).magnitude;
                if (Shake > shakeThreshold)
                {
                    Disturbed = true;
                    Reason = "shake";
                }
            }

#if !UNITY_WEBGL
            if (mic != null)
            {
                var pos = Microphone.GetPosition(micDevice) - samples.Length;
                if (pos < 0) pos += mic.samples;
                mic.GetData(samples, pos);
                var sum = 0f;
                foreach (var s in samples) sum += s * s;
                Loudness = Mathf.Sqrt(sum / samples.Length);
                if (Time.unscaledTime < calibrateUntil)
                    roomLevel = roomLevel < 0 ? Loudness : Mathf.Max(roomLevel, Loudness);
                else if (Loudness > Mathf.Max(loudFloor, roomLevel * loudFactor))
                {
                    Disturbed = true;
                    Reason = "sound";
                }
            }
#endif
        }
    }
}
