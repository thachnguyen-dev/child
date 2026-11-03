using System.Collections.Generic;
using System.IO;
using ChildGame.UI;
using UnityEditor;
using UnityEngine;

namespace ChildGame.EditorTools
{
    /// <summary>
    /// Bakes the hand art to PNG sprites in Resources/Art/Hands (official art) — every pose seen from the back of the hand,
    /// and the rock-paper-scissors poses also from the palm side — and a contact sheet in logs/hands-sheet.png for review
    /// (rows: player back, opponent back, player palm, opponent palm). Runs as part of <see cref="ChildSetup.Configure"/>.
    /// </summary>
    public static class HandArtBaker
    {
        private const string Folder = "Assets/Child/Resources/Art/Hands";

        [MenuItem("Child/Developer/Bake Hand Art")]
        public static void Bake()
        {
            Directory.CreateDirectory(Folder);
            var poses = (HandArt.Pose[])System.Enum.GetValues(typeof(HandArt.Pose));
            var whos = (HandArt.Who[])System.Enum.GetValues(typeof(HandArt.Who));
            var sheet = new Texture2D(512 * poses.Length, 832 * 4, TextureFormat.RGBA32, false);
            var bg = new Color32[sheet.width * sheet.height];
            for (var i = 0; i < bg.Length; i++) bg[i] = new Color32(214, 236, 248, 255);
            sheet.SetPixels32(bg);
            var written = new List<string>();
            foreach (var palm in new[] { false, true })
            foreach (var who in whos)
                for (var col = 0; col < poses.Length; col++)
                {
                    var pose = poses[col];
                    if (palm && !HandArt.HasPalm(pose)) continue;
                    var tex = HandArt.RenderTexture(pose, who, palm);
                    var path = $"Assets/Child/Resources/{HandArt.ResourceName(pose, who, palm)}.png";
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    written.Add(path);
                    var px = tex.GetPixels();
                    var ox = col * 512;
                    var oy = ((palm ? 2 : 0) + (int)who) * 832;
                    for (var y = 0; y < tex.height; y++)
                    for (var x = 0; x < tex.width; x++)
                    {
                        var c = px[y * tex.width + x];
                        var under = sheet.GetPixel(ox + x, oy + y);
                        sheet.SetPixel(ox + x, oy + y, Color.Lerp(under, new Color(c.r, c.g, c.b, 1), c.a));
                    }
                }
            sheet.Apply();
            AssetDatabase.Refresh();
            foreach (var path in written)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                importer.SetTextureSettings(settings);
                importer.spritePixelsPerUnit = 100;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            var logs = Path.GetFullPath("../logs");
            Directory.CreateDirectory(logs);
            File.WriteAllBytes(Path.Combine(logs, "hands-sheet.png"), sheet.EncodeToPNG());
            Debug.Log("[CHILD-SETUP] Hand art baked.");
        }
    }
}
