using System.Collections.Generic;
using Game.State;
using KeyValue.Runtime;
using Newtonsoft.Json;

namespace RMROC451.TweaksAndThings;

internal static class NpcServiceStore
{
    private const string Key = "RMROC451.TweaksAndThings.NpcServices.v1";
    private static IKeyValueObject? loadedObject;
    private static string? loadedJson;
    internal static NpcServiceState State { get; private set; } = new NpcServiceState();

    internal static bool Load()
    {
        var kv = StateManager.Shared?.KeyValueObjectForId(GameStorage.ObjectId);
        if (kv == null) return false;
        var value = kv[Key];
        if (ReferenceEquals(kv, loadedObject) && (value.IsNull ? null : value.StringValue) == loadedJson) return true;
        loadedJson = value.IsNull ? null : value.StringValue;
        State = value.IsNull ? new NpcServiceState() :
            JsonConvert.DeserializeObject<NpcServiceState>(value.StringValue) ?? new NpcServiceState();
        loadedObject = kv;
        return true;
    }

    internal static void Save()
    {
        if (StateManager.IsHost && loadedObject != null)
        {
            var json = JsonConvert.SerializeObject(State);
            if (json != loadedJson) loadedObject[Key] = Value.String(json);
            loadedJson = json;
        }
    }

    internal static void Reset() { loadedObject = null; loadedJson = null; State = new NpcServiceState(); NpcDailyTrafficPlans.Reset(); TimetableLiveForecast.Reset(); }
}
