using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace TGame.ToolBox
{
    internal static class BuildProfileManager
    {
        private const string ProfilesRoot = "Assets/Settings/BuildProfiles";

        public static List<BuildProfile> GetAllProfiles()
        {
            EnsureDirectory();
            var guids = AssetDatabase.FindAssets($"t:{nameof(BuildProfile)}");
            var profiles = new List<BuildProfile>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var p = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
                if (p != null) profiles.Add(p);
            }
            return profiles;
        }

        public static BuildProfile CreateProfile(string name)
        {
            EnsureDirectory();
            var profile = ScriptableObject.CreateInstance<BuildProfile>();
            profile.name = name;

            var safeName = SanitizeFileName(name);
            var path = $"{ProfilesRoot}/{safeName}.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BuildProfileManager] created: {path}");
            return profile;
        }

        public static void DeleteProfile(BuildProfile profile)
        {
            if (profile == null) return;
            var path = AssetDatabase.GetAssetPath(profile);
            if (string.IsNullOrEmpty(path)) return;
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();
        }

        public static void RenameProfile(BuildProfile profile, string newName)
        {
            if (profile == null || string.IsNullOrEmpty(newName)) return;
            var path = AssetDatabase.GetAssetPath(profile);
            if (string.IsNullOrEmpty(path)) return;
            AssetDatabase.RenameAsset(path, SanitizeFileName(newName));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static string GetProfileGUID(BuildProfile profile)
        {
            if (profile == null) return null;
            var path = AssetDatabase.GetAssetPath(profile);
            return AssetDatabase.AssetPathToGUID(path);
        }

        public static BuildProfile FindByGUID(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
        }

        public static BuildProfile FindByName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return null;
            return GetAllProfiles().FirstOrDefault(p => p.name == displayName);
        }

        public static string GetDisplayName(BuildProfile profile)
        {
            return profile != null ? profile.name : "(null)";
        }

        public static string[] PlatformNames { get; } =
        {
            "Windows", "macOS", "Linux", "Android", "iOS", "WebGL"
        };

        private static void EnsureDirectory()
        {
            if (!AssetDatabase.IsValidFolder(ProfilesRoot))
            {
                var parent = Path.GetDirectoryName(ProfilesRoot);
                var dirName = Path.GetFileName(ProfilesRoot);
                if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                {
                    var parentParent = Path.GetDirectoryName(parent);
                    var parentName = Path.GetFileName(parent);
                    AssetDatabase.CreateFolder(parentParent, parentName);
                }
                AssetDatabase.CreateFolder(parent, dirName);
            }
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "UnnamedProfile" : sanitized;
        }
    }
}
