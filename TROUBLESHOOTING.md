# Troubleshooting and reporting bugs or feature requests

Start with the symptom and the affected train/car, then capture the mod's saved state and nearby log entries. A service waiting for safe track can look idle without being broken; a timetable plan can exist before a physical train has spawned. The checks below help distinguish those cases.

Feature guides: [NPC traffic](NPC-TRAFFIC.md), [interchanges and pool power](INTERCHANGE-SERVICES.md), [freight and industries](FREIGHT-AND-INDUSTRIES.md), [timetable and time controls](TIMETABLE-AND-TIME.md), [player tools](PLAYER-TOOLS.md), and [installation/builds](INSTALLATION-AND-RELEASES.md).

## Get the right log

The mod writes **TweaksAndThings-debug.log** inside its installed mod directory, normally:

~~~text
Railroader/Mods/RMROC451.TweaksAndThings/TweaksAndThings-debug.log
~~~

Open **AI traffic / interchanges → Debug**, then use **Open log file**. **Write debug snapshot** captures the current scheduler and services. **/npcTraffic** in the game's console returns a read-only snapshot and logs it. **/npcPickups** opens the delinquent pickup list.

Routine scheduler diagnostics go to this file rather than repeatedly echoing into the game console. Departure/approach telegraphs still appear in the native messages. A quiet console therefore does not establish that the scheduler is inactive.

The Debug tab shows the latest 160 lines, bounded to 32 KB. It polls once per real second while visible, avoids rereading an unchanged file and scrolls to the bottom when displayed text changes. Older errors may have fallen outside that tail. Use Open log file or copy the whole file when investigating them.

At approximately 5 MB, the file rotates to **TweaksAndThings-debug.log.previous**; one previous file is retained. Share both files if present. Entries use UTC wall-clock timestamps, while snapshots and telegraphs also identify game time. Make clear which time you mean when reporting “it stopped at 10:00.” A state snapshot is written approximately every two real minutes, and repeated detailed blockers are throttled to avoid log spam.

If neither file nor a new session entry appears, confirm UMM loaded and enabled the mod, verify the actual installed folder and check UMM's log for a startup or file-write failure. A stale railloader.log from before migration is not the current mod log. UMM logs and the game's current Player.log can help with startup, native errors or rendering; include them when relevant rather than relying only on a legacy loader file.

## No interchange train appears

Check in this order:

1. **Authority:** run the scheduler on the host or in single player. Client-only installation cannot create host services.
2. **Mode:** confirm Simulated delivery / pickup is selected. The through-traffic toggle alone does not replace normal interchange service.
3. **Demand:** inspect inbound orders and eligible outbound waybills. Empty service demand intentionally produces no inbound train.
4. **Timing:** compare the current game time with dispatch time, not just the interchange service hour. Wait for incremental approach planning to finish.
5. **Existing work:** inspect active inbound/outbound services and parked pool power. A pool assignment may collect pickups without requiring another empty inbound train.
6. **Route:** use Interchanges navigation to inspect spawn and service targets. Look for disconnected or disabled track and obsolete overrides.
7. **Capacity and clearance:** check the full train's staging space, yard fit, usable passing siding and route authority to a safe onward refuge.
8. **Catalog:** inspect POWER CHECK and CONSIST entries for unsuitable/missing locomotive definitions, steam tender problems or unavailable saved custom freight types.

CTC is not required, and enabled unowned track may be used by NPC operations. Buying Sylva track or adding a caboose is not a general fix for a dispatch blocker. Repeated failed attempts should produce a status/snapshot, even when no locomotive notice exists.

## Engines arrive without freight cars

A pickup-only inbound service can legitimately arrive with power alone. A completed no-pickup service can leave power parked, and an outbound pool service spends time collecting cars before departure. Compare its phase, planned inbound count, setout progress and actual pickup reservations before treating the engine-only appearance as a failure.

If the snapshot shows planned/attached inbound cars but only engines are physically present, capture the service ID and nearby **CONSIST**, **PLACEMENT**, loading and transfer entries. Include whether the discrepancy appears on the host, clients or both. The mod publishes native placement snapshots for transfers, but multiplayer visibility still requires physical in-game verification.

Do not try to repair an active NPC by manually breaking its consist. Use **Reestablish programming** when there is enough clear track. That action preserves saved accounting; it does not clear a missing-delivery record.

## Too much power, missing tender, wrong orientation or immediate derailment

Report the actual freight weight, power count/types, route, spawn location and service ID. POWER CHECK shows grade/speed constraints and power after locomotive/tender weight. Long sustained grades and a larger reserved return pickup can explain extra engines, while a short grade spike should not simply be applied to the whole train.

Steam locomotives must have their defined and connected tenders. The lead should face the destination with cars behind it; randomly flipped trailing MU units are permitted. A missing tender, lead behind the cars, or immediate derailment is a useful regression report even if the build and policy tests passed.

Include the full spawn/transfer log interval and a save just before placement when possible. A screenshot should show both ends of the train and nearby switches, not just its locomotive controls. Custom track/car definitions and game version are especially relevant.

## Holding for clearance or in a passing siding

Inspect native signals, switch locks, the full consist's position relative to both siding throats and any opposing approach. A physically empty stretch can still conflict with another train's reserved corridor. Planned meet times also do not replace live movement authority.

In manual CTC, the dispatcher must provide the appropriate signal/switch clearance. On uncontrolled track, stopped waypoint NPCs retry native car-aware routing; recovery triggers an immediate attempt. An alternate route must actually be connected and usable for the complete train.

If two trains hold indefinitely, record both symbols, locations, directions, speeds, hold reasons and intended destinations in one snapshot. Include whether either is human/manual, CTC mode and the exit-switch IDs from routing diagnostics. Clear a fouled throat or genuinely occupied refuge before trying recovery. Already trapped nose-to-nose trains may need dispatcher intervention; the current logic is not a guarantee against every possible deadlock.

## Setouts or pickups appear stalled

Normal transfer batches take about two game minutes per car, or one with free nearby-caboose assistance. Check game time and phase rather than assuming an operation should complete immediately in real time. Yard space, backend acceptance and room behind the service train can extend the wait.

For setouts, inspect available interchange spans and protected locomotive exit paths. Capacity deferrals should appear under Held deliveries. For pickups, inspect the exact waybill destination and recorded pickup span, including an off-map loader's span for purchased coal. A car reserved by another active outbound pool assignment is excluded from a second train's new pickup list.

If a delivered car remains unclickable, determine whether it is a completed setout or still attached to an active NPC pickup. Successful setouts should lose the NPC marker and AI crew. Capture car ID, service progress and **SETOUT RELEASE** entries for a stale restriction.

## Pool power cannot move or uncouple

Internal pool-unit connections are user-locked by design, including in Sandbox. Only outer connections, gladhands and anglecocks are available. The automated interchange service can detach stopped pool power and split surplus engines while keeping tenders with their steam engines.

A player locomotive attached to pooled power cannot pull it outside its recorded native game area. Check the locomotive notice and try moving back toward that area; detach at an outer connection to leave without the pool. The restriction is not a configurable radius around the locomotive's current point.

If an outer connection is incorrectly locked, or inward movement is inhibited, capture both car IDs, pool-unit ID, recorded Area ID, coupling orientation and position. For failure to reuse power, check whether the unit is moving, incomplete, or lacks capacity/clearance for the assignment.

## Missing switch-list cars, incorrect premium or duplicate orders

Confirm the current crew/list and whether a client request is still pending host confirmation. Unwaybilled cars and repair orders can have different labels from ordinary freight waybills. The railroad-wide delinquent list is a separate view.

For held-delivery bonuses, inspect the original bonus start, deadline and current game time. Repeated delays do not reset that clock. The configured premium is an extra percentage of base payout, separate from native fines/bonuses. For pickup penalties, include the first and second missed attempts and the ledger entry reclaimed.

For suspected duplicate industry orders, capture the industry/destination, pending native orders and held reservations before and after the new order. For pulpwood, include requested/accepted car counts, fee setting and actual fulfilled load capacity.

## Choppy movement and recurring lag spikes

1. Return simulation to **Normal**; higher multipliers deliberately run more physics per real second.
2. Let initial terrain loading and day-plan/approach generation complete, then compare steady running.
3. Capture a fresh debug snapshot and inspect UMM output for **Slow mod update** reports identifying the component and elapsed time.
4. Compare with the chart/Debug window closed, noting the difference without changing several settings at once.
5. On a backed-up test save, compare with the mod disabled to distinguish mod work from the game's terrain/rendering load or another mod.

Approach searches are incremental and cached, but a single synchronous native route call can still exceed the nominal per-frame planning budget. Large graphs, many simultaneously active trains and 8× physics can remain expensive. The visual symptom of trees moving a few frames at a time indicates stalled frames; it does not prove that tree animation caused the stall.

Record the approximate real-time interval between spikes, train count, graph/custom-map size, multiplier, active traffic options and relevant slow-update timestamps. A profiler capture or before/after frame-time measurement is stronger evidence than a single FPS estimate. See the [historical performance investigation](docs/performance-investigation.md) for development observations, rather than treating its older implementation notes as current configuration instructions.

## Fast-forward or UI controls do not work

Fast-forward needs host authority, at least one selected player controlling engine in ROAD/WAYPOINT mode, a loaded/unpaused railroad and no conflicting wait or active switching block. Destination runs end on a selected train's first stop after moving; NPC activation also returns the simulation to normal. See [Timetable and time](TIMETABLE-AND-TIME.md).

If the toolbar icon is absent or unclickable, try the timetable **AI traffic** tab or UMM entry and record whether that alternative opens. For overlapping buttons, missing tags or unexpected chart layouts, include UI scale/resolution and a screenshot. Host-only grayed settings on clients are expected.

For installation/build errors, use the symptom table in [Installation and releases](INSTALLATION-AND-RELEASES.md). Missing Assembly-CSharp/Ops references, missing dotnet, missing test runtime and failed package restore are separate problems and need their exact error output.

## Report a bug or propose a feature

Use **Report a bug / feature** in UMM or the traffic Settings tab, or open the [GitHub issues list](https://github.com/rmroc451/TweaksAndThings/issues). Search for an existing report first; use [New issue](https://github.com/rmroc451/TweaksAndThings/issues/new) for a new reproducible problem. Automated traffic work is associated with [issue #63](https://github.com/rmroc451/TweaksAndThings/issues/63), but a separate regression report can make a specific failure easier to track.

Suggested bug-report template:

~~~text
Title: Short description including affected feature/interchange

Mod version/build:
Railroader version:
Single player or multiplayer; host/client:
Host/client mod versions if multiplayer:
Other relevant mods/custom track/car definitions:

Steps to reproduce:
1.
2.
3.

Expected behavior:
Actual behavior:

Game day/time and relevant real/UTC log time:
Train symbol/service ID/pool ID:
Car IDs, waybill destination and pickup span if relevant:
CTC mode, traffic mode, simulation rate:

Attached: current debug log, .previous log if present,
snapshot, relevant UMM/native error output, screenshots,
and a reproducing save if available.
~~~

Keep the original logs intact and share the interval surrounding the problem. Before posting publicly, review attachments for personal paths, multiplayer player names or unrelated private data such as webhook credentials. A feature request should describe the desired player workflow, a concrete example and how it differs from current behavior; logs are optional unless it also describes a failure.
