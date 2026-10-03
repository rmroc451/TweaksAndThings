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
    public enum CarArchetype { Tender, Freight, Caboose }
}

namespace HarmonyLib
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class HarmonyPatch : System.Attribute { public HarmonyPatch(System.Type type, string method) { } }
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class HarmonyPatchCategory : System.Attribute { public HarmonyPatchCategory(string category) { } }
}
namespace Model.Definition.Data
{
    public enum LoadUnits { Quantity, Pounds }
    public sealed class LoadSlot
    {
        public string RequiredLoadIdentifier;
        public float MaximumCapacity;
        public LoadUnits LoadUnits;
        public LoadSlot(LoadUnits units, float capacity, string identifier)
        { LoadUnits = units; MaximumCapacity = capacity; RequiredLoadIdentifier = identifier; }
    }
    public sealed class CarDefinition
    {
        public Model.Definition.CarArchetype Archetype;
        public System.Collections.Generic.List<LoadSlot> LoadSlots = new System.Collections.Generic.List<LoadSlot>();
    }
}

namespace Game.Messages
{
    public sealed class SwitchListToggleCarIds
    {
        public string TrainCrewId;
        public System.Collections.Generic.List<string> CarIds;
        public bool On;
        public SwitchListToggleCarIds(string crew, System.Collections.Generic.List<string> ids, bool on)
        { TrainCrewId = crew; CarIds = ids; On = on; }
    }
}

namespace Game.State
{
    public sealed class TestCrew { public string Id = "crew"; }
    public sealed class TestPlayersManager { public TestCrew? MyTrainCrew = new TestCrew(); }
    public sealed class StateManager
    {
        public static bool IsHost = true;
        public static bool AcceptSwitchListUpdates = true;
        public static StateManager Shared = new StateManager();
        public TestPlayersManager PlayersManager = new TestPlayersManager();
        public static Game.Messages.SwitchListToggleCarIds? LastMessage;
        public static void ApplyLocal(Game.Messages.SwitchListToggleCarIds message)
        {
            LastMessage = message;
            if (!AcceptSwitchListUpdates) return;
            RMROC451.TweaksAndThings.Tests.SwitchListTestController.Shared.AddedIds.AddRange(message.CarIds);
            UI.SwitchList.SwitchListPanel.Shared ??= new UI.SwitchList.SwitchListPanel();
            foreach (var id in message.CarIds) UI.SwitchList.SwitchListPanel.Shared.Ids.Add(id);
        }
    }
}

namespace UI.SwitchList
{
    public sealed class SwitchListPanel
    {
        public static SwitchListPanel? Shared;
        public readonly System.Collections.Generic.HashSet<string> Ids = new System.Collections.Generic.HashSet<string>();
        public bool SwitchListContains(string id) => Ids.Contains(id);
    }
}

namespace Model.Ops
{
    public enum OverrideDestination { Repair }
    public static class RepairDestinationExtensions
    {
        public static bool TryGetOverrideDestination(this Model.Car car, OverrideDestination kind, OpsController ops, out int? repair)
        { repair = car.HasRepairBill ? 1 : null; return repair.HasValue; }
    }
    public sealed class OpsController
    {
        public static OpsController Shared = new OpsController();
        public TestSwitchListController SwitchListController = new TestSwitchListController();
    }
    public sealed class TestSwitchListController
    {
        public void SendSwitchListUpdate(string crew) { }
    }
}

namespace Model
{
    public sealed class CarDescriptor
    {
        public sealed class Info { public Model.Definition.Data.CarDefinition Definition = new Model.Definition.Data.CarDefinition(); }
        public Info DefinitionInfo = new Info();
    }
    public sealed class Car
    {
        public void Setup(CarDescriptor descriptor) { }
        public string id = string.Empty;
        public Model.Definition.CarArchetype Archetype = Model.Definition.CarArchetype.Freight;
        public bool IsMotivePower;
        public bool HasRepairBill;
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
        internal static void LogDiagnostic(string message) { }
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
