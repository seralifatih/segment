using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Segment.App.Models;

namespace Segment.App.Services
{
    public static class HotkeyBindingService
    {
        public static bool TryParse(string value, string name, out HotkeyBinding binding)
        {
            binding = new HotkeyBinding { Name = name };
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] parts = value
                .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                return false;
            }

            if (!Enum.TryParse(parts[^1], ignoreCase: true, out Key key))
            {
                return false;
            }

            ModifierKeys modifiers = ModifierKeys.None;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i].Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                    parts[i].Equals("Control", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= ModifierKeys.Control;
                }
                else if (parts[i].Equals("Shift", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= ModifierKeys.Shift;
                }
                else if (parts[i].Equals("Alt", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= ModifierKeys.Alt;
                }
                else if (parts[i].Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                         parts[i].Equals("Windows", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= ModifierKeys.Windows;
                }
                else
                {
                    return false;
                }
            }

            binding = new HotkeyBinding
            {
                Name = name,
                Key = key,
                Modifiers = modifiers
            };
            return true;
        }

        public static IReadOnlyList<string> FindConflicts(params HotkeyBinding[] bindings)
        {
            return bindings
                .Where(x => x != null)
                .GroupBy(x => $"{x.Modifiers}:{x.Key}", StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => string.Join(", ", group.Select(x => x.Name)))
                .ToList();
        }
    }
}
