namespace RMROC451.TweaksAndThings;

/// <summary>
/// Developer switches for comparing signal placement while the second mainline is being tuned.
/// Keep this code-only so changing the placement strategy does not become a player setting.
/// </summary>
internal static class DoubleMainlineSettings
{
    internal static bool Enabled =>
        TweaksAndThingsPlugin.Instance?.settings?.EnableExperimentalSecondMainlineProgressions == true;

    // Independent placement is the working layout. Set to true to mirror existing CTC signal
    // markers onto the parallel line while comparing the two arrangements in game.
    internal static bool MirrorExistingCtcSignalLocations { get; set; } = false;

    // Temporary in-session diagnostic override for the generated track renderer's frustum culling.
    internal static bool ForceGeneratedTrackVisible { get; set; }

    // Railroader map coordinates use feet. The extra clearance around tunnels is deliberately
    // larger than ordinary double-track spacing to avoid the neighboring tunnel meshes clipping.
    internal const float TrackCenterSpacing = 18f;
    internal const float TunnelCenterSpacing = 32f;
    // Probe broadly enough to notice roadside objects, telegraph poles and tree colliders
    // near the proposed rail centerline. Side selection is evaluated at every route node.
    internal const float PlacementObstacleProbeRadius = 12f;
    // Prefer an existing mainline switch close to the native group's start when creating
    // the second-mainline entry. The s2 switch by the Bryson signal is within this window.
    internal const float EntrySwitchSearchDistance = 2400f;
    internal const float PassingSidingTransitionLength = 600f;
    internal const float PassingSidingMinimumLength = 250f;
    internal const float PassingSidingMaximumRouteSpan = 3000f;
    internal const float TunnelCrossoverClearance = 120f;
    // Crossovers are laid out over a long turnout-like transition so they meet the
    // existing track with a shallow angle instead of a short, sharp diagonal.
    internal const float CrossoverTransitionLength = 1200f;
    internal const int CrossoverCurveSamples = 16;
}
