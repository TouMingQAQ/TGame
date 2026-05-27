using System.Collections.Generic;
using UnityEngine;

namespace TGame.SceneNavigator
{
    [CreateAssetMenu(fileName = "SceneNavigatorProfile", menuName = "TGame/Scene Navigator")]
    public class SceneNavigatorProfile : ScriptableObject
    {
        public List<SceneEntry> scenes = new();
    }
}
