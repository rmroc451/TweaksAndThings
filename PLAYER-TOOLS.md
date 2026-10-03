# Car tags, switch lists, cabooses and player tools

These tools make car state, destinations and consist work easier to inspect. NPC service ownership still takes precedence: ordinary consist helpers cannot be used to take over an active NPC train. Parked pool power has limited coupling/air permissions described in [Interchange services](INTERCHANGE-SERVICES.md).

Related guides: [freight and industry work](FREIGHT-AND-INDUSTRIES.md), [timetable controls](TIMETABLE-AND-TIME.md), [installation](INSTALLATION-AND-RELEASES.md), and [troubleshooting](TROUBLESHOOTING.md).

## Expanded car tags

Enable **UI → Enable tag updates for air, handbrake, oil, and hotbox status** in UMM, and enable the game's own car-tag display. Fresh mod settings enable the update option by default; existing settings retain their saved choice.

The mod adds air-system/coupler status, a handbrake indicator, oil/hotbox information where applicable, and a caboose refill marker. Locomotive oil information can summarize the worst applicable car in the connected consist, subject to the existing caboose requirement setting.

Each filled load slot gets a detail line under the title with a fill indicator, current quantity, maximum capacity and load description. Multiple filled slots produce multiple lines. Pounds-based freight uses consistent units on both sides of the fraction; gallons and quantity loads use their relevant display units. Empty slots do not add a filled-load line. A speed line in mph follows the load detail lines, and native destination/passenger information is retained below them.

The speed is therefore often the third visible line for a car with one filled load, but cars with multiple loads need more lines. This is intentional rather than dropping information to force a fixed three-line layout.

If the tag is visible but details are absent, check the mod's tag setting first. Oil indicators also depend on the game's oil feature and the car's oiling support. Compare a normal loaded freight car with a locomotive or caboose to distinguish a general tag problem from a load-specific one.

## Compact inspector helpers and switch lists

The car inspector's consist helper buttons use compact rows to avoid overlapping its normal controls. **To Switch List** adds connected work to the current crew's list, including cars without freight waybills and locomotives/tenders with repair or overhaul destinations.

Unassigned cars appear as **No waybill / Unassigned**. Repair work orders have their repair destination shown even if a freight waybill also exists. The action does not set a destination or generate a work order by itself. The host confirms visible additions; on a client, a pending request can be shown until the host responds.

If an addition appears confirmed but cannot be found, check the current crew and switch list, filters and whether the car was already listed. Capture the car ID and list state for a report. This is different from the Delinquent pickups tab, which deliberately spans all crews.

## Waybill summary window

The caboose inspector has a small **Waybill summary** button that opens a resizable window. The switch list's tools menu can open the same summary. Use its **Caboose consist** and **Current switch list** tabs to inspect grouped work without filling the caboose inspector with a large permanent summary.

The summary shows car counts, loaded/empty counts, tonnage and length, grouped by destination and current location. Locate actions help find the associated work. The switch-list tab follows your current train crew and includes repair bills and unwaybilled stock. It refreshes approximately every three seconds while open; it is not an instantaneous per-frame ledger.

## Set an AE destination without an existing waypoint

Selecting a freight/repair destination from the player locomotive's AE cog can establish its first waypoint. You no longer need to set an unrelated waypoint beforehand. The action sends a Waypoint command and preserves a positive configured speed, using the game's default if starting from zero.

This convenience applies to player locomotives. Active NPC cogs expose their restricted recovery actions, and parked pool power cannot be reassigned to an arbitrary player route. A selected destination still requires a valid native route and appropriate signal/switch access.

## Generate a random consist

In the native **Consist Placer**, choose the car types/categories using the filter buttons at the top, then click **Random consist…**. Enter a whole number from 1 to 200 and confirm **Generate**. The generated selection replaces the current preview; use the game's normal **Place** action to place it.

The pool uses the same visible native definitions and filters as the placer. Clearing the category filter permits all visible types. Sampling can repeat a definition, so a request for ten cars does not require ten unique models. Steam engines with defined tenders count as two vehicles toward the requested total. A selection consisting only of engine/tender pairs requires an even count.

Canceling or entering an invalid count leaves the existing preview unchanged. If no definitions match, the tool explains that instead of silently creating nothing. Normal game placement, mode and space restrictions still apply; generating a preview does not change ownership permissions or make room on a blocked track.

## Caboose crew hours and legacy JSON customization

The UMM version adds crew-hour slots in code during car setup. It checks the native **Caboose** archetype, including compatible custom caboose definitions. If the definition already contains a crew-hours slot, that slot is retained. Otherwise, an eight-hour quantity slot is added.

The canonical load identifier is **crew-hours**, with a hyphen. **crew_hours** is a different identifier and is not supported. Adding a slot also requires that the running game can resolve the actual load definition; a misspelled identifier in custom content is not fixed by the slot patch.

The old Railloader cabeese.json file is not a UMM configuration source. This mod does not read additional mixin entries from that file. Custom cabooses using the correct native archetype participate automatically. Custom entries that changed capacity, targeted non-caboose archetypes, or modified unrelated definition fields are not automatically translated into UMM settings. Preserve that file as a reference and migrate those changes through your custom-content provider's supported definition mechanism. There is no new drop-in cabeese.json override reader in this branch.

The existing caboose helper costs, crew refilling and player Safety First settings remain available. NPC locomotives are exempt from the player caboose speed cap. Nearby-caboose assistance for interchange transfer time is a separate free benefit and does not debit crew hours.

## Bryson CTC mirrored controls

The Bryson panel has a second row of mirrored switch/signal controls and independent-looking block lamps below the original controls. Both rows reference the same existing block, switch, code-button and interlocking IDs. Either row operates the existing controls; it is not a second independently signaled track.

The copies follow ABS/CTC changes, and a code action executes once. They are removed when the mod is disabled. The feature prepares the panel's visual layout for future dual track; it neither edits the track graph nor creates new block IDs.

## Multiplayer settings and bug reports

Host-only gameplay settings are disabled on multiplayer clients, including traffic scheduling, caboose gameplay and repair-service rules. Personal display preferences, keybindings, diagnostics and reporting remain accessible. Disabled settings are an authority indication, not evidence that their saved values failed to load.

For missing tag indicators, overlapping buttons or summary problems, record the UI scale, screen resolution, active crew and affected car IDs. A screenshot of the full relevant panel helps separate layout issues from missing data. See [Troubleshooting](TROUBLESHOOTING.md), then [report a bug or feature request](https://github.com/rmroc451/TweaksAndThings/issues).
