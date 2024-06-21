using Model;
using System;

namespace RMROC451.TweaksAndThings.Extensions
{
    public static class Car_Extensions
    {
        public static bool EndAirSystemIssue(this Car car)
        {
            bool AEndAirSystemIssue = car[Car.LogicalEnd.A].IsCoupled && !car[Car.LogicalEnd.A].IsAirConnectedAndOpen;
            bool BEndAirSystemIssue = car[Car.LogicalEnd.B].IsCoupled && !car[Car.LogicalEnd.B].IsAirConnectedAndOpen;
            bool EndAirSystemIssue = AEndAirSystemIssue || BEndAirSystemIssue;
            return EndAirSystemIssue;
        }

        public static bool HandbrakeApplied(this Model.Car car) =>
            car.air.handbrakeApplied;

        public static bool CarOrEndGearIssue(this Model.Car car) =>
            car.EndAirSystemIssue() || car.HandbrakeApplied();

        public static bool CarAndEndGearIssue(this Model.Car car) =>
            car.EndAirSystemIssue() && car.HandbrakeApplied();

        public static Car? DetermineFuelCar(this Car engine, bool returnEngineIfNull = false)
        {
            Car? car;
            if (engine is SteamLocomotive steamLocomotive && new Func<Car>(steamLocomotive.FuelCar) != null)
            {
                car = steamLocomotive.FuelCar();
            }
            else
            {
                car = engine is DieselLocomotive ? engine : null;
                if (returnEngineIfNull && car == null) car = engine;
            }
            return car;
        }
    }
}
