using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TGame.ToolBox
{
    [Serializable]
    public class PlatformBuildNumber
    {
        public string platformName;
        public int buildNumber;
    }

    [CreateAssetMenu(fileName = "BuildConfig", menuName = "TGame/Build Config")]
    public class BuildConfig : ScriptableObject
    {
        // --- Player build settings ---
        public string outputPath = "Builds";
        public bool developmentBuild;
        public bool autoconnectProfiler;
        public bool deepProfiling;
        public bool buildScriptsOnly;
        public bool cleanBuild;

        // --- AB build settings ---
        public string abOutputPath = "Builds/AssetBundles";
        public bool abFullRebuild;

        // --- Player pipeline selection ---
        public bool useCustomPipeline;
        public string selectedPipelineTypeName;

        // --- AB pipeline selection ---
        public bool useCustomABPipeline;
        public string selectedABPipelineTypeName;

        // --- Standalone/other build numbers ---
        public List<PlatformBuildNumber> platformBuildNumbers = new();

        // --- BuildProfile 会话持久化（Editor 重启后恢复上次选中的 Profile） ---
        public string lastProfileGUID;
    }
}
