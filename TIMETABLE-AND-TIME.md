# Timetable string charts, forecasts, history and time controls

The main timetable page presents trains as paths through time and locations. It combines the published plan, current train positions, recorded actual movement and a live forecast. These layers answer different questions: what was scheduled, where a train is now, what already happened, and what may happen next.

Related guides: [NPC traffic](NPC-TRAFFIC.md), [interchange services](INTERCHANGE-SERVICES.md), and [troubleshooting](TROUBLESHOOTING.md).

## Read the string chart

Time runs left to right and locations run vertically. Station spacing is schematic rather than geographical distance. Separate branch charts use the same clock scale, allowing you to compare junction traffic across branches.

| Display | Meaning |
| --- | --- |
| Solid blue path | Planned eastbound train |
| Solid amber path | Planned westbound train |
| Horizontal portion | Scheduled dwell at a station |
| White diamond | Explicitly scheduled meet |
| Red vertical line | Current game time |
| Named live marker | Current train position and speed |
| Dashed path | Forecast based on current conditions |
| Green path | Recorded actual running |

A geometric crossing of two train lines is not necessarily a scheduled meet. Look for the meet marker and selected train details. Click a path or its train symbol to show arrival/departure times and meet partners. **Expand time scale** gives more horizontal detail; **Fit day** returns to the full-day view. Scroll horizontally when needed. Trips crossing midnight wrap at the day edges rather than drawing a misleading line across the entire day.

The timetable header has **Maximize / Restore**. The native **Edit Timetable** action remains available for ordinary trains. NPC reservations and active service entries are protected against editing or deletion.

## Daily plans are visible before the trains spawn

At the start of a game day, the mod publishes saved plans for eligible simulated interchanges and random through freights. Interchange plans include estimated return trips. A mid-day load publishes the remaining day's applicable work. Native route planning is incremental and shared travel estimates are cached, so a complex railroad can take some time to finish publishing its plans.

Pending reservations have no physical car IDs or AI crews. They therefore do not appear as pre-spawn engines in the world, roster or crew list. When dispatch succeeds, the service gets its physical consist, protected timetable assignment and an AI-only crew. Players cannot join or reassign that crew. Completion removes the active entry and crew, while recorded movement can remain visible in today's chart.

The timetable snapshots are saved with the railroad. If an edit removes an NPC-owned row, the mod restores that row while preserving edits to ordinary trains. The lock exists to keep saved service progress and dispatch plans consistent; changing the symbol or deleting a column is not a supported way to cancel a train.

Interchange plans remain conditional on actual demand and safe placement. An empty or canceled reservation can disappear without a physical train. Blocked random departures retain their planned entry while waiting to retry. Future return estimates can change when the number of cars and transfer work become known.

## Intermediate locations and meet planning

Generated NPC plans include enabled timetable locations along their branch/junction path. The game's fast station-to-station estimates are used for first class and slow estimates for other classes, with route timing as a fallback. An ordinary intermediate passing entry has no dwell and does not force a generated through freight to stop.

Meet planning compares opposing NPC paths against all published timetable trains, including manually operated and human-crew trains. Sparse human schedules are expanded internally using native travel-time weights; their source entries, dwell and ownership are left intact. Waiting allowances are added to the new NPC plan, and opposing NPC plans can receive reciprocal meet markers. The planner uses a nearby usable siding and a five-minute clearance allowance.

Planning initially screens for approximately 400 m of usable siding. Actual routing still checks complete train length, occupancy, fouling clearance and signaling. A published meet is an estimate, not a signal clearance or reservation of the whole railroad. Human trains without a timetable have no published meet plan, but still occupy track and affect live routing.

After an interchange train finishes pickups, its timetable entry reverses the origin/destination and direction for the return journey. The service retains its symbol and crew. Separate outbound pool-power assignments receive their own entry.

## Current positions and dashed forecasts

NPC leads and crew-assigned player controlling locomotives are matched to track locations. Positions between stations are interpolated along cached routes. A player train needs a suitable crew/timetable association to appear as that timetable train's live marker.

Forecasts consider current speed, future track limits, scheduled departures/dwell, meets, passenger unloading and remaining interchange transfers. They do not assume every train will instantly reach track speed. A stopped train without a known release time, or a service blocked on work, shows an uncertain departure instead of a fabricated ETA.

Signals, acceleration, player switching, new pickup cars and changing yard conditions can invalidate an estimate. Compare the dashed path with the solid plan to identify developing delays; use the active-service waiting reason to investigate their cause. The chart performs bounded route work while open, reusing cached legs rather than searching every route on every frame.

## Recorded actual movement and variance

The host samples movement even while the chart is closed, at most once per game minute per train. The selected train's details show the latest observed HH:mm and estimated minutes early or late against its plan at the observed location.

History is saved with the railroad and shared with clients. It retains the current and previous day, with a total cap of 20,000 observations. Gaps longer than three game minutes or changes of locomotive break the green path instead of implying continuous observed travel. Today's completed trains can remain in the chart through these records. History starts when a version supporting recording is installed; it cannot reconstruct older running.

## Dispatch board markers

NPC plans automatically receive markers on loaded dispatch boards. Their labels include the symbol, departure time and direction. New markers begin stacked at board edges so the dispatcher can position them.

Manual marker moves and deletions are respected, including across reloads. Completing or canceling a service removes its mod-owned markers. At midnight, yesterday's mod-owned markers are cleared, with new-day markers supplied for active carry-over traffic. Player-created markers are preserved. A board marker is planning information, not permission for a train to enter an occupied block.

## Clock-only waiting versus simulation fast-forward

The Time window provides both native waiting actions and **Fast-forward simulation**. Simulation fast-forward actually accelerates the clock and all world movement together; it does not teleport selected trains to a destination or move only selected consists.

To use it:

1. Put a player controlling locomotive into **ROAD** or **WAYPOINT** mode with a valid route.
2. Open the clock's **Time** window and find **Fast-forward simulation**.
3. Select the player trains whose stopping should end the destination run.
4. Choose **2× to destination**, **4× to destination**, or **8× to destination**.
5. Use **Normal** to stop acceleration manually.

A destination run returns to normal when any watched train that has started moving stops. That can be its destination, a signal or another AE wait. It also stops if the selected train becomes unavailable or leaves ROAD/WAYPOINT mode. NPCs and trailing MU engines are not selectable player controlling locomotives in this panel.

For a time-limited run, enter **Stop time (HH:mm)** and choose **4× until stop time**. A valid time is 00:00–23:59; a time already passed is treated as its next occurrence. At least one suitable player ROAD/WAYPOINT train must still be selected. The time-limited option uses its requested clock boundary rather than the destination-run first-stop rule.

Both acceleration and relevant clock-only waits respect scheduled freight switching windows: timetable entries with distinct arrival and departure times. Fast-forward will not start in an active protected switching interval and ends at an upcoming boundary. Make sure the crew's freight timetable actually represents switching as a dwell interval; unplanned switching cannot be inferred from an arbitrary comment.

## Interchange dispatch interception

Simulated interchange waiting checks the requested clock interval and stops at the earliest needed NPC dispatch time. Pending approach calculations must finish before this boundary can be resolved. Demand is refreshed when requesting **Next interchange AI** or **Warp to next interchange AI**, and empty interchanges are excluded.

The warp target includes travel lead time and saved dispatch planning. A new NPC interchange/through train cancels an active wait and returns simulation acceleration to normal. Activating an outbound pool assignment also cancels it. Unused waiting time is discarded; start another wait afterward if appropriate. Failed spawn attempts do not trigger successful-spawn cancellation.

Normal company clock settings are preserved. The simulation multiplier is host-controlled, synchronized through game state and reset on load, unload or mod disable. Pausing, entering a native wait or a conflicting timing change can also end acceleration.

## Performance and troubleshooting

Higher multipliers run more normal physics steps per real second. They can increase CPU load and may not achieve their nominal rate on a large or busy railroad. Start with 2× before using 8×. Switching back to Normal is the first useful comparison when diagnosing choppy motion.

If live markers are missing, check the controlling locomotive's crew and timetable symbol. If a dashed path is uncertain, inspect the service wait rather than treating it as a rendering error. If fast-forward ends immediately, check switching windows, pause/wait state, selected AE mode and whether an NPC just spawned. For repeated holds or skipped dispatches, use the [troubleshooting guide](TROUBLESHOOTING.md) and [report the problem](https://github.com/rmroc451/TweaksAndThings/issues) with game time and train symbols.
