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
}
