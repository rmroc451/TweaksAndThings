using RMROC451.TweaksAndThings;

namespace Serilog
{
    internal static class Log
    {
        public static void Debug(string message) { }
    }
}

namespace UnityModManagerNet
{
    public sealed class KeyBinding
    {
        public static bool ControlHeld;
        public static bool ShiftHeld;
        public static bool AltHeld;
        public static bool KeyHeld;
        public UnityEngine.KeyCode keyCode;
        public byte modifiers;
        public bool Pressed() => KeyHeld;
        public void Change(UnityEngine.KeyCode key, byte modifier = 0)
        {
            keyCode = key;
            modifiers = modifier;
        }
        public static bool Ctrl() => ControlHeld;
        public static bool Shift() => ShiftHeld;
        public static bool Alt() => AltHeld;
    }

    public static class UnityModManager
    {
        public sealed class ModEntry { }

        public abstract class ModSettings
        {
            public static T? Load<T>(ModEntry entry) where T : ModSettings => default;
            public static void Save<T>(T settings, ModEntry entry) where T : ModSettings { }
            public virtual void Save(ModEntry entry) { }
        }
    }
}

namespace UnityEngine
{
    public enum KeyCode
    {
        None,
        LeftAlt,
        RightAlt,
        LeftControl,
        RightControl,
        LeftShift,
        RightShift,
        A
    }
}

namespace Model.Definition
{
    public enum CarArchetype { Tender, Freight }
}

namespace Model
{
    public sealed class Car
    {
        public string id = string.Empty;
        public Model.Definition.CarArchetype Archetype = Model.Definition.CarArchetype.Freight;
        public bool IsMotivePower;
        public System.Collections.Generic.IEnumerable<Car>? Consist;
        public System.Collections.Generic.IEnumerable<Car> EnumerateCoupled() => Consist ?? new[] { this };
        public bool MotivePower() => IsMotivePower;
        public static implicit operator bool(Car? car) => car != null;
    }
}

namespace RMROC451.TweaksAndThings
{
    public sealed class TweaksAndThingsPlugin
    {
        public static TweaksAndThingsPlugin? Instance { get; set; }
        public bool IsEnabled { get; set; }
        public Settings? settings { get; set; }
    }
}

namespace RMROC451.TweaksAndThings.Extensions
{
    internal static class TestCarExtensions
    {
        public static bool MotivePower(this Model.Car car) => false;
        public static Model.Car? FindMyCabooseSansLoadRequirement(this Model.Car car) => null;
    }
}
