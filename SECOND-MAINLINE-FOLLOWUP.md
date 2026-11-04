# Second mainline follow-up

This document tracks the agreed route and presentation requirements for the code-generated second mainline. It is a working checklist for milestone testing and implementation; items below are not claims that the route currently satisfies them.

## Route requirements

- Generate and maintain the track through this mod's code-level track graph changes. Do not implement the route as JSON patch files.
- Keep the second mainline beside the existing mainline where practical, with comparable grades and gentle switch curvature.
- Give existing scenery and infrastructure priority. Route around roads, telegraph poles, trees, and other assets instead of allowing the new rail to overlap or hide them.
- Prefer a surface route. If a segment would otherwise be deeply buried, first look for a practical above-ground reroute; use tunnel geometry only when a surface alignment is not practical.
- At Bryson, connect before the yard using the mainline switch beneath switch 47 on the CTC panel.
- Where the route encounters an existing passing siding, use the siding between its existing switches as the second route, then resume the parallel alignment beyond the far switch. This should avoid adding unnecessary crossover switches.
- Add gentle crossovers where needed to connect the new mainlines. At minimum, include one between Andrews and Topton, Topton and Alarka Junction, Bryson and Whittier, and Whittier and Cowee. Add further crossovers where the route needs them.
- Add or adjust road crossings to match the new alignment. Connect road splines to the crossings and make crossings continuous where the new parallel tracks pass through them.
- Once placed, keep the generated track geometry stable; do not move it in response to runtime conditions or player actions.

## Progression and CTC requirements

- Add each second-mainline segment as progression alongside the related new-area/mainline progression. The intended route includes the Bryson–Alarka area, Alarka–Nantahala, and Nantahala–Andrews; when Sylva unlocks, add the Whittier–Sylva alternate as well.
- Keep the layout strategy configurable in code so independent placement and mirroring can be compared during testing. Independent placement is the current preference.
- If second-mainline track is added after an area's CTC progression, charge the equivalent CTC progression cost for adding its signal parts. If added before signaling, double the CTC progression requirements for signaling the double-tracked area. The added CTC progression should copy the cost of the corresponding area's CTC progression.
- Show the new track as unsignaled on the CTC panel until its CTC progression is complete. Then add occupancy indicators and alignment switches for the generated route, using the same formats and behavior as the existing mainline CTC panel implementation.
- These new signals and controls are the ones to wire into the in-progress copy of the Bryson CTC panel. Do not copy the panel's existing switches and indicators as substitutes for controls attached to the generated track.

## Testing checklist

- [ ] Confirm the second mainline is visible in the 3D world, not only on the map.
- [ ] Confirm the Ela-side connection branches gently from the mainline and supports fluid eastbound and westbound movement.
- [ ] Confirm the Bryson connection uses the existing pre-yard switch and signal location.
- [ ] Confirm every passing siding on the second-mainline route is used between its native switches.
- [ ] Inspect roads, crossings, poles, trees, tunnel approaches, terrain clearance, grades, and switch curvature in the 3D world.
- [ ] Confirm the unsignaled second mainline appears on the CTC panel, and its controls appear after the corresponding CTC progression.
- [ ] Check the progression order and costs both when the second mainline is built before CTC and when it is added after CTC.
- [ ] Check the CTC HUD overlay opens and controls the generated second-mainline switches and occupancy indicators.

## Known follow-up from the last in-game inspection

The user reported that the Ela–Bryson second mainline appeared on the map but not in the 3D world, that its Ela-side entrance was not fluid, and that roads/telegraph poles and existing sidings were not respected. The user also specified the pre-yard Bryson connection described above. Treat these as unresolved until verified in game against the checklist.

The user also reported that the Governor's Island siding was bypassed, the generated line did not connect back to the existing mainline, and multiple generated sections were broken near mile marker 61. Runtime fixes now allow siding discovery across native progression-group boundaries, continue the generated line across those boundaries, and only let a siding replace a crossover when that siding covers the crossover's actual route midpoint. These geometry changes still require live-save inspection.

The next live log confirmed the previous build loaded, but exposed why the geometry remained unchanged: the `s2` route began at route index 0 with no switch, so the generator used a disconnected native group boundary; no siding was selected. The follow-up build now targets the panel node under switch 47 for Bryson (and skips generation if it cannot route from that node), allows longer native siding paths, and logs route anchors, both endpoints, connected track ids, siding candidate pairs, rejection reasons, and selected siding segments for verification.

## Code changes awaiting in-game confirmation

- Generated mainline, entry, and crossover segments are explicitly marked available and group-enabled as a safeguard for group-less code-added track. The earlier map-only symptom was caused by a CTC panel exception blocking the graph rebuild; that issue is already fixed and is separate from the remaining 3D clearance/alignment concerns.
- The `s2` entry now searches backward from the native group boundary for a nearby existing switch, preferring an existing CTC-marked switch, then builds the gradual entry from there.
- Passing-siding discovery now accepts available, non-industrial native paths between mainline switches even when the game's track class is Mainline rather than Branch. Candidate paths remain bounded by the distance of the mainline interval.
- Clearance probing now checks every route node with a wider radius and gives roads, telegraph poles, vegetation, bridges, and structures higher obstruction weights.
- Native route and passing-siding scans are cached for the lifetime of the track graph and invalidated when `Graph.RebuildCollections` runs. Completed crossovers now short-circuit before route lookup.

These changes still need a live save to confirm which switch and siding paths were selected and whether scenery is actually clear. Road spline/crossing mesh extension and above-ground rerouting around buried stretches remain open until those map objects and terrain can be inspected in game; parallel segments currently preserve the source route's elevation and tunnel style.
