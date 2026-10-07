# AE Local: physical industry switching

AE Local assigns a player locomotive and its coupled equipment to a host-managed switching job. It uses real waybills, native Auto Engineer movements, and normal coupling and air commands. It never teleports a sort. Physical movements still need validation in the game on the layouts you use; policy tests and game API checks cannot prove every switching move.

## Start and configure a job

1. Stop the locomotive and its coupled consist. NPC traffic, derailed equipment, and another local's assigned equipment cannot be claimed.
2. Select **AE Local** in the engine's control-mode dropdown. The engine holds while you configure the job.
3. Check industries for pickup visits. The checklist is grouped by area. Pending waybills on coupled cars are delivered even if their destination is not checked, including company-owned coal cars going to an unloading stand.
4. Optionally include nearby setouts and pickups. Nearby cars with pending waybills can be delivered regardless of the industry checklist. The initial manifest is snapshotted at start.
5. Pick a separate **yard return track** and **final engine parking waypoint**. The return track must be a named operational span in the originating area.
6. Choose **Start AE Local**. Each waypoint selection returns to setup and keeps the engine held until you start.

The engine-panel speed slider and the local window's speed selector change the saved job limit. Setup and paused jobs remain held; coupling approaches are capped at 3 mph or your lower requested limit. Native track, signal, fuel, water, oil, wear and player safety rules still apply. Trailing locomotives use MU and cut-out brakes.

Close the window without stopping the job. Reopen it through **AE cog → AE Local job / status**, or select AE Local again. Pause retains the equipment reservation. Cancel releases it for manual operation.

## Planning destination groups

The planner follows actual coupler connections from the power end. It groups contiguous cars by setout destination and compares accessible groups at both ends. Native graph routes supply distance, facing-point and trailing-point turnout counts, and required reversals. A cheaper group at the other end can justify a physical runaround after accounting for that extra move.

Cars in the middle are reached through physical cuts, staging and recoupling. The planner does not pretend that an inaccessible car has already been sorted. It evaluates both pull-in and shove-in spotting arrangements. Freight must fit its loading span; the power can remain outside that span on connected clear track. Native route searches check the full moving unit, other cars, and room for reversals.

A runaround is checked before power uncouples. The route must go around the stationary freight; a path through the freight string does not qualify. The native engineer reserves the train's full length plus turnout fouling clearance and an eight-meter margin for reversing moves. Dispatcher signals and switch protection stay in effect.

## Large cuts and deferred work

A failed berth, route-length or power check triggers binary size attempts: for example **16 → 8 → 4 → 2 → 1**. The single-car attempt remains available. This reduces the working group without declaring the omitted cars finished.

When the full consist cannot fit its home track or needs sorting space, the local looks for clear pockets on the selected return track and other yard/interchange spans in its originating area. It can stage the tail half, leave the leading half connected to power, and work that half. Smaller tail batches are tested if the first storage attempt does not fit.

Deferred cuts remain owned and saved as part of the job. Retrieving one string removes only that string from the deferred list. Older stored strings and the other half remain queued. The local chooses exposed ends using travel distance and destination-route cost.

An unavailable destination group can be peeled into storage to expose other work. That destination is rechecked after a ten-minute game-time delay. The local continues other accessible deliveries or pickups first. If every remaining task needs unavailable space, it waits and automatically retries rather than discarding the tasks.

This is a bounded route-and-cut planner, not a guarantee of the mathematically optimal answer on every custom track graph. No reachable safe storage, disconnected track, missing equipment, or an impossible power arrangement can still require assistance. Such holds explain the attempted plan in the roster and log.

## Making space in yards and industries

The local may temporarily claim and move stationary freight that obstructs its route or spotting area, including cars already at an industry destination. Pending delivery waybills are preserved, and those cars become delivery tasks so they can be returned to their destinations after making space. Freight without a pending delivery is returned to home-yard storage.

Other moving trains, locomotive units, NPC traffic and equipment assigned to another local remain protected. Repeatedly moving the same obstruction without completing any work is avoided; completing work permits another necessary relocation. Cars temporarily taken from another existing crew regain that crew when released.

A complete delivery or staged cut must be inside its intended span and clear of switches before detaching. Native AE's ordinary 'waypoint underneath the train' rule is insufficient for these moves. Detached cuts use the game's automatic consist handbrake helper, rather than setting every car's handbrake.

## Pickups, returns and completion

Industry pickups use uncompleted outbound waybills. Nearby pending company-car deliveries can also be included, even when they have no current operational position. Completed incoming waybills alone do not count as outbound pickup requests.

The local approaches an exposed end of a physical string. It collects the whole string when power allows, or halves the accessible pickup batch when necessary. The uncollected half remains for a later trip. Power checks use the return route from the pickup location, including grades and headroom. A car that cannot be handled individually can still require more power; the mod does not spawn replacement engines for player locals.

Selected-industry pickups bound for another selected industry are delivered on this job. Coupled and optional nearby direct-delivery waybills can serve other destinations as well. Other pickups and retained cars return to the home-yard track. Native industries handle loading, unloading and payment; AE Local does not award duplicate delivery payouts.

After all work, the power moves to its final parking target, applies parking brakes and returns to player control.

## Crew and ordered switch list

At job start, a locomotive without a crew gets a normal, joinable crew named **AEL {road number}**. An existing locomotive crew is reused. Assigned freight and reserved pickup tasks are added to that crew's native switch list. The active working cut comes first, followed by deferred and pending work. Unrelated entries already on the crew's list are preserved.

Select or join this crew through the game's crew interface to see its switch list. The mod does not move you out of another crew automatically. AEL crews are player-local crews, not the locked AI-only crews used by NPC traffic. The crew remains available for manual work after completion or cancellation.

## Status, logs and save/load

The engine roster shows completion percentage, abbreviated **S** setout / **P** pickup counts, phase and current action. Its tooltip provides full text. The local window shows the chosen spotting route, native engineer status, deferred-car count and crew. The locomotive's keyed notice updates the current task and area without repeated reposting.

Signal holds identify the signal and requested destination in a dispatcher message with a locomotive link. Unchanged holds are not repeatedly broadcast. Signals are not overridden to force a move.

The job saves the manifest, equipment ownership, working and deferred cuts, staging targets, destination retry times, crew and completed work. Reloading resumes native AE programming without replacing cars or forgetting the deferred half.

The mod log includes:

- **AE LOCAL PLAN:** groups, track distance, facing/trailing turnouts, reversals, berth/power checks, binary splits and staging decisions.
- **AE LOCAL ENGINEER:** rate-limited native engineer status changes.
- **AE LOCAL CREW / AE LOCAL SWITCH LIST:** crew creation and planned list updates.
- **AE LOCAL / AE LOCAL HOLD / LOCAL DISPATCH:** movements, explained holds and dispatcher requests.

Use the traffic window's Debug tab or its log-file link. Engine-roster text columns can be resized through the mod's General settings. The local window is resizable and its text wraps.

## Troubleshooting

- **No work:** Check waybills, pickup industries and the nearby-work option. A completed incoming waybill is not a new pickup order.
- **Waiting capacity:** Read the deferred count and retry time. The cars remain assigned; make space to let the next automatic retry succeed.
- **No safe storage:** The planner checked the selected track and other yard/interchange pockets. Clear a reachable pocket or cancel and select a better home track.
- **Blocked runaround:** Check for a physical route around the freight and sufficient room beyond the turnouts. Dispatcher-controlled switches may need routing.
- **Insufficient power:** The local tries smaller groups, down to one car. If one car still exceeds capability, cancel and assign a suitable locomotive unit.
- **Charging brakes or signal stop:** Read the native engineer field and locomotive notice before changing the plan. These are operational restrictions, not evidence that a task was completed.
- **Missing cars or unexpected coupling:** Inspect the manifest. The local holds rather than crediting work it cannot verify.

For problems, [report a bug or feature](https://github.com/rmroc451/TweaksAndThings/issues) with the save, debug log, locomotive identity, selected industries and yard/parking targets. See [Troubleshooting](TROUBLESHOOTING.md) for log locations.

## Interchange reuse

These physical operations do not replace the interchange backend's existing simulated cut transfers. Reusing AE Local for NPC interchange switching remains separate work requiring timing, backend and pool-power validation.
