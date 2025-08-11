using HarmonyLib;
using Helpers;
using Map.Runtime;
using Model;
using Model.AI;
using Railloader;
using Serilog;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Track;
using UI;
using UI.EngineControls;
using UI.Map;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using static UI.AutoEngineerDestinationPicker;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(MapWindow))]
[HarmonyPatch(nameof(MapWindow.OnClick), typeof(Vector2))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class MapWindow_OnClick_Patch
{
    private static Serilog.ILogger _log => Log.ForContext<MapWindow_OnClick_Patch>();
    private static GameObject _prefabHolder;
    private static MapIcon _waypointPrefab;
    private static Dictionary<string, MapIcon> wps = new Dictionary<string, MapIcon>();

    public static Sprite? LoadTexture(string fileName, string name)
    {
        string path = Path.Combine(SingletonPluginBase<TweaksAndThingsPlugin>.Shared.ModDirectory, fileName);
        Texture2D texture2D = new Texture2D(128, 128, TextureFormat.DXT5, mipChain: false);
        texture2D.name = name;
        texture2D.wrapMode = TextureWrapMode.Clamp;
        if (!ImageConversion.LoadImage(texture2D, File.ReadAllBytes(path)))
        {
            _log.Information($"Unable to load {name} icon!");
            return null;
        }
        Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f));
        sprite.name = name;

        //if (!SpriteLibrary.Shared.entries.Any(s => s.name.ToString() == name)) SpriteLibrary.Shared.entries.Add(new() { name = SpriteName.Meh, sprite = sprite });
        return sprite;
    }
    public static MapIcon? waypointPrefab
    {
        get
        {
            if (_waypointPrefab == null)
            {
                CreateWaypointPrefab();
            }
            return _waypointPrefab;
        }
    }

    internal static GameObject prefabHolder
    {
        get
        {
            if (_prefabHolder == null)
            {
                _prefabHolder = new GameObject("Prefab Holder");
                _prefabHolder.hideFlags = HideFlags.HideAndDontSave;
                _prefabHolder.SetActive(value: false);
            }
            return _prefabHolder;
        }
    }

    private static void CreateWaypointPrefab()
    {
        float num = 0.6f; //come back
        Sprite sprite = LoadTexture("Map_pin_icon.png", "MapPin");
        _waypointPrefab = Object.Instantiate(TrainController.Shared.locomotiveMapIconPrefab, prefabHolder.transform);
        GameObject obj = _waypointPrefab.gameObject;
        obj.hideFlags = HideFlags.HideAndDontSave;
        obj.name = "Map Waypoint Icon";
        if ((bool)_waypointPrefab.Text)
        {
            Object.DestroyImmediate(_waypointPrefab.Text.gameObject);
        }
        Image componentInChildren = _waypointPrefab.GetComponentInChildren<Image>();
        componentInChildren.sprite = sprite;
        componentInChildren.transform.localScale = Vector3.one * num;
    }

    static bool Prefix(MapWindow __instance, Vector2 viewportNormalizedPoint)
    {
        if (GameInput.IsControlDown && GameInput.IsAltDown)
        {
            Ray ray = RayForViewportNormalizedPoint(__instance, viewportNormalizedPoint);
            Vector3 gamePoint = MapManager.Instance.FindTerrainPointForXZ(WorldTransformer.WorldToGame(ray.origin));
            var selectedLoco = TrainController.Shared?.SelectedLocomotive;
            if (selectedLoco != null)
            {
                Hit? valueOrDefault = null;
                Camera _camera = null;
                if (MainCameraHelper.TryGetIfNeeded(ref _camera))
                {
                    float rad = 200f;
                    if (Graph.Shared.TryGetLocationFromGamePoint(gamePoint, rad, out Location location))
                    {
                        Hit? hit = HitLocation(location, selectedLoco);
                        if (hit.HasValue)
                        {
                            valueOrDefault = hit.GetValueOrDefault();
                            location =  valueOrDefault.Value.Location;
                        }
                        var aeoh = new AutoEngineerOrdersHelper(persistence: new AutoEngineerPersistence(selectedLoco.KeyValueObject), locomotive: selectedLoco);
                        var mw = (location: (Location)location, carId: valueOrDefault?.CarInfo?.car?.id ?? string.Empty);
                        
                        aeoh.SetWaypoint(mw.location, mw.carId);
                        aeoh.SetOrdersValue(maybeWaypoint: mw);
                        AutoEngineerDestinationPicker.Shared.Cancel();
                    }
                }
            }
            return false;
        }
        return true;
    }

    public static Ray RayForViewportNormalizedPoint(MapWindow i, Vector2 v2) =>
        i.mapBuilder.mapCamera.ViewportPointToRay(new Vector3(v2.x, v2.y, 0f));

    private static Hit? HitLocation(Location? location, BaseLocomotive selectedLoco)
    {
        var _graph = TrainController.Shared.graph;
        if (location.HasValue)
        {
            Location valueOrDefault = location.GetValueOrDefault();
            TrainController shared = TrainController.Shared;
            Vector3 position = _graph.GetPosition(valueOrDefault);
            float num = 2f;
            Hit? result = null;
            HashSet<Car> value;
            using (CollectionPool<HashSet<Car>, Car>.Get(out value))
            {
                shared.CheckForCarsAtPoint(position, 2f, value, valueOrDefault);
                foreach (Car item in value)
                {
                    if (selectedLoco.EnumerateCoupled().ToHashSet().Contains(item))
                    {
                        continue;
                    }
                    if (!item[item.EndToLogical(Car.End.F)].IsCoupled)
                    {
                        Location location2 = _graph.LocationByMoving(item.LocationF, 0.5f, checkSwitchAgainstMovement: false, stopAtEndOfTrack: true);
                        float distanceBetweenClose = _graph.GetDistanceBetweenClose(valueOrDefault, location2);
                        if (distanceBetweenClose < num)
                        {
                            num = distanceBetweenClose;
                            result = new Hit(location2, (item, Car.End.F));
                        }
                    }
                    if (!item[item.EndToLogical(Car.End.R)].IsCoupled)
                    {
                        Location location3 = _graph.LocationByMoving(item.LocationR, -0.5f, checkSwitchAgainstMovement: false, stopAtEndOfTrack: true).Flipped();
                        float distanceBetweenClose2 = _graph.GetDistanceBetweenClose(valueOrDefault, location3);
                        if (distanceBetweenClose2 < num)
                        {
                            num = distanceBetweenClose2;
                            result = new Hit(location3, (item, Car.End.R));
                        }
                    }
                }
                if (value.Count > 0)
                {
                    return result;
                }
            }
            return new Hit(valueOrDefault, null);
        }
        return null;
    }
}