[![MIT license](https://img.shields.io/badge/License-MIT-blue.svg)](https://lbesson.mit-license.org/) [![Buymeacoffee](https://badgen.net/badge/icon/buymeacoffee?icon=buymeacoffee&label)](https://ko-fi.com/rmroc451)
# RMROC451's Tweaks and Things
Please Read this **ENTIRE** README before continuing.

This is a mod for Railroader which is available on Steam.

This mod requires Railloader by Zamu.

## Usage
1. Download and install Railloader by Zamu from here: https://railroader.stelltis.ch/
    * Verify you have a Mods folder in the root of your railroader directory. If You do NOT have a Mods folder, you didn't complete this step successfully.
2. Download the `RMROC451.TweaksAndThings.x.x.zip` from the Releases Page of this repo.
3. There are 2 ways to install this mod.
    * ONLY DO ONE OF THE BELOW STEPS, NOT BOTH
    1. Open the zip folder, select the entire contents of the folder and drag the contents to the ROOT Railroader directory.
        * this should put a `RMROC451.TweaksAndThings` folder into the Mods folder created in step 1
    2. Drag the zip file onto Railloader.exe and have Railloader install the mod.
        * as with option 1, this will put a `RMROC451.TweaksAndThings` folder into the Mods folder created in step 1
    * If you DO NOT have the `RMROC451.TweaksAndThings` folder in the Mods folder after completing ONE of the above steps, you didn't complete the step successfully.
3. Run the game and enjoy all of the tweaks and things!

## Notes
1. This mod currently supports Railroader version 2024.4.4. This mod may break in future updates. I will do my best to continue to update this mod.
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
               <ul>
                  <li><b>A5a:</b> `alt left click` : toggle car hand brake and connect glad hands on both ends</li>
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
            <li><b>D5: Customizable</b> from the in-game Railloader settings, find <b>RMROC451.TweaksAndThings</b> (see <b>S3</b>)</li>
        </ul>
  </li>
  <li><b>M:</b> Miscellaneous
        <ul>
            <li><b>M1 (🟢 NEW v2.0.0):</b> Repair tracks now require cars to be waybilled, or they will not be serviced/overhauled.<br/>They will report on the company window's location section as <b>'No Work Order Assigned'</b>.</li>
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
                    Will allow interchange service and repair shops to still function when you are insolvent, at a 20% overdraft fee.</li>
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
Yes, these are client side mods. Host doesn't need to have them.

### What version of Railroader does this mod work with?
2024.6 -> [Full Requirements](./TweaksAndThings/Definition.json)

*Special thanks and credit to Zamu for creating Railloader and for help with making the mod a bit more robust.*
