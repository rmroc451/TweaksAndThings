# Simulated interchange services and parked pool power

Simulated interchange service replaces instantaneous yard delivery with an NPC train that travels to the interchange, transfers cars, and handles outbound work. The game's interchange backend remains responsible for ordering, waybills, charges, track allocation, blocking difficulty, sales and off-map loading/return behavior.

Related guides: [NPC traffic](NPC-TRAFFIC.md), [held deliveries and pickup penalties](FREIGHT-AND-INDUSTRIES.md), [timetables and time controls](TIMETABLE-AND-TIME.md), and [troubleshooting](TROUBLESHOOTING.md).

## Arrival tracks and clearing the main

Inbound NPC services prefer completely empty interchange service spans. The engineer aims for the far end of a reachable span, leaving clearance at its end and room for the whole train behind the locomotive. Another active service's reserved arrival span is excluded. The mod checks the berth again near the interchange, so cars added while the service is traveling can change its arrival track.

Dispatch limits the train to both the route's passing-siding capacity and the longest interchange service span with arrival clearance. Excess deliveries stay in native orders for a later trip; excess pickups remain for another service. This prevents a road-length train from being dispatched when it cannot fit into any destination service track.

If no empty span fits, the service looks for a clear entrance pocket on an occupied span. It checks both the final train footprint and the route into that pocket; a vacant space beyond parked cars is not treated as reachable. Setouts and pickups begin only after the train has stopped near its target and the whole consist is inside its service span. When no safe berth fits, its status reads **Waiting for an interchange track long enough to clear the main**. Clear enough track for the complete incoming consist; the service retries automatically.

The debug log records berth changes under **ARRIVAL**, including the span, target, train length and end clearance. Include those entries and the saved railroad in a [bug report](https://github.com/rmroc451/TweaksAndThings/issues) if a train stops short or cannot enter a visibly available track.

## First service: setup checklist

1. Load a railroad in single player or as the multiplayer host.
2. Open **AI traffic / interchanges**, then **Settings**.
3. Change **Interchange service** from **Default (auto-magic)** to **Simulated delivery / pickup**.
4. Confirm the interchange is enabled in the game and has its native service time configured.
5. Leave **Spawn / exit** and **Service point** overrides blank initially.
6. Open **Interchanges** and inspect the planned dispatch time, route and status.
7. Ensure there are actual inbound orders or eligible pickup cars and clear track for the complete train.

No CTC coverage, purchased locomotive template, caboose, special timetable symbol or manually entered span ID is required for normal automatic setup. Enabling the mode does not immediately create a service without demand.

The default remains the game's auto-magic mode. Switching modes allows already active simulated services to finish; it is not a command to erase their physical cars or progress.

## Service time and dispatch time are different

A train must leave early enough to reach the yard by the configured service time. The basic dispatch calculation subtracts estimated travel time and a five-minute margin. Published timetable planning can supply a saved dispatch time with meet allowances, so use the displayed dispatch time rather than recalculating only from straight-line distance.

For example, a service configured for 10:00 with a 45-minute approach has a basic dispatch time of 09:10. Placement problems or occupied corridors can delay the actual departure. A train that arrives early waits for its configured service time before starting interchange work.

**Warp to next interchange AI** targets the earliest needed dispatch across eligible interchanges, including the travel lead time. It does not simply jump to the next yard service hour. Failed placement attempts are shown through diagnostics and can have no physical progress notice because no locomotive was created.

## Automatic spawn selection and overrides

**Nearest map / track edge** favors usable terminal ends near the enabled graph's outer bounds, then proximity to the interchange. **Farthest reachable track edge** ranks connected terminal approaches by track distance. These are track-graph edges, rather than the terrain boundary.

The initial location is an approach reference. The complete train may extend across several connected segments. Placement scans inward from the terminal and refines the closest fitting bracket using the native whole-consist clearance helper. It checks staging space and existing cars and may try another terminal. A short individual span does not by itself require a manual override.

Physically enabled unowned track can participate in NPC routes and placement. Progression-disabled or disconnected track cannot. Player placement rights remain unchanged.

If automatic selection is unsuitable for a custom layout, use native location strings for **Spawn / exit** and **Service point**:

~~~text
segment-id|a-or-b|distance-in-metres
~~~

These fields are native graph locations, not station names or industry names. Copy a verified location from diagnostics or your layout tools rather than guessing an ID. Overrides take precedence but still undergo complete-train placement checks; an override does not force a train into occupied or insufficient track. The Interchanges navigation menu can locate or teleport to the calculated spawn and service target to help inspect the result.

## How cars move through a service

| Phase | Meaning | Common reason for waiting |
| --- | --- | --- |
| Preparing consist | Power is placed and native deliveries are attached to the remote service train. | Missing definitions, no room, or backend work not yet accepted. |
| In transit / Approaching | The train is traveling toward its service target. | Signals, route authority, a meet, or blocked track. |
| Waiting for configured service time | The train arrived ahead of schedule. | Normal early arrival. |
| Setting out | Incoming cars are moved to native interchange tracks. | Yard capacity or departure-path protection. |
| Picking up | Reserved eligible outbound cars are accepted by the native backend and attached. | Pickup no longer present, backend acceptance, or room behind the train. |
| Departing / In transit (return) | Power leads the pickup cars toward the exit. | Route authority or room to orient the departure consist. |
| Parked pool power | Work finished without outbound cars to take. | Waiting for a future service request with pickups. |

Transfers occur in groups of up to two cars. The normal rate is approximately two game minutes per car: a two-car batch takes about four minutes and a final one-car batch about two minutes. A blocked operation can take longer; the timer is not permission to overlap occupied cars.

A caboose on interchange tracks or within the configured nearby-caboose search radius halves transfer time to approximately one minute per car. When that radius is unset, the fallback is 500 feet. This benefit charges no money or crew hours. Adding or removing assistance during a batch rescales its remaining work.

Each interchange maintains independent saved service progress. Multiple interchanges can work at once, and an outbound pool service can coexist with a new inbound service. Traffic notices and timetable entries distinguish the trains even when they share an interchange.

## Full yards, compact setouts and safe exits

Before dispatch, the native placement fitter checks what can fit at the destination. Randomly selected deliveries are removed from the planned load until the remaining cars fit. Deferred cars stay reserved for later service. If nothing can be delivered and nothing can be picked up, the mod skips the new inbound train; a pickup-only service may legitimately approach with locomotives alone.

Yard conditions can change during travel. At arrival, cars that no longer fit can be held off-map using the game's bardo storage with their identities, loads and waybills retained. They enter the held-delivery accounting described in [Freight and industries](FREIGHT-AND-INDUSTRIES.md).

Setouts prefer available space beside stationary freight cuts and join nearby exposed couplers where possible. This reduces the space wasted by repeatedly placing isolated two-car cuts. Placement protects the power's potential departure paths and switch fouling clearance. Moving cuts, cuts attached to locomotives and other active NPC consists are excluded from consolidation.

Successful setouts immediately lose their NPC marker and AI crew assignment. Players can then inspect, couple, move and assign them normally. Cars still on an active service remain protected. This distinction also applies when an older saved service contains a stale restriction on an already completed setout.

## Outbound pickup selection

The incoming service snapshots eligible pickup cars at dispatch so its return power can be sized for them. At arrival it can accept additional cars placed on eligible pickup tracks while en route, within 90% of installed return-route power capacity and the passing-siding length limit. Original reservations retain priority.

Eligible pickups include native exports/sales and player-owned cars sent to the interchange's off-map loaders, such as purchased coal cars. A loader's own destination and pickup spans are used; an ordinary owned car is not exported merely because it is parked nearby. Native loader charges and off-map return scheduling still apply.

Cars beyond the capacity/length limit remain at the interchange for later work. They are not considered missed just because they were deliberately left outside the reservation. A reserved car removed from its recorded pickup span can become delinquent, with a second missed attempt reclaiming its credited payout. See the freight guide before moving a reserved pickup away.

## Parked power is a service pool

When a service finishes its work without collecting outbound cars, its locomotives and tenders stay at the interchange. Handbrakes are applied and autonomous orders are turned off. The unit, native parking area and intended exit survive saving and loading. It is visible in Interchanges, locomotive notices and debug snapshots.

Players may couple to its outer ends and operate its air hoses and anglecocks. Internal connections between pooled engines/tenders cannot be uncoupled by a user. Throttle, manual/AE reassignment and ordinary locomotive controls remain locked, including in Sandbox. The parked inspector offers Jump to rather than reestablishing an active service that no longer exists.

If a player locomotive is attached, it can reposition pool power within the native game area where it was parked. There is no separate configurable 500-foot movement allowance. Approaching the area boundary inhibits player traction and applies parking brakes; a locomotive notice explains why movement stopped. If an existing saved position overlaps the boundary, movement back toward the area is allowed. Detach at the outer connection to take the player train farther away.

At the next simulated service request, the game can detach stopped pool power from player cars and split the engines needed for an outbound assignment. This is an automated-service exception to the user uncoupling restriction. The selection keeps steam engines with their tenders, respects route power and passing-siding limits, and leaves surplus power parked. Moving or incomplete units defer assignment until safe.

The outbound assignment receives its own protected timetable and AI crew, performs native pickups in timed batches, and departs with power ahead of the cars. Reusing pool power cancels an active wait/fast-forward when the assignment is activated. A new inbound train can leave its power as the next pool after setouts if no pickups remain for it.

## Verification and help

Builds, policy tests and game API checks cover the implementation, but do not substitute for in-game physics, coupling and multiplayer verification. Pool area boundaries, steam/tender orientation, closely packed yards and client visibility are particularly useful regression scenarios. Save before reproducing a problem and include the relevant service/unit IDs in a report.

Follow [Troubleshooting](TROUBLESHOOTING.md) for log capture and symptom-specific checks. [Report a bug or request a feature](https://github.com/rmroc451/TweaksAndThings/issues).
# Full-cut setouts and repair-track pool parking

## Era-style identities and motive power

New NPC locomotive road numbers start in the 1800–1899 range, such as 1830, 1846 or 1889. Numbers are checked against existing equipment with the same reporting mark and against other engines being created in that train. When the range fills, allocation advances to 1900–1999, then 2000–2099, and continues. Retired NPC numbers can be reused once they are no longer occupied.

In **AI Traffic → Settings**, **NPC reporting mark** overrides the mark on newly generated NPC equipment. Leave it blank to keep default marks. Native interchange freight retains the identities supplied by the game's interchange backend. Changing this setting does not rename existing locomotives, parked pool power, or already planned player equipment. Multiplayer clients see this host-controlled setting as read-only.

NPC motive-power selection follows the proportion of player-owned diesel locomotives versus steam locomotives in the company's roster. NPC equipment and tenders do not count toward that proportion. With no company locomotives, selection prefers steam. As diesels enter the owned roster, NPC services increasingly draw diesel units, including mixed power where a multi-engine train needs it. The selector first finds the minimum feasible engine count, then tries to match the roster mix within that count, choosing the least excess power among otherwise matching combinations. It falls back to a feasible alternative when the preferred type cannot haul the assignment. Existing and reused pool units are preserved; the roster weighting applies when generating new power.

NPC-owned cars and power do not consume bearing oil or develop hotboxes. This applies to through trains, interchange trains, and their parked pool units. Existing hotboxes are cleared when cars become NPC pickups; a low-frequency recovery check also clears affected NPC cars from older saves. Their stored oil level is preserved. As soon as a setout's NPC marker is cleared, the game resumes normal oil consumption and hotbox rules for that car, subject to the game's oil feature setting.

Services alternate timed delivery and pickup batches while both types of work remain. After a delivery batch, nearby pending pickups are preferred so their removal can open space for the next deliveries. Native pickup eligibility, the reserved manifest, capacity checks, and missed-pickup tracking still apply.

Before departure or repair-track parking, the service performs a final tidy pass on stationary freight cuts touched by its deliveries or pickups. It compacts those cuts using the same placement and exit-clearance checks, reconnects adjacent cars, and secures their handbrakes. It excludes locomotive consists, moving cars, active NPC cars, and cuts extending outside the interchange. Work is shown as **Tidying yard**, with one cut rearranged per ten game seconds so changes happen progressively. This uses native placement updates rather than driving a physical switching locomotive through each movement. Time acceleration advances the tidy intervals.

New interchange services always include a randomly selected catalog caboose, with its weight and length included in dispatch sizing. It stays at the rear of the freight consist, including when pickups are added or remaining setouts are rejoined. Dropping an intact freight cut recovers the caboose onto the rear of the separate power unit only when clear track ahead allows that unit to fit without disturbing the delivered cars; otherwise the service uses smaller setout cuts. With no outbound pickups it stays at the end of the engine unit during repair-track parking and is included in the parked pool unit's coupling restrictions. Reused pool power also requires a service caboose and retains it when surplus engines are split off.

The NPC caboose carries no usable crew-hours: daily and track refills skip it, loading positive crew-hours is blocked, and player helper/payment logic cannot draw crew-hours from it. It does not provide the nearby player-caboose transfer-time bonus. A regular player caboose nearby can still provide that bonus.

For small-cut deliveries, the placement planner keeps the largest contiguous group that fits as a coupled string, reducing the group one car at a time only when space requires it. It tries compact placement beside existing freight cuts and fills available track pockets before spilling into another track. Every proposed cut footprint is reserved within the plan, so cuts in the same transfer batch cannot overlap. Delivered cuts connect to nearby exposed freight couplers on the same interchange span, including their air hoses and angle cocks; native integration-set reconciliation establishes the actual coupled consist. Handbrakes secure the resulting string. Active AE Local equipment, moving cars and locomotive consists are excluded from these joins. The planner continues to protect the service power's exit route; space on that route is not usable setout capacity. The final tidy pass preserves coupled strings where they fit, rather than deliberately scattering their cars. **YARD JOIN** diagnostics record the joining cars, gap and whether native coupling succeeded.

When an inbound NPC service fits completely on one interchange track, it pulls toward that track's far end with clearance at the boundary. At the scheduled service time, the mod can secure and uncouple the whole delivery cut in place instead of relocating pairs of cars. Those cars immediately return to normal railroad service, keeping their existing identities and native waybills. A partial or obstructed arrival continues to use the existing capacity checks and smaller transfer groups.

The power is repositioned toward the departure direction before pickups begin. Pickup transfers still take their normal simulated time, including the nearby-caboose assistance. If power cannot be safely repositioned, the service waits and reports the reason instead of placing engines through parked cars.

With no successful pickups, power is routed to a clear pocket on a repair track within the interchange's game area. The track can contain other cars: the entire engine/tender/caboose unit must fit without overlapping them or fouling the area's switches, and its approach must be unobstructed. The search checks both entrances and works inward from each far end. It remains an active NPC while moving there and becomes parked pool power only after stopping with the entire power unit inside the repair span. Parking brakes are applied, and the existing pool-power coupling and movement restrictions continue to apply. If no clear, reachable repair berth fits the power, the service waits; clear a suitable pocket and it will retry.

Completed transfers waiting for parking show **Awaiting repair parking**, without a transfer completion countdown. The debug log reports repair-track rejection counts for occupied tracks, insufficient space or turning clearance, locations outside the interchange area, and unreachable tracks. The parking target is inside the far end of the berth, leaving room for the whole engine/tender/caboose unit. Yard tidying leaves blocked car placements in place rather than trying to place a car through another part of its own cut.

Arrival checks retain the footprint of an accepted berth rather than rebuilding the train's rear using switches that may have changed behind it. Existing dispatched services reconstruct that footprint from their original approach when possible. If a berth becomes blocked, the NPC receives one fixed holding order; a hold in the yard uses zero-speed AE ROAD mode, avoiding repeatedly moving a waypoint to the locomotive's current position. When the berth is available again, the saved arrival waypoint resumes.

After a save finishes restoring, the host automatically reconciles NPC programming once. Active units regain their lead/MU configuration; travelling services resume their saved destination through normal routing and clearance checks. Transfers and scheduled waits remain stopped with their progress and transfer deadlines preserved. Parked pool units retain parking brakes and disabled locomotive controls. NPC engines without a saved assignment are held and logged for recovery. An empty service accidentally saved in the departing phase is redirected to repair parking. `LOAD RECOVERY` messages identify the restored services and any missing leads; this reconciliation does not spawn replacement trains or repeat payments.

The engine roster's status column and locomotive service notice show setout/pickup progress and the estimated completion clock time, with remaining minutes. Blocked or suspended work shows a waiting status instead of a ticking completion estimate. Estimates cover transfers; subsequent travel to the railroad exit or repair track takes additional time.

New inbound NPC trains enter at a rolling speed after the full consist has been assembled and its first native AE targets are available. The initial speed is limited by the track speed beneath every car and by native engineer signal, obstruction and braking-distance calculations. A blocked train can therefore enter stopped or below track speed. This applies to newly spawned interchange arrivals and through traffic, not parked pool power, yard work, departures after servicing or save-load recovery. The debug log records the actual initial speed under **ROLLING SPAWN**.

Setouts use the game's automatic consist-brake helper (**ApplyHandbrakesAsNeeded**) on each delivered or joined freight string. It selects the necessary handbrakes and releases excess brakes, instead of applying a handbrake to every car. Full-cut deliveries use the same helper before the power is separated. Pool-power parking retains its separate locomotive-unit brake policy.

When a player locomotive couples to pooled power, the pool engines remain unpowered and have their brake controls cut out. For permitted movement within the parked area, their handbrakes and independent locomotive brakes release. At the start of the haul, uncoupled outer pool ends have their angle cocks closed so they cannot vent the connected brake pipe; connections between coupled vehicles remain player-operated. Native brake charging still takes time after connecting hoses, and any open outer end on the player's own equipment must be closed. The mod does not skip the engineer's brake-readiness checks. **POOL AIR** diagnostics identify this preparation.

## Returning captive-load empties to their source

The mod records the supplying interchange on ordered-load cars. Captive company cars retain that source while their servicing waybill is temporarily cleared in Bardo; pulpwood/log orders also record their booked source. When the game issues the empty-car return waybill, the mod routes it back to that recorded interchange and uses native move-payment and grace-day calculations. The source is saved on the car. If an installed map no longer contains that interchange, the native return waybill is kept and the missing source is logged under CAPTIVE RETURN.
