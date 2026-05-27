using System;

namespace TGame.SceneNavigator
{
    [Serializable]
    public class SceneEntry
    {
        [ScenePath]
        public string scenePath;

        public string alias;
    }
}
