# Pass The Fear Battle Scar Selector

Choose a complete Battle Scar loadout for *Pass The Fear* before a run. The Windows selector presents every available Blessing and Curse with its icon and effect text, then the companion BepInEx plugin applies the six selected IDs when the next run starts.

The repository is organized for players first. The ready-to-use files are in [`dist/`](dist); source and rebuild material is kept under [`developer/`](developer).

## Quick start for players

1. Install **BepInEx 6 IL2CPP x64** in your own Windows copy of *Pass The Fear*, then launch the game once so BepInEx can finish its first-run setup. The [official BepInEx IL2CPP guide](https://github.com/BepInEx/bepinex-docs/blob/master/articles/user_guide/installation/unity_il2cpp.md) covers this step.
2. Download these two files from [`dist/`](dist):
   - [`BattleScarSelector.exe`](dist/BattleScarSelector.exe)
   - [`PassTheFearBattleScarSelector.dll`](dist/PassTheFearBattleScarSelector.dll)
3. Create the folder `BepInEx/plugins/PassTheFearBattleScarSelector` inside the game folder and place both files there.
4. Run `BattleScarSelector.exe` from that folder while the game is closed.
5. Select three Blessings and three Curses, then press **Start**. The selector saves the loadout beside the plugin DLL.
6. Launch the game normally. BepInEx reads the saved loadout at the beginning of the next run.

The selector does not launch the game for you. The executable already contains the catalogue, IDs, descriptions, and icons, so players do not need to download a separate icons folder, CSV, or TSV file.

## The three screens

### Home screen

The entrance screen uses the game's artwork as its background and places three centered game-style buttons in a simple flow:

- **Blessings** opens the Blessings selection page.
- **Curses** opens the Curses selection page.
- **Start** stays locked until exactly three Blessings and three Curses have been selected.

Returning from either selection page keeps the choices visible to the selector. When Start is pressed, the six IDs are written to `BattleScarSelector.cfg`; the game is then started separately so the plugin can apply them.

### Blessings page

The Blessings page uses a black background and shows the complete Blessing icon catalogue. Moving the pointer over an icon highlights it and shows the game's four green directional arrows. The details panel on the right displays the icon, name, and positive effect text. A left click selects or removes a Blessing, with a maximum of three selections; selected icons keep a visible frame so the loadout is easy to review. The arrow button in the lower-left corner returns to the home screen.

### Curses page

The Curses page follows the same layout and controls as the Blessings page. Hovering shows the highlight and green arrows, while the right-hand details panel displays the Curse name and effect. Curse effects use red text to match their negative in-game presentation. Up to three Curses can be selected, and the lower-left arrow returns to the home screen.

## What players download

The [`dist/`](dist) folder is the complete player package:

- `BattleScarSelector.exe` — the standalone visual selector with the catalogue and artwork embedded inside it.
- `PassTheFearBattleScarSelector.dll` — the BepInEx plugin that reads the saved selection and applies it in-game.

No compiler, Cpp2IL, source folder, private-assets folder, or separate data files are required for the prebuilt package. BepInEx remains a prerequisite because it is the runtime that loads the plugin.

## Folder guide

- `dist/` — the only folder a normal player needs.
- `developer/` — optional source and rebuild material. See [`developer/README.md`](developer/README.md).
- `LICENSE`, `NOTICE.md`, and this README — project and usage information.

The `developer` folder contains:

- `developer/src/` — C# source for the selector window and runtime plugin.
- `developer/tools/` — PowerShell helpers for building and installing a local build.
- `developer/private-assets/` — local game-derived inputs used only when rebuilding from source; its contents are ignored by Git.
- `developer/NuGet.Config` — package restore settings for the developer build.

## Requirements

For the prebuilt player package:

- Windows x64.
- A legally installed Windows x64 copy of *Pass The Fear*.
- BepInEx 6 IL2CPP x64 installed in that game folder.
- .NET Framework 4.8 or later for the standalone selector.

## Optional: build from source

Source builds are intended for contributors who want to update the catalogue, artwork, or plugin. Follow [`developer/README.md`](developer/README.md) and provide inputs from your own game copy. The build script embeds those local assets into a new selector executable and writes the two player files to `dist/`.

The repository intentionally does not include the game executable, `GameAssembly.dll`, BepInEx files, generated IL2CPP interop assemblies, or extracted game data. Those remain in each contributor's own local game installation; see [`NOTICE.md`](NOTICE.md) for ownership and third-party notices.

## Troubleshooting

- **Start is locked:** select three Blessings and three Curses first. A partial loadout cannot be saved.
- **The game starts but the loadout is unchanged:** confirm that both files are in `BepInEx/plugins/PassTheFearBattleScarSelector`, then launch the selector from that same folder and start the game after saving.
- **The selector cannot find the plugin folder:** do not run the executable from a separate download folder; keep it beside `PassTheFearBattleScarSelector.dll`.
- **The selector does not open:** install or repair .NET Framework 4.8, then try again while the game is closed.

## License

The project source is released under the MIT License. Game code and artwork remain the property of their respective owners. See [`NOTICE.md`](NOTICE.md) for the BepInEx notice and asset details.