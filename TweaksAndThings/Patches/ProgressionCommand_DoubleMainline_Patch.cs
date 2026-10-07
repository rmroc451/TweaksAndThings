using System;
using System.Linq;
using Game.Progression;
using HarmonyLib;
using UI.Console.Commands;

namespace RMROC451.TweaksAndThings.Patches;

/// <summary>
/// Makes generated milestones addressable through the native progression console command,
/// including finishing a phase that was already paid for but is waiting on deliveries.
/// </summary>
[HarmonyPatch(typeof(ProgressionCommand), nameof(ProgressionCommand.Execute))]
[HarmonyPatchCategory("RMROC451TweaksAndThings")]
internal static class ProgressionCommand_DoubleMainline_Patch
{
    private static readonly System.Reflection.MethodInfo? CompletePhase = AccessTools.Method(
        typeof(Progression), "PhaseCompleted", new[] { typeof(Section), typeof(int) });

    private static bool Prefix(string[] __0, ref string __result)
    {
        if (!DoubleMainlineSettings.Enabled) return true;
        if (__0 == null || __0.Length < 3) return true;

        bool advance = string.Equals(__0[1], "adv", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(__0[1], "advance", StringComparison.OrdinalIgnoreCase);
        bool revert = string.Equals(__0[1], "rev", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(__0[1], "revert", StringComparison.OrdinalIgnoreCase);
        if (!advance && !revert) return true;

        var progression = Progression.Shared;
        if (progression == null) return true;

        string requestedId = __0[2];
        // The runtime-added levels are appended to Progression.Sections, but Unity may not
        // expose an instantiated section clone beneath the progression transform in every
        // save/load state. Resolve the authoritative collection by identifier first.
        var section = progression.Sections?.FirstOrDefault(candidate => candidate != null &&
            string.Equals(candidate.identifier, requestedId, StringComparison.Ordinal));
        section ??= progression.GetComponentsInChildren<Section>(true).FirstOrDefault(candidate =>
        {
            var trackTag = candidate.GetComponent<DoubleMainlineProgressionTag>();
            var ctcTag = candidate.GetComponent<DoubleMainlineCtcProgressionTag>();
            return (trackTag != null &&
                    (string.Equals(trackTag.ProgressionId, requestedId, StringComparison.Ordinal) ||
                     string.Equals(trackTag.LegacyProgressionId, requestedId, StringComparison.Ordinal))) ||
                   (ctcTag != null &&
                    (string.Equals(ctcTag.ProgressionId, requestedId, StringComparison.Ordinal) ||
                     string.Equals(ctcTag.LegacyProgressionId, requestedId, StringComparison.Ordinal)));
        });
        if (section == null)
        {
            if (requestedId.EndsWith(".tnt", StringComparison.OrdinalIgnoreCase))
            {
                ModLog.Warning("Progression console command could not resolve generated section {SectionId}; known generated ids: {KnownIds}",
                    requestedId,
                    string.Join(",", progression.Sections?.Where(candidate => candidate != null)
                        .Where(candidate => candidate.GetComponent<DoubleMainlineProgressionTag>() != null ||
                                            candidate.GetComponent<DoubleMainlineCtcProgressionTag>() != null)
                        .Select(candidate => candidate.identifier) ?? Enumerable.Empty<string>()));
                __result = $"Generated progression section {requestedId} was not found in the current progression.";
                return false;
            }
            return true;
        }
        ModLog.Information("Intercepted progression command {Operation} for {SectionId}: available={Available}, paid={Paid}, fulfilled={Fulfilled}, phases={Phases}",
            advance ? "advance" : "revert", section.identifier, section.Available, section.PaidCount,
            section.FulfilledCount, section.PhaseCount);
        try
        {
            if (revert)
            {
                progression.Revert(section);
                __result = null!;
                return false;
            }

            if (!section.Available)
            {
                __result = $"Section {section.identifier} is not available.";
                return false;
            }

            if (section.FulfilledCount >= section.PhaseCount)
            {
                __result = $"Section {section.identifier} is already complete.";
                return false;
            }

            if (section.PaidCount > section.FulfilledCount)
            {
                // A normal advance already paid for this phase and ordered its deliveries.
                // Treat the console command as the explicit debug override it is and fulfill
                // that pending phase rather than calling Advance again (which throws "Already paid").
                if (CompletePhase == null)
                    throw new MissingMethodException(typeof(Progression).FullName, "PhaseCompleted");
                CompletePhase.Invoke(progression, new object[] { section, section.FulfilledCount });
            }
            else
            {
                // Advance(section) is the same native operation used by the built-in command;
                // unlike the purchase UI, it force-fulfills deliveries for that phase.
                progression.Advance(section);
            }

            __result = null!;
            return false;
        }
        catch (Exception exception)
        {
            var cause = exception is System.Reflection.TargetInvocationException { InnerException: not null } invocation
                ? invocation.InnerException
                : exception;
            __result = $"Could not {(advance ? "advance" : "revert")} {section.identifier}: {cause!.Message}";
            return false;
        }
    }
}
