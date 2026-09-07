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

The selector does not launch the game for you. The executable already contains the catalogue, IDs, descriptions, and icons, so no separate data or icon files are needed.

## Screenshots

### Home screen

![Home screen](docs/screenshots/home.png)

### Blessings screen

![Blessings screen](docs/screenshots/blessings.png)

### Curses screen

![Curses screen](docs/screenshots/curses.png)

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