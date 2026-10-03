using System;
using System.Linq;
using Game;
using Model;
using Model.Ops;
using RMROC451.TweaksAndThings.Patches;
using UI;
using UnityEngine;

namespace RMROC451.TweaksAndThings;

internal sealed class NpcDelinquentPickupWindow : MonoBehaviour
{
    private static NpcDelinquentPickupWindow? instance;
    private Rect rect = new Rect(100, 150, 720, 400);
    private Vector2 scroll;
    private bool visible;
    internal static void Show()
    {
        if (instance == null) instance = new GameObject("TweaksAndThings delinquent pickups").AddComponent<NpcDelinquentPickupWindow>();
        instance.visible = true;
    }
    private void OnGUI()
    {
        if (!visible || TweaksAndThingsPlugin.Instance?.IsEnabled != true || OpsController.Shared == null || !NpcServiceStore.Load()) return;
        rect = GUILayout.Window(GetInstanceID(), rect, Draw, "Delinquent interchange pickups");
    }
    private void Draw(int id)
    {
        if (GUILayout.Button("Close", GUILayout.Width(65))) visible = false;
        var state = NpcServiceStore.State;
        scroll = GUILayout.BeginScrollView(scroll);
        foreach (var expected in state.DelinquentPickups.Values.OrderBy(p => p.InterchangeId).ThenBy(p => p.CarName).ToList())
        {
            var interchange = OpsController.Shared.AllInterchanges.FirstOrDefault(i => i.Identifier == expected.InterchangeId);
            var active = state.Services.FirstOrDefault(s => s.InterchangeId == expected.InterchangeId && s.PickupSnapshot.Any(p => p.CarId == expected.CarId) && !s.MissesHandled.Contains(expected.CarId));
            double next = active?.Scheduled ?? interchange?.GetNextServiceTime(TimeWeather.Now, out _).TotalSeconds ?? double.NaN;
            string eta = double.IsNaN(next) ? "Unavailable" : next <= TimeWeather.Now.TotalSeconds ? "Service working / awaiting arrival" : TimeSpan.FromSeconds(next - TimeWeather.Now.TotalSeconds).ToString(@"d\d\ hh\h\ mm\m");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{expected.CarName} → {interchange?.DisplayName ?? expected.InterchangeId}");
            GUILayout.Label($"{(state.ForfeitedPickupCars.Contains(expected.CarId) ? "Payout forfeited" : "First miss—payout at risk")} | Next attempt: {eta}");
            GUILayout.Label("Pickup spans: " + string.Join(", ", expected.SpanIds));
            GUILayout.BeginHorizontal();
            if (TrainController.Shared.TryGetCarForId(expected.CarId, out var car))
            {
                if (GUILayout.Button("Locate car")) CameraSelector.shared.ZoomToCar(car);
                if (GUILayout.Button("Add to switch list"))
                {
                    bool accepted = SwitchListAccess.TryAddCars(new[] { car }, out var count);
                    TweaksAndThingsPlugin.LogDiagnostic(accepted ? $"Delinquent pickup: switch-list request for {car.DisplayName}, {count} added." : "Join a train crew; switch-list update could not be confirmed.");
                }
            }
            else GUILayout.Label("Car unavailable");
            if (interchange != null && GUILayout.Button("Locate destination")) CameraSelector.shared.ZoomToPoint(interchange.CenterPoint);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
        if (state.DelinquentPickups.Count == 0) GUILayout.Label("No delinquent pickups.");
        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0, 0, rect.width, 22));
    }
}
