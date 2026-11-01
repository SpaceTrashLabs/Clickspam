using System.Collections.Generic;
using System.Windows.Input;

namespace AutoClicker
{
    /// <summary>A global hotkey: a key plus optional Ctrl/Alt/Shift/Win modifiers.</summary>
    readonly struct Hotkey
    {
        public readonly ModifierKeys Modifiers;
        public readonly Key Key;

        public Hotkey(ModifierKeys modifiers, Key key)
        {
            Modifiers = modifiers;
            Key = key;
        }

        public uint VirtualKey => (uint)KeyInterop.VirtualKeyFromKey(Key);

        // RegisterHotKey's MOD_ALT/MOD_CONTROL/MOD_SHIFT/MOD_WIN happen to share WPF's ModifierKeys bit values.
        public uint NativeModifiers => (uint)Modifiers;

        /// <summary>Keys that type text; binding them globally without a modifier would hijack normal typing.</summary>
        public bool IsTypingKey =>
            (Key >= Key.A && Key <= Key.Z) || (Key >= Key.D0 && Key <= Key.D9) || Key == Key.Space ||
            Key == Key.Enter || Key == Key.Tab || Key == Key.Back;

        public static bool IsModifierKey(Key key) =>
            key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin;

        public bool Equals(Hotkey other) => Modifiers == other.Modifiers && Key == other.Key;

        public override string ToString()
        {
            var parts = new List<string>();
            if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            parts.Add(KeyName(Key));
            return string.Join("+", parts);
        }

        static string KeyName(Key key)
        {
            if (key >= Key.D0 && key <= Key.D9) return ((char)('0' + (key - Key.D0))).ToString();
            if (key >= Key.NumPad0 && key <= Key.NumPad9) return "Num" + (key - Key.NumPad0);
            switch (key)
            {
                case Key.Next: return "PageDown";
                case Key.Prior: return "PageUp";
                case Key.Capital: return "CapsLock";
                case Key.Snapshot: return "PrintScreen";
                case Key.Oem3: return "`";
                default: return key.ToString();
            }
        }
    }
}
