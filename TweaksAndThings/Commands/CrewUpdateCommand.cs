using Game.Notices;
using Game.State;
using Helpers;
using Model.Ops;
using Model.Ops.Timetable;
using Network;
using RMROC451.TweaksAndThings.Extensions;
using RMROC451.TweaksAndThings.Patches;
using System.Linq;
using Track;
using UI.Console;
using UnityEngine;

namespace RMROC451.TweaksAndThings.Commands;

[ConsoleCommand("/cu", "generate a formatted message about a locomotive's status.")]
public class EchoCommand : IConsoleCommand
{
    public string Execute(string[] comps)
    {
        if (comps.Length < 4)
        {
            return "Usage: /cu <car>|. +|- <message>";
        }

        string query = comps[1];
        Model.Car car = query == "." ? TrainController.Shared.SelectedLocomotive : TrainController.Shared.CarForString(query);

        if (car.DetermineFuelCar() == null)
        {
            return "Car not found.";
        }
        string message = string.Join(" ", comps.Skip(3)).Truncate(512);

        switch (comps[2])
        {
            case "+":
                break;
            case "-":
                break;
            default:
                return "+ to include area or - to leave it out";
        }

        var gamePoint = car.GetCenterPosition(Graph._graph);
        EntityReference entityReference = new EntityReference(EntityType.Position, new Vector4( gamePoint.x, gamePoint.y, gamePoint.z, 0));
        EntityReference loco = new EntityReference(EntityType.Car, car.id);
        if (comps[2] == "+") message = new Hyperlink(entityReference.URI(), string.Format(message, OpsController.Shared.ClosestArea(car)?.name ?? "???"));

        string hlt = Hyperlink.To(car);
        hlt = car.TryGetTimetableTrain(out Timetable.Train t) ? hlt.Replace(car.DisplayName, t.DisplayStringLong) : hlt;

        if (StateManager.IsHost)
        {
            car.PostNotice(nameof(EchoCommand), $"{message} :{StateManager.Shared._playersManager.LocalPlayer}");

            ExpandedConsole_Add_Patch.SendMs(null, $"{StateManager.Shared._playersManager.LocalPlayer} {hlt} {message}");
        }
        if (!StateManager.IsHost) Multiplayer.Broadcast($"{StateManager.Shared._playersManager.LocalPlayer} {hlt}: \"{message}\"");

        return string.Empty;
    }
}