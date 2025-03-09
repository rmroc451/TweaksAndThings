using Game.State;
using Helpers;
using Model.Ops;
using Network;
using RMROC451.TweaksAndThings.Extensions;
using System.Linq;
using UI.Console;

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

        if (comps[2] == "+") message += $" {OpsController.Shared.ClosestArea(car)?.name ?? "???"}";
            Multiplayer.Broadcast($"{StateManager.Shared._playersManager.LocalPlayer} {Hyperlink.To(car)}: \"{message}\"");
        return string.Empty;
    }
}