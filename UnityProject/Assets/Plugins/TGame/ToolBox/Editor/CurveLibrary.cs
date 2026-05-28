using System.Collections.Generic;
using UnityEngine;

namespace TGame.ToolBox
{
    [CreateAssetMenu(fileName = "CurveLibrary", menuName = "TGame/Curve Library")]
    public class CurveLibrary : ScriptableObject
    {
        public List<CurveEntry> Entries = new();
    }
}
