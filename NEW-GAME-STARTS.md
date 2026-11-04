# New-game starting areas

The game loads its track graph after you submit the new-game form. The original map dropdown therefore cannot discover additional starting areas on a fresh main menu. The mod adds **Choose starting area after map loads**, enabled by default for company games.

1. Enter the railroad name, reporting mark, save name and starting equipment as usual.
2. Leave **Choose starting area after map loads** checked and create the railroad.
3. After the map and mod patches finish loading, select a starting area in the new starting-area dialog.
4. Selecting a starting area walks the map's native progression tree and checks every prerequisite needed to reach it. For Bryson, all required approach sections and bridge/progression unlocks are included automatically. Those prerequisites cannot be unchecked while Bryson is the selected start. Select any additional unlocks you want; removing an optional unlock removes its dependent selections.
5. Choose **Begin railroad**. The mod validates equipment placement, applies progression and places the native starting equipment at the selected origin.

The simulation pauses while this dialog is open. NPC planning and joining clients wait until starter placement completes. The selected origin and unlocks are saved with the railroad. Common native track does not need to belong to an unlock feature to qualify as staging track.

New starts retain the native progression tree. They no longer reverse the expansion chain or leave required approach territory locked. Older saves created with the previous reverse-expansion start retain their saved behavior.

Starting equipment is relocated from the selected native equipment package. Freight cars are staged in clear yard space; locomotives and tenders remain together and use the area's repair/engine-service tracks, near the roundhouse at Bryson. Separate cuts can use separate tracks. The original money, loads, oil and equipment condition are preserved. Placement checks the entire cut, existing cars, reserved starter placements and switch fouling clearance before confirming the start. On maps without named engine-service tracks, clear local staging track is used. If engine-service tracks exist but cannot fit the power, confirmation reports the problem instead of putting the engines on a mainline.

Uncheck the option on the main menu, or choose **Use original East Whittier start** in the dialog, to use the native start. The original map dropdown still identifies the game's East Whittier progression dataset; the later starting-area selection chooses your actual origin within that map.

## Troubleshooting

If the main menu only lists East Whittier, leave the loaded-map choice enabled and continue creation. The full selection appears after loading, before starter equipment is placed. Refreshing an unloaded graph cannot discover locations.

Unavailable starts display a reason. Missing prerequisites, dependency cycles and insufficient staging space are rejected. Multiple approach branches are all included. Confirmation also checks the complete selected starter equipment across yard and engine-service tracks; a preliminary staging-track candidate may still lack enough switch-clear space. Pick another location or the original start if placement cannot be validated.

The mod records starting-area candidates under `START-AREAS` and selection/placement under `NEW GAME` in `TweaksAndThings-debug.log`. These checks use the native map when no other mods are installed and re-evaluate after graph changes.

These features still require live new-game and multiplayer validation. For missing areas or placement failures, [report a bug](https://github.com/rmroc451/TweaksAndThings/issues) with the selected start, equipment, mod version, map patches and debug log. See [Troubleshooting](TROUBLESHOOTING.md) for log locations and reporting details.
