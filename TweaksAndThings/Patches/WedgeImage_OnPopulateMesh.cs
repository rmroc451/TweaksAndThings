using HarmonyLib;
using Railloader;
using UI.ContextMenu;
using UnityEngine.UI;

namespace RMROC451.TweaksAndThings.Patches;

[HarmonyPatch(typeof(WedgeImage))]
[HarmonyPatch(nameof(WedgeImage.OnPopulateMesh), typeof(VertexHelper))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal class WedgeImage_OnPopulateMesh_Patch
{
    private static void Postfix(VertexHelper vh)
    {
        TweaksAndThingsPlugin tweaksAndThings = SingletonPluginBase<TweaksAndThingsPlugin>.Shared;
        if (!tweaksAndThings.IsEnabled) return;

        vh.Clear(); //clear the image backgrounds for now.
    }
}
