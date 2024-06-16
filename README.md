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
1. This mod currently supports Railroader verison 2024.4.4. This mod may break in future updates. I will do my best to continue to update this mod.
2. It is possible that the developers of Railroader will implement their own fix for this issue. At such time this mod will be deprecated and no longer maintained. 
3. As the saying goes, use mods at your own risk.

## FAQ
### What does this mod do?
**PLEASE READ AS THE WAY THIS MOD FUNCTIONS HAS CHANGED FROM PRIOR VERSIONS**

1. Car Inspector : Handbrake & Air Line Helper
 * Gives two buttons that will scan the current car's connections, including the whole consist, and automatically release handbrakes, set anglecocks and connect glad hands.
2. Car Tag Updates
 * Shows an indication of which cars in the FOV have Air System or Handbrake issues. 
 * **hold SHIFT** to only show the tags in the FOV for cars with an issue!
3. Discord Webhooks
 * Allows the console messages to post to a discord webhook. useful for those wanting to keep an eye on 24/7 hosted saves.
 * Locomotive messages grab the locomotive `Ident.RoadNumber` and check the `CTC Panel Markers` if they exist.  If found, they will use the red/green color and embed the locmotive as an image in the message.  If no marker is found, it defaults to blue.
 * Currently, One person per server should have this per discord webhook, otherwise you will get duplicate messages to the webhook.
 * **Multiple hooks**: Allows for many different webhooks per client to be setup, and filtered to the `Ident.ReportingMark` so you can get messages to different hooks based on what save/server you are playing on.
 * **Customizable** from the in-game Railloader settings, find `RMROC451.TweaksAndThings`

### Does this work in Multiplayer?
Yes, these are client side mods. Host doesn't need to have them.

### What version of Railroader does this mod work with?
2024.4.4

### RMROC451 TaDo
- ![Work In Progress](https://img.shields.io/badge/Status%3F-Work%20In%20Progress-green.svg)
    - [ ] WebhookNotifier
        - [X] Move base webclient calling to new WebhookNotifier
            - [X] Extend WebhookNotifier in DiscordNotifier, to do formating of payload only.
        - [X] Add Settings UI Elements for webhook url
        - [ ] create thread per host || save game name || nearest city || locomotive??
        - [X] store in a settings file per host & save game name?
    - [ ] inspector button(s)
        - [X] auto connect glad hands/angle cocks
        - [X] release/set car brake?    
        - [ ] Cost Ideas
            - [ ] $$$ based on usage per day? or flat $5 per use?
            - [ ] Caboose only?
                - [ ] detect car having a nearby caboose to use? `UpdateCarsNearbyPlayer` but for car.
                - [ ] add crew to caboose (similar to engine service), $25/day to fill up, must have crew to use and caboose near cars.
                - [ ] crew number modifier for how many cars away from caboose it can effectively work?
                - [ ] crew need to refill at a station?
- ![Pending](https://img.shields.io/badge/Status%3F-Pending-yellow.svg)
    - [ ] Camera offset to coupler
        - [ ] shift + 9/0
    - [ ] finance exporter/discord report at end of day for Game.State.Ledger

*Special thanks and credit to Zamu for creating Railloader and for help with making the mod a bit more robust.*
