using Model;
using Model.Ops;
using Model.Physics;
using Railloader;
using RMROC451.TweaksAndThings.Patches;
using UI;
using UI.CarInspector;
using UI.Common;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.UI.Image;
using Object = UnityEngine.Object;

namespace RMROC451.TweaksAndThings.TweakedOriginalMethods
{
    internal static class TrainBrakeDisplay
    {
        internal static void ColorForCar(UI.TrainBrakeDisplay __instance, Car car, ref Color __result)
        {
            __result = Color.gray;

            if (car.IsDerailed)
            {
                __result = __instance.derailedColor;
                return;
            }
            CarAirSystem air = car.air;
            OpsController opsController = OpsController.Shared;
            if (air.handbrakeApplied)
            {
                __result = __instance.handbrakeAppliedColor;
                return;
            }
            else if (
                (Object)(object)opsController != (Object)null &&
                opsController.TryGetDestinationInfo(car, out var destinationName, out var isAtDestination, out var destinationPosition, out var destination)
            )
            {
                Area area = opsController.AreaForCarPosition(destination);
                if ((Object)(object)area != (Object)null)
                {
                    __result = area.tagColor;
                }
                if (!isAtDestination)
                {
                    float num = 1f / __result.maxColorComponent;
                    __result *= num;
                }
            }

            if (Mathf.InverseLerp(0f, 72f, air.BrakeCylinder.Pressure) >= 10f) __result = (__result + __instance.ColorForPsi(air.BrakeCylinder.Pressure)) / 2f;
        }

        internal static Image GetCarImage(UI.TrainBrakeDisplay __instance, int index, float xCyl, float yCyl, Car car)
        {
            Image output = null;
            if (__instance._carImages.Count - 1 < index)
            {
                var tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
                GameObject val = new GameObject();
                val.transform.SetParent(((Component)__instance).transform, false);
                ((Object)val).name = $"Car {index}";
                float height = 12f;
                val.AddComponent<RectTransform>().SetFrame(xCyl, yCyl, __instance._imageWidth, height);
                EventTrigger trigger = val.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerClick;
                entry.callback.AddListener((eventData) => {
                    PointerEventData ped = (PointerEventData)eventData;
                    PickableActivation pa = PickableActivation.Primary;
                    if (ped.button == PointerEventData.InputButton.Right) pa = PickableActivation.Secondary;

                    bool checkFurther = ped.button == PointerEventData.InputButton.Middle ? true : CarPickable_Activate_Patch.HandleCarOrTrainBrakeDisplayClick(car, tweaksAndThings, pa);
                    if (checkFurther & GameInput.IsControlDown) CarInspector.Show(car);
                });
                trigger.triggers.Add(entry);
                output = val.AddComponent<Image>();

                output.type = (Type)1;
                __instance._carImages.Add(output);
            }
            else
            {
                output = __instance._carImages[index];
                ((Component)output).gameObject.SetActive(true);
            }
            output.sprite = (car.IsLocomotive ? __instance.locomotiveTile : __instance.carTile);
            return output;
        }

        internal static void Update(UI.TrainBrakeDisplay __instance)
        {
            Car selectedCar = __instance._trainController.SelectedCar;
            if (selectedCar == null || selectedCar.set == null) return;

            int numberOfCars = selectedCar.set.NumberOfCars;
            if (numberOfCars != __instance._lastNumCars)
            {
                __instance.RemoveAllImages();
                int num = Mathf.Clamp(numberOfCars, 0, 100);
                float num2 = (float)num * 8f + (float)(num - 1) * 1f;
                if (num2 > __instance._rectTransform.rect.width)
                {
                    float num3 = __instance._rectTransform.rect.width / num2;
                    __instance._imageWidth = 8f * num3;
                    __instance._spacing = 1f * num3;
                }
                else
                {
                    __instance._imageWidth = 8f;
                    __instance._spacing = 1f;
                }

                __instance._lastNumCars = numberOfCars;
            }

            int num4 = 0;
            float num5 = __instance._imageWidth / 2f;
            float num6 = 0f;
            float y = 12f - 2f * __instance._spacing;
            Car.LogicalEnd logicalEnd = ((selectedCar.set.IndexOfCar(selectedCar).GetValueOrDefault(0) >= numberOfCars / 2) ? Car.LogicalEnd.B : Car.LogicalEnd.A);
            Car.LogicalEnd end = ((logicalEnd == Car.LogicalEnd.A) ? Car.LogicalEnd.B : Car.LogicalEnd.A);
            bool stop = false;
            int carIndex = selectedCar.set.StartIndexForConnected(selectedCar, logicalEnd, IntegrationSet.EnumerationCondition.Coupled);
            Car car;
            while (!stop && (car = selectedCar.set.NextCarConnected(ref carIndex, logicalEnd, IntegrationSet.EnumerationCondition.Coupled, out stop)) != null && !(num5 > __instance._rectTransform.rect.width))
            {
                Image carImage = TweakedOriginalMethods.TrainBrakeDisplay.GetCarImage(__instance, num4, num5, 0f, car);
                num5 += __instance._imageWidth + __instance._spacing;
                carImage.color = __instance.ColorForCar(car);
                Image airImage = __instance.GetAirImage(num4, num6, y);
                num6 += __instance._imageWidth + __instance._spacing;
                Color color;
                if (!car[logicalEnd].IsCoupled)
                {
                    color = ColorForOuterAnglecock(car[logicalEnd].AnglecockSetting);
                }
                else
                {
                    Car car2 = car.CoupledTo(logicalEnd);
                    bool num7 = car2 != null && car2[end].AnglecockSetting > 0.9f;
                    bool flag = car[logicalEnd].AnglecockSetting > 0.9f;
                    color = ((num7 && flag && car[logicalEnd].IsAirConnected) ? Color.clear : Color.white);
                }

                airImage.color = color;
                if (!car[end].IsCoupled)
                {
                    __instance.GetAirImage(num4 + 1, num6, y).color = ColorForOuterAnglecock(car[end].AnglecockSetting);
                }

                num4++;
            }

            for (int i = num4; i < __instance._carImages.Count; i++)
            {
                __instance._carImages[i].gameObject.SetActive(value: false);
            }

            for (int j = num4 + 1; j < __instance._airImages.Count; j++)
            {
                __instance._airImages[j].gameObject.SetActive(value: false);
            }

            static Color ColorForOuterAnglecock(float value)
            {
                if (!((double)value < 0.01))
                {
                    return Color.white;
                }

                return Color.clear;
            }
        }
    }
}
