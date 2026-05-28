using System.Collections.Generic;
using UnityEngine;

namespace TGame.ToolBox
{
    [CreateAssetMenu(fileName = "ColorLibrary", menuName = "TGame/Color Library")]
    public class ColorLibrary : ScriptableObject
    {
        public List<ColorEntry> Entries = new();
    }
}
