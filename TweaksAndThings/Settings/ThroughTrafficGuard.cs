using Game.Messages;
using Game.Notices;
using UI;
using Game.State;
using Model;
using Network;
using Network.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

/// <summary>Identifies generated cars and enforces their NPC-only interaction policy.</summary>
internal static class ThroughTrafficGuard
{
    internal const string MarkerKey = "RMROC451.TweaksAndThings.ThroughTrafficNpc";
    private const string Notice = "This is an NPC through-traffic train. Please leave it alone.";
    private static readonly HashSet<string> notifiedCarIds = new HashSet<string>(StringComparer.Ordinal);
    private static readonly HashSet<string> interactiveControlKeys = BuildInteractiveControlKeys();
    [ThreadStatic] private static int trustedMutationDepth;

    internal static bool IsSandbox => StateManager.IsSandbox;
    internal static bool IsTrustedMutation => trustedMutationDepth > 0;

    internal static void BeginTrustedMutation() => trustedMutationDepth++;
    internal static void EndTrustedMutation() => trustedMutationDepth = Math.Max(0, trustedMutationDepth - 1);

    internal static bool IsGenerated(Car? car)
    {
        if (car == null || car.KeyValueObject == null) return false;
        try { return car.KeyValueObject[MarkerKey].BoolValue; }
        catch { return false; }
    }

    internal static bool IsGeneratedCarId(string? carId)
    {
        if (string.IsNullOrEmpty(carId) || TrainController.Shared == null) return false;
        return TrainController.Shared.TryGetCarForId(carId, out var car) && IsGenerated(car);
    }

    internal static bool BlockCarIdInteraction(string? carId)
    {
        if (string.IsNullOrEmpty(carId) || TrainController.Shared == null ||
            !TrainController.Shared.TryGetCarForId(carId, out var car)) return false;
        return BlockInteraction(car);
    }

    internal static bool IsRestricted(Car? car) => IsGenerated(car);

    internal static bool BlockInteraction(Car? car)
    {
        if (IsTrustedMutation || !IsRestricted(car)) return false;
        Notify(car!);
        return true;
    }

    internal static bool ShouldBlockPropertyChange(string objectId, string key)
    {
        if (IsTrustedMutation || !IsGeneratedCarId(objectId)) return false;
        if (TrainController.Shared.TryGetCarForId(objectId, out var pooled) && NpcPoolPower.IsPooled(pooled) && IsCouplerKey(key))
        {
            bool locked = NpcPoolPower.ConnectionBlocked(pooled, key);
            if (locked) Notify(pooled);
            return locked;
        }
        bool markerKey = key.Equals(MarkerKey, StringComparison.OrdinalIgnoreCase);
        bool block = markerKey || IsCouplerKey(key) ||
            IsEditableIdentityKey(key) && !IsTrustedMutation ||
            (interactiveControlKeys.Contains(key) || IsPassengerMarkerKey(key)) && !IsTrustedMutation;
        if (block && TrainController.Shared.TryGetCarForId(objectId, out var car)) Notify(car);
        return block;
    }

    internal static bool ShouldBlockConsistMutation(IEnumerable<object> arguments)
    {
        if (IsTrustedMutation) return false;
        var car = arguments.OfType<Car>().FirstOrDefault(IsGenerated);
        if (car == null) return false;
        Notify(car);
        return true;
    }

    internal static void Notify(Car car)
    {
        if (!notifiedCarIds.Add(car.id)) return;
        try
        {
            var localPlayer = StateManager.Shared?.PlayersManager?.LocalPlayer;
            if (localPlayer != null) Multiplayer.SendError(localPlayer, Notice, AlertLevel.Info);
            else car.PostNotice("through-traffic-npc", Notice);
        }
        catch { car.PostNotice("through-traffic-npc", Notice); }
    }

    internal static void ForgetNotice(string carId) => notifiedCarIds.Remove(carId);

    private static HashSet<string> BuildInteractiveControlKeys()
    {
        var output = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PropertyChange.Control control in Enum.GetValues(typeof(PropertyChange.Control)))
        {
            string name = control.ToString();
            // These are changed by normal simulation, so keep wear and damage updates live.
            if (name == "Condition" || name == "Derailment" || name == "Hotbox") continue;
            output.Add(PropertyChange.KeyForControl(control));
        }
        return output;
    }

    private static bool IsCouplerKey(string key) =>
        Contains(key, "IsCoupled") || Contains(key, "IsAirConnected") || Contains(key, "Anglecock") || Contains(key, "CutLever") ||
        key.EndsWith(".coupled", StringComparison.OrdinalIgnoreCase) || key.EndsWith(".airConnected", StringComparison.OrdinalIgnoreCase);

    private static bool IsEditableIdentityKey(string key) =>
        key.Equals(MarkerKey, StringComparison.OrdinalIgnoreCase) ||
        key.Equals(NpcPoolPower.Marker, StringComparison.OrdinalIgnoreCase) ||
        Contains(key, "Ident") || Contains(key, "Bardo") || Contains(key, "TrainCrew") ||
        Contains(key, "Waybill") || Contains(key, "Owned") || Contains(key, "Destination");

    private static bool IsPassengerMarkerKey(string key) => Contains(key, "PassengerMarker");

    private static bool Contains(string value, string part) => value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
}
