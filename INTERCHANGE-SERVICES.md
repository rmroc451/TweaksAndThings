# Simulated interchange services and parked pool power

Simulated interchange service replaces instantaneous yard delivery with an NPC train that travels to the interchange, transfers cars, and handles outbound work. The game's interchange backend remains responsible for ordering, waybills, charges, track allocation, blocking difficulty, sales and off-map loading/return behavior.

Related guides: [NPC traffic](NPC-TRAFFIC.md), [held deliveries and pickup penalties](FREIGHT-AND-INDUSTRIES.md), [timetables and time controls](TIMETABLE-AND-TIME.md), and [troubleshooting](TROUBLESHOOTING.md).

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
