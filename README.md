# Pass The Fear Battle Scar Selector

A Windows selector for choosing up to three Blessings and three Curses before the next run of *Pass The Fear*. The selector shows the local Battle Scar icons and descriptions, then writes the six selected IDs to the configuration read by the BepInEx plugin.

This repository is source-first. Game-owned art and extracted data are deliberately kept out of Git. Each user supplies those files from their own game installation when building the selector.

## Features

- English visual interface with the game's button and artwork style.
- Separate Blessings and Curses pages with hover arrows and selection frames.
- Details panel with the selected Battle Scar's name and effect text.
- Maximum of three selections per category.
- IDs and icons are embedded into the built selector executable.
- Start saves `BattleScarSelector.cfg` beside the plugin DLL; the next run applies it.

## Requirements

- Windows x64.
- A legally installed Windows x64 copy of *Pass The Fear*.
- BepInEx 6 IL2CPP x64 in the game folder for runtime effects.
- .NET Framework 4.x for the selector build and a .NET SDK for the plugin build.

The official BepInEx IL2CPP guide explains the x64 installation and first-run generation steps: <https://github.com/BepInEx/bepinex-docs/blob/master/articles/user_guide/installation/unity_il2cpp.md>.

The selector is a separate Windows program, but it must run beside the plugin DLL so it can save `BattleScarSelector.cfg` in the same plugin directory. The plugin itself is loaded by BepInEx when the game starts.

## Build from source

1. Install BepInEx in your own game copy and run the game once so its IL2CPP interop files exist.
2. Create `private-assets` inputs from your own local game/mod work:

   - `private-assets/home-background.png`
   - `private-assets/button-frame.png`
   - `private-assets/battle_scar_ids.csv`
   - `private-assets/battle_scar_effects.tsv`
   - `private-assets/icons/<id>.png`

3. From the repository root, run:

   ```powershell
   .\tools\Build-Selector.ps1 -GameRoot "C:\Games\Pass The Fear"
   ```

The script embeds the private catalogue and icons into `BattleScarSelector.exe`, builds the plugin against the BepInEx/interop files in the selected game folder, and places both outputs in `dist/`.

## Install a build

Run the installer helper from the repository root after building:

```powershell
.\tools\Install-Selector.ps1 -GameRoot "C:\Games\Pass The Fear"
```

It copies the two files in `dist/` into `BepInEx/plugins/PassTheFearBattleScarSelector`. Run `BattleScarSelector.exe`, make six selections, and press Start. The selector does not launch the game; the saved configuration is consumed when the next run begins.

## What is intentionally absent

The repository does not contain the game executable, `GameAssembly.dll`, generated IL2CPP interop assemblies, extracted icons, background art, or extracted Battle Scar data. Those files belong to the game installation or are generated from it. They are required only as local build inputs and are ignored by Git.

## Project layout

`src/BattleScarSelector` contains the standalone Windows selector. `src/PassTheFearBattleScarSelector` contains the runtime plugin. `tools/Build-Selector.ps1` is the Windows build script. `private-assets` is local-only input and is ignored by Git.

