using System;

namespace TGame.Addressable
{
    /// <summary>单资源加载完成事件,通过 BaseManager.Call 广播</summary>
    public readonly struct AddressableLoadCompletedEvent
    {
        public readonly string Key;
        public readonly Type AssetType;
        public readonly bool Success;
        public readonly float DurationMs;

        public AddressableLoadCompletedEvent(string key, Type assetType, bool success, float durationMs)
        {
            Key = key;
            AssetType = assetType;
            Success = success;
            DurationMs = durationMs;
        }
    }

    /// <summary>资源释放事件(引用计数归零时)</summary>
    public readonly struct AddressableReleasedEvent
    {
        public readonly string Key;
        public readonly Type AssetType;

        public AddressableReleasedEvent(string key, Type assetType)
        {
            Key = key;
            AssetType = assetType;
        }
    }

    /// <summary>批量预热进度事件</summary>
    public readonly struct AddressablePreloadProgressEvent
    {
        public readonly string LabelOrContext;
        public readonly int Completed;
        public readonly int Total;
        public readonly float Percent; // 0.0..1.0

        public AddressablePreloadProgressEvent(string context, int completed, int total)
        {
            LabelOrContext = context;
            Completed = completed;
            Total = total;
            Percent = total > 0 ? (float)completed / total : 1f;
        }
    }

    /// <summary>批量预热完成事件</summary>
    public readonly struct AddressablePreloadCompletedEvent
    {
        public readonly string LabelOrContext;
        public readonly int TotalCount;
        public readonly float TotalDurationMs;

        public AddressablePreloadCompletedEvent(string context, int total, float durationMs)
        {
            LabelOrContext = context;
            TotalCount = total;
            TotalDurationMs = durationMs;
        }
    }

    /// <summary>容器加载完成事件(全部 address 加载完或部分失败/取消)</summary>
    public readonly struct AddressableContainerLoadedEvent
    {
        public readonly string ContainerName;
        public readonly int TotalRequested;
        public readonly int Succeeded;
        public readonly float DurationMs;

        public AddressableContainerLoadedEvent(string name, int total, int succeeded, float durationMs)
        {
            ContainerName = name;
            TotalRequested = total;
            Succeeded = succeeded;
            DurationMs = durationMs;
        }
    }

    /// <summary>容器卸载完成事件</summary>
    public readonly struct AddressableContainerUnloadedEvent
    {
        public readonly string ContainerName;
        public readonly int ReleasedCount;
        public readonly float DurationMs;

        public AddressableContainerUnloadedEvent(string name, int released, float durationMs)
        {
            ContainerName = name;
            ReleasedCount = released;
            DurationMs = durationMs;
        }
    }
}
