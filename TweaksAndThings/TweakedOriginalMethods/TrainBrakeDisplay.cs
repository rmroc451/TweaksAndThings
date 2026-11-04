using Model;
using Model.Ops;
using Model.Physics;
using RMROC451.TweaksAndThings.Patches;
using UI;
using UI.CarInspector;
using UI.Common;
using UI.Tags;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.UI.Image;
using Object = UnityEngine.Object;

namespace RMROC451.TweaksAndThings.TweakedOriginalMethods
{
    internal static class TrainBrakeDisplay
    {
        private static readonly List<Car> currentConsist = new();
        private static int lastLoggedCarCount = -1;
        private static string lastLoggedSelectedCarId = string.Empty;
        private static int lastLoggedStatusSignature;
        private static bool? lastLoggedColorsEnabled;
        private static bool? lastLoggedTagsVisible;
        private static float nextColorDiagnosticTime;

        internal static void ColorForCar(UI.TrainBrakeDisplay __instance, Car car, ref Color __result)
        {
            __result = Color.gray;
            if (car.IsDerailed)
            {
                __result = __instance.derailedColor;
                return;
            }

            CarAirSystem air = car.air;
            if (air.handbrakeApplied)
            {
                __result = __instance.handbrakeAppliedColor;
                return;
            }

            // Use the same destination tint as car tags, while retaining the
            // tweaked display's higher-priority condition colors.
            __result = CarTagColor.For(car, OpsController.Shared);
            // Pressure is in PSI; comparing InverseLerp's 0..1 result to 10
            // prevented this warning blend from ever appearing.
            if (air.BrakeCylinder.Pressure >= 10f)
                __result = (__result + __instance.ColorForPsi(air.BrakeCylinder.Pressure)) / 2f;
        }

        internal static Image GetCarImage(UI.TrainBrakeDisplay __instance, int index, float xCyl, float yCyl, Car car)
        {
            Image output = null;
            if (__instance._carImages.Count - 1 < index)
            {
                var tweaksAndThings = TweaksAndThingsPlugin.Instance!;
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
                    if (checkFurther & HotkeyBindings.ControlDown) CarInspector.Show(car);
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
            if (selectedCar == null) return;

            BuildCurrentConsist(selectedCar);
            List<Car> cars = currentConsist;

            int numberOfCars = cars.Count;
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
            bool colorsEnabled = TweaksAndThingsPlugin.Instance?.TrainBrakeDisplayShowsColorsInCalloutMode() == true;
            bool tagsVisible = TagController.Shared?.TagsVisible == true;
            int statusSignature = 17;
            foreach (Car car in cars)
            {
                int flags = (car.air.handbrakeApplied ? 1 : 0) |
                    (car.IsDerailed ? 2 : 0) |
                    (car.EndGearA.IsAirConnected ? 4 : 0) |
                    (car.EndGearB.IsAirConnected ? 8 : 0) |
                    (car.EndGearA.IsCoupled ? 16 : 0) |
                    (car.EndGearB.IsCoupled ? 32 : 0) |
                    (car.EndGearA.AnglecockSetting > 0.9f ? 64 : 0) |
                    (car.EndGearB.AnglecockSetting > 0.9f ? 128 : 0);
                statusSignature = unchecked(statusSignature * 31 + car.id.GetHashCode());
                statusSignature = unchecked(statusSignature * 31 + flags);
            }
            bool logColors = selectedCar.id != lastLoggedSelectedCarId || numberOfCars != lastLoggedCarCount ||
                statusSignature != lastLoggedStatusSignature ||
                colorsEnabled != lastLoggedColorsEnabled || tagsVisible != lastLoggedTagsVisible ||
                Time.realtimeSinceStartup >= nextColorDiagnosticTime;
            string colorSample = string.Empty;
            bool sampledDestination = false;
            bool sampledVisibleTag = false;
            int derailedCars = 0;
            int handbrakeCars = 0;
            int destinationCandidates = 0;
            int pressureBlendCars = 0;
            float num5 = __instance._imageWidth / 2f;
            float num6 = 0f;
            float y = 12f - 2f * __instance._spacing;
            Car.LogicalEnd logicalEnd = Car.LogicalEnd.A;
            Car.LogicalEnd end = ((logicalEnd == Car.LogicalEnd.A) ? Car.LogicalEnd.B : Car.LogicalEnd.A);
            foreach (Car car in cars)
            {
                if (num5 > __instance._rectTransform.rect.width) break;

                Image carImage = TweakedOriginalMethods.TrainBrakeDisplay.GetCarImage(__instance, num4, num5, 0f, car);
                num5 += __instance._imageWidth + __instance._spacing;
                if (!TrainBrakeDisplay_ColorForCar_Patch.TryGetDestinationColor(__instance, car, out var carColor))
                    carColor = __instance.ColorForCar(car);
                carImage.color = carColor;
                if (logColors)
                {
                    if (car.IsDerailed) derailedCars++;
                    else if (car.air.handbrakeApplied) handbrakeCars++;
                    else
                    {
                        Color tagColor = CarTagColor.For(car, OpsController.Shared);
                        bool hasDestinationColor = tagColor != CarTagColor.NoDestination;
                        TagCallout? callout = car.TagCallout;
                        bool visibleTag = callout != null && callout.gameObject.activeInHierarchy;
                        if (hasDestinationColor) destinationCandidates++;
                        if (car.air.BrakeCylinder.Pressure >= 10f) pressureBlendCars++;
                        if (colorSample.Length == 0 || (visibleTag && !sampledVisibleTag) ||
                            (hasDestinationColor && !sampledDestination && !sampledVisibleTag))
                        {
                            string tagImageColor = callout != null && visibleTag && callout.colorImages != null && callout.colorImages.Length > 0 && callout.colorImages[0] != null
                                ? callout.colorImages[0].color.ToString()
                                : "none";
                            colorSample = $"{car.id}: computedTag={tagColor} visibleTag={visibleTag} tagImage={tagImageColor} pressure={car.air.BrakeCylinder.Pressure:F1} applied={carImage.color}";
                            sampledDestination = hasDestinationColor;
                            sampledVisibleTag = visibleTag;
                        }
                    }
                }
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

            if (logColors)
            {
                ModDiagnosticLog.Write("TRAIN BRAKE COLORS",
                    $"selected={selectedCar.id}; coupled={numberOfCars}; drawn={num4}; setting={colorsEnabled}; tagsVisible={tagsVisible}; colorsActive={colorsEnabled && tagsVisible}; opsReady={OpsController.Shared != null}; status={statusSignature}; destinationCandidates={destinationCandidates}; pressureBlends={pressureBlendCars}; handbrakes={handbrakeCars}; derailed={derailedCars}; sample={colorSample}");
                lastLoggedCarCount = numberOfCars;
                lastLoggedSelectedCarId = selectedCar.id;
                lastLoggedStatusSignature = statusSignature;
                lastLoggedColorsEnabled = colorsEnabled;
                lastLoggedTagsVisible = tagsVisible;
                nextColorDiagnosticTime = Time.realtimeSinceStartup + 30f;
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

        private static void BuildCurrentConsist(Car selectedCar)
        {
            currentConsist.Clear();
            currentConsist.Add(selectedCar);
            if (selectedCar.set == null) return;

            // Walk each side of the selected car through current coupler connections.
            // EnumerateCoupled uses cached set boundaries, which can omit an end car
            // immediately after a mid-consist cut.
            AddConnectedSide(selectedCar.CoupledTo(Car.LogicalEnd.A), selectedCar);
            currentConsist.Reverse();
            AddConnectedSide(selectedCar.CoupledTo(Car.LogicalEnd.B), selectedCar);
        }

        private static void AddConnectedSide(Car current, Car previous)
        {
            while (current != null && !currentConsist.Contains(current))
            {
                currentConsist.Add(current);
                if (current.set == null) break;
                Car next = current.CoupledTo(Car.LogicalEnd.A);
                if (next == null || next == previous)
                    next = current.CoupledTo(Car.LogicalEnd.B);
                previous = current;
                current = next;
            }
        }
    }
}
