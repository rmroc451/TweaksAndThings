using UnityEngine;

namespace RMROC451.TweaksAndThings;

/// <summary>
/// The game used Alt, Ctrl, and Shift as click modifiers. These saved bindings
/// keep those defaults while allowing each modifier action to be remapped.
/// </summary>
internal static class HotkeyBindings
{
    internal static bool AltDown => IsHeld(TweaksAndThingsPlugin.Instance?.settings?.ClickAltBinding);
    internal static bool ControlDown => IsHeld(TweaksAndThingsPlugin.Instance?.settings?.ClickControlBinding);
    internal static bool ShiftDown => IsHeld(TweaksAndThingsPlugin.Instance?.settings?.ClickShiftBinding);

    internal static bool IsHeld(HotkeyBinding? binding)
    {
        if (binding == null || binding.keyCode == KeyCode.None) return false;

        bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        byte actualModifiers = (byte)((control ? 1 : 0) | (shift ? 2 : 0) | (alt ? 4 : 0));
        if ((actualModifiers & binding.modifiers) != binding.modifiers) return false;

        bool keyIsControl = binding.keyCode == KeyCode.LeftControl || binding.keyCode == KeyCode.RightControl;
        bool keyIsShift = binding.keyCode == KeyCode.LeftShift || binding.keyCode == KeyCode.RightShift;
        bool keyIsAlt = binding.keyCode == KeyCode.LeftAlt || binding.keyCode == KeyCode.RightAlt;

        // Modifier keys stand for either physical key, matching the game's
        // original IsAltDown/IsControlDown/IsShiftDown behavior.
        if (keyIsControl || keyIsShift || keyIsAlt)
            return keyIsControl && control || keyIsShift && shift || keyIsAlt && alt;

        return (actualModifiers & binding.modifiers) == binding.modifiers && Input.GetKey(binding.keyCode);
    }
}
