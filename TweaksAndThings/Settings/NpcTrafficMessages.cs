using System;
using Game.Messages;
using Game.State;
using Model;
using Network;
using Track;

namespace RMROC451.TweaksAndThings;

internal static class NpcTrafficMessages
{
    internal static void Send(NpcServiceRecord service, string eventId, string message)
    {
        if (!StateManager.IsHost || service.Announcements.Contains(eventId)) return;
        string name = service.TrainSymbol.Length > 0 ? service.TrainSymbol : "Interchange " + service.InterchangeId;
        try
        {
            string sender = TrainController.Shared.TryGetCarForId(service.LeadId, out var lead)
                ? $"{name} ({Hyperlink.To(lead)})" : name;
            Multiplayer.Broadcast($"Telegraph — NPC {sender}: {message}");
            service.Announcements.Add(eventId);
            ModDiagnosticLog.Write("TELEGRAPH", $"game={Game.TimeWeather.Now}; service={service.Id}; event={eventId}; {name}: {message}");
            NpcServiceStore.Save();
        }
        catch (Exception ex) { TweaksAndThingsPlugin.LogException("NPC telegraph " + name, ex); }
    }

    internal static void Approaching(NpcServiceRecord service, BaseLocomotive lead, Location target, string destination, string eventId)
    {
        if (service.Announcements.Contains(eventId)) return;
        if (UnityEngine.Vector3.SqrMagnitude(lead.LocationF.GetPosition() - target.GetPosition()) > 1609.344f * 1609.344f) return;
        // Route distance avoids reporting proximity across disconnected or parallel track.
        if ((NpcTrainOperations.Route(lead.LocationF, target, out _, out float meters) && meters <= 1609.344f) ||
            NpcTrainOperations.Arrived(lead, target))
            Send(service, eventId, $"Approaching {destination}; inbound traffic within one track mile.");
    }
}
