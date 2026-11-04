# Installation, updates, building and release packaging

Tweaks and Things is a native Railroader DLL mod. UnityModManager is not required. DLL loading is an experimental game feature and must be enabled before the mod can load.

Related guides: [NPC traffic](NPC-TRAFFIC.md), [player tools and caboose migration](PLAYER-TOOLS.md), and [troubleshooting](TROUBLESHOOTING.md).

## Install a release

1. In Railroader, enable **Preferences → Features → Experimental → Allow Unsafe DLL Mods**, then restart the game.
2. Download the installable **RMROC451.TweaksAndThings_VERSION.zip** from [GitHub Releases](https://github.com/rmroc451/TweaksAndThings/releases). GitHub's automatically generated source ZIP is not the mod package.
3. Extract the pack into a folder under `Documents\Railroader\Mods`. Keep `Info.json`, `RMROC451.TweaksAndThings.dll`, `0Harmony.dll`, and `Newtonsoft.Json.dll` together at the pack folder's root.
4. Start the game. Press **F10** to open the settings window. Settings are stored in `Settings.json` beside the mod files.

Update by closing the game and replacing the existing mod pack files. Do not install a second copy of the mod under a different folder.

## Existing saves and settings

NPC services, daily plans, protected timetable rows, AI crew ownership, held deliveries, delinquency and parked pool records are saved in railroad game state. Mod preferences are stored in `Settings.json`. Reinstalling a ZIP does not reset saved traffic accounting.

NPC traffic and simulated interchange settings are currently hidden from F10; their setup controls are forthcoming. Existing saved preferences and traffic state are retained across reinstalls.

Back up a save before comparing versions or testing physics regressions. Reopening a railroad resumes its saved work where applicable. Existing settings from UnityModManager are not imported automatically; review the options in the F10 settings panel after migration.

Legacy `cabeese.json` mixins are not loaded. See [Player tools](PLAYER-TOOLS.md) for what the code-based crew-hours migration preserves and what needs custom-content migration.

## Multiplayer installation and authority

Automated spawning, interchange accounting and gameplay rules are host-authoritative. The host must run the mod for those features. Use matching mod versions on the host and participating clients when testing UI and interaction restrictions. Host-only settings are disabled for clients.

Do not interpret an AI-only crew as a player crew you can join. Its assignment and protected timetable are managed by the host and cleaned up with its service. If clients disagree with host state, include both versions and both sides' logs in a report.

## Configure a local build

The project targets .NET Framework 4.7.2 to match the documented Railroader DLL mod toolchain. Tests target .NET 8. Configure `GameDir` in the root **Paths.user** file:

~~~xml
<Project>
  <PropertyGroup>
    <GameDir>E:\Program Files\Steam\steamapps\common\Railroader</GameDir>
  </PropertyGroup>
</Project>
~~~

`GameManagedDir` resolves the game's `*_Data/Managed` directory. The mod references `Railroader.Modding.dll` for the loader contract and game/Unity assemblies for its gameplay integration. Those game assemblies are build references and are not included in the release package. Harmony and Newtonsoft.Json are packaged beside the mod assembly.

Typical commands from the repository root:

~~~powershell
dotnet test Tests/TweaksAndThings.Tests.csproj
dotnet build TweaksAndThings.sln -c Release
~~~

Debug output defaults to `Documents\Railroader\ModDev\RMROC451.TweaksAndThings`; Release builds package the mod and copy its install files there. To build for review without deploying to the game's development-mod folder, redirect both paths:

~~~powershell
dotnet build TweaksAndThings/RMROC451.TweaksAndThings.csproj -c Release -p:GameModDir=C:/git/TweaksAndThings/artifacts/build -p:OutDir=C:/git/TweaksAndThings/artifacts/build/
~~~

Adjust paths for your checkout. Omit `--no-restore` on a first build so package restore can run.

## What the build packages

Both Debug and Release builds produce **RMROC451.TweaksAndThings_VERSION.zip** beside the output DLL, including when `OutDir` is customized. The pack contains the generated `Info.json`, mod DLL, `0Harmony.dll`, and `Newtonsoft.Json.dll`. Game assemblies, old archives and debug symbols are excluded.

`Assembly.version` is the version source. The source `Info.json` contains a placeholder replaced in the build output; distribute the generated manifest, which declares the DLL assembly and required minimum `rr/game` version.

## Publish a release

1. Increase `Assembly.version` to the intended numeric major.minor.patch version.
2. Build and review the package. Check the ZIP's `Info.json` and confirm it contains the four install files and no game DLLs.
3. Create the matching GitHub release tag and upload the generated ZIP asset. Do not substitute GitHub's generated source-code archive.
4. Users install updates by replacing the mod pack while the game is closed.

Building a ZIP does not upload it, create a tag, or publish a GitHub release.

## Build and installation troubleshooting

| Symptom | Check |
| --- | --- |
| DLL mod is not loaded | Enable **Allow Unsafe DLL Mods** and restart. Check Manage Mods for an unmet `rr/game` dependency. |
| `Railroader.Modding.dll` cannot resolve | Check `GameDir`, `GameManagedDir`, and the installed game's Managed directory. |
| Harmony or Newtonsoft.Json cannot resolve | Confirm `0Harmony.dll` and `Newtonsoft.Json.dll` are beside `Info.json` and the mod DLL. |
| Restore fails | Inspect the restore error separately from compilation; confirm package access and framework targeting-pack prerequisites. |
| Settings appear reset | The native loader stores them in `Settings.json`; UnityModManager's prior settings are not imported automatically. |
| A game update prevents loading | Update the mod to a build whose manifest `rr/game` floor supports that game version. |
