using RMROC451.TweaksAndThings;

namespace Serilog
{
    internal static class Log
    {
        public static void Debug(string message) { }
    }
}

namespace UI.Builder
{
    public sealed class UIState<T>
    {
        public UIState(T value) { }
    }
}

namespace Model
{
    public sealed class Car
    {
        public static implicit operator bool(Car? car) => car != null;
    }
}

namespace RMROC451.TweaksAndThings
{
    public sealed class TweaksAndThingsPlugin
    {
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
