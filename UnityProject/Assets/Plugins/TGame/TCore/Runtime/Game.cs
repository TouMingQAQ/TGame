using System;
using System.Collections.Generic;
using UnityEngine;

namespace TGame.TCore.Runtime
{
    public partial class Game : MonoBehaviour
    {
        public static Game Instance;
        protected Dictionary<Type, BaseManager> _systemMap = new();
        
        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }

        public void AddManager<T>(T manager) where T : BaseManager
        {
            var type = typeof(T);
            _systemMap[type] = manager;
        }
        public T GetManager<T>() where T : BaseManager
        {
            var type = typeof(T);
            if (_systemMap.TryGetValue(type, out var value))
                return value as T;
            else
                Debug.LogError($"<color=red>[{GetType()}]</color> can`t find system:{type}");
            return default(T);
        }

        public static void LogInfo(string tag,string message,Color tagColor)
        {
            string color = ColorUtility.ToHtmlStringRGB(tagColor);
            string log = $"[<color=#{color}>{tag}</color>]:{message}";
            Debug.Log(log);
        }
    }
}
