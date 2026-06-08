using System;
using UnityEngine.UIElements;

namespace TGame.ToolBox
{
    public class BoxRegistration
    {
        public string Name { get; set; }
        public string Group { get; set; }
        public string Icon { get; set; }
        public Func<VisualElement> Factory { get; set; }
    }
}
