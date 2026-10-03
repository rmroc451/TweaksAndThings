[![MIT license](https://img.shields.io/badge/License-MIT-blue.svg)](https://lbesson.mit-license.org/) [![Buymeacoffee](https://badgen.net/badge/icon/buymeacoffee?icon=buymeacoffee&label)](https://ko-fi.com/rmroc451)
# RMROC451's Tweaks and Things
Start with the installation steps and the detailed feature guides below. The original caboose and quality-of-life reference remains in this README.

This is a mod for Railroader which is available on Steam.

This version is packaged as a UnityModManager mod.

UnityModManager is the only mod loader required from 2.1.9 onward. Tweaks and Things has no Railloader or Strange Customs dependency and does not use `Definition.json` or Railloader content mixins. If an older Railloader installation remains active, it may warn that this UMM folder lacks `Definition.json`; do not add a dummy manifest or install the mod twice. Remove the legacy loader using its supported uninstall procedure if no other installed mods need it, then verify/reinstall UMM's injection as needed.

## Detailed feature guides

The expanded traffic and railroad-management features have dedicated Markdown guides beside this README. They describe the current implementation on this branch; development features still require in-game verification before release.

| Guide | What it explains |
| --- | --- |
| [NPC traffic](NPC-TRAFFIC.md) | Traffic types, daily random freight ranges, catalog power/MU/tenders, track speeds, route authority, telegraphs, NPC restrictions and recovery. |
| [Interchange services and pool power](INTERCHANGE-SERVICES.md) | Enabling simulated service, dispatch lead time, automatic spawn locations, timed transfers, free caboose assistance, full-yard deferrals, pickups and saved parked power. |
| [Freight and industries](FREIGHT-AND-INDUSTRIES.md) | Held orders without duplicates, declining delivery premiums, delinquent pickup penalties, purchased coal pickups, pulpwood orders/fees and switch-list accounting. |
| [Timetable and time controls](TIMETABLE-AND-TIME.md) | String charts, planned meets, current positions, forecasts, actual running/variance, protected daily schedules, dispatch-board markers and 2×/4×/8× simulation. |
| [Player tools](PLAYER-TOOLS.md) | Expanded load/speed/status tags, compact inspector helpers, waybill summaries, first-waypoint selection, random consist generation, crew-hours migration and Bryson mirrored CTC controls. |
| [Installation and releases](INSTALLATION-AND-RELEASES.md) | UMM installation/update checking, multiplayer authority, local assembly references, automatic ZIP output and publishing releases after 2.1.9. |
| [Troubleshooting and bug reports](TROUBLESHOOTING.md) | Missing trains/cars, clearance holds, stalled transfers, pool restrictions, accounting problems, lag, UI/build errors and exactly which logs to attach. |

For a quick start, follow [Simulated interchange setup](INTERCHANGE-SERVICES.md#first-service-setup-checklist). For a train that is not doing what you expect, use [Troubleshooting](TROUBLESHOOTING.md) before changing several settings at once. These guides distinguish implemented behavior from planned estimates and behavior that still needs physical multiplayer validation.

Use the in-game **Report a bug / feature** button or [search/report on GitHub](https://github.com/rmroc451/TweaksAndThings/issues). The [reporting checklist](TROUBLESHOOTING.md#report-a-bug-or-propose-a-feature) includes train/car IDs, game time, host/client details, snapshots and the mod's current and rotated debug logs.

## Credits & Project History

This project was originally created and developed by RMROC451.

I have created fixes and additional features which maybe pulled to main project over time.

The goal of this repository is to preserve, maintain, and continue improving the mod for the Railroader community while respecting and crediting the original work and vision of RMROC451.

Special thanks to:
- RMROC451 — original creator of Tweaks and Things
- Zamu

## Usage
1. Install UnityModManager and configure it for Railroader.
2. Download the `RMROC451.TweaksAndThings_<version>.zip` release asset from the [Releases page](https://github.com/rmroc451/TweaksAndThings/releases).
3. Install the release ZIP from UnityModManager's **Mods** tab, or manually extract its files into `Mods/RMROC451.TweaksAndThings`. The folder must contain `Info.json`, `RMROC451.TweaksAndThings.dll`, and `Newtonsoft.Json.dll`.
4. Start Railroader, open the UnityModManager menu, and enable **RMROC451's Tweaks and Things**. Its settings and keybindings are available in the mod panel.


## Notes
1. This mod currently supports Railroader version 2024.6 and later versions may require updates. I will do my best to continue maintaining it.
2. It is possible that the developers of Railroader will implement their own fix for this issue. At such time this mod will be deprecated and no longer maintained. 
3. As the saying goes, use mods at your own risk.

## FAQ
### What does this mod do?
**PLEASE READ AS THE WAY THIS MOD FUNCTIONS HAS CHANGED FROM PRIOR VERSIONS**

Basically, this mod has a couple zones of focus. Caboose tweaks and other QOL things.  Some of those QOL things, I added the option for the cabeese to be required & charge you a "crew salary" to utilize, or pay a monetary penalty.

I was disappointed the vanilla cabeese were largely for show, didn't provide any real reason to have them except for role playing.

Enter Tweaks and Things.

### QOL & Cabeese Modifications:
<ul>
  <li><b>A:</b> Car Level Updates:
        <ul>
            <li><b>A1:</b> If a car is a participant in a disconnected air hose (currently uses the copy waybill icon)</li>
            <li><b>A2:</b> If a car's hand brake is set (currently uses the handbrake icon)</li>
            <li><b>A3  (🟢 NEW v2.0.0):</b> Oiling Level/Hotbox Indication : pie chart or 🔥 icon
                <ul>
                    <li><b>A3a:</b> <u>On Rolling Stock:</u> Indicates the car's oiling level, if oiling feature is enabled</li>
                    <li><b>A3b:</b> <u>On Locomotive:</u> Indicates the worst oiling level of a car from the connected consist (see <b>S1</b>)</li>
                </ul>
            </li>
            <li><b>A4:</b> Adds a "+" on cabeese tags when on a track span that reloads their crew hours load (see <b>C</b>)</li>
            <li><b>A5 (🟢 NEW v2.0.0):</b> Car Click Hotkey Modifiers 
               <br/>The original modifier actions default to Alt, Alt+Shift, Ctrl+Shift, and Ctrl+Alt (with Shift optionally held). Rebind the Alt, Control, and Shift action keys from the UnityModManager **Keybindings** tab; combinations remain composable.
               <ul>
                  <li><b>A5a:</b> `alt left click` : toggle car hand brake and connect glad hands on both ends
                     <ul>
                        <li>[ ] https://github.com/rmroc451/TweaksAndThings/issues/43 : Add setting to dump air vs connecting when used (requested by CD WEISS)</li>
                     </ul>
                  </li>
                  <li><b>A5b:</b> `alt shift click` : toggle consists brakes and connect all glad hands</li>
                  <li><b>A5c:</b> `ctrl alt click` : drop all brakes and connect all glad hands in the consist</li>
                  <li><b>A5d:</b> `ctrl alt shift click` : same as above but will auto oil the entire consist!</li>
               </ul>
            </li>
        </ul>
  </li>
  <li><b>B:</b> Car Context Menu Updates
     <ul>
        <li><b>B1 (🔵 MODIFIED v2.0.0):</b> Context Menu (right click car) Updates<br/>
              When right clicking on a car you get some new individual car options:
              <ul>
                 <li><b>B1a:</b> Bleed<br/>
                 Dumps all of the air in the car's air system</li>
                 <li><b>B1b:</b> Apply/Release Handbrake<br/>
                 Toggles the individual car's handbrake</li>
              </ul>
           </li>
           <li><b>B2 (🟢 NEW v2.0.0):</b> SHIFT Context Menu (right click car) Updates<br/>
              When right clicking on a car and holding SHIFT you get some new consist level options:
              <ul>
                 <li><b>B2a:</b> Bleed Consist<br/>
                 Dumps all of the air in all the consist car air systems</li>
                 <li><b>B2b:</b> Set/Release Consist Handbrakes<br/>
                 If handbrakes are detected on, it knocks them all off.<br/>
                 If no handbrakes are detected in the consist, it utilizes the RailRoader base game handbrake detection for when cuts of cars are spawned.</li>
                 <li><b>B2c:</b> Air Up Consist<br/>
                 Connects all gladhands and opens angle cocks for the consist.
              </ul>
           </li>
     </ul>
  </li>
  <li><b>C:</b> Cabeese Modifications
        <ul>
            <li><b>C1:</b> Adding `crew-hours` load to caboose type cars
                <ul>
                    <li><b>C1a:</b> Gives the cabeese a depletable resource that is used to simulate the crew that resided in the caboose.</li>
                    <li><b>C1b:</b> When certain actions are utilized at a consist level, the depletion of this resource is used to simulate a crew's stamina for the day.</li>
                    <li><b>C1c:</b> When a request to adjust the <b>crew-hours</b> below the remaining quantity, things start costing over time, if <b>S1b</b> is enabled (1.5x modifier).</li>
                    <li><b>C1d:</b> This load/resource is utilized when <b>S1b</b> is enabled and for <b>S1e</b> integration.</li>
                    <li><b>C1e:</b> See <b>S1b1/S1b2/S1b3</b> for what this is used for.</li>
                </ul>
            </li>
            <li><b>C2 (🔵 MODIFIED v2.0.0):</b> Proximity Detection<br/>
                Order of detection when requesting to adjust <b>crew-hours</b> for an action:
                <ul>
                    <li><b>C2a:</b> Car initiating the action is a caboose, if so use this caboose</li>
                    <li><b>C2b:</b> Cabeese near the car that initiated the request:</li>
                        <ul>
                            <li><b>C2a:</b> Gather from consist of the requesting car.</li>
                            <li><b>C2b:</b> Gather from <b>OpsController.Shared.ClosestArea</b> for cars that are in the same <b>Area</b>.</li>
                        </ul>
                    </li>
                    <li><b>C2c:</b> Sort the found cars by:
                        <ul>
                            <li><b>C2c1:</b> Preference for cars with same <b>crew-id</b> as selected locomotive (engine controls, bottom left), still order by <b>C2c2</b></li>
                            <li><b>C2c2:</b> Distance from the requesting car, ascending (pick closest one)</li>
                        </ul>
                    </li>
                </ul>
            </li>
        </ul>
  </li>
  <li><b>D:</b> Discord Webhooks
        <ul>
            <li><b>D1:</b> Allows the console messages to post to a discord webhook. useful for those wanting to keep an eye on 24/7 hosted saves.</li>
            <li><b>D2:</b> Locomotive messages grab the locomotive `Ident.RoadNumber` and check the `CTC Panel Markers` if they exist. If found, they will use the red/green color and embed the locomotive as an image in the message.  If no marker is found, it defaults to blue.</li>
            <li><b>D3:</b> Currently, One person per server should have this per discord webhook, otherwise you will get duplicate messages to the webhook.</li>
            <li><b>D4: Multiple hooks</b>: Allows for many different webhooks per client to be setup, and filtered to the `Ident.ReportingMark` so you can get messages to different hooks based on what save/server you are playing on.</li>
            <li><b>D5: Customizable</b> from the UnityModManager settings panel, find <b>RMROC451.TweaksAndThings</b> (see <b>S3</b>)</li>
        </ul>
  </li>
  <li><b>M:</b> Miscellaneous
        <ul>
            <li><b>M1 (🟢 NEW v2.0.0):</b> Repair tracks service cars without a waybill by default, matching stock Railroader. Turn off <b>Allow repair-track service without a waybill</b> in UMM UI settings to require a work order; cars without one report as <b>'No Work Order Assigned'</b>.</li>
            <li><b>M2:</b> Engine Roster Tweaks<br/>
                <ul>
                    <li><b>M2a  (🟢 NEW v2.0.0):</b> MU'd locomotives will automatically be hidden unless they are <b>SELECTED</b> or <b>FAVORITED</b>.</li>
                    <li><b>M2b:</b> Fuel Display in Engine Roster<br/>
                    Will add reamaing fuel indication to Engine Roster (with details in roster row tool tip (see <b>S2c</b>))</li>
                        <ul>
                            <li><b>M2b1:</b> MU'd locomotives fuel information will combine with MU primary (see <b>S2c</b>).</li>
                        </ul>
                    </li>
                </ul>
            </li>
            <li><b>M3 (🟢 NEW v2.0.1):</b> MU Adjacency Restriction Removal <h3 style="color:red; display:inline">(USE AT OWN RISK)</h3></br>
                Engines no longer are required to be adjacent to eachother to contribute to MU. They can be dispersed throughout the train.<br/>
                The primary MU engine still acts as the main air reservoir, meaning train braking emits from that engine at this time.
            </li>
            <li><b>M4 (🟢 NEW v2.0.0):</b> `ctrl alt click` on a track in the map, sets the selected locomotives waypoint there when in waypoint mode.<br/>
               If you have mapenhancer with cars displayed, if you keycombo click on a car icon, it will set the auto couple attempt.
            </li>
            <li><b>M5:</b> The formatted `/cu` locomotive status message can be sent from the UnityModManager **Crew Update** tab.</li>
            <li><b>M6:</b> The car inspector's <b>Add Consist to Switch List</b> button adds the connected cars to the current switch list even when the consist has no locomotive.</li>
        </ul>
  </li>
  <li><b>S:</b> Settings
         <ul>
            <li><b>S1:</b> Caboose Mods</li>
                <ul>
                    <li><b>S1a:</b> Consist Oil Indication<br/>A caboose is required in the consist to report the lowest oil level in the consist in the locomotive's tag(see <b>A3b</b>) & roster entry(see <b>M2</b>).</li>
                    <li><b>S1b:</b> Caboose Use / Enable End Gear Helper Cost
                        <ul>
                            <li><b>S1b1:</b> Will cost 1 minute of AI Brake Crew & Caboose Crew time per car in the consist when the new <b>inspector</b> or <b>shift context wheel</b> buttons are utilized.</li>
                            <li><b>S1b2:</b> 1.5x multiplier penalty to AI Brake Crew cost if no sufficiently crewed caboose nearby (see <b>C2</b>).</li>
                            <li><b>S1b3:</b> Caboose starts reloading `Crew Hours` at any Team or Repair track (no waybill), after being stationary for 30 seconds.</li>
                            <li><b>S1b4:</b> <b>AutoOiler Update:</b> Increases limit that crew will oiling a car from 75% -> 99%, also halves the time it takes (simulating crew from lead end and caboose handling half the train).
                            <li><b>S1b5:</b> <b>AutoOiler Update:</b> if <b>S1b</b> & <b>S1d</b> checked, when a caboose is present (see <b>C2</b>), the AutoOiler will repair hotboxes afer oiling them to 100%.
                            <li><b>S1b6:</b> <b>AutoHotboxSpotter Update:</b> decrease the random wait from 30 - 300 seconds to 15 - 30 seconds (Safety Is Everyone's Job)</li>
                            <li><b>S1b6:</b> <b>Costs from S1B1/S1B2:</b> added to financials at end of day with an entry of <b>AI Brake Crew</b>.</li>
                        </ul>
                    <li><b>S1c (🟢 NEW v2.0.0):</b> Refill / Crew Hours Load Option<br/>Select whether you want to manually reload cabeese via:
                        <ul>
                            <li><b>S1c1:</b> track method - (team/repair/passenger stop)</li>
                            <li><b>S1c2:</b> daily caboose top off - refill to 8h at new day</li>
                        </ul>
                    </li>
                    <li><b>S1d:</b> AutoAI Requirement (AI Hotbox\Oiler Requires Caboose)<br/>A caboose is required in the consist to check for Hotboxes and perform Auto Oiler, if checked.</li>
                    <li><b>S1e (🟢 NEW v2.0.0):</b> Safety First!<br/>On non-express timetabled freight consists, a caboose with some crew-hours (see <b>C1</b>) is required in the consist to increase AE max speed > 20 in <b>ROAD</b>/<b>WAYPOINT</b> modes.</li>
                </ul>
            <li><b>S2:</b> UI
                <ul>
                    <li><b>S2a:</b> Enable Tag Updates<br/>
                    Allows all tag updates from <b>A</b> to display.</li>
                    <li><b>S2b (🟢 NEW v2.0.0):</b> Debt Allowance<br/>
                    Allows repair-track servicing to continue when you are insolvent, at a 20% overdraft fee.</li>
                    <li><b>S2c:</b> Engine Roster Fuel/Info
                        <ul>
                            <li><b>S2c1:</b> Enable Fuel Display in Engine Roster<br/>
                                Will add reamaing fuel indication to Engine Roster (with details in roster row tool tip). <br/>
                                Select where to display:
                                <ul>
                                    <li><b>S2c1a</b> None/Off</li>
                                    <li><b>S2c1b</b> Engine Column</li>
                                    <li><b>S2c1c</b> Crew Column</li>
                                    <li><b>S2c1d</b> Status Column</li>
                                </ul>
                            </li>
                            <li><b>S2c2:</b> Always Visible?<br/>
                                Always displayed, if you want it hidden and only shown when you care to see, uncheck this, and then you can press ALT for it to populate on the next UI refresh cycle.
                            </li>
                        </ul>
                    </li>
                    <li><b>S2d:</b> Allow repair-track service without a waybill (on by default) and show or hide the <b>WP SET</b> waypoint notification (shown by default).</li>
                </ul>
            </li>
            <li><b>S3:</b> Webhooks
                <ul>
                    <li><b>S3a:</b> Webhook Enabled<br/>Will parse the console messages and transmit to a Discord webhook.</li>
                    <li><b>S3b:</b> Reporting Mark<br/>Reporting mark of the company this Discord webhook applies to.</li>
                    <li><b>S3c:</b> Webhook Url<br/>Url of Discord webhook to publish messages to.</li>
                </ul>
            </li>
         </ul>
  </li>
</ul>

### Does this work in Multiplayer?
The display tools include client-side functionality, but automated traffic, interchange accounting and gameplay settings are host-authoritative. The host must run the mod for those features. Use matching versions on participating clients for the traffic UI and interaction protections; host-only settings are disabled on clients. See [Installation and multiplayer](INSTALLATION-AND-RELEASES.md#multiplayer-installation-and-authority).

### What version of Railroader does this mod work with?
2024.6 or later. The mod must be built against the Railroader and UnityModManager assemblies installed locally.

### Building and tests
Set `GameDir` in `Paths.user` to the Railroader install folder. By default, the build expects UnityModManager assemblies in `Railroader_Data/Managed/UnityModManager`; set `UnityModManagerDir` in `Paths.user` if your installation uses another directory.

Run the focused settings and hotkey tests with `dotnet test Tests/TweaksAndThings.Tests.csproj`. Build the release archive with `dotnet build TweaksAndThings.sln -c Release`.

Both Debug and Release builds create `RMROC451.TweaksAndThings_<version>.zip` alongside the DLL in the actual output directory (including a custom `OutDir`). The Unity Mod Manager archive contains the generated `Info.json`, the mod DLL and `Newtonsoft.Json.dll`. Rebuilding replaces the archive for that version without including old archives, game assemblies or debug symbols. Release builds also retain deployment to `GameModDir`.

*Special thanks to Zamu for the modding tools and guidance that helped make the original project more robust.*

### Consist placer random generation

In the game's Consist Placer, choose the car category using its top filter buttons, then click **Random consist…**. Enter 1–200 cars and choose Generate to replace the current preview. Clearing the category filter uses all visible placer car types. Cars are sampled with repetition; automatically paired steam tenders count toward the requested total. A tender-only locomotive selection requires an even count. Cancel or invalid input leaves the preview unchanged. Use the normal Place button to place the generated train.

### Tag indicators

Enable **UI → Enable tag updates for air, handbrake, oil, and hotbox status** in the mod settings to show the indicators on visible game car tags. New settings enable this by default; existing saved settings retain their chosen value.

### Simulated interchange setup

Open **AI traffic / interchanges** with the leftmost **AI** button in the top-right toolbar, then select **Settings → Simulated delivery / pickup**. The UMM AI Traffic page also opens this window. Run in single player or on the multiplayer host. The interchange must be enabled in the game and its native service time configured. No timetable row, through-traffic toggle, CTC or caboose is required. A nearby caboose only supplies the optional free transfer-speed benefit.

NPC power is randomly selected from the game's visible consist-placer locomotive definitions, including loaded custom definitions. You do not need to own a template locomotive. Steam locomotives include their defined tender. Unsuitable candidates are skipped; power is sized for the route (at most 16 locomotive units). Connected enabled track and room for the service consist are still required. Trains dispatch at service time minus estimated travel time and a five-minute margin, rather than immediately when the setting is selected.

Automatic spawn options are Nearest map / track edge (default) and Farthest reachable track edge. Edges are terminal ends of the enabled track graph; the map's terrain boundary is not used. Farthest mode ranks connected terminal approaches by track distance, then validates native route reachability and placement. Nearest mode favors terminals on the outer bounds of the enabled graph, then proximity to the interchange; interior sidings are fallback candidates. These options apply to new simulated interchange services; timetable through trains retain their scheduled origin. Manual spawn overrides take precedence.

Leave approach overrides blank for automatic selection. If the layout has no usable automatic terminal, configure Spawn / exit and Service point with native track-location strings (`segment-id|a-or-b|distance-in-metres`). These are internal locations, not station names. Failed dispatch attempts currently may produce no HUD entry; the HUD and telegraphs begin when a train is created.

The AI traffic window contains active services, interchange schedules and blockers, delinquent pickups, held deliveries, settings and diagnostics. Each active NPC locomotive has a native notice showing its phase and interchange setout/pickup progress. The same notice updates in place without replaying the telegraph sound or animating a replacement row. It shows **In transit** until the service's approach telegraph confirms it is within one track mile of its next destination, then **Approaching**. Notices clear when services finish or the mod is disabled. After interchange pickup finishes, the original timetable entry reverses direction, stations and schedule for the return trip, preserving its symbol and AI crew.

Before dispatch, the native placement fitter checks the destination yard. Randomly selected setout cars are removed from the planned consist until the remaining cars fit; removed cars stay reserved for a later service. Spawn space is checked for the whole consist. Loading uses the remote service train's space rather than destination-yard occupancy. A train with no fitting deliveries and no pickups is skipped; a pickup-only service legitimately travels inbound with power alone. If the yard fills before arrival, randomly selected inbound cars are held off-map through the game's bardo backend with their identities, loads and waybills intact, and returned through the native interchange backend on a later service.

Held deliveries survive saves and are restored to the native pending orders before industries order more cars. These reservations count as existing demand, preventing replacement orders. **Held deliveries** and each interchange's listing show the missing car types/loads, destination, next service, bonus countdown and current payout. The industry's Company → Locations panel shows the same information; dispatched cars also show the countdown and payout beside their destination in the car inspector. Payout estimates for cars not yet dispatched are updated to their actual waybill amounts when spawned.

Held delivery settings default to a **4-hour** delivery window and **100%** starting premium: the base waybill payout plus an extra equal amount gives **2× base payout** at the next service time. The extra premium declines linearly with elapsed game time, reaching zero at the deadline. For a $1,000 base payout, the total is $2,000 initially, $1,500 after two hours and $1,000 after four hours. Deliver the car to its industry's waybill destination to collect the premium. Native contract bonuses and damage fines still apply separately. Later delays or repeated missed services do not reset the original bonus clock; an earlier extra service can advance it. The deadline hours and starting premium percentage are configurable under Settings. Timewarp advances the countdown and premium calculation.

Diagnostics are written automatically to **`TweaksAndThings-debug.log` in the installed mod directory** (normally `Railroader/Mods/RMROC451.TweaksAndThings`). The file contains UTC timestamps, game-time snapshots, plans, power/consist checks, loading blockers, service IDs and exception stacks. Repeated detailed blockers are throttled; a snapshot is written every two real minutes. At 5 MB the log rotates to `TweaksAndThings-debug.log.previous`, keeping one previous file. The traffic window's **Debug** tab reads the latest 160 lines (at most 32 KB) from disk, polls once per second only while visible, and avoids reading unchanged files. **Open log file** opens it in your configured text editor; **Write debug snapshot** or `/npcTraffic` records current state before you share both files. Routine mod diagnostics no longer echo into the game console. Telegraph messages remain in the game's message system. Native instantaneous “received no cars” messages are suppressed during remote NPC loading; automatic interchange mode retains its normal messages.

### Publishing UMM updates (2.1.9 onward)

`Info.json` points UMM to the raw `Repository.json` on `main`. The homepage opens GitHub Releases. Users on older manifests need to install a UMM package with this feed address once. UMM compares the installed version with the feed; enable update checks in UMM's settings. The in-game manager flags newer versions and provides the homepage for downloading them.

For each release after 2.1.9:

1. Increment `Assembly.version` to the new numeric `major.minor.patch` version (for example, 2.1.10).
2. Build and validate the mod. Both build configurations produce `RMROC451.TweaksAndThings_<version>.zip` beside the built DLL. Check the ZIP's `Info.json` version and run the tests and game checks before release.
3. Create the matching GitHub release tag (for example, `v2.1.10`) and upload `RMROC451.TweaksAndThings_2.1.10.zip` as a release asset. Do not substitute GitHub's generated source-code archive.
4. After the asset is publicly downloadable, update the release entry in `Repository.json` on `main`: keep `Id` as `RMROC451.TweaksAndThings`, set `Version` to `2.1.10`, and set `DownloadUrl` to the actual asset URL (`https://github.com/rmroc451/TweaksAndThings/releases/download/v2.1.10/RMROC451.TweaksAndThings_2.1.10.zip`).
5. Verify the raw feed returns valid JSON and the download URL returns the installable ZIP. Test detection from an older UMM installation.

The feed records the latest public stable release; do not advertise development builds or prereleases there. The initial 2.1.9 entry in this branch is release preparation: publish its asset before exposing that feed entry on `main`. Ordinary local builds do not publish releases or change the public feed.

Interchange diagnostics: enter `/npcTraffic` in the game's console to display the current mode, host status, game time, each enabled interchange's next service time, calculated approach and dispatch status, and active trains' transfer progress and waiting reasons. This is a read-only report; on multiplayer clients, scheduling calculations run on the host. Routine diagnostics go to the mod-directory debug file and its Debug tab; repeated blockers are throttled. The requested console snapshot is read-only, and telegraphs remain in the native messages. See [Troubleshooting](TROUBLESHOOTING.md#get-the-right-log).

With simulated interchange service enabled, waiting/timewarp checks the requested interval before advancing the clock. It finishes pending approach calculations, stops at the earliest calculated NPC dispatch time, and attempts to spawn all service trains due at that boundary. The rest of the requested wait is canceled so the player can observe the arriving traffic. Overdue dispatches stop the wait immediately; blocked power or spawn placement is reported by `/npcTraffic`. Automatic interchange mode retains the game's normal waiting behavior.

Any successful NPC interchange or through-train spawn immediately cancels an active wait, including a spawn outside the calculated dispatch boundary. The unused wait time is discarded; the player can start another wait afterward. Failed spawn attempts do not trigger this cancellation. Normal configured clock speed is retained.

In simulated mode, **Next interchange AI** in the Time window and the single **Warp to next interchange AI** action shared by the timetable/standalone interchange views use the earliest needed train's dispatch time across enabled interchanges, including travel lead time. Routes are resolved before waiting, and demand is refreshed on click. Already active services and interchanges without delivery/pickup demand are excluded. Locate and teleport actions are grouped in each interchange's navigation menu.

After native setout placement succeeds, delivered cars immediately lose their NPC marker and AI crew assignment. Their loads, identities and waybills remain intact, and players can couple, move, inspect and assign them normally. Completed setouts from older saved services are also released if stale NPC restrictions remain; cars already collected by a later NPC service retain that service's protection.

NPC trains choose a close-fitting combination of catalog locomotives for the route's grade and horsepower-per-tonne requirement, up to 16 units and subject to available spawn space. Different locomotive types can operate together in MU. Interchange power is sized separately for the outbound setouts and the return pickup snapshot on their respective routes. The same prepared descriptors are passed to the native interchange backend. All power precedes the freight/passenger cars; steam engines retain their required tenders. The first locomotive controls the train with MU off and the train brake cut in. Trailing locomotives have MU on and their train brakes cut out, as required by the game's MU source selection; connected air and released handbrakes are established before orders are issued.

Simulated services capture their pickup cars at dispatch. On arrival, they can add cars that reached the pickup tracks while the train was en route, up to 90% of its installed power's capacity on the return route. Original reservations retain priority. A snapshot car missing from its pickup span receives a telegraph warning on its first missed attempt. A second missed attempt forfeits its delivery payout and reclaims an already credited payout. Pickup history and delinquency survive saving and loading.

Open the timetable's **Delinquent pickups** tab to see all delinquent cars across the railroad, regardless of your train crew. Rows show the return interchange, payout status and estimated game time until the next pickup attempt. Click a car or interchange to locate it; **+ List** adds the car to your current crew's switch list. The list refreshes automatically. `/npcPickups` also opens a delinquent pickup listing. Countdown estimates use active service progress or the interchange's next configured service time; blocked services can delay the actual attempt.

Selecting a destination from the AE cog assigns the first waypoint directly; no existing waypoint is required. It sends one command in Waypoint mode, preserving a positive configured speed or using the game's default when starting from zero. Freight/repair destinations remain available without a timetable document.

The inspector's consist helpers occupy two compact rows. “To Switch List” includes ordinary rolling stock and locomotives/tenders assigned a repair or overhaul destination. Cars without a waybill appear as “No waybill / Unassigned”; repair orders show their repair destination, including when a freight waybill also exists. The host checks visible rows before confirming additions; clients report a request pending host confirmation.

### NPC traffic controls and scheduling

The standalone traffic window uses the timetable icon at the left of the top-right toolbar. The timetable also has an **AI traffic** tab; either view can be used. Its header offers Maximize / Restore. Interchange schedules show HH:MM times and miles, with a navigation menu for locating or teleporting to spawn and service targets and one shared warp-to-next-AI-dispatch action. Held delivery estimates show approximately the nearest hour remaining. The Debug tab scrolls to the bottom when the visible log text changes.

The main **Timetable** tab uses a string chart instead of a table. Time runs left to right across the day; station rows run vertically, with schematic spacing. Blue paths travel east, amber paths west, horizontal portions show station dwell, and white diamonds mark explicitly scheduled meets. A red line tracks the current game time. Click a path or train symbol to see arrival/departure times and meet partners; expand the time scale for more detail and scroll horizontally. Midnight trips wrap at the day edges instead of crossing the entire chart. Separate branch charts share the same clock scale. The native **Edit Timetable** action remains available, with existing NPC schedule protection intact.

At the start of each game day, the dispatcher publishes saved, locked NPC schedules for random through freights and enabled simulated interchanges, including estimated return trips. Route calculations run incrementally and shared journeys are cached to limit planning spikes. Random departure times, classes and freight car definition selections survive reloads; they are not rerolled. On loading an existing game mid-day, the remaining day's plans are published. Pending trains have no physical cars or AI crews and therefore stay out of the world, roster and crew list until actual dispatch. Timetable-defined through trains remain visible through their existing entries. Interchange plans are conditional on demand, route availability and placement; empty or canceled services are removed rather than spawning empty trains. Return times initially estimate current demand at two minutes per car and can change as actual work becomes known.

NPC train markers are added automatically to each loaded dispatch board, with train symbol, departure time and direction. They start stacked at the board edges so the dispatcher can move them into position. Marker positions and manual deletions are respected, including after reload. Completion/cancellation removes that service's markers. At midnight, yesterday's mod-owned markers are cleared; active carry-over traffic receives new-day markers. Player-created markers are preserved.

The chart overlays live train positions and dashed forecasts on the solid timetable plan. NPC leads and crew-assigned player controlling locomotives are matched to their physical track locations, with interpolation between station locations along the cached route. Forecasts use current speed capped by future track limits, scheduled dwell/departure times, meets, passenger unloading and remaining interchange transfers. Named markers show current speed, and selected train details show planned versus forecast times. A stopped train without a known departure or a blocked service shows an uncertain departure instead of an invented ETA. Forecasts are estimates: future signal changes, acceleration, newly arriving work and unplanned player operations can change them. Route searches happen only while the chart is open, at most one per frame, with cached legs reused for subsequent updates.

Open the clock's **Time** window for **Fast-forward simulation**. Select player locomotives currently in ROAD/WAYPOINT mode, then choose **2× / 4× / 8× to destination**, or enter a **HH:mm** stop time and use **4× until stop time**. The clock and all world movement accelerate together using normal fixed physics steps; selection determines the stop triggers rather than which trains move. A destination run returns to normal when any selected train stops, including at a signal or other AE wait. Both simulation fast-forward and clock-only waiting stop at scheduled freight switching windows (a freight stop with distinct arrival/departure times). NPC spawning also returns simulation to normal. Simulation rate is host-controlled and synchronized through game state; it resets on load, unload or mod disable. Higher multipliers perform more physics work per real second and may achieve a lower effective rate on a busy railroad. Existing company clock settings are preserved.

**Report a bug / feature** in the UMM mod settings or AI traffic Settings tab opens the [GitHub issues list](https://github.com/rmroc451/TweaksAndThings/issues).

The caboose inspector's compact **Waybill summary** button opens a separate resizable window with **Caboose consist** and **Current switch list** tabs. Each shows car counts, loaded/empty counts, tonnage and length grouped by destination and current location, with locate actions. The switch list's tools menu also opens this summary. The switch list tab follows the current crew and includes repair bills and unwaybilled cars; summaries refresh every three seconds while open.

NPC trains cannot be operated or reassigned, including in Sandbox. Their AE actions are **Jump to** and **Reestablish programming**. Recovery restores their consist and route orders while retaining service progress. NPC engines consume no fuel or water. The lead locomotive faces its destination and power stays ahead of the cars on departure; trailing MU units may be flipped, with steam tenders kept at their required ends. Power selection favors the fewest sufficient units, then the closest capacity match.

NPC consist length is capped by the longest usable passing siding along its route to the destination. Enabled tracks must leave the route at one switch and rejoin it at another; dead-end spurs and sidings beyond the destination do not count. Native switch fouling distances are deducted. Locomotives and tenders count toward the limit on both delivery and pickup legs. Excess setouts remain reserved for a later service, and excess pickups stay at the interchange without being marked missed. Extra pickups must satisfy this length limit as well as the 90% power limit. Dispatch is blocked, with a diagnostic, when no usable passing siding is found.

Power sizing averages route grade over the estimated train footprint rather than applying a short grade spike to every car. Catalog tractive effort is already measured at the wheels; sizing does not deduct drivetrain losses a second time. Every one- and two-engine combination is checked before larger combinations. Track limits remain the AE speed limits; heavy trains on sustained climbs may still need more power. **POWER CHECK** log entries show speed, required HP and available HP after accounting for locomotive/tender weight. Recovery can rerail saved NPC vehicles and turn their power toward the destination when there is enough clear track; it retains the existing engines and service accounting.

Spawn locations identify a starting approach, rather than requiring the train to fit on that single segment. Placement uses the native consist placer's whole-train clearance helper over connected track. It scans inward from the chosen terminal, then binary-refines the first fitting bracket to within 0.25 m; another terminal is tried if necessary. The check includes staging clearance and existing trains. Manual approach overrides use the same whole-consist search.

Enable random through freights in the traffic settings alongside the master through-traffic toggle. Choose eligible first/second/third classes, car-count limits and a traffic multiplier. The daily minimum and maximum scale with the sum of active industry contract tiers and are displayed in settings. Departure times and routes are randomized once per game day and saved; settings changes apply to the next generated daily plan. At least two enabled interchange stations and one selected class are required. Blocked departures retry when placement becomes possible.

Every active NPC service receives a protected timetable entry and an AI-only crew. Players cannot join these crews, change their assignments, or edit/delete the NPC timetable rows. Saved snapshots restore those rows while retaining edits to ordinary trains. Entries and crews are removed when their services finish. Random through-freight reservations are published in the daily timetable before physical spawning; AI crews and physical assignments are added at dispatch and cleaned up on completion.

Industries with pulpwood unloaders offer **Order pulpwood from interchange**. Orders join the native interchange backend and count existing storage, pending orders and held deliveries toward demand. The extra ordering fee defaults to 10% of load value and is configurable. Booking charges an estimate using nominal car capacity; filling the order adjusts it to the actual load capacity. Native waybills, delivery costs and yard limits still apply.

In automatic signaling, AE routing reserves clear corridors between protected passing sidings. Both the selected track and its alternate must fit the complete train after switch fouling clearance; dead-end spurs do not qualify. Reservations prevent conflicting AE grants and remain behind the engine until its tail clears. ROAD follows lined track; WAYPOINT uses its native route. Manual CTC follows the dispatcher's signals and switch locks. When no suitable protected siding exists, the check continues to the route endpoint. Manual trains remain under player control.

Stopped NPC waypoint trains retry the native car-aware route search every 15 real seconds. **Reestablish programming** forces an immediate search, including bypasses around occupied track on uncontrolled sections. Searches retain native length, curvature and CTC switch restrictions; a reverse departure that would put the engines behind the cars is rejected. Routing state changes appear in the mod debug log. This reduces preventable head-on deadlocks; occupied sidings, manually driven trains and trains already trapped nose-to-nose still require dispatcher intervention.

Through trains check onward corridors ahead of arrival, using the native braking lookahead plus a safety margin, and pass through protected sidings without stopping when the next corridor is clear. Only a blocked onward corridor requires a siding hold. A required hold stays pinned across route recalculation; release requires a complete stop, the entire consist clear of both throats, no conflicting opposing approach, and an atomic reservation of the next corridor. While held, AE cannot throw or restore its exit switch. Routing diagnostics record the exit switch and the hold reason.

NPC day plans now include each enabled timetable location on their branch/junction path. Leg times use the game's configured fast estimate for first class and slow estimate for other classes, with route estimates as a fallback. Ordinary intermediate entries are passing times, with no dwell and no forced stop for generated through freights. Opposing NPC plans receive reciprocal meet markers at a nearby usable passing siding, with waiting time and a five-minute clearance allowance added to the new plan; existing published times remain unchanged. Preliminary meet candidates require 400 m of usable siding; live routing still checks actual train length, occupancy and signals. These are planning estimates, not movement authority: interchange trains continue to follow live routing grants, and a dispatcher may need to adjust a meet when conditions change. Undispatched saved plans are upgraded once; already running trains keep their current targets until their next trip. Random trains preserve the day plan and its allowances when spawning. Interchange dispatch and warp boundaries use the saved planned dispatch time.

Multiplayer clients see host-only traffic, caboose gameplay and repair-service settings disabled with a Host only label. Personal display preferences, keybindings, diagnostic access and the report button remain available.

The Bryson CTC panel has a second row of mirrored switch/signal controls and stand-alone block lamps beneath the original row. Both rows share the existing knob, code-button, block and interlocking IDs; coding executes once. The copies follow ABS/CTC mode changes and are removed when the mod is disabled. They prepare the panel visually for future dual track, but do not define independent blocks or modify the track graph.

String charts record observed actual running in green alongside the blue/amber plan and dashed forecast. Sampling runs on the host while the window is closed, at most once per game minute per train. Selected trains show the latest recorded HH:mm and minutes early/late against the plan at the observed position. History is saved with the railroad and shared with clients; today's completed trains remain on the chart. Records retain the current and previous day, capped at 20,000 observations overall. Gaps longer than three game minutes and changes of locomotive are drawn as breaks. History starts with this version; earlier movements cannot be reconstructed.

Automatic signaling (native ABS) uses reachable protected passing sidings as successive refuges, checking the intervening corridor before granting each hop. Stop placement accounts for fouling clearance across multiple short switch segments. A fully stopped train safely inside the siding can request its next corridor even if native braking stopped short of the exact target. Manual CTC leaves movement authority to the dispatcher's native signals and switch locks. Routing logs distinguish taking a protected siding from holding there and include remaining stop distance and speed on state changes.

NPC meet planning compares against all published timetable trains, including human crews and manually operated trains. Sparse human schedules are expanded internally using native travel-time weights between their declared locations; their source entries, dwell times and ownership remain unchanged. Only the NPC plan receives waiting allowances. Human trains without timetable entries cannot receive a scheduled meet; live occupancy and signals still protect against them.

NPC interchange pickups include player-owned cars sent to that interchange's off-map loaders, such as purchased coal loads. Loader waybills and loader pickup spans are recorded separately from ordinary exports; the native backend retains loading charges and off-map return scheduling. Ordinary owned cars are still only exported with a sell waybill. Transfer/reposition operations publish native placement snapshots to multiplayer clients before integration-set deltas; client visibility requires in-game validation.

Interchange NPC spawn searches include physically enabled, connected unowned track, including the Sylva approach before purchase. NPC placement bypasses the native player-ownership check only within the host's scoped NPC operations. Occupancy, full-consist fit, switch alignment, disabled-track exclusions and route-grant checks remain in force; this does not unlock track or change player placement permissions.

NPC setouts prefer compact placement alongside stationary freight cuts and couple exposed nearby ends, instead of leaving the native ten-metre separation for every two-car group. Before placement, both possible return departure paths are protected with switch fouling clearance. Every new car footprint must stay inside an interchange span and outside those paths. Unsafe deliveries are held for a later service; transfer timing remains unchanged. Cuts attached to locomotives, moving cuts and other active NPC consists are excluded from consolidation. Remaining NPC cars are repositioned as a whole cut after setouts so flipped-car orientation is preserved. Yard consolidation and multiplayer visibility still require in-game validation.

When an interchange train finishes its work without collecting any outbound cars, its locomotives and tenders remain as saved pool power with handbrakes applied. Players may use the unit's outer couplings, air hoses and anglecocks; internal power connections and locomotive controls remain locked, including in Sandbox. A player locomotive coupled to pool power may move it within the native game area where it was parked. Moving toward the boundary inhibits traction and applies parking brakes, with a locomotive notice explaining the restriction. Movement back toward the area is permitted if a saved unit already overlaps its boundary.

At the next simulated service request, parked power is assigned outbound cars within its route power and passing-siding limits. Service may detach it from player cars once stopped and split surplus engines from the unit; users cannot split those internal connections. Steam engines keep their tenders. Surplus power stays parked, and the new inbound service can take its place after setouts unless it collects pickups. Pool assignments have their own protected timetable and AI crew, use the native pickup backend and normal transfer timing, and cancel waiting/timewarp when activated. Parked power appears in the Interchanges tab, locomotive notices and debug snapshots. Pool reuse, coupling and movement restrictions require in-game verification on host and multiplayer clients.
