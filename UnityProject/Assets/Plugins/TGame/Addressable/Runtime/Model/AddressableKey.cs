using System;

namespace TGame.Addressable
{
    /// <summary>
    /// 资源定位的复合键:资源类型 + Addressables 地址/label。
    /// 以 AssetType=null + Key 为纯 key 模式(用于 label 预热、string 查找等)。
    /// </summary>
    public readonly struct AddressableKey : IEquatable<AddressableKey>
    {
        /// <summary>资源类型。null 表示纯字符串 key(如 label 预热场景)</summary>
        public readonly Type AssetType;

        /// <summary>Addressables address 或 label 字符串</summary>
        public readonly string Key;

        public AddressableKey(Type assetType, string key)
        {
            AssetType = assetType;
            Key = key ?? throw new ArgumentNullException(nameof(key));
        }

        public AddressableKey(string key) : this(null, key) { }

        public bool Equals(AddressableKey other)
            => AssetType == other.AssetType && Key == other.Key;

        public override bool Equals(object obj)
            => obj is AddressableKey other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(AssetType, Key);

        public static bool operator ==(AddressableKey left, AddressableKey right)
            => left.Equals(right);

        public static bool operator !=(AddressableKey left, AddressableKey right)
            => !left.Equals(right);

        public override string ToString()
            => AssetType != null ? $"[{AssetType.Name}]{Key}" : Key;
    }
}
