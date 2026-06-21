# Project Change Log

## 2026-06-21 — FromList Args UI Enhancement

### Added
- `KnownValues.FromListBiomeArgs` — list of available biome names for FromList selector (Grass, Snow, Lava, Sand, Dirt, Deathland, Autumn)
- `KnownValues.FromListFactionArgs` — list of available faction names for FromList selector (Castle, Rampart, Tower, Inferno, Necropolis, Dungeon, Fortress, Cove, Conflux, Neutral, Random)
- `KnownValues.DifferentFromPrefix` — constant for "differentFrom:" prefix
- New localization strings: `S.EC.FromListAvailableArgs`, `S.EC.FromListAddArg`, `S.EC.FromListRemoveArg`, `S.EC.FromListArgHint`

### Modified
- `AddBiomeSelector()` — added dropdown with available biome args when "FromList" is selected; args box declared earlier for event handler access
- `FactionTypeCombo_SelectionChanged()` — new helper method to show/hide faction args dropdown based on selector type
- `FactionUpdateArgsBox()` — new helper method to update faction args text box
- Faction section in mainObject editor — added dropdown with available faction args when "FromList" is selected
- `UpdateBiomeArgsVisibility()` — updated to find available args panel by Tag reference

### Notes
- Serialization already works correctly via `JsonSerializer` with `List<string> Args` property
- Symphony.rmg.json has non-standard format (commas inside single array element) — file is now ignored per AGENTS.md rule
