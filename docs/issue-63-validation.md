# Automated traffic validation

Work tracked by [issue #63](https://github.com/rmroc451/TweaksAndThings/issues/63). Current player documentation is beside the root README: [NPC traffic](../NPC-TRAFFIC.md), [interchanges and pool power](../INTERCHANGE-SERVICES.md), [freight accounting](../FREIGHT-AND-INDUSTRIES.md), [timetables/time controls](../TIMETABLE-AND-TIME.md), [player tools](../PLAYER-TOOLS.md), [installation/releases](../INSTALLATION-AND-RELEASES.md), and [troubleshooting](../TROUBLESHOOTING.md).

## Latest implementation checks

The latest implementation verification before this documentation update used the installed Railroader assemblies and .NET SDK 10:

- Mod build: zero errors, 61 warnings.
- Unit tests: 260 passed, zero failed.
- Metadata inspection: 75 selected Harmony bindings matched the installed game methods and parameter types/names.
- Output: installable 2.1.9 ZIP under artifacts/build; building there did not install it into the game.

The SDK is available at C:/Program Files/dotnet/dotnet.exe. Tests target net8 and have run on the installed .NET 10 runtime using DOTNET_ROLL_FORWARD=Major. The earlier missing-dotnet blocker no longer applies on this machine. These are recorded implementation results, not a new full-game test performed for a Markdown edit.

Compilation, policy tests and metadata checks cannot prove physical placement, multiplayer replication, UI rendering or absence of every routing deadlock. The documentation identifies those limitations rather than presenting a build as in-game verification.

## Current behavior to verify in the game

Use a backed-up test save and retain current and previous debug logs. Capture a snapshot at each unexpected result.

1. Compare Default (auto-magic) with Simulated delivery / pickup; verify native waybills, costs, track assignment and blocking difficulty without duplicate delivery.
2. Run traffic without CTC. Check enabled unowned approaches, including Sylva before purchase, without changing player placement rights.
3. Verify early dispatch from travel/margin or saved plan timing. Check wait interception, successful-spawn cancellation and blocked-spawn diagnostics.
4. Operate simultaneous interchanges and concurrent inbound/outbound pool services; confirm independent progress, timetable entries and crew cleanup.
5. Check two-car and final one-car batches at two game minutes per car. Add/remove a nearby caboose and verify rescaling to one minute per car without charge.
6. Fill yards before dispatch and during approach. Verify trimming, held reservations, no duplicate orders and compact setouts outside protected exits.
7. Confirm successful setouts lose their restriction/crew while active pickups stay protected. Verify host/client visibility after repositioning.
8. Collect exports, sales and owned off-map loader cars, including purchased coal. Preserve native filling charges and return behavior.
9. Move a reserved pickup off its span; check first warning and second-miss forfeiture/reclaim. Reload and verify no repeated reclaim of the same bill.
10. Inspect railroad-wide delinquent lists, switch-list additions, held countdowns and declining delivery premiums; compare ledger/waybill identity.
11. Order pulpwood; test unmet-demand limits, percentage fee, actual-capacity adjustment and held-order coverage.
12. Compare power sizing on short/long grades and large pickup demand. Check steam tenders, leading orientation, MU/cut-out, speed limits and no NPC fuel/water use.
13. Verify active NPC restrictions in every mode, including Sandbox. Test Jump to and Reestablish programming with clear track.
14. Leave no-pickup power parked. Save/reload, use only outer coupling/air permissions and verify users cannot split internal connections.
15. Reposition pooled units with player power inside their native area. Verify boundary holds in manual/AE, notices and permitted movement back toward the area.
16. Reuse stopped pool power, detach player cars and split surplus engines while retaining tenders. Verify outbound assignment and surplus records.
17. Publish daily plans before physical spawning. Check protected snapshots, AI-only crew joining/assignment restrictions and cleanup.
18. Read passing times and planned meets against NPC/human timetables. Confirm live authority still follows occupancy/signals and manual CTC.
19. Verify lookahead through clear refuges without unnecessary stops. Reproduce opposing approaches, throat clearance and siding release conditions.
20. Inspect chart plans, markers, uncertain/dashed forecasts and saved green history/variance. Check midnight, completed trains and branches.
21. Check board marker creation, movement, manual deletion, completion and midnight cleanup without modifying player markers.
22. Run 2x/4x/8x simulation with player ROAD/WAYPOINT engines; test destinations, HH:mm stops, switching windows and activation resets.
23. Verify filled-load/speed/status tags at multiple UI scales, compact inspector rows, repair work and waybill summaries.
24. Generate filtered placer consists, including tender counts, invalid/canceled input and ordinary placement.
25. Inspect Bryson mirrors in ABS/CTC and confirm shared IDs/single coding, without assuming independent dual track.
26. On clients, verify disabled host settings, local display/reporting access, notices and authoritative car state.
27. Compare steady frame times at Normal/higher rates after planning/terrain loading. Preserve slow-update diagnostics.

Use the [reporting template](../TROUBLESHOOTING.md#report-a-bug-or-propose-a-feature) and [GitHub issues](https://github.com/rmroc451/TweaksAndThings/issues).
