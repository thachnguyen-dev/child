#if UNITY_IOS
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace ChildGame.EditorTools
{
    /// <summary>
    /// Makes the Windows-exported Xcode project work on the Mac the first time (docs/7-ios-release.md). Runs after Unity,
    /// LevelPlay and the External Dependency Manager have written the export:
    /// <list type="bullet">
    /// <item>Podfile: only the CocoaPods CDN as source, <c>platform :ios</c> = the Player Settings minimum, and a
    /// <c>post_install</c> hook giving every pod target that same deployment target.</item>
    /// <item>Info.plist: no non-exempt encryption (skips the export-compliance question); English and Vietnamese
    /// localisations with the Vietnamese home-screen name "Những ngày thơ ấu".</item>
    /// <item><c>mac-setup.sh</c> (LF line endings): restores execute bits lost in a Windows zip, removes the quarantine,
    /// checks Homebrew and CocoaPods, runs <c>pod install --repo-update</c> and opens the workspace.</item>
    /// </list>
    /// </summary>
    public static class IosExport
    {
        private const string CdnSource = "source 'https://cdn.cocoapods.org/'";

        /// <summary>The App Tracking Transparency prompt's explanation, English then Vietnamese.</summary>
        private static readonly (string lang, string text)[] TrackingText =
        {
            ("en", "Your answer only changes which ads you see when you watch one for đồng in the shop. The game itself collects nothing."),
            ("vi", "Lựa chọn của bạn chỉ thay đổi loại quảng cáo bạn thấy khi xem quảng cáo đổi đồng trong cửa hàng. Bản thân trò chơi không thu thập gì."),
        };

        [PostProcessBuild(1000)] // after the External Dependency Manager writes the Podfile (orders 10–50)
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var version = PlayerSettings.iOS.targetOSVersionString;
            FixPodfile(path, version);
            FixPlist(path);
            AddLocalizedName(path);
            WriteMacSetup(path);
            Debug.Log($"[CHILD-IOS] Export prepared for the Mac: iOS {version}, Podfile fixed, mac-setup.sh written.");
        }

        private static void FixPodfile(string path, string version)
        {
            var podfile = Path.Combine(path, "Podfile");
            if (!File.Exists(podfile))
            {
                Debug.LogError("[CHILD-IOS] No Podfile in the export: the External Dependency Manager did not generate one.");
                return;
            }
            var text = File.ReadAllText(podfile).Replace("\r\n", "\n");
            // One source only: the CDN. The GitHub Specs repo is a multi-gigabyte clone and duplicates every spec.
            text = Regex.Replace(text, @"^\s*source\s+'[^']*'\s*\n", "", RegexOptions.Multiline);
            text = CdnSource + "\n" + text;
            // The platform line equals the Player Settings minimum.
            text = Regex.IsMatch(text, @"^\s*platform\s+:ios", RegexOptions.Multiline)
                ? Regex.Replace(text, @"^\s*platform\s+:ios.*$", $"platform :ios, '{version}'", RegexOptions.Multiline)
                : $"platform :ios, '{version}'\n" + text;
            // One post_install hook (CocoaPods allows only one): every pod target builds for the same minimum.
            if (!text.Contains("IPHONEOS_DEPLOYMENT_TARGET"))
            {
                var hook = new StringBuilder();
                hook.Append("\npost_install do |installer|\n");
                hook.Append("  installer.pods_project.targets.each do |t|\n");
                hook.Append("    t.build_configurations.each do |config|\n");
                hook.Append($"      config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '{version}'\n");
                hook.Append("    end\n  end\nend\n");
                if (Regex.IsMatch(text, @"^post_install do", RegexOptions.Multiline))
                    Debug.LogError("[CHILD-IOS] The Podfile already has a post_install hook; merge the deployment target into it by hand.");
                else text = text.TrimEnd('\n') + "\n" + hook;
            }
            File.WriteAllText(podfile, text, new UTF8Encoding(false));
        }

        private static void FixPlist(string path)
        {
            var plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false); // only standard HTTPS: no export compliance documents
            root.SetString("NSUserTrackingUsageDescription", TrackingText[0].text); // the ATT prompt (Plugins/iOS/ChildAtt.mm)
            var langs = root.CreateArray("CFBundleLocalizations");
            langs.AddString("en");
            langs.AddString("vi");
            plist.WriteToFile(plistPath);
        }

        /// <summary>The home-screen name per language: "Childhood Days" / "Những ngày thơ ấu".</summary>
        private static void AddLocalizedName(string path)
        {
            var projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            var main = proj.GetUnityMainTargetGuid();
            proj.AddFrameworkToProject(proj.GetUnityFrameworkTargetGuid(), "AppTrackingTransparency.framework", false);
            foreach (var (lang, name) in new[] { ("en", "Childhood Days"), ("vi", "Những ngày thơ ấu") })
            {
                var folder = lang + ".lproj";
                var tracking = lang == "vi" ? TrackingText[1].text : TrackingText[0].text;
                Directory.CreateDirectory(Path.Combine(path, folder));
                File.WriteAllText(Path.Combine(path, folder, "InfoPlist.strings"),
                    $"\"CFBundleDisplayName\" = \"{name}\";\n\"CFBundleName\" = \"{name}\";\n" +
                    $"\"NSUserTrackingUsageDescription\" = \"{tracking}\";\n", new UTF8Encoding(false));
                if (proj.FindFileGuidByProjectPath(folder) == null)
                    proj.AddFileToBuild(main, proj.AddFolderReference(folder, folder));
            }
            proj.WriteToFile(projPath);
        }

        private static void WriteMacSetup(string path)
        {
            // The script lives in Editor/Templates/mac-setup.sh; the copy is always written with Unix (LF) line endings.
            var template = Path.Combine(Application.dataPath, "Child/Code/Editor/Templates/mac-setup.sh");
            var script = File.ReadAllText(template);
            File.WriteAllText(Path.Combine(path, "mac-setup.sh"), script.Replace("\r\n", "\n"), new UTF8Encoding(false));
        }
    }
}
#endif
