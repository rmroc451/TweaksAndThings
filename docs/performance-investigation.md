# Periodic game stalls — October 2, 2026

This is a historical investigation record. Some implementation notes below describe intermediate versions that have since changed. For current log capture, settings and performance checks, use [Troubleshooting](../TROUBLESHOOTING.md#choppy-movement-and-recurring-lag-spikes) and the [NPC traffic guide](../NPC-TRAFFIC.md). Railloader references below concern earlier sessions; the current mod uses UMM and its own disk log.

The user reports trees animating a few frames at a time with recurring large lag spikes, persisting with Through Traffic and Simulated interchange service enabled. Installed Settings.xml confirms both enabled. This visual symptom indicates whole-frame stalls; it does not establish that tree animation is the cause.

The latest railloader.log shows terrain builds continuing from roughly 10:28 through 10:30:20, with initial priority loading taking 21 seconds. Terrain streaming is a possible additional contributor. The current log tail does not reproduce the earlier repeated locomotive-UI exceptions.

Source inspection found expensive mod paths:

- Simulated service formerly searched graph segments and ran routes from eligible terminals every half-second for each unowned interchange, including when its service time was distant. It also did this when no source locomotive existed. Approach geometry and travel estimates are now cached for 60 real seconds, failed searches for 10 seconds, and invalidated by graph identity/segment-count changes, override edits or disabled endpoint segments. Actual dispatch still validates its route and placement. Planning is skipped when no non-NPC locomotive exists.
- The crew-hours patch used reflection and scanned load slots on repeated Car.Definition getter calls. Successfully inspected shared definitions now use a weak-reference cache, including non-caboose definitions. Incomplete definitions are retried; existing crew slots remain intact.
- The HUD rebuilt text and layout every rendered frame. It now refreshes at most four times per real second. Transfer deadlines remain based on game time.

Build and policy/metadata checks pass, but these findings have not been confirmed by a live Unity CPU profile or before/after frame-time measurement. Cached route planning may still cause a spike during initial planning or refresh on complex maps.

## Follow-up in the game

Install the newly built 2.1.9 ZIP and restart. Allow terrain loading to finish, then compare steady-state movement and timewarp. Check Unity Mod Manager's log for `Slow mod update`: component name and elapsed milliseconds identify timetable traffic, interchange service or the NPC HUD. Reports begin at 50 ms and are limited to one per component per 30 real seconds.

If stalls persist without slow-mod notices, capture fresh UMM and Railloader logs and compare a disposable save with the mod disabled. If notices identify interchange planning, the next step is incremental planning across frames or explicit spawn/service overrides to eliminate automatic terminal searching. If stalls happen only while terrain builds are logged, investigate the game's terrain/rendering workload separately.

## Follow-up fixes

Later UMM logs confirmed interchange update stalls of 74–119 ms. Approach planning is now an incremental iterator: nearest-track examination yields every 16 segments, connectivity work every 64 segments/nodes, and candidate route searches between frames. The host advances pending plans with a nominal two-millisecond budget; an individual native route operation is synchronous and can exceed that budget. Dispatch waits for a completed plan. Candidate work still uses the Unity main thread; no game objects are accessed from background threads.

Approach notifications now reject trains farther than a track mile geometrically before running a route search. Game-time transfer scheduling is unchanged. This removes whole-map planning from a single half-second scan, but renewed live-game timing is needed to establish remaining frame costs.

The legacy Railloader startup call was removed from the installed Unity core assembly after backup, preserving the verified UMM startup call. Subsequent sessions should use UMM's Log.txt and the game's Player.log for diagnostics; old railloader.log files describe earlier sessions.
