using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChildGame
{
    /// <summary>
    /// Local JSON save in <c>Application.persistentDataPath</c>. Writes go to a temp file first and the previous save is
    /// kept as <c>.bak</c>, so a crash mid-write never loses progress.
    /// </summary>
    public static class SaveStore
    {
        /// <summary>The file name; the capture harness swaps it so it never touches the real save.</summary>
        public static string FileName = "save.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

#if UNITY_WEBGL && !UNITY_EDITOR
        // The browser build keeps the save in PlayerPrefs (IndexedDB), which Unity flushes reliably on Save().
        private static string Key => "child." + FileName;

        /// <summary>True when no save existed at start (a first visit).</summary>
        public static bool FreshStart { get; private set; }

        public static SaveData Load()
        {
#if CRAZYGAMES
            // CrazyGames: the Data module is the save (it follows the player's account across devices). Browser storage
            // is only used when the SDK is blocked, e.g. by an ad blocker.
            if (Services.CrazySdk.Ready)
            {
                var cloud = TryParse(Services.CrazySdk.ChildCGGetItem(Key));
                FreshStart = cloud == null;
                return cloud ?? new SaveData();
            }
#endif
            var local = TryParse(PlayerPrefs.GetString(Key, null)) ?? TryParse(PlayerPrefs.GetString(Key + ".bak", null));
            FreshStart = local == null;
            return local ?? new SaveData();
        }

        public static void Write(SaveData data)
        {
#if CRAZYGAMES
            if (Services.CrazySdk.Ready && Services.CrazySdk.ChildCGSetItem(Key, ToJson(data)) == 1) return;
#endif
            if (PlayerPrefs.HasKey(Key)) PlayerPrefs.SetString(Key + ".bak", PlayerPrefs.GetString(Key));
            PlayerPrefs.SetString(Key, ToJson(data));
            PlayerPrefs.Save();
        }

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(Key + ".bak");
            PlayerPrefs.Save();
        }

        private static SaveData TryParse(string json)
        {
            try { return string.IsNullOrEmpty(json) ? null : FromJson(json); }
            catch (Exception e) { Debug.LogWarning($"[CHILD-SAVE] Could not read the save: {e.Message}"); return null; }
        }
#else
        public static SaveData Load() => TryRead(FilePath) ?? TryRead(FilePath + ".bak") ?? new SaveData();

        public static void Write(SaveData data)
        {
            try
            {
                var path = FilePath;
                var temp = path + ".tmp";
                File.WriteAllText(temp, ToJson(data));
                if (File.Exists(path)) File.Copy(path, path + ".bak", true);
                File.Copy(temp, path, true);
                File.Delete(temp);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CHILD-SAVE] Write failed: {e.Message}");
            }
        }

        public static void Delete()
        {
            foreach (var p in new[] { FilePath, FilePath + ".bak", FilePath + ".tmp" })
                if (File.Exists(p)) File.Delete(p);
        }
#endif

        public static string ToJson(SaveData data) => JsonUtility.ToJson(data, true);

        public static SaveData FromJson(string json)
        {
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null) return null;
            Upgrade(data);
            return data;
        }

#if !(UNITY_WEBGL && !UNITY_EDITOR)
        private static SaveData TryRead(string path)
        {
            try
            {
                return File.Exists(path) ? FromJson(File.ReadAllText(path)) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CHILD-SAVE] Could not read {path}: {e.Message}");
                return null;
            }
        }
#endif

        /// <summary>Repairs older or damaged data. Version migrations go here.</summary>
        private static void Upgrade(SaveData data)
        {
            data.games ??= new List<MiniGameRecord>();
            data.achievements ??= new List<string>();
            data.owned ??= new List<string>();
            data.adsToday ??= new MiniGameRecord { id = "ads" };
            data.grantedOrders ??= new List<string>();
            if (data.version < 2)
            {
                // Saves from before the shop: give the new-game starting kit.
                data.paper = 1;
                data.sandalPairs = 1;
            }
            foreach (var r in data.games)
            {
                r.flags ??= new List<string>();
                if (r.wins != null && r.wins.Length == 3) continue;
                var wins = new bool[3];
                if (r.wins != null) Array.Copy(r.wins, wins, Math.Min(3, r.wins.Length));
                r.wins = wins;
            }
            data.version = SaveData.CurrentVersion;
        }
    }
}
