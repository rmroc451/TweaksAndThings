# Timetable string charts, forecasts, history and time controls

The chart header's **Spacing: even rows / Spacing: track distance** button toggles proportional spacing. In distance mode, each gap represents its share of the displayed connected network's track distance, using the same distance scale across branches. Short spans draw closer together. Grid lines, planned paths, recorded history, live forecasts, markers and click targets all use that spacing. Distances are measured along track routes and cached; a branch with unresolved distances explicitly falls back to even rows.

Each location label includes its surveyed mile marker, rounded to the nearest tenth of a mile. The mod interpolates between the game's milepost objects and preserves their prefix. **MP —** means no usable surveyed markers could be resolved. Mile markers appear in both spacing modes.

The green history line uses connected locked locations as anchors, matching the live marker. Older samples that skipped those locations are reprojected along the active train's route when possible. Return timetable symbols retain their published plan, but do not show a live dashed estimate or marker until the locomotive actually begins that return service.

The main timetable page presents trains as paths through time and locations. It combines the published plan, current train positions, recorded actual movement and a live forecast. These layers answer different questions: what was scheduled, where a train is now, what already happened, and what may happen next.

Related guides: [NPC traffic](NPC-TRAFFIC.md), [interchange services](INTERCHANGE-SERVICES.md), and [troubleshooting](TROUBLESHOOTING.md).

## Physical reach, passing locations and running allowance

The string chart includes locked locations when enabled physical track connects them to the player's usable railroad. They stay grey and italicized. An unfinished bridge or another disabled track section breaks that connection: locations beyond it are hidden until the connection is restored. For example, connected East Whittier–Dillsboro–Sylva territory remains visible before purchase, while Ela West and Bryson are hidden when the intervening bridge is out. Changing a switch position, a red signal or temporary occupancy does not remove locations from the chart. A Bryson start uses Bryson's connected area instead of assuming East Whittier is the player's starting point.

Interchange train markers and their forecasts follow the service's full physical route. Passing Dillsboro or another intermediate location does not end that route or remove the train marker. Platforms on parallel tracks are anchored to that route for chart placement. A passing timestamp is a prediction target: it does not add a horizontal scheduled wait to the forecast. Actual meet holds, signals and service work can still produce waits.

New interchange plans add **15% travel allowance plus two game minutes**, in addition to the existing five-minute dispatch margin. The allowance is distributed across the passing times, with the inbound service time kept at the interchange's configured time. Return estimates receive the same travel allowance. Already published schedules retain their saved times; the new allowance applies to newly generated plans and return schedules.

When an interchange train has more than two minutes of predicted schedule slack, its engineer gently reduces its permitted speed. This pacing never raises a track or signal limit and never orders a stop merely for an early passing time. Pacing is removed when the train runs late or reaches the final 300 metres of its approach, so it can enter and clear the yard normally. The configured service time and genuine safety stops still apply. The debug log records occasional **PACING** messages and reports route-projection failures under **TRAFFIC** for troubleshooting.

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

New-day planning waits until the game has evaluated industry performance, applied requested contract tiers, and finished daily industry accounting. Initial planning also waits for the restored industry collection and pending first-time contracts. The same gate applies to warp-triggered planning, so an early check cannot permanently save a zero-tier daily schedule.

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

The fast-forward panel also offers **2× fast clock**, **4× fast clock**, and **8× fast clock**. These run without selecting a destination train, including when locomotives are stopped in AE YARD, ROAD, or WAYPOINT mode. The stop-time option likewise does not require a selected moving train. Every player controlling locomotive on the railroad must be in AE: manual mode blocks starting acceleration, and changing a player controlling locomotive to manual returns the host and clients to normal speed. NPC trains and pooled power are excluded from this eligibility check, including parked pool engines with AE switched off. Fast clock can also run when there are no player controlling engines to check. Trailing MU engines follow their controlling engine rather than requiring independent AE orders. NPC spawns, scheduled switching windows, pause/wait state and unload still stop acceleration. Use **Normal** to end an unrestricted fast-clock run.

NPC timetable generation includes locked locations in its station links, endpoint selection, and intermediate passing entries. Ownership does not truncate the planned journey. The string chart includes those locations in grey italic text and orders the mainline with Andrews at the top and Sylva at the bottom. On load, older saved NPC plans gain missing intermediate passing entries while retaining published arrival/departure times, meets and switching waits. Active through trains retain their current target through a remapped entry index. Player timetable source is unchanged.

The native usable-timetable ownership filter removes locked stations and can discard an NPC column entirely. The mod preserves complete entries for its protected NPC trains through that filter. The string chart also reads saved NPC plans directly, so they remain visible before the company's player timetable feature is unlocked. Player timetables continue to follow native progression rules.

Waiting across midnight pauses at the new day until industry tier evaluation finishes, then regenerates NPC planning. **Next interchange AI** recalculates its dispatch target before continuing. A successful NPC spawn still ends the wait. An unsuccessful dispatch retains its pending service time instead of silently advancing to the following service.

When simulated service has no demand, **Next interchange AI** uses the game's next normal interchange service time instead of refusing to wait. This covers a new railroad whose industries remain at tier zero until the first new day. Midnight still pauses for tier evaluation. If the new tiers create demand, the remaining wait changes to the earliest needed NPC dispatch, including travel lead time. Otherwise it continues to the normal service time. No enabled scheduled interchanges still produces an explanatory message.

At that midnight checkpoint, the mod also finishes publishing timetables for the operating day that has just begun: roughly the upcoming 24 hours, plus the following morning's interchange service when its approach must depart during that operating day. Random freight counts use the evaluated active industry tiers. Route planning remains spread across frames, and the clock waits for pending calculations and publication. Unavailable routes retain their retry status rather than holding the clock forever.

NPC corridor grants apply to generated trains. Player ROAD and WAYPOINT trains use native AE routing, occupancy and signal checks, including yard movements. NPCs still consider player trains when checking their own routes. A distant switch lined against ROAD movement ends that train's known lookahead; it does not abort another interchange's dispatch.

Simulated interchange waiting checks the requested clock interval and stops at the earliest needed NPC dispatch time. Pending approach calculations must finish before this boundary can be resolved. Demand is refreshed when requesting **Next interchange AI** or **Warp to next interchange AI**, and empty interchanges are excluded.

The warp target includes travel lead time and saved dispatch planning. A new NPC interchange/through train cancels an active wait and returns simulation acceleration to normal. Activating an outbound pool assignment also cancels it. Unused waiting time is discarded; start another wait afterward if appropriate. Failed spawn attempts do not trigger successful-spawn cancellation.

Normal company clock settings are preserved. The simulation multiplier is host-controlled, synchronized through game state and reset on load, unload or mod disable. Pausing, entering a native wait or a conflicting timing change can also end acceleration.

## Performance and troubleshooting

Higher multipliers run more normal physics steps per real second. They can increase CPU load and may not achieve their nominal rate on a large or busy railroad. Start with 2× before using 8×. Switching back to Normal is the first useful comparison when diagnosing choppy motion.

If live markers are missing, check the controlling locomotive's crew and timetable symbol. If a dashed path is uncertain, inspect the service wait rather than treating it as a rendering error. If fast-forward ends immediately, check switching windows, pause/wait state, selected AE mode and whether an NPC just spawned. For repeated holds or skipped dispatches, use the [troubleshooting guide](TROUBLESHOOTING.md) and [report the problem](https://github.com/rmroc451/TweaksAndThings/issues) with game time and train symbols.
Protected NPC trains in the legacy timetable editor show their location names, including a `(locked)` label for calls through connected locations awaiting progression. Their schedules remain read-only. Player station selection continues to require unlocked locations.
Track-distance spacing uses unrounded surveyed or calculated milepost differences when adjacent locations share a milepost prefix. Thus MP T46.3–T47.8 occupies one quarter of the height of a six-mile interval at the same scale. Sidings without their own milepost are projected onto the surveyed route from nearby markers, cancelling the extra distance along the siding. Locations without a resolved native track marker can snap from their map-feature position. A gap without compatible mileposts uses routed track distance, then map distance if routing is unavailable; one such gap no longer forces the entire branch into even-row spacing.
Each string chart now has a dedicated explorer viewport. Location names and mileposts remain pinned at the left while the graph moves horizontally; clock headers remain at the top while the graph moves vertically. Drag inside the graph to pan in both directions, and use the mouse wheel over it to zoom around the pointer. Clicking a line still selects its train, while dragging does not select a train. Zoom ranges from half size to four times size, and the existing Fit day / Expand time scale control resets the base time scale when rebuilding the chart.

Interchange return reservations include time for all planned delivery and pickup cars plus five minutes for repositioning. They are revised when the inbound manifest is confirmed and again when arrival confirms pickups and any full-cut delivery. An existing active service receives the corrected reservation after loading this version. Live forecasts use the actual loaded manifest after preparation, so deferred cars are not counted again as pending transfers. The actual return schedule is still registered at departure after servicing finishes.
Newly generated NPC services use short numbered train symbols inspired by mid-century employee timetables. Odd numbers run westward and even numbers eastward. Interchange locals and pool-power outbound services use 301–398; first-class through freights use 101–198, second-class through freights 201–298, and third-class through freights 401–498. The interchange return normally takes the paired opposite-direction number, such as 301/302, and the locomotive changes to that symbol when the return actually begins. Occupied player and NPC symbols are skipped. Very busy saves can use section notation such as `2-301`. Existing saved letter-prefixed assignments remain unchanged.

These are fictional number assignments for this railroad, not a reconstruction of historical Murphy Branch schedules. For an example of the period's numbered freight columns and odd/even opposing trains, see the [Camas Prairie employee timetable effective August 13, 1950](https://research.nprha.org/NP%20Timetable%20Scans/CP%2019500813%20ETT%20094.pdf).

During fast clock, camera panning, mouse-look smoothing, scroll-wheel zoom, ground following and UI movement counters retain normal timing. The mod removes only its own fast-clock multiplier from these input/camera calculations, including the strategy camera's repeated physics updates. Train simulation and the game clock remain accelerated; native pause and toolbar visibility behavior remain intact.

## Completed NPC trains and visible hours

Completed NPC timetable entries remain visible and protected until the next new day, including their recorded actual path. The equipment and AI crew can be cleaned up immediately; the day's timetable history stays available. Retention is saved with the game. At new day the completed NPC entries are removed.

The string chart fits the planned service range: one hour before the earliest entry and one hour after the latest planned finish. Overnight schedules remain continuous beyond midnight. Dragging, zooming and pinned axes continue to work within that range.
