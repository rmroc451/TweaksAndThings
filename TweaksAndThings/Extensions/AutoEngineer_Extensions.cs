using Model.AI;
using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RMROC451.TweaksAndThings.Extensions
{
    internal static class AutoEngineer_Extensions
    {
        private static float CabooseHalvedFloat(this float input, Model.Car? hasCaboose) =>
            hasCaboose ? input / 2 : input;

        private static float CabooseAutoOilerLimit(this Model.Car? caboose) =>
            caboose ? 0.99f : AutoOiler.OilIfBelow;

        public static IEnumerator MrocAutoOilerLoop(this AutoOiler oiler, Serilog.ILogger _log, bool cabooseRequired)
        {
            int originIndex = oiler.FindOriginIndex();
            Model.Car? foundCaboose = oiler._originCar.FindMyCaboose(0.0f, false);
            if (originIndex < 0)
            {
                _log.Error("Couldn't find origin car {car}", oiler._originCar);
                oiler._coroutine = null;
                yield break;
            } else if (CabooseRequirementChecker(string.Format("{0} {1}", oiler.GetType().Name, oiler.name), cabooseRequired, foundCaboose, _log))
            {
                yield break;
            }
            oiler._reverse = originIndex > oiler._cars.Count - originIndex;
            _log.Information(
                "AutoOiler {name} starting, rev = {reverse}, caboose required = {req}, caboose halving adjustment = {hasCaboose}, oil limit = {limit}",
                oiler.name,
                oiler._reverse,
                cabooseRequired,
                foundCaboose,
                foundCaboose.CabooseAutoOilerLimit()
            );
            while (true)
            {
                yield return new WaitForSeconds(AutoOiler.StartDelay.CabooseHalvedFloat(foundCaboose));
                foundCaboose = oiler._originCar.FindMyCaboose(0.0f,false);
                int carIndex = originIndex;
                float adjustedTimeToWalk = AutoOiler.TimeToWalkCar.CabooseHalvedFloat(foundCaboose);
                do
                {
                    if (oiler.TryGetCar(carIndex, out var car))
                    {
                        float num = 0f;
                        float origOil = car.Oiled;
                        if (car.NeedsOiling && car.Oiled < foundCaboose.CabooseAutoOilerLimit())
                        {
                            float num2 = 1f - car.Oiled;
                            car.OffsetOiled(num2);
                            float num3 = num2 * AutoOiler.TimeToFullyOil.CabooseHalvedFloat(foundCaboose);
                            num += num3;
                            oiler._pendingRunDuration += num3;
                            oiler._oiledCount++;
                            _log.Information("AutoOiler {name}: oiled {car} from {orig} => {new}", oiler.name, car, origOil, car.Oiled);
                        }
                        if (car.HasHotbox && car.Oiled == 1f && cabooseRequired && foundCaboose)
                        {
                            _log.Information("AutoOiler {name}: {foundCaboose} repaired hotbox {car}", oiler.name, foundCaboose, car);
                            car.AdjustHotboxValue();
                        }
                        num += adjustedTimeToWalk;
                        oiler._pendingRunDuration += adjustedTimeToWalk;
                        yield return new WaitForSeconds(num);
                    }
                    carIndex = oiler.NextIndex(carIndex);
                }
                while (oiler.InBounds(carIndex));
                oiler._reverse = !oiler._reverse;
                oiler.PayWages();
            }
        }

        public static IEnumerator MrocAutoHotboxSpotterLoop(this AutoHotboxSpotter spotter, Serilog.ILogger _log, bool cabooseRequired)
        {
            Func<Model.Car?> foundCaboose = () => spotter._locomotive.FindMyCaboose(0.0f, false);
            while (true)
            {
                if (!spotter.HasCars)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }
                var fc = foundCaboose();
                _log.Information("AutoHotboxSpotter {name}: Hotbox Spotter Running, Found Caboose => {hasCaboose}; Has Cars {hasCars}; Requires Caboose {requiresCaboose}", 
                    spotter.name, fc, spotter.HasCars, cabooseRequired);
                if (CabooseRequirementChecker(string.Format("{0} {1}", spotter.GetType().Name, spotter.name), cabooseRequired, fc, _log))
                {
                    yield break;
                }
                spotter.CheckForHotbox();
                while (spotter.HasCars)
                {
                    int num = Random.Range(60, 300);
                    fc = foundCaboose();
                    if (fc)
                    {
                        var numOrig = num;
                        num = Random.Range(15, 30);
                        _log.Information("AutoHotboxSpotter {name}: Next check went from num(60,300) => {numOrig}; to num(15,30) => {hasCaboose}; Requires Caboose {requiresCaboose}", spotter.name, numOrig, num, fc, cabooseRequired);
                    }
                    yield return new WaitForSeconds(num);
                    spotter.CheckForHotbox();
                }
            }
        }

        private static bool CabooseRequirementChecker(string name, bool cabooseRequired, Model.Car? foundCaboose, Serilog.ILogger _log)
        {
            bool error = cabooseRequired && foundCaboose == null;
            if (error) {
                _log.Debug("{name}: Couldn't find required caboose!", name);
            }
            return error;
        }
    }
}
