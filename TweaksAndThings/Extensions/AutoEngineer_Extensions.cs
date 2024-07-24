using Model.AI;
using System.Collections;
using UnityEngine;

namespace RMROC451.TweaksAndThings.Extensions
{
    internal static class AutoEngineer_Extensions
    {
        private static float CabooseHalvedFloat(this float input, bool hasCaboose) =>
            hasCaboose ? input / 2 : input;

        private static float CabooseAutoOilerLimit(this bool hasCaboose) =>
            hasCaboose ? 0.99f : AutoOiler.OilIfBelow;

        public static IEnumerator MrocAutoOilerLoop(this AutoOiler oiler, Serilog.ILogger _log)
        {
            int originIndex = oiler.FindOriginIndex();
            bool hasCaboose = oiler._cars.CabooseInConsist();
            if (originIndex < 0)
            {
                _log.Error("Couldn't find origin car {car}", oiler._originCar);
                oiler._coroutine = null;
                yield break;
            }
            oiler._reverse = originIndex > oiler._cars.Count - originIndex;
            _log.Information(
                "AutoOiler {name} starting, rev = {reverse}, caboose halving adjustment = {hasCaboose}, oil limit = {limit}", 
                oiler.name, 
                oiler._reverse, 
                hasCaboose, 
                hasCaboose.CabooseAutoOilerLimit()
            );
            while (true)
            {
                yield return new WaitForSeconds(AutoOiler.StartDelay.CabooseHalvedFloat(hasCaboose));
                int carIndex = originIndex;
                float adjustedTimeToWalk = AutoOiler.TimeToWalkCar.CabooseHalvedFloat(hasCaboose);
                do
                {
                    if (oiler.TryGetCar(carIndex, out var car))
                    {
                        float num = 0f;
                        float origOil = car.Oiled;
                        if (car.NeedsOiling && car.Oiled < hasCaboose.CabooseAutoOilerLimit())
                        {
                            float num2 = 1f - car.Oiled;
                            car.OffsetOiled(num2);
                            float num3 = num2 * AutoOiler.TimeToFullyOil.CabooseHalvedFloat(hasCaboose);
                            num += num3;
                            oiler._pendingRunDuration += num3;
                            oiler._oiledCount++;
                            _log.Information("AutoOiler {name}: oiled {car} from {orig} => {new}", oiler.name, car, origOil, car.Oiled);
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
    }
}
