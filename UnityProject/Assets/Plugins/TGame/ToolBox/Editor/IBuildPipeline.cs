using System;
using UnityEditor;
using UnityEditor.Build.Profile;

namespace TGame.ToolBox
{
    // ============================================================
    // Player Build Pipeline
    // ============================================================

    public class BuildPipelineContext
    {
        public BuildTarget buildTarget;
        public BuildTargetGroup buildTargetGroup;
        public string outputPath;
        public string productName;
        public bool developmentBuild;
        public bool autoconnectProfiler;
        public bool deepProfiling;
        public bool buildScriptsOnly;
        public bool cleanBuild;
        public string[] scenes;

        /// <summary> Unity 6 BuildProfile（可为 null，向下兼容）</summary>
        public BuildProfile profile;
    }

    public interface IBuildPipeline
    {
        bool Execute(BuildPipelineContext ctx);
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class BuildPipelineAttribute : Attribute
    {
        public string Name { get; }
        public BuildPipelineAttribute(string name) => Name = name;
    }

    // ============================================================
    // AssetBundle Build Pipeline
    // ============================================================

    public class ABBuildPipelineContext
    {
        public string outputPath;
        public BuildTarget buildTarget;
        public bool fullRebuild;
    }

    public interface IABBuildPipeline
    {
        bool Execute(ABBuildPipelineContext ctx);
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class ABBuildPipelineAttribute : Attribute
    {
        public string Name { get; }
        public ABBuildPipelineAttribute(string name) => Name = name;
    }
}
