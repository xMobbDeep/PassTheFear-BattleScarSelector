# Private build inputs

This folder is for contributors who want to rebuild the selector from source. It is not needed by players who download the files in the repository's `dist/` folder.

Before a source build, place data from your own copy of *Pass The Fear* here:

- `home-background.png` and `button-frame.png` for the standalone selector artwork.
- `battle_scar_ids.csv` for the Blessing and Curse catalogue.
- `battle_scar_effects.tsv` for the effect text shown in the details panel.
- `icons/<id>.png` for the Battle Scar icons, using the IDs from the catalogue.

The contents are ignored by Git because they are game-owned or generated from the game. They are embedded into a locally built executable and are never required in a player's install folder.
