using System;
using UnityEngine.UIElements;

namespace TGame.ToolBox
{
    public class BoxRegistration
    {
        public string Name { get; init; }
        public string Group { get; init; }
        public string Icon { get; init; }
        public Func<VisualElement> Factory { get; init; }
    }
}
