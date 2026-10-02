using Model;
using RMROC451.TweaksAndThings.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RMROC451.TweaksAndThings.Patches;

/// <summary>
/// Uses the game's loaded switch-list API without binding the mod to an
/// implementation detail that has changed between Railroader versions.
/// </summary>
internal static class SwitchListAccess
{
    private static readonly string[] AddMethodNames =
    {
        "AddCarToSwitchList", "AddCarsToSwitchList", "AddToSwitchList", "AddCar", "AddCars", "AddToList"
    };

    internal static bool TryAddConsist(Car selectedCar, out int addedCount)
    {
        var cars = selectedCar.EnumerateCoupled()
            .Where(car => !car.MotivePower() && car.Archetype != Model.Definition.CarArchetype.Tender)
            .GroupBy(GetCarIdentity, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        return TryAddCars(cars, out addedCount);
    }

    internal static bool TryAddCars(IReadOnlyList<Car> cars, out int addedCount)
    {
        addedCount = 0;
        if (cars.Count == 0) return true;

        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(type => type.Name.IndexOf("SwitchList", StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderByDescending(type => type.Name.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) >= 0)
            .ThenBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

        foreach (var type in types)
        {
            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .Where(method => AddMethodNames.Contains(method.Name, StringComparer.OrdinalIgnoreCase)))
            {
                var target = method.IsStatic ? null : FindSingleton(type);
                if (!method.IsStatic && target == null) continue;

                var parameters = method.GetParameters();
                if (parameters.Length == 1 && (parameters[0].ParameterType == typeof(Car) || parameters[0].ParameterType == typeof(string)))
                {
                    foreach (var car in cars)
                    {
                        try
                        {
                            object? value = parameters[0].ParameterType == typeof(Car) ? car : GetCarIdentity(car);
                            var result = method.Invoke(target, new[] { value });
                            if (result is bool success && !success) break;
                            addedCount++;
                        }
                        catch (TargetInvocationException) { break; }
                        catch (ArgumentException) { break; }
                    }

                    if (addedCount == cars.Count) return true;
                    if (addedCount > 0) return false;
                    continue;
                }

                if (!TryBuildArguments(method, cars, out var arguments)) continue;

                try
                {
                    var result = method.Invoke(target, arguments);
                    if (result is bool success && !success) continue;
                    addedCount = cars.Count;
                    return true;
                }
                catch (TargetInvocationException)
                {
                    // A candidate may exist for a different switch-list context. Try the next API.
                }
                catch (ArgumentException)
                {
                    // Ignore API overloads that cannot accept this game's car/list types.
                }
            }
        }

        return false;
    }

    private static bool TryBuildArguments(MethodInfo method, IReadOnlyList<Car> cars, out object?[] arguments)
    {
        var parameters = method.GetParameters();
        arguments = Array.Empty<object?>();
        if (parameters.Length != 1) return false;

        var parameterType = parameters[0].ParameterType;
        if (parameterType.IsAssignableFrom(typeof(List<Car>)))
        {
            arguments = new object?[] { cars.ToList() };
            return true;
        }

        if (parameterType.IsAssignableFrom(typeof(List<string>)))
        {
            arguments = new object?[] { cars.Select(GetCarIdentity).ToList() };
            return true;
        }

        return false;
    }

    private static object? FindSingleton(Type type)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var property in type.GetProperties(flags))
        {
            if (property.GetIndexParameters().Length != 0 || !type.IsAssignableFrom(property.PropertyType)) continue;
            if (property.Name is not ("Shared" or "Instance" or "Current" or "CurrentSwitchList")) continue;
            try { return property.GetValue(null, null); }
            catch { }
        }

        foreach (var field in type.GetFields(flags))
        {
            if (!type.IsAssignableFrom(field.FieldType)) continue;
            if (field.Name is not ("Shared" or "Instance" or "Current" or "CurrentSwitchList")) continue;
            try { return field.GetValue(null); }
            catch { }
        }

        return null;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException exception) { return exception.Types.OfType<Type>(); }
        catch { return Array.Empty<Type>(); }
    }

    private static string GetCarIdentity(Car car)
    {
        var property = car.GetType().GetProperty("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       ?? car.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null) return property.GetValue(car, null)?.ToString() ?? car.GetHashCode().ToString();
        var field = car.GetType().GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? car.GetType().GetField("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field?.GetValue(car)?.ToString() ?? car.GetHashCode().ToString();
    }
}
