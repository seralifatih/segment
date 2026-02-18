using System.Windows.Input;

namespace Segment.App.Models
{
    public class HotkeyBinding
    {
        public string Name { get; set; } = "";
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }

        public string ToSettingValue()
        {
            string prefix = Modifiers == ModifierKeys.None
                ? string.Empty
                : Modifiers.ToString().Replace(", ", "+") + "+";
            return $"{prefix}{Key}";
        }
    }
}
