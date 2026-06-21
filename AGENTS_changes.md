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

## 2026-06-21 — Faction FromList Args UI Verification

### Verified
- Section 4 (`RebuildMainObjectEditor`): `facArgsPanel` visibility correctly toggled when `facTypeCombo` selection changes to/from "FromList"
- Section 5 (`RebuildAdditionalMainObjectsList`): Each city item has independent `facTypeCombo` and `facArgsPanel` — changing one doesn't affect others
- Both sections properly clear `factionListBox` selection and `mo.Faction.Args` when switching away from "FromList"
- Initial visibility set correctly based on `mo.Faction?.Type`

### Fixed
- `RebuildMainObjectEditor` (Section 4): Fixed crash when selecting faction args - removed premature `SelectedItems.Add` during ListBox initialization; moved logic to SelectionChanged handler and initial state setup
- `RebuildAdditionalMainObjectsList` (Section 5): Same fix for each city item in the loop
- Both sections: Changed initial `facArgsPanel.Visibility` to `Collapsed` instead of conditional, now properly set in code-behind after initialization
- Added null checks for `item.Content?.ToString()` before `List.Contains()` to prevent null reference warnings

### Notes
- Root cause: `factionListBox.SelectedItems.Add(item)` was called during initialization when ListBox wasn't fully ready, causing crash on first selection change
- Now faction args ListBox is always initialized empty, and selection is restored only when switching to "FromList" type or during initial setup after all controls are created

## 2026-06-21 — Faction Args UI Complete Rewrite

### Changed
- Replaced `ListBox` + `SelectionChanged` pattern with `ComboBox` (for adding) + `TextBox` (for display/edit) pattern — same approach as biome selector
- Section 4 (`RebuildMainObjectEditor`): Removed `factionListBox`, added `availableFactionsCombo` and `factionArgsBox`
- Section 5 (`RebuildAdditionalMainObjectsList`): Same changes for each city item
- Removed `isFactionInitializing` flag — no longer needed with new pattern
- Added `ParseFactionArgs()` and `UpdateFactionArgsBox()` helper methods

### Notes
- New pattern: User selects faction from dropdown → clicks to add → appears in text box → can edit manually or remove
- No more `SelectedItems` manipulation that caused crashes
- Each section (4 and 5) has independent controls, each city in section 5 has its own independent faction selector
