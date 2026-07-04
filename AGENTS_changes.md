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

## 2026-06-21 — Guard Fields & Owner Section Updates

### Section 4 Changes
- **RemoveGuardIfHasOwner**: When checked → clears GuardChance, GuardValue, GuardWeeklyIncrement to null, hides all guard fields
- **RemoveGuardIfHasOwner**: When unchecked → shows guard fields again
- Added `UpdateGuardFieldsVisibility()` helper that checks both object type AND RemoveGuardIfHasOwner flag
- Added initial guard fields visibility check based on existing RemoveGuardIfHasOwner state

### Section 5 Changes
- **Owner field**: Added Owner ComboBox visible only when type is "City"
- Owner field updates Faction to Match with arg "0" when set
- Added `showOwner` boolean in `UpdateFieldVisibility()` to toggle ownerPanel
- Added `ownerPanel` to itemPanel.Children

### Serialization Notes
- `removeGuardIfHasOwner` serializes as boolean? via `[JsonPropertyName("removeGuardIfHasOwner")]`
- `owner` serializes as string? via `[JsonPropertyName("owner")]`
- `guardChance`, `guardValue`, `guardWeeklyIncrement` serialize as null when cleared (nullable types)
- Matches JSON structure: `{"type":"City","owner":"Player1","faction":{"type":"FromList","args":["Castle"]}}`

## 2026-06-21 — Generator Bug Fixes (vs map_templates reference)

### Fixed
- **Spawn guard fields**: Removed `GuardChance = 1` and `GuardValue`/`GuardWeeklyIncrement` from Spawn objects. Reference templates only use `removeGuardIfHasOwner: true` on Spawns without guard fields.
- **contentCountLimits**: Removed `PlayerMin`/`PlayerMax` fields from all `content_limits_side_*` definitions. Reference templates never have these fields.
- **Removed `content_limits_side_0_0`**: This entry doesn't exist in reference templates.

## 2026-06-21 — UI Fixes

### Fixed
- **HoldCity label overlap**: Rewrote `AddCheckField` to use `DockPanel` with separate `CheckBox` and `TextBlock` controls. The `TextBlock` has `TextWrapping.Wrap` to prevent overlap with adjacent input fields.
- **Owner field auto-hide (Section 4)**: Wrapped owner `ComboBox` in `ownerPanel` StackPanel. Panel visibility is toggled in `UpdateFieldVisibility` — hidden for Spawn, AbandonedOutpost, GladiatorArena. Owner value is cleared when type changes to non-City.
- **Owner field auto-hide (Section 5)**: Already had `ownerPanel` wrapper. Added owner clearing logic to existing `typeCombo.SelectionChanged` handler.
- **Removed "Add Content Objects" button**: This button only called `BuildInspector()` and had no real functionality. Removed entirely.

### Known Issues Found (not yet fixed)
- **Faction args**: Generator uses `FromList` with empty `Args = []` (correct), but visual editor may show raw faction names like "Nature", "Undead", "Demon" which are invalid. Reference templates only use `[]` or `differentFrom:` expressions.
- **Connection names**: Auto-generated connections in UI get numeric names like "123", "1", "2". Reference templates use descriptive names like "Bridge-A-B", "Spawn-A-Treasure-1".
- **MatchZone self-reference**: Zone-2 referencing itself (`"args": ["Zone-2"]`) is circular. Reference templates reference Spawn zones or parent zones.

## 2026-06-21 — Content Pool Viewer

### Added
- `ContentPoolViewerWindow` — separate window showing pool contents in a table format
- `ContentPoolInfo` and `ContentPoolData` classes in `Models/ContentPoolInfo.cs` with full pool metadata
- "📋 Просмотр содержимого пулов" button in the Pools tab that opens the viewer

### Pool Viewer Features
- Dropdown to select any pool
- Table showing: SID, Category (Guarded/Unguarded/Resources), Tier, Content Type, Description
- Pre-selects pools that are currently assigned to the zone
- Shows descriptive names for content types (items, pandora boxes, hires, unit banks, resource banks, stat buildings, magic buildings, resources)

## 2026-06-21 — Pool Creator & Game Data Integration

### Added
- `ContentPoolCreatorWindow` — window for creating new content pools
- `ContentListInfo` class with ~60 content lists from game data
- "➕ Создать новый пул" button in Pools tab
- Category filter for content lists (Предметы, Шкатулки, Наёмники, Банки существ, etc.)
- Add/remove lists via + button or double-click
- Export pool as JSON file matching game format

### Pool Viewer Rewrite
- Now loads real pool data from game files (`StreamingAssets/generator/content_pools/`)
- Shows actual pool contents: list name, object SID, weight, biome
- Displays pool statistics: groups count, objects count, total weight
- Highlights missing lists in red
- Fixed label text color to black
- Fixed localization: "списки контента" (was "спикои"), "Ящики Пандоры" (was "Шкатулки"), "Внешние жилища" (was "Наёмники")

### Game Data Analysis
- Parsed content pool files from `StreamingAssets/generator/content_pools/`
- Parsed content list files from `StreamingAssets/generator/content_lists/`
- Identified ~60 unique content lists across 15+ categories
- Pool structure: name → groups (weight + includeLists) → bans
- Content lists contain SIDs with weights and optional biome restrictions

### Game Data as Embedded Resources
- Copied 78 JSON files from game directory to `Resources/GameData/`
- Configured as Embedded Resources in `.csproj`
- `GamePoolDataLoader` now reads from assembly manifest resources
- No dependency on external game installation path

## 2026-07-01 19:50 — User: "найди где теперь находится кнопка 'менеджер связей' и все её взаимосвязи ... нужно изменить ..."

### Fixed
- XAML syntax error: stray `'` on line 49 of `TemplateEditorWindow.xaml`
- `BtnConnectionManager_Click` now non-modal via `Show()` instead of `ShowDialog()`
- Reopens existing window if already open (focus, no duplicate)
- `ConnectionManagerWindow` XAML completely rewritten: proper WPF styles, GridView columns, binding

### Added
- `ConnectionManagerWindow`: non-modal connection manager, double-click opens settings editor, numbering column, remove button per row
- `ConnectionSettingsWindow`: standalone connection property editor (name, type, guard value, road) opened on double-click
- `ConnectionInfo.Number` property for sequential numbering
- Connection lines in `RebuildGraph()` now use per-group fan-out (perpendicular offset with `fanSpread=18px`) to eliminate visual overlap
- Groups sorted by connection name for deterministic left-to-right fanning

### Changed
- `RebuildGraph()` made `internal` for access from manager window
- `BuildInspector()` made `internal` for access from manager window
- `MarkDirty()` made `internal` for access from manager window
- `BtnConnectionManager_Click` stores window reference, reuses on re-click
- First connection in a group placed at leftmost position (negative perpendicular offset), subsequent ones progressively right

## 2026-07-01 20:10 — User: "сделай бат скрипт для сборки билда"

### Added
- `build.bat` — cmd-совместимый скрипт сборки с restore → build → publish single-file
- `build.ps1` — PowerShell-версия того же скрипта
- Оба скрипта: принимают параметр Debug/Release, публикуют single-file self-contained .exe в `build/<Configuration>/`

## 2026-07-01 22:10 — User: "продолжай" — crash diagnostics

### Added
- try-catch with MessageBox in `BtnConnectionManager_Click` — перехватывает и показывает любую ошибку при открытии менеджера связей
- try-catch в конструкторе `ConnectionManagerWindow` — перехватывает ошибки `InitializeComponent()`
- try-catch в конструкторе `ConnectionSettingsWindow` — перехватывает ошибки `InitializeComponent()`

### Changed
- Clean rebuild (obj/bin полностью очищены) — 0 errors, 14 pre-existing warnings
- App запускается без ошибок

### Fixed
- **Crash on "Менеджер связей"**: `GridViewColumn Width="*"` на 107 строке `ConnectionManagerWindow.xaml`. `*` (star sizing) не поддерживается `GridViewColumn` — только `Auto` или пиксели. Заменено на `Width="200"`.

## 2026-07-01 22:30 — User: "линии графа после перемещения любой из соединённых зон сбрасывают позицию"

### Fixed
- **Lines reset to center on zone drag**: `RepositionZone` устанавливала концы линий в центр зоны (`line.X1 = p.X`), но `RebuildGraph` рисует линии от **границы** зоны (`p.X + ux * r1`). После перетаскивания → при следующем перестроении линии "прыгали" на границу = воспринималось как сброс позиции.

  **Решение**: добавлен `_edgeFanOffsets` — словарь, хранящий перпендикулярное смещение для каждого соединения. `RepositionZone` теперь пересчитывает точки на границе зоны по той же формуле, что и `RebuildGraph` (от `conn.From` к `conn.To`, с радиусом + смещением).

- **Линии не доходят до квадрата зоны / начинаются за его пределами**: перпендикулярное смещение (`fanSpread = 18px`) могло превышать радиус зоны (например, 4 соединения → offset 27px > radius 24px). Точка начала/конца линии оказывалась за пределами квадрата зоны.

  **Решение**: смещение зажимается в `[-maxPerp, maxPerp]`, где `maxPerp = min(rFrom, rTo) * 0.85`. Линии всегда начинаются/заканчиваются в пределах квадрата зоны.

### Notes
- `_edgeFanOffsets` очищается в начале `RebuildGraph()`
- Все вычисления теперь единообразны: направление всегда от `conn.From` к `conn.To`, с корректным применением радиуса и перпендикулярного смещения

## 2026-07-01 22:50 — User: геометрические несоответствия схемы (асимметрия, углы, сетка)

### Fixed
- **Центральная зона не распознавалась как хаб**: `LayoutZonesRing` искал хаб по имени (`"Hub"` / `"Hub-*"`), но реальные шаблоны используют зону с layout `zone_layout_center` (обычно названную "Center"). Хаб не находился → все зоны (включая центр) размещались на кольце, а центр был пуст. С 8 спавнами + центром на кольце получалось 9 узлов с углами 40° вместо 45°, что вызывало все описанные искажения.

  **Решение**: добавлено определение хаба по layout (`zone_layout_center`, `zone_layout_center_zone`) в дополнение к проверке по имени.

- **Позиции не привязаны к сетке**: перетаскивание зон использует `SnapToGrid` (50px), но начальные позиции из `LayoutZonesRing` не привязывались. Зоны визуально не совпадали с линиями фоновой сетки.

  **Решение**: добавлен `SnapAllPositionsToGrid()` в `ComputePositions()` — все зоны после расчёта позиционируются строго по узлам сетки 50×50px, что даёт идеальное совпадение с фоновой решёткой.

## 2026-07-01 23:00 — User: "удлини линии связей что бы они не обрывались"

### Fixed
- **Линии обрываются, не доходя до квадрата зоны**: линии рисовались от границы зоны (`center + ux * r`). Из-за перпендикулярного смещения (fan-out) и квадратной формы зоны точка границы могла не совпадать с видимым краем квадрата — линия казалась обрезанной.

  **Решение**: линии теперь рисуются от **центра** до **центра** зоны (с перпендикулярным смещением). Квадраты зон рисуются ПОВЕРХ линий и скрывают внутреннюю часть. Благодаря этому линии всегда доходят до видимого края квадрата. `RebuildGraph()` и `RepositionZone()` синхронизированы — оба используют center-to-center подход.

## 2026-07-01 23:10 — User: "при создании зоны в биомах выставляются аргументы по умолчанию измени их на: matchmainzone..."

### Changed
- **Biome defaults**: при создании зоны (`AddZoneAt`) тип биома изменён с `MatchZone` на `MatchMainZone`, аргументы пустые (вместо `Args = [name]`)
- **Добавлен "MatchMainZone"** в `KnownValues.SelectorTypes`
- **Скрытие поля ввода аргументов** при выборе `FromList` в `AddBiomeSelector`: теперь `argsBox` скрывается (`Visibility.Collapsed`), а панель available-args показывается
- **Авто-дороги между главными объектами**: при добавлении дополнительного главного объекта (`addAdditionalMoBtn.Click`) автоматически создаётся `Road` от `MainObject["0"]` к `MainObject[newIndex]` с типом `Stone`

## 2026-07-01 23:40 — User: "локализацию скрытых полей тоже нужны скрывать так же нужно добавить возможность полного копирования зон..."

### Fixed
- **Скрытие локализации поля аргументов биома**: `BiomeArgs` (label + textbox) теперь обёрнуты в `StackPanel argsSection`. При выборе `FromList` скрывается весь `argsSection`, а не только textbox. `UpdateBiomeArgsVisibility` тогглит видимость `argsSection` вместо `argsBox`.

### Added
- **Копирование/вставка зоны**: `CopySelectedZone()` — глубокое копирование выбранной зоны через JSON (сериализация/десериализация), сохранение в `_copiedZone`. `PasteCopiedZone()` — вставка копии с новым уникальным именем, позиция со смещением.
- **Горячие клавиши**: `Ctrl+C` — копировать, `Ctrl+V` — вставить (в `Window_KeyDown`).
- **Кнопки на тулбаре**: `BtnCopyZone` (`S.Ed.013`) и `BtnPasteZone` (`S.Ed.014`).
- **Новые строки локализации**: `S.EC.SelectZoneFirst`, `S.EC.NothingCopied`, `S.EC.ZoneCopied`, `S.EC.ZonePasted`, `S.Ed.013`, `S.Ed.014` (RU + EN).

## 2026-07-02 00:10 — Fix visual editor after template audit

### Fixed
- **Чистка дорог при удалении связи** (`TemplateEditorWindow.xaml.cs`): добавлен `RemoveRoadReferences(string)`, удаляет `roads[]` во всех зонах, ссылающиеся на удаляемую связь. Вызывается при удалении связи через `BtnDelete_Click` и `ConnectionManagerWindow`.
- **Чистка дорог при удалении зоны**: перед удалением связей зоны, для каждой вызывается `RemoveRoadReferences`.
- **Уникальность имён связей при создании** (`HandleConnectClick`): если `"Direct-A-B"` занято, добавляет суффикс `-2`, `-3`...
- **Уникальность имён связей при редактировании** (инспектор `ConnName`): при изменении имени проверяется уникальность, иначе `MessageBox` + отмена.
- **Валидация дубликатов связей** (`ZoneGraphValidator`): добавлена проверка `connNames.Add(c.Name)`, выводит `"Duplicate connection name"`.
- **`ContentSidLimit.Sid` → `string?`**: пустые SID не сериализуются (null → `WhenWritingNull`). Добавлены поля `Variant`, `IncludeLists`, `Content` для сохранения entry с `includeLists`/`content` вместо `sid` (которые терялись при round-trip).
- **Очистка пустых SID при сохранении**: `BtnSave_Click` перед записью удаляет `ContentSidLimit` с пустым Sid из `contentCountLimits` и `mandatoryContent`.
- **`NewEmptyTemplate`**: добавлены дефолтные `Orientation` (MinimalBoundingSquare) и `Border` (CornerRadius=0, ObstaclesWidth=3), чтобы не терялись при создании нового шаблона.
- **`MatchMainZone` → `MatchMainObject["0"]`**: дефолтный тип биома при создании зоны заменён на `MatchMainObject`. `MatchMainZone` удалён из `KnownValues.SelectorTypes`.
- **`simTurnSquad = true`** при создании `Direct`-связи.
- **`Length = 0.94`** при создании `Proximity`-связи.

## 2026-07-02 00:30 — JSON preview with edit capability

### Added
- **`JsonPreviewWindow`** — новое окно предварительного просмотра JSON шаблона:
  - `JsonBox` — большой TextBox (Consolas, моноширинный) с сериализованным `_template`
  - Валидация JSON в реальном времени при изменении текста (зелёный/красный статус)
  - Кнопка **«Применить»** — десериализует JSON в `RmgTemplate`, вызывает `LoadTemplate()`, обновляет граф; если ошибка — изменения сбрасываются, показывается сообщение
  - Кнопка **«Сбросить»** — перезаписывает текст текущей сериализацией `_template`
  - Кнопка **«Закрыть»**
- **Кнопка `📄 JSON`** на тулбаре редактора (`BtnJsonPreview`)
- **`LoadTemplate(RmgTemplate)`** — public метод для перезагрузки шаблона из JSON
- **`CurrentTemplate`** — public свойство для доступа к `_template` из `JsonPreviewWindow`
- **`UpdateStatus`** → public (для вызова из `JsonPreviewWindow`)
- **Строки локализации**: `S.Ed.JP`, `S.Ed.JP.Title`, `S.Ed.JP.Hint`, `S.Ed.JP.Apply`, `S.Ed.JP.Reset`, `S.Ed.JP.Close` (RU + EN)

## 2026-07-02 — Proximity Length & Placement editor for additional main objects

### Added
- **Length field in connection inspector**: поле `length` отображается только для типа `Proximity`; скрывается/показывается при смене типа связи
- **Placement/PlacementArgs editor for new additional objects**: при добавлении нового главного объекта (секция 5) показываются:
  - ComboBox выбора `Placement` (Center/Connection/NearZone/Uniform)
  - TextBox для `PlacementArgs` (через запятую; скрыт при Center)
  - CheckBox «Авто-дорога» (вкл по умолчанию) — создаёт `Road { MainObject[0] → MainObject[N] }`
- **PlacementArgs editing in additional objects list**: поле `PlacementArgs` добавляется в `RebuildAdditionalMainObjectsList` для каждого объекта; редактируется через запятую; скрывается при Center

### Fixed
- **CS0841 ordering**: `lengthPanel` объявлен до `AddComboField` (который ссылается на него через callback)

## 2026-07-02 18:50 — Player de-dup, guard-vs-owner, zone validation, placement fix

### Added
- **`GetUsedPlayers(excludeZoneName)`** — helper that scans all zones for already-assigned `Owner` values
- **`ValidateZone(Zone)`** — post-paste/insert validation that fixes:
  - Invalid `Placement` → `"Uniform"`
  - `Spawn` cleared on non-Spawn types
  - `Owner` cleared on non-City types
  - Guard values zeroed + `RemoveGuardIfHasOwner=true` when `Owner` is set
  - Stale roads (index out of range) removed

### Changed
- **Owner duplicate prevention** (both `RebuildMainObjectEditor` + `RebuildAdditionalMainObjectsList`):
  - При выборе игрока-владельца проверяется, не занят ли он в другой зоне
  - Если занят — автоматически назначается первый свободный игрок с предупреждением
- **Guard fields auto-hide on owner/spawn**:
  - `UpdateGuardFieldsVisibility` (primary) and `UpdateFieldVisibility` (additional) now check `mo.Owner != null || mo.Spawn != null`
  - Когда владелец назначен: `RemoveGuardIfHasOwner=true`, guard values → `null`, поля скрыты
  - Когда владелец снят: `RemoveGuardIfHasOwner=false`, поля показываются
- **`PlacementArgs` editor added to primary `RebuildMainObjectEditor`** (ранее был только в additional list)
- **`removeGuardCheck.Unchecked`** now calls `UpdateGuardFieldsVisibility()` instead of directly setting visibility (respects owner check)
- **Spawn player combo** now calls `UpdateGuardFieldsVisibility()` to hide guards when spawn player is set

### Fixed
- **Placement defaults UI**: `addAdditionalMoBtn.Click` uses the new placement/args controls (from previous sessionʼs change) instead of hardcoded `"Uniform"`

## 2026-07-03 — Auto-rewrite roads on main object removal + removeGuardIfHasOwner on spawn

### Added
- **`AdjustRoadIndicesAfterRemoval(Zone, int)`** — static method in `TemplateEditorWindow.xaml.cs:2798`
  - Удаляет дороги, ссылающиеся на `MainObject[removedIndex]`
  - Декрементирует индексы > `removedIndex` в оставшихся дорогах
- **`ShiftArgs(List<string>, int)`** — helper для декремента числовых args

### Changed
- **Оба remove-обработчика** (`RebuildMainObjectsList` ~line 1405, `RebuildAdditionalMainObjectsList` ~line 1541):
  - Перед `z.MainObjects.RemoveAt(capturedIdx)` вызывается `AdjustRoadIndicesAfterRemoval(z, capturedIdx)`
- **Spawn combo handler** (`RebuildMainObjectEditor`, moved после `removeGuardCheck`):
  - При назначении `Spawn` → `RemoveGuardIfHasOwner = true`, guard values → null, UI-боксы → "0"
  - При снятии `Spawn` → `RemoveGuardIfHasOwner = false`
- **`ValidateZone`** (~line 2868): условие расширено с `mo.Owner != null` до `mo.Owner != null || mo.Spawn != null`
  - На load/paste также проставляется `RemoveGuardIfHasOwner` для spawn-объектов

### Fixed
- **Crash on zone click** — duplicate `panel.Children.Add(spawnPanel)` removed (WPF throws `InvalidOperationException` when same child added twice)

## 2026-07-03 — Duplicate player check on paste zone

### Changed
- **`PasteCopiedZone()`** — после `ValidateZone(copy)` проверяет конфликты `Owner` и `Spawn` с существующими зонами:
  - Собирает `usedOwners` (через `GetUsedPlayers()`) и `usedSpawns` (все `mo.Spawn` из существующих зон)
  - При конфликте переназначает первого свободного игрока из `KnownValues.SpawnPlayers`
  - Показывает `MessageBox.Warning` если были переназначения

### Serialization
- Без изменений. JSON-формат дорог и main objects не меняется.

## 2026-07-03 — NearZone dropdown + panel relocation

### Added
- **`BuildNearZoneCombo(MainObject)`** — helper method in `TemplateEditorWindow.xaml.cs`, creates a zone name ComboBox populated from `Zones` list. On selection, sets `mo.PlacementArgs = [selectedZone]`. Used in all three placement editor sections.
- **NearZone zone selector dropdown** in all three Placement editor locations:
  - `RebuildMainObjectEditor` (primary MO): `nearZonePanel` with zone ComboBox, hidden by default, visible only when `Placement = "NearZone"`
  - `RebuildAdditionalMainObjectsList` (additional MO list): same pattern for each city item
  - `BuildInspector` (new MO panel): `newMoNearZonePanel` + `newMoNearZoneCombo`, values read in `addAdditionalMoBtn.Click` handler
- **Localization strings**: `S.EC.MoAdditionalArgs` (RU: "Аргументы дополнительно добавленных главных объектов", EN: "Additional main object arguments")

### Changed
- **Placement visibility logic**: When `Placement = "NearZone"`, the text box (`PlacementArgs`) is hidden and the zone dropdown (`NearZone`) is shown. When switching away from NearZone, the dropdown is hidden and the text box is shown again.
- **New MO panel moved**: `newMoPlacePanel` moved from `mainPanel.Children` into `additionalMoSection.Children` as its first child (inside Section 5 instead of between sections)
- **Header text**: Changed from `L("S.EC.MoPlacement") + " (новый)"` to `L("S.EC.MoAdditionalArgs")`

### Serialization
- `"placement": "NearZone"` with `"placementArgs": ["ZoneName"]` — matches format found in `Christmas Tree.rmg.json`

## 2026-07-03 — Road sync, auto-roads for Connection, NearZone visibility, player reassignment

### Added
- **`SyncConnectionRoadFlags()`** — scans all `zone.roads`, finds road endpoints with `Type = "Connection"`, and sets `conn.Road = true` for the matching connection by name. Called in `LoadTemplate`, `RebuildGraph` (covers paste, JSON apply too).
- **`AddUniqueRoad(Zone, fromType, fromArgs, toType, toArgs)`** — adds a road to zone.Roads only if an identical endpoint pair doesn't already exist (duplicate-safe).

### Changed
- **Auto-roads for Connection-placed MainObjects** (`addAdditionalMoBtn.Click`):
  - Canonical OctoJebus pattern: creates `MainObject[0] → Connection[name]` + `MainObject[newIdx] → Connection[name]`
  - Non-Connection placements keep `MainObject[0] → MainObject[newIdx]` (unchanged)
  - `name` = the value of `placementArgs[0]` from the new main object
- **NearZone panel initial visibility**: `nearZonePanel` now starts with `Visibility = mo.Placement == "NearZone" ? Visible : Collapsed` (was always Visible before first placement change). Applied in both `RebuildMainObjectEditor` and `RebuildAdditionalMainObjectsList`.
- **Player reassignment ascending**:
  - `GetUsedPlayers()` now collects both `mo.Owner` AND `mo.Spawn` values (was Owner-only). Prevents assigning a player that's already used as Spawn in another zone.
  - `PasteCopiedZone()` uses unified `usedPlayers` set for both Owner and Spawn conflict resolution, with ascending Player1→Player8 selection via `KnownValues.SpawnPlayers.FirstOrDefault`.

## 2026-07-03 — User: "реализуй" (auto-roads for all Connection-placed MOs in loaded/generated templates)

### Added
- **`RebuildConnectionRoads()`**: scans all zones, for each Connection-placed `MainObject[i>0]`:
  - Removes stale roads that don't point to the correct connection
  - Adds `MainObject[0] → Connection[name]` and `MainObject[i] → Connection[name]` via `AddUniqueRoad`
- **Called in**: `LoadTemplate` (after `AutoGenerateConnectionNames`), `RebuildGraph` (before `SyncConnectionRoadFlags`)

## 2026-07-03 — User: "реализуй" (sync placementArgs to existing connections)

### Changed (rewrite)
- **`SyncConnectionPlacementArgs()`** rewritten:
  - Replaced `.ToHashSet()` with `.ToList()` for **deterministic iteration order** (was non-deterministic, caused wrong connection assignment in pasted zones)
  - **Priority**: road connections (`Road == true`) first, then non-road connections
  - For each invalid `PlacementArgs[0]`: finds first **free road connection** (unused by other MOs), falls back to any free connection
- **Called in**: `LoadTemplate` (after `AutoGenerateConnectionNames`, before `RebuildConnectionRoads`), `RebuildGraph` (before `RebuildConnectionRoads`)

## 2026-07-03 - User: 'реализовывай' (zone-aware + bidirectional road priority)

### Rewritten again
- **'SyncConnectionPlacementArgs()'**:
  - **Zone-aware**: placementArgs[0] считается невалидным, если соединение не участвует в этой зоне
  - **Priority** при подборе замены:
    1. Road=true + участвует в зоне + другая сторона тоже имеет road (bidirectional)
    2. Road=true + участвует в зоне
    3. Участвует в зоне (любое)
    4. Road=true (любая зона)
    5. Любое свободное
  - Детерминированный порядок (List)
- **Called in**: LoadTemplate, RebuildGraph


## 2026-07-03 - User: 'реализуй' (fix stale MO[0] roads on connection reassignment)

### Fixed
- **RebuildConnectionRoads()**: added cleanup of MO[0] -> oldConn roads when a Connection MO is reassigned
  - Detects old connection name from existing zone roads
  - If oldConn != connName AND no other Connection MO still uses oldConn:
    - Removes all MO[0] -> oldConn (and reversed) roads from the zone
  - Prevents duplicate roads and stale road flags on connections


## 2026-07-03 - User: 'реализуй' (fix road flag leak on connection deletion)

### Changed
- **SyncConnectionRoadFlags()**: now accepts optional Dictionary<string, List<Road>>? preRoads parameter
  - When provided, uses preRoads snapshot instead of zone.Roads (avoids syncing auto-generated roads)
- **RebuildGraph()**: snapshots zone.Roads BEFORE RebuildConnectionRoads, passes to SyncConnectionRoadFlags
- **LoadTemplate()**: removed direct SyncConnectionPlacementArgs/RebuildConnectionRoads/SyncConnectionRoadFlags calls
  - All road logic now handled by RebuildGraph with proper pre-auto snapshot

### Fixed
- Deleting a road connection no longer causes the reassigned connection to get Road=true
- Auto-generated roads from RebuildConnectionRoads no longer set conn.Road flag


## 2026-07-03 - User: 'сейчас меньше дорог заражается, но эффект при загрузки шаблона соравно сохранился'

### Changed
- **RebuildGraph()**: added managed-roads cleanup BEFORE snapshot
  - Strips all MO -> Connection roads for Connection-placed MOs from zone.Roads
  - These managed roads are regenerated by RebuildConnectionRoads anyway
  - Prevents stale saved managed-roads from influencing road flags via snapshot

### Fixed
- Loading old template files (saved with the bug) no longer causes incorrect road flags
- Managed roads from previous saves are cleaned before snapshot, so SyncConnectionRoadFlags only sees general roads

## 2026-07-03 — User: "теперь флаг 'дорога' может выставиться на дороге если создавать связь"

### Fixed
- Stale managed roads (MO → Connection for old connection names) were not stripped before snapshot
  - After `SyncConnectionPlacementArgs` reassigned a MO from oldConn to newConn, `managedConns` only contained `newConn` (from `placementArgs`)
  - Stale `MO[*] → Connection[oldConn]` roads survived into `preRoads` snapshot
  - `SyncConnectionRoadFlags(preRoads)` then set `oldConn.Road = true` even though oldConn no longer has managed roads
- **Fix**: road scan in managed stripping now also collects stale connection names from existing `MO → Connection` roads, so both current and stale managed roads are stripped before snapshot
  - Road scan only runs for zones that already have Connection-placed MOs — prevents stripping original template roads (`AutoGenerateRoadsForConnection` roads, etc.) from unrelated zones

## 2026-07-03 — User: "реализуй" (auto-assignment, ComboBox, Center roads, road serialization, MO-index roads)

### Added
- **`GetAutoPlacementArgs(Zone)`** — auto-assigns PlacementArgs[0] for Connection-placed MOs with priority:
  1. Connections where the other zone has no castle Connection-placed MOs
  2. Connections with `Road == true`
  3. Even distribution (least-used connection first)
- **`HasCastleConnection(string zoneName)`** — checks if a zone has a City/AbandonedOutpost with `Placement == "Connection"`
- **`BuildConnectionArgsCombo(ComboBox, Zone, int moIndex)`** — fills a ComboBox with:
  - All connection names involving this zone
  - All other MO indices in this zone (excluding moIndex)
- **Part C: Center MO roads** — in `RebuildConnectionRoads`, for zones with a Center-placed MO, auto-adds `MO[0] → Connection[name]` for every connection involving this zone with `conn.Road == true`
- **Part D: Road serialization fix** — in `RebuildGraph`, after `SyncConnectionRoadFlags`, iterates all connections with `Road == true` and calls `AutoGenerateRoadsForConnection` to ensure zone.roads entries exist (fixes missing roads on save/load)
- **Part E: MO-index roads** — when `PlacementArgs[0]` is an integer (MO index), generates `MO[i] → MO[index]` + `MO[0] → MO[index]` roads instead of Connection roads

### Changed
- **`SyncConnectionPlacementArgs()`** — rewritten to use `GetAutoPlacementArgs` for the new priority system; also accepts MO-index as valid (no reassignment needed)
- **`RebuildConnectionRoads()`** — stale road removal now handles MO-index targets (`MainObject → MainObject`); `connName` → `connOrIndex` throughout
- **`placeCombo.SelectionChanged` (Section 4 & 5)** — auto-assigns `PlacementArgs[0]` via `GetAutoPlacementArgs` when switching Placement to "Connection"
- **`addAdditionalMoBtn.Click`** — reads from Connection ComboBox (`newMoConnArgsCombo`) when Placement="Connection"; falls back to `GetAutoPlacementArgs` if nothing selected; MO-index road generation
- **UI: PlacementArgs** — in Section 4, Section 5, and New MO panel: when Placement is "Connection", shows a Connection ComboBox (with connections + MO indices) instead of the free-text TextBox; TextBox shown for Uniform/other

### Fixed
- Roads for `conn.Road == true` connections are now properly generated on template load (not just from UI checkbox)
- Center-placed MOs now get roads to all road connections involving their zone
- Connection-placed MOs can target other MOs by index with proper MO → MO roads

### Fixed
- `BuildConnectionArgsCombo` in Section 5 now passes correct `i` (actual MO index) instead of hardcoded `0` — previously hardcoded `0` prevented additional MOs from targeting the primary MO (index 0); now only the MO's own index is excluded

## 2026-07-03 — User: "реализуй" (5 fixes: MO[0] roads, stale cleanup, numeric collision, usedCounts, HasCastleConnection)

### Fixed
- **Fix 1: MO[0] in RebuildConnectionRoads** — цикл расширен с `i=1` до `i=0`, так что City с `Placement="Connection"` и MO-index target теперь получает дороги `MO[0]→MO[idx]`. Очистка stale roads и old-target cleanup работают и для MO[0].
- **Fix 2: Stale road регенерация** — после `AutoGenerateRoadsForConnection` (Part D) добавлен cleanup (Part D2): удаляются `MO[0]→Connection[name]`, если ни один Connection MO в этой зоне не указывает на `name`. Предотвращает восстановление stale `MO[0]→oldConn` после смены таргета.
- **Fix 3: Numeric connection name collision** — в `RebuildConnectionRoads` и `SyncConnectionPlacementArgs` перед `int.TryParse` добавляется проверка `isConnName`, чтобы имя соединения вида `"1"` не интерпретировалось как MO-index.
- **Fix 4: usedCounts per-zone** — `GetAutoPlacementArgs` считает использование connection name только в рамках своей зоны, а не глобально по всем зонам.
- **Fix 5: HasCastleConnection расширен** — убрано ограничение `mo.Type is "City" or "AbandonedOutpost"`. Теперь любой MO с `Placement == "Connection"` считается «занятой зоной» для P1 приоритета.

### Changed
- **Managed road strip в RebuildGraph** — цикл сбора `managedConns` расширен на `i=0` для консистентности с `RebuildConnectionRoads` и `SyncConnectionPlacementArgs`.

## 2026-07-03 — User: "реализуй" (приоритет Road + MO[0]→target только для i=0)

### Changed
- **`GetAutoPlacementArgs` приоритеты**: новый порядок — P1: Road + зона без castle-Connection, P2: Road (любая), P3: Non-road + зона без castle-Connection, P4: остальные. Road-соединения всегда предпочтительнее non-road.
- **`RebuildConnectionRoads` генерация MO[0]→target**: `MO[0]→Connection[name]` / `MO[0]→MO[idx]` теперь генерируется только при `i == 0` (когда обрабатывается сам MO[0]). Для `i > 0` генерируется только `MO[i]→target`. Part C отвечает за `MO[0]→Connection[name]` ко всем road-соединениям.

## 2026-07-03 — User: "реализуй" (SyncConnectionPlacementArgs — дубликаты + excludeUsed)

### Changed
- **`SyncConnectionPlacementArgs` двухпроходный**: проход 1 — валидирует все MO и собирает `occupied` (уникальные таргеты). Проход 2 — для дубликатов/невалидных вызывает `GetAutoPlacementArgs`. Больше ни один Connection MO не получит duplicate target.
- **`GetAutoPlacementArgs` новый параметр `excludeUsed`**: исключает из выбора уже занятые connection names. Если все Road-соединения заняты, возвращается следующее по приоритету свободное.
- **Road variant upgrade**: если MO таргетирует non-road соединение, а между теми же зонами существует road-вариант, происходит автоматическое обновление на road-вариант (например, `Direct-Zone-1-Zone-5` → `Direct-Zone-1-Zone-5-3`).

## 2026-07-03 — User: "реализуй" (HasCastleConnection corrected + road upgrade order-independent)

### Changed
- **`HasCastleConnection` переписана**: теперь проверяет наличие **входящих** Connection MO из **других** зон, а не свои собственные Connection MO. Zone-1 больше не считается «имеющей castle-Connection» из-за своих же Connection MO — `Direct-Center-Zone-1` попадает в P1 приоритет для Center зоны.
- **Road upgrade**: поиск road-варианта теперь order-independent (проверяет оба направления пары зон). `Direct-Zone-1-Zone-2` (Zone-1→Zone-2) найдёт road-вариант `Direct-Zone-2-Zone-1-2` (Zone-2→Zone-1, Road=true).



