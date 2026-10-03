# Installation, updates, building and release packaging

From version 2.1.9 onward, Tweaks and Things uses Unity Mod Manager (UMM). It has no Railloader or Strange Customs dependency and does not use a Railloader Definition.json manifest or content mixins.

Related guides: [NPC traffic](NPC-TRAFFIC.md), [player tools and caboose migration](PLAYER-TOOLS.md), and [troubleshooting](TROUBLESHOOTING.md).

## Install a release

1. Install and configure UMM for your Railroader installation using the manager's supported procedure.
2. Download the mod's installable asset from [GitHub Releases](https://github.com/rmroc451/TweaksAndThings/releases). The generated package is named **RMROC451.TweaksAndThings_VERSION.zip**.
3. Install that ZIP through UMM's **Mods** tab. GitHub's automatically generated source-code ZIP is not the installable mod package.
4. Confirm the resulting Mods/RMROC451.TweaksAndThings folder contains **Info.json**, **RMROC451.TweaksAndThings.dll**, and **Newtonsoft.Json.dll**.
5. Start the game and enable the mod in UMM. Configure settings/keybindings there, then load a railroad to access the traffic and timetable windows.

Manual extraction into that same mod folder is an alternative. Avoid a nested extra version folder between the mod directory and Info.json. Updating while the game is closed also avoids trying to replace an assembly the game has already loaded.

If a legacy Railloader installation remains for other mods, it can warn that this UMM folder lacks Definition.json. Do not add a dummy manifest or install another copy of Tweaks and Things to silence it. If no other mods need that loader, use its supported uninstall procedure and then verify/reinstall UMM injection as appropriate. This documentation does not require manually editing the game's Unity DLLs.

## Existing saves and settings

NPC services, daily plans, protected timetable rows, AI crew ownership, held deliveries, delinquency and parked pool records are saved in railroad game state. UMM preferences remain mod settings. Reinstalling a ZIP is not a reset button for saved traffic accounting.

Back up a save before comparing versions or testing physics regressions. Reopening a railroad resumes its saved work where applicable. Existing choices such as disabled tag updates can remain disabled after an upgrade even when a fresh installation's default is enabled.

Legacy cabeese.json mixins are not loaded by the UMM entry point. See [Player tools](PLAYER-TOOLS.md) for what the code-based crew-hours migration preserves and what needs custom-content migration.

## Multiplayer installation and authority

Automated spawning, interchange accounting and gameplay rules are host-authoritative. The host must run the mod for those features. This is broader than the older client-side UI-only behavior of the project.

Use matching versions on the host and participating clients when testing the mod's UI and interaction restrictions. Clients with the mod can view traffic, timetable history and diagnostic information, while host-only settings are disabled. Native placement snapshots and integration updates publish transfer results to clients; physical visibility and coupling still need in-game validation.

Do not interpret an AI-only crew as a player crew you can join. Its assignment and protected timetable are managed by the host and cleaned up with its service. If clients disagree with host state, include both versions and both sides' logs in a report.

## UMM update checking

The generated Info.json points UMM at **Repository.json** on this repository's main branch. Its homepage points to GitHub Releases. Enable update checks in UMM; the manager compares the installed version with the release feed and can flag a newer version and provide the release homepage.

Users with an older manifest lacking the feed address must first install a UMM package containing it. The initial feed entry is release preparation until its corresponding downloadable asset is published. A development branch or a locally built version does not automatically become a public update.

If UMM does not show an update, check the installed Info.json version/feed address, manager settings and the main-branch feed. Installing the release ZIP from the homepage remains the practical update path. The mod's in-game **Report a bug / feature** button opens issues, not the update installer.

## Configure a local build

The project targets .NET Framework 4.8 for the game. Its tests target .NET 8. Configure GameDir in the root **Paths.user** file as an MSBuild project pointing to your Railroader installation:

~~~xml
<Project>
  <PropertyGroup>
    <GameDir>E:\Program Files\Steam\steamapps\common\Railroader</GameDir>
  </PropertyGroup>
</Project>
~~~

GameManagedDir normally resolves the game's *_Data/Managed directory. UnityModManagerDir defaults to its UnityModManager subdirectory; override it in Paths.user if your manager assemblies are elsewhere.

The mod project explicitly lists **Assembly-CSharp** and **Ops**, along with Core, Definition, KeyValue.Runtime and the required Unity/game assemblies. Successfully resolving Core alone does not prove Assembly-CSharp or Ops was found. The references are generated from GameAssembly items in Directory.Build.targets. Private game API access is publicized at build time; game DLLs remain external references and are not packaged with the mod.

Typical commands from the repository root:

~~~powershell
dotnet test Tests/TweaksAndThings.Tests.csproj
dotnet build TweaksAndThings.sln -c Release
~~~

Debug output defaults to GameModDir; Release retains deployment to GameModDir. To build for review without installing into the game, explicitly redirect both GameModDir and OutDir:

~~~powershell
dotnet build TweaksAndThings/RMROC451.TweaksAndThings.csproj -c Release -p:GameModDir=C:/git/TweaksAndThings/artifacts/build -p:OutDir=C:/git/TweaksAndThings/artifacts/build/
~~~

Adjust those paths for your checkout. Omit --no-restore on a first build so package restore can run. The current development machine has .NET at C:/Program Files/dotnet/dotnet.exe; if dotnet is absent from PATH, invoke that executable explicitly. On this machine the net8 tests can run on the installed .NET 10 runtime with DOTNET_ROLL_FORWARD=Major. A missing-runtime error is different from a missing-SDK error.

## What the build packages

Both Debug and Release builds produce **RMROC451.TweaksAndThings_VERSION.zip** beside the actual output DLL, including when OutDir is customized. Rebuilding replaces that version's ZIP. The package contains the versioned Info.json, mod DLL and Newtonsoft.Json.dll; game assemblies, old archives and debug symbols are excluded.

Assembly.version is the version source. The source Info.json contains a placeholder replaced in the build output; distribute the generated manifest rather than copying the unresolved source placeholder. These root Markdown guides are repository documentation and are not currently additional files inside the three-file install package.

## Publish a release after 2.1.9

1. Increase Assembly.version to the intended numeric major.minor.patch version, for example 2.1.10.
2. Build, run the tests and check patch bindings against the target game installation. Perform in-game checks for the changed behavior.
3. Inspect the generated ZIP and its Info.json version. Confirm it includes the three install files and no game DLLs.
4. Create the corresponding GitHub release tag, for example v2.1.10, and upload the generated RMROC451.TweaksAndThings_2.1.10.zip asset.
5. After the asset is publicly downloadable, update Repository.json on main. Keep Id as RMROC451.TweaksAndThings and set Version and DownloadUrl to the published asset.
6. Verify the raw feed is valid JSON and its download URL actually downloads the installable ZIP. Test update detection from an older UMM installation.

Example asset URL:

~~~text
https://github.com/rmroc451/TweaksAndThings/releases/download/v2.1.10/RMROC451.TweaksAndThings_2.1.10.zip
~~~

The feed should advertise the latest public stable release, not an unpublished local build or prerelease. Building a ZIP does not upload it, create a tag, publish a GitHub release or update the public feed.

## Build and installation troubleshooting

| Symptom | Check |
| --- | --- |
| dotnet is not recognized | Check SDK installation and PATH; use the full executable path when available. |
| Assembly-CSharp or Ops cannot resolve | Check GameDir, GameManagedDir and the installed game's Managed DLLs; retain their GameAssembly entries. |
| Harmony or UnityModManager cannot resolve | Check UnityModManagerDir and its actual DLLs. |
| Restore fails | Inspect the restore error separately from compilation; confirm package access and SDK/framework prerequisites. |
| Tests request a missing .NET 8 runtime | Install the appropriate runtime or use the documented supported roll-forward environment on the development machine. |
| UMM cannot recognize the ZIP | Use the release asset, inspect its three files and verify the generated Info.json. |
| Definition.json warning | Check for a legacy loader scanning the UMM folder; do not create a second install. |

For unresolved problems, follow [Troubleshooting and bug reporting](TROUBLESHOOTING.md). [Search existing issues or report a new problem](https://github.com/rmroc451/TweaksAndThings/issues).
