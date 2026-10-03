# NPC traffic: setup, dispatching, and operation

This guide describes the automated traffic features implemented on the issue-63 branch. A timetable reservation represents a planned service; the physical train is created later, after demand, route, power and placement checks pass. A visible schedule therefore does not guarantee that a train can depart at its planned time.

Related guides: [interchange services and pool power](INTERCHANGE-SERVICES.md), [timetables and fast-forward](TIMETABLE-AND-TIME.md), [freight accounting](FREIGHT-AND-INDUSTRIES.md), and [troubleshooting and bug reports](TROUBLESHOOTING.md).

## Choose the kind of traffic you want

| Feature | What triggers it | Configuration |
| --- | --- | --- |
| Default interchange service | The game's normal interchange schedule | Keep Interchange service at Default (auto-magic). |
| Simulated interchange service | Native inbound orders or eligible outbound cars, with travel lead time | Select Simulated delivery / pickup. |
| Timetable-defined through traffic | An eligible first-class timetable departure with the configured symbol prefix | Enable through traffic and create the timetable service. |
| Random through freights | A saved daily plan generated from industry contract tiers | Enable through traffic and random through freights; choose classes and a multiplier. |
| Outbound pool-power service | The next simulated interchange request has eligible pickups and parked power | Pool reuse is automatic; see the interchange guide. |

The interchange setting and through-traffic toggle serve different purposes. Simulated interchange service does not require a manually created timetable train, the through-traffic toggle, a reference locomotive, CTC, or a caboose. Random through freights require both their own toggle and the through-traffic toggle.

## Open the traffic controls

After loading a railroad, use the leftmost added button in the top-right toolbar. It reuses the game's timetable icon and has the tooltip **AI traffic / interchanges**. The same controls are available from the timetable's **AI traffic** tab and the UMM **AI Traffic** page. Keeping both windows is intentional: use whichever fits your dispatching workflow.

The traffic view contains:

- **Active traffic:** physical NPC services, their current phase, progress and waiting reasons.
- **Interchanges:** service schedules, approach information, navigation, parked power and outbound pool assignments.
- **Delinquent pickups:** cars that missed a reserved pickup, across the whole railroad.
- **Held deliveries:** deliveries deferred because they could not fit, with deadlines and payout estimates.
- **Settings:** traffic, interchange, random freight and ordering options.
- **Debug:** a view of the mod's own disk log, with open-file and snapshot actions.

Scheduling and gameplay changes run on the multiplayer host. Host-only controls are disabled on clients. Clients can still inspect information, read diagnostics and use personal display settings. See [installation and multiplayer](INSTALLATION-AND-RELEASES.md).

## Timetable-defined through trains

The default symbol prefix is **Z-**. A matching timetable train must be first class, have at least two usable entries, and begin and end at enabled interchange stations. Intermediate entries supply its operating schedule. The prefix is configurable; an ordinary player train should not use that prefix unless you intend the mod to generate it.

Passenger services generate passenger consists from available definitions and demand. Passengers are unloaded through native stop handling. Settlement applies to passengers actually unloaded, rather than an assumed full train. The defaults are a five-minute on-time grace and $3 per passenger; the traffic settings can change these. On-time delivery earns the configured amount and an out-of-grace delivery applies the corresponding penalty. This is a separate schedule adjustment; the game still pays its native passenger fare.

Manually defined through freights can use the **Freight reference car** setting. The spawner uses freight from its reference consist; when the field is blank, it looks for an available non-NPC freight reference. This is separate from automatic locomotive selection and random freight catalog generation. If such a freight will not spawn, check its reference and route before assuming interchange demand is involved.

Schedule eligible departures before their departure time. These timetable-defined departures are evaluated as the clock crosses their scheduled time and are recorded for that day. They do not have the same retained retry queue as blocked random departures. Reloading or repeatedly changing the clock is not a reliable way to force another departure.

## Random through freights

Enable **Random through freights**, select at least one class, and set the traffic multiplier and minimum/maximum freight car counts. Defaults allow first, second and third class, use a multiplier of 1, and request 5–15 freight cars. The configured car-count limits are constrained to 1–50. These trains draw freight definitions from the visible catalog; custom definitions can participate when they are available in the placer.

At multiplier 1, the daily range starts at one to two trains and grows with active industry contract tiers. For total tiers T and multiplier M, the current policy is:

~~~text
minimum = floor((1 + T / 4) × M)
maximum = ceiling((2 + T / 4) × M)
~~~

Only active, progression-enabled industry contracts contribute. A zero multiplier produces no random departures; the setting is capped at 10. With eight total tiers and multiplier 1, the range is 3–4 trains per full game day. The Settings tab displays the actual current minimum and maximum, so you do not need to calculate them manually.

Departure times, routes, selected classes and freight definitions are generated once per game day and saved. Loading a railroad partway through its first planned day generates a proportional share for the remaining hours. Reloading does not reroll an existing plan. Changes to random traffic settings affect the next newly generated plan; they do not rewrite every reservation already published for today.

At least two enabled interchange stations and one selected class are required. Blocked random departures remain in their queue and retry. A missing custom freight definition can leave a saved departure waiting until that definition is available again.

## Power, speed and train length

Generated locomotive power comes from visible store/consist-placer definitions, including suitable custom locomotives. You do not need to purchase a template engine for simulated interchange service. Selection evaluates native tractive effort, route speed, sustained grade, locomotive/tender weight and the expected train footprint. Delivery and pickup legs are evaluated separately.

The selector checks one- and two-engine combinations before larger combinations and favors a sufficient, close-fitting selection. A difficult sustained climb can still require more than two engines; the normal ceiling is 16 locomotive units, further constrained by available track space and passing-siding length. The **POWER CHECK** diagnostic explains the required and available power.

Steam locomotives require their defined tenders. The lead engine controls the train; trailing engines use MU with their train brakes cut out. Engines remain ahead of the cars on departure. Trailing power may be randomly flipped, while a steam tender stays at its engine's required end. Generated engines consume no fuel or water and do not need refueling stops.

NPC traffic follows native track and signal speed limits and is exempt from the mod's player caboose speed restriction. This exemption is independent of the free caboose transfer assistance described in the interchange guide. A route speed limit is a permitted maximum, not a promise that a heavy train can maintain that speed uphill.

The consist must fit within the longest usable passing siding along its route. The check includes power, tenders, cars and switch fouling clearance. Dead-end spurs and sidings beyond the destination do not qualify. A route with no usable passing siding can block dispatch; it does not justify spawning an arbitrarily long train.

## CTC, uncontrolled track and meets

CTC is optional. NPC searches may use physically enabled, connected unowned track, including the Sylva approach before purchase. This scoped permission does not unlock track for players or allow ordinary player placement there. Disabled track, occupancy, whole-consist fit, switch alignment and route authority still matter.

In automatic signaling, the routing logic checks onward corridors toward passing refuges. It looks ahead using braking distance and a safety margin so a clear onward route can remain on the main line without stopping at every siding. A required siding hold remains in place until the train is stopped and clear of both throats, the opposing approach is safe, and the next corridor can be reserved.

Manual CTC follows dispatcher signals and switch locks. On uncontrolled track, a stopped NPC waypoint train retries a native car-aware route search approximately every 15 real seconds. **Reestablish programming** requests an immediate recalculation. An available bypass still has to satisfy native route and switch constraints.

Planned timetable meets include published human trains as well as NPC trains. Human trains without a timetable can only be accounted for by live occupancy and signaling. Planned meets do not grant movement authority. The code cannot guarantee the absence of every deadlock, especially when manually driven trains enter a throat, all usable sidings are occupied, or opposing trains are already trapped. Clear the obstruction or adjust dispatching, then use recovery if needed.

## Notices, restrictions and recovery

Departure and approach telegraphs appear in the game's messages, identify the service and include a clickable locomotive reference. The approach notice is sent when the train is within approximately one track mile of its next destination. Saved announcement history avoids replaying the same service event after reload.

The progress display uses native locomotive notices instead of a permanently fixed extra HUD panel. A notice says **In transit** while far away, then **Approaching** near the destination, and shows interchange setout/pickup progress and relevant waits. Its text updates in place.

Active NPC trains cannot be manually operated, reassigned or dismantled, including in Sandbox. Their recovery controls offer **Jump to** and **Reestablish programming**. Recovery retains service accounting and saved vehicle identities; it may rerail/reassemble and orient those vehicles when there is sufficient clear track. It does not create a new payout or erase missed pickups. Parked pool power has its own limited interaction rules and a Jump to action.

If a train does not appear or holds unexpectedly, follow the [traffic troubleshooting checklist](TROUBLESHOOTING.md). Report reproducible problems through the [GitHub issues list](https://github.com/rmroc451/TweaksAndThings/issues), with the debug snapshot and relevant log files.
