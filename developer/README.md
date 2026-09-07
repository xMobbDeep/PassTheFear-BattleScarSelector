# Developer files

The repository root is arranged for players. This folder keeps the optional material needed to build or maintain the project from source.

## Folders and files

- `src/` contains the two C# projects: the standalone selector window and the BepInEx runtime plugin.
- `tools/` contains the PowerShell scripts that build the selector and install the two output files into a game folder.
- `private-assets/` contains local inputs extracted or generated from a contributor's own game copy. Its contents are ignored by Git; see its README for the expected files.
- `NuGet.Config` keeps the plugin's package restore settings with the project.

## Build

Install BepInEx 6 IL2CPP x64 in a local copy of the game and launch the game once so its interop assemblies are generated. Then run this from the repository root:

```powershell
.\developer\tools\Build-Selector.ps1 -GameRoot "C:\Games\Pass The Fear"
```

The script reads the private inputs, embeds the catalogue and icons into `dist/BattleScarSelector.exe`, compiles the plugin against the selected game folder, and writes the two player files to `dist/`.

To copy an existing build into the game folder:

```powershell
.\developer\tools\Install-Selector.ps1 -GameRoot "C:\Games\Pass The Fear"
```

Players can ignore this folder and use the prebuilt files in `dist/`.
