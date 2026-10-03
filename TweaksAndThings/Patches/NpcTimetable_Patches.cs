using System.Collections.Generic;
using Game.State;
using HarmonyLib;
using Model.Ops.Timetable;
using UI;
using UI.Builder;
using UI.Common;
using UI.Timetable;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(TimetableController), "HostSetCurrent")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcTimetable_Protect_Patch
{
    private static bool Prefix(TimetableController __instance, ref string content)
    {
        if (NpcTimetableRegistry.InternalChange || !NpcServiceStore.Load() || NpcServiceStore.State.ProtectedTimetables.Count == 0) return true;
        if (!__instance.TryRead(content, out var timetable, null))
        { Toast.Present("Invalid timetable: planned and active NPC entries must remain protected. Correct the timetable before applying it."); return false; }
        if (NpcTimetableRegistry.Merge(timetable))
        { content = TimetableWriter.Write(timetable); Toast.Present("Planned and active NPC timetable entries were restored from their saved schedules."); }
        return true;
    }
}

[HarmonyPatch(typeof(VisualTimetableEditor), "BuildTrainEditorContent")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcTimetable_ReadOnly_Patch
{
    private static bool Prefix(VisualTimetableEditor __instance, UIPanelBuilder builder)
    {
        var train = __instance.SelectedTrain;
        if (train == null || !NpcTimetableRegistry.IsProtected(train.Name)) return true;
        builder.AddLabel(train.DisplayStringLong);
        builder.AddLabel("AI dispatcher controls this train. Its class, times, route and symbol are locked until the service completes.");
        foreach (var entry in train.Entries) builder.AddField(entry.Station, entry.DepartureTime.TimeString());
        return false;
    }
}

[HarmonyPatch(typeof(VisualTimetableEditor), "DidChangeTimetable")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcTimetable_EditorRestore_Patch
{
    private static void Prefix(VisualTimetableEditor __instance)
    { if (NpcServiceStore.Load()) NpcTimetableRegistry.Merge(__instance._timetable); }
}

[HarmonyPatch(typeof(PlayersManager), nameof(PlayersManager.HandleRequestTrainCrewMembership))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcTimetable_CrewJoin_Patch
{
    private static bool Prefix(string trainCrewId, bool join) => !join || !NpcTimetableRegistry.IsAiCrew(trainCrewId);
}

[HarmonyPatch(typeof(PlayersManager), nameof(PlayersManager.HandleRequestDeleteTrainCrew))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcTimetable_CrewDelete_Patch
{
    private static bool Prefix(string trainCrewId) => NpcTimetableRegistry.InternalChange || !NpcTimetableRegistry.IsAiCrew(trainCrewId);
}

[HarmonyPatch(typeof(PlayersManager), nameof(PlayersManager.HandleRequestRenameTrainCrew))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcTimetable_CrewRename_Patch
{
    private static bool Prefix(string trainCrewId) => !NpcTimetableRegistry.IsAiCrew(trainCrewId);
}

[HarmonyPatch(typeof(PlayersManager), nameof(PlayersManager.HandleRequestSetTrainCrewTimetableSymbol))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcTimetable_CrewSymbol_Patch
{
    private static bool Prefix(string trainCrewId, string symbol) => !NpcTimetableRegistry.IsAiCrew(trainCrewId) && !NpcTimetableRegistry.IsProtected(symbol);
}

[HarmonyPatch(typeof(PlayersManager), nameof(PlayersManager.HandleRequestCreateTrainCrew))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcTimetable_CrewCreate_Patch
{
    private static bool Prefix(Game.Messages.Snapshot.TrainCrew trainCrew) => NpcTimetableRegistry.InternalChange || !NpcTimetableRegistry.IsProtected(trainCrew.TimetableSymbol);
}

[HarmonyPatch(typeof(TrainController), nameof(TrainController.HandleSetCarTrainCrew))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcTimetable_CarCrew_Patch
{
    private static bool Prefix(string trainCrewId) => NpcTimetableRegistry.AssigningCrew || !NpcTimetableRegistry.IsAiCrew(trainCrewId);
}

[HarmonyPatch(typeof(Network.Multiplayer), nameof(Network.Multiplayer.Broadcast))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class NpcTimetable_AssignmentMessage_Patch
{
    private static bool Prefix() => !NpcTimetableRegistry.AssigningCrew && !NpcTimetableRegistry.InternalChange;
}
