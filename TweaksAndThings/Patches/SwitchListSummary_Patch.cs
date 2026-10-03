using HarmonyLib;
using UI;
using UI.Common;
using UI.SwitchList;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(SwitchListPanel), "Start")]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class SwitchListSummary_Patch
{
    private static void Postfix(SwitchListPanel __instance)
    {
        __instance.toolsMenu.Configure(new[]
        {
            new DropdownMenu.RowData("Add from Train", "Add cars in selected train to the switch list"),
            new DropdownMenu.RowData("Cleanup", "Remove completed rows"),
            new DropdownMenu.RowData("Sort by Destination", null),
            new DropdownMenu.RowData("Sort by Location", null),
            new DropdownMenu.RowData("Location / tonnage summary", "Open the current crew's switch list grouped by destination and location")
        }, index =>
        {
            switch (index)
            {
                case 0: __instance.ClickAddFromTrain(); break;
                case 1: __instance.ClickCleanup(); break;
                case 2: __instance.ClickSortByDestination(); break;
                case 3: __instance.ClickSortByCurrentLocation(); break;
                case 4: WaybillSummaryWindow.Show(); break;
            }
        });
    }
}
