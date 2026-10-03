using System.Linq;
using Track.Signals;
using UnityEngine;
using Game.State;

namespace RMROC451.TweaksAndThings;

internal sealed class BrysonCtcMirror : MonoBehaviour { }

internal static class BrysonCtcMirrors
{
    private static float nextCheck;
    internal static void Update()
    {
        if (Time.realtimeSinceStartup < nextCheck || RestoreNotifier.Shared == null || !RestoreNotifier.Shared.HasRestored) return;
        nextCheck = Time.realtimeSinceStartup + 1;
        if (CTCPanelController.Shared != null) Add(CTCPanelController.Shared);
    }
    internal static void Remove()
    {
        foreach (var mirror in Object.FindObjectsByType<BrysonCtcMirror>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            mirror.gameObject.SetActive(false);
            Object.Destroy(mirror.gameObject);
        }
        var panel = CTCPanelController.Shared;
        if (panel != null) panel._panelGroups = panel.GetComponentsInChildren<CTCPanelGroup>(true).Where(g => g.GetComponent<BrysonCtcMirror>() == null).ToArray();
        nextCheck = 0;
    }
    internal static void Add(CTCPanelController panel)
    {
        if (panel.GetComponentInChildren<BrysonCtcMirror>(true) != null) return;
        var groups = panel.GetComponentsInChildren<CTCPanelGroup>(true);
        if (groups.Length == 0) return;
        var reference = groups.FirstOrDefault(g => g.switchKnob != null && g.signalKnob != null &&
            Vector3.Distance(g.switchKnob.transform.position, g.signalKnob.transform.position) > 0.01f);
        // The panel is a world-space model. Derive its downward direction from
        // the existing switch/signal row rather than assuming world Y is down.
        Vector3 down = reference == null ? -panel.transform.up :
            (reference.signalKnob.transform.position - reference.switchKnob.transform.position).normalized;
        float spacing = 0.35f;
        foreach (var group in groups)
        {
            var renderers = group.GetComponentsInChildren<Renderer>(true);
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (var renderer in renderers) {
                var bounds = renderer.bounds;
                float center = Vector3.Dot(bounds.center, down);
                float radius = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(down.x), Mathf.Abs(down.y), Mathf.Abs(down.z)));
                min = Mathf.Min(min, center - radius); max = Mathf.Max(max, center + radius);
            }
            if (renderers.Length > 0) spacing = Mathf.Max(spacing, max - min + 0.06f);
        }
        // Snapshot stand-alone block/signal lamps before cloning any columns.
        var lamps = panel.GetComponentsInChildren<CTCPanelLamp>(true).Where(l => l.GetComponentInParent<CTCPanelGroup>() == null).ToList();
        foreach (var original in groups)
        {
            var copy = Object.Instantiate(original.gameObject, original.transform.parent);
            copy.name = original.name + " — mirrored second track";
            copy.transform.position = original.transform.position + down * spacing;
            copy.AddComponent<BrysonCtcMirror>();
            var group = copy.GetComponent<CTCPanelGroup>();
            group._switchKnobExists = original._switchKnobExists;
            group._signalKnobExists = original._signalKnobExists;
            group.UpdateForMode(panel.SystemMode);
        }
        foreach (var original in lamps)
        {
            var copy = Object.Instantiate(original.gameObject, original.transform.parent);
            copy.name = original.name + " — mirrored second track";
            copy.transform.position = original.transform.position + down * spacing;
            copy.AddComponent<BrysonCtcMirror>();
        }
        // Include copies in ABS/CTC visibility updates, but keep the original
        // button observers so a shared code-button ID executes exactly once.
        panel._panelGroups = panel.GetComponentsInChildren<CTCPanelGroup>(true);
        ModDiagnosticLog.Write("CTC", "Added mirrored lower row to " + panel.name + "; columns=" + groups.Length + "; lamps=" + lamps.Count);
    }
}
