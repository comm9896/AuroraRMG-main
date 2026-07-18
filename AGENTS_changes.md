# Project Change Log

## 2026-07-17 — Fix: heroCountMin записывался как (uiMin − increment) → получался 0

### Root cause
- `TemplateGenerator.BuildGameRules` (TemplateGenerator.cs:416) писал `HeroCountMin = HeroSettings.HeroCountMin - HeroSettings.HeroCountIncrement`. При совпадении min и increment (напр. 12/12/12) в файл попадал `heroCountMin: 0`.
- Обратная пара в `ApplyTemplateToUi` (MainWindow.xaml.cs:2536) восстанавливала `uiMin = gr.HeroCountMin + gr.HeroCountIncrement`.
- Несогласованность: путь SettingsFile (MainWindow.xaml.cs:2114) хранит `SldHeroMin` 1:1 без смещения, а GameRules — со смещением.

### Fixed
- `TemplateGenerator.cs:416`: `HeroCountMin = SingleHeroMode ? 1 : HeroSettings.HeroCountMin` (без вычитания increment). Теперь значение слайдера пишется в файл как есть.
- `MainWindow.xaml.cs:2536`: `SldHeroMin.Value = gr.HeroCountMin ?? 1` (убран `+ increment`).
- Тест `BuildGameRules_MapsSettingsToGameRules` (UnitTest1.cs:954): ожидание изменено с 2 на 3 (HeroCountMin=3, Increment=1).
- H3T-импорт к hero-лимитам не относится (H3TParser не трогает hero-счётчики) — баг в формуле сериализации, независимо от источника шаблона.

## 2026-07-17 — Fix: краш окна «Добавить бонус» (и других диалогов) при открытии

### Root cause
- Все диалоги (BonusPickerWindow, SpellPickerWindow, ItemPickerWindow, MirrorSettingsWindow, NamePromptWindow, UpdateProgressWindow, ValueOverridePickerWindow, MainWindow) ссылались на иконку через `Icon="pack://application:,,,/favicon.ico"` и `<Image Source="pack://application:,,,/favicon.ico">`.
- Сборка — single-file publish (`PublishSingleFile=true`, `EnableCompressionInSingleFile=true`, `RuntimeIdentifier=win-x64`), и `favicon.ico` НЕ попадает в WPF-ресурсы сборки (в DLL только 2 manifest-ресурса, favicon отсутствует). `pack://application:,,,/favicon.ico` не разрешается → `XamlParseException` (IOException "Не удается найти ресурс favicon.ico") при `InitializeComponent` → окно крашится при открытии. Это и было падение по кнопке «Добавить бонусы».

### Fixed
- Удалены все `Icon="pack://application:,,,/favicon.ico"` из XAML окон (иконка процесса задаётся через `<ApplicationIcon>` в csproj — для таскбара достаточно).
- Удалены `Source="pack://application:,,,/favicon.ico"` у `<Image>` в MainWindow.xaml и UpdateProgressWindow.xaml (оставлен пустой Image-элемент без Source, без краша).
- Проверено репродьюсом: сборка окна больше не бросает XamlParseException по favicon.

### Notes
- `<Resource Include="favicon.ico" />` в csproj можно оставить (безвредно) или убрать — на разрешение `pack://` в single-file сборке это не влияет.

## 2026-07-17 — Fix: «Изменить базовые настройки» не сохранял размер карты

### Fixed
- `PatchMetadataAndRules` (MainWindow.xaml.cs): теперь явно записывает `target.SizeX = settings.MapSize` и `target.SizeZ = settings.MapSize`. Ранее размер карты хранился только на топ-уровне шаблона (`RmgTemplate.SizeX/SizeZ`) и не прокидывался из `BuildSettings`, поэтому после правки размера в режиме редактирования и сохранения в файле оставался старый размер.
- Сохранён legacy-флаг `GameRules.TournamentRules` (top-level bool): `BuildGameRules` его не выставлял, а `target.GameRules = gr` затирал импортированное значение. Добавлено `gr.TournamentRules = settings.TournamentRules.Enabled`.
- Авто-сохранение файла не добавлено (сохраняется дизайн "только метаданные, без save"): кнопка меняет шаблон в памяти, пользователь сохраняет явно.

## 2026-07-11 — Visual editor: mirror creation mode (vertical split, dialog-driven options)

### Added
- Localization keys `S.EC.Mirror`, `S.EC.MirrorTip`, `S.EC.MirrorOn`, `S.EC.MirrorOff`, `S.EC.MirrorSettings`, `S.EC.MirrorEnable`, `S.EC.MirrorProps`, `S.EC.MirrorConns` (RU + EN)
- New `MirrorSettingsWindow` dialog (salvaged from `NamePromptWindow` style) with a master «Включить зеркальное создание» checkbox and two sub-options: «Отзеркаливание свойств» and «Отзеркаливание соединений»
- Mirror mode: when ON, the canvas splits into left/right halves with a vertical gold dashed divider at `CanvasWidth/2`; all existing zones are duplicated mirrored immediately (each twin gets a fresh unique name — names are never mirrored)

### Changed
- `BtnMirror_Click` now opens `MirrorSettingsWindow` (no longer a direct toggle); on OK it applies the master enable + the two sub-options; button highlights when mode is on
- Reflection axis is now **vertical** (`x → CanvasWidth − x`)
- `AddZoneAt` — when mirror mode is on, creating a zone also creates a mirrored twin and records the bidirectional `_mirrorMap`
- `CanvasHost_MouseMove` — dragging a zone also moves its mirrored twin live (guarded by `_applyingMirror` to prevent recursion)
- New `MirrorZoneProperties(Zone)` + `MirrorMarkDirty(Zone)` — when «Отзеркаливание свойств» is on, every zone-property edit in `BuildInspector` (and `RebuildMainObjectEditor`) is copied to the twin via reflection (name excluded)
- New `MirrorConnection(Connection)` — when «Отзеркаливание соединений» is on, creating a connection also creates the mirrored connection between the twins (settings copied), except when the connection is itself between a zone and its mirror, or a duplicate already exists; hooked into `HandleConnectClick`
- `RenameZone` — keeps `_mirrorMap` keys consistent (names are re-keyed, not mirrored)
- `BtnDelete_Click` — clears both `_mirrorMap` entries for a deleted zone
- **Divider visibility fixed**: `RebuildGraph()` was calling `GraphCanvas.Children.Clear()`, which wiped the divider; now `RebuildGraph` detaches+nulls the divider before clearing and re-adds it at the end when `_mirrorMode` is on. `DrawMirrorDivider` draws a full-height translucent gold band + 2px vertical dashed gold line + «Зеркальное создание» label
- `BtnMirror` moved into the main toolbar `DockPanel` (was a floating button overlapping `BtnGridSnap`)
- New helpers: `EnableMirrorMode`, `DisableMirrorMode`, `DrawMirrorDivider`, `RemoveMirrorDivider`, `Mirror(Point)`, `GetZoneByName`, `CloneZone`, `MirrorMarkDirty`, `MirrorZoneProperties`, `MirrorConnection`

## 2026-07-11 — Connection inspector: restore keys, remove leftover tooltips, localize + style static note

### Changed
- Re-added removed localization keys `S.EC.GuardMatchGroup` and `S.EC.SimTurnSquad` (RU + EN) and switched the Advanced section fields back to `L("S.EC.SimTurnSquad")` / `L("S.EC.GuardMatchGroup")` (inspector) and `{DynamicResource S.EC.SimTurnSquad}` / `{DynamicResource S.EC.GuardMatchGroup}` (settings window) — removed hardcoded RU labels
- Added `S.EC.AdvancedNote` (RU "во всех шаблонах пустая если знаете что ставить ставьте =)" / EN "Empty in all templates; if you know what to set, set it =)") and used it for the static note in both windows
- Removed the leftover `ToolTip` on the Advanced `Expander` controls (both `ConnectionSettingsWindow.xaml` and `TemplateEditorWindow.xaml.cs`)
- Static note `TextBlock` is now bold (`FontWeight=Bold`) and font size increased by 2 (11 → 13) in both windows
- `UnitTest1.NewConnectionKeysExist` re-includes `S.EC.GuardMatchGroup`, `S.EC.SimTurnSquad`, `S.EC.AdvancedNote`

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

## 2026-07-03 — User: "добавь их отдельным окном ... измени название ... убери проверку на обновление ... напиши readme"

### Added
- **OrientationWindow** (`OrientationWindow.xaml` / `.xaml.cs`): новое окно для редактирования orientation (mode, zeroAngleZone, baseAngleMin/Max, randomAngleAmplitude/Step) и border (cornerRadius, obstaclesWidth, obstaclesNoise, waterWidth, waterNoise, waterType). Вызывается по кнопке «Макет и границы» на панели инструментов визуального редактора зон.
  - Значения по умолчанию: самая распространённая комбинация (mode=MinimalBoundingSquare, zeroAngleZone=Spawn-A, baseAngle{Min,Max}=45, randomAngleAmplitude=360, randomAngleStep=90, cornerRadius=0.0, obstaclesWidth=3, noise amp=1 freq=12, waterWidth=0, waterType=water grass).
  - Mode: выбор между MinimalBoundingSquare, BoundingCircle и «не задано».
  - WaterType: read-only (всегда "water grass").
  - Кнопка «Убрать border»: устанавливает Border = null.
- **EditorHelpWindow** (`EditorHelpWindow.xaml` / `.xaml.cs`): окно справки с описанием возможностей визуального редактора зон. Вызывается по кнопке «?» на панели инструментов редактора.

### Changed
- **AssemblyInfo.cs**: AssemblyTitle, Company, Product изменены с AuroraRMG на CommFork.
- **MainWindow.xaml.cs**: title, app title изменены на CommFork.
- **MainWindow.xaml**: Title, wordmark изменены на CommFork; Loaded-обработчик заменён на пустой; блок UpdateBanner удалён.
- **MainWindow.Update.cs**: содержимое заменено на пустой обработчик.
- **Strings.cs (локализация)**: все упоминания AuroraRMG заменены на CommFork (обновление, game assets disclaimer и т.д.).
- **AppSettings.cs, IconResolver.cs, GameCatalogService.cs**: пути в %LOCALAPPDATA% изменены с AuroraRMG на CommFork.
- **TemplateGenerator.cs**: строка в сгенерированном шаблоне изменена с "Olden Era Template Generator" на "CommFork Template Generator".
- **UpdateService.cs**: Repo, PreferredAssetName, UserAgent, temp paths, комментарии изменены на CommFork.
- **TemplateEditorWindow.xaml.cs**: добавлены обработчики BtnOrientation_Click и BtnHelp_Click. Добавлена локализация S.EC.OrientationApplied (RU/EN).

### Removed
- **Проверка обновлений**: `Window_Loaded` больше не вызывает `CheckForUpdatesAsync`. Баннер обновления удалён из MainWindow.xaml.

### Fixed
- **ZeroAngleZone**: убран IsEditable — строгий ComboBox только из списка зон.
- **Локализация кнопок**: добавлены ключи (S.Ed.015, S.Ed.016, S.EC.BtnViewPools, S.EC.BtnCreatePool, S.EC.BtnAutoRoad) — заменены хардкоженные русские строки «Менеджер связей», «Макет и границы», «📋 Просмотр содержимого пулов», «➕ Создать новый пул», «Авто-дорога» на DynamicResource / L() вызовы. ConnectionManagerWindow.Title и OrientationWindow.Title тоже переведены на DynamicResource.
- **Ключи локализации**: RU/EN синхронизированы (по 546 ключей).

## 2026-07-03 — User: "напиши файл ридми изменений ... измени версию приложения на 2.0 ... напиши ридми разницы между проектами"

### Added
- **`README_changes_2.5-2.8.md`** — описание изменений между коммитами 2.5→2.6→2.7→2.8: Connection Manager, JSON Preview, Placement Args, Copy/Paste зон, дороги, OrientationWindow, EditorHelpWindow, ребрендинг CommFork, удаление авто-обновления.
- **`README_fork.md`** — сравнение CommFork 2.0 с оригинальным AuroraRMG 1.6.0 (таблица различий, список улучшений и отсутствующих функций).

### Changed
- **AssemblyInfo.cs**: Version изменена с `1.4.1.0` на `2.0.0.0`. Код `Window_Loaded`, `CheckForUpdatesAsync`, `ShowUpdateBanner`, `BtnUpdateNow_Click`, `BtnUpdateNotes_Click`, `BtnUpdateDismiss_Click` заменён на пустой обработчик.

## 2026-07-11 09:10 — User: "добавь все поля связи на панель инспектора соединений"

### Added
- **`GuardRandomization` (double?)** to `Connection.cs` with `[JsonPropertyName("guardRandomization")]`
- **Connection inspector fields** in `TemplateEditorWindow.xaml.cs` (after Road checkbox): GuardZone (combo with zone names), GuardEscape (checkbox), SimTurnSquad (checkbox), GuardWeeklyIncrement (text), GuardMatchGroup (text), GatePlacement (combo), GuardRandomization (text)
- **Portal rules panel** in connection inspector — visible only when `connectionType == "Portal"`: inline editor for `PortalPlacementRulesFrom` / `PortalPlacementRulesTo` with Type (combo: "Crossroads"), Args (comma-separated text), TargetMin/Max (text), Weight (text), Add/Remove buttons
- **Connection type combo** now toggles portal panel visibility
- **`ConnectionSettingsWindow.xaml`** — added all same fields: CmbGuardZone, ChkGuardEscape, ChkSimTurnSquad, TxtGuardWeeklyInc, TxtGuardMatchGroup, CmbGatePlacement, TxtGuardRandomization, PortalRulesPanel (code-behind populated)
- **`ConnectionSettingsWindow.xaml.cs`** — full save/load for all new fields + portal rules inline editor (BuildRuleList, BuildPortalRulesPanel)
- **Localization strings** (RU+EN): `S.EC.GuardMatchGroup`, `S.EC.GuardRandomizationConn`, `S.EC.PortalRulesFrom`, `S.EC.PortalRulesTo`, `S.EC.PlacementRuleType`, `S.EC.PlacementRuleArgs`, `S.EC.PlacementRuleTargetMin`, `S.EC.PlacementRuleTargetMax`, `S.EC.PlacementRuleWeight`, `S.EC.AddRule`, `S.EC.RemoveRule`
- **Test project** `tests/OldenEraTemplateEditor.Tests/` with xUnit tests:
  - Connection serialization round-trip (all new properties)
  - Minimal connection (default nulls)
  - JSON property name verification (camelCase match)
  - Localization key parity (RU/EN same keys + new keys exist)
  - ContentPlacementRule serialization round-trip + game format deserialization

### Changed
- **`build.bat`** — added `[4/4]` test step + `pause` at end
- **`build.ps1`** — added `[4/4]` test step + `Read-Host` at end

### Fixed
- **ConnectionSettingsWindow** — `_template.Zones` → `_template.Variants?[0]?.Zones` (RmgTemplate has Variants list, not direct Zones)
- **TemplateEditorWindow** — `RebuildZoneEditor()` → `BuildInspector()` (correct method name)

## 2026-07-11 09:27 — Build.bat CRLF fix + simplified structure

### Fixed
- **`build.bat`** — rewritten with clean `GOTO`-based error handling (no nested `if` blocks)
  - CRLF (`0D 0A`) line endings enforced (cmd.exe breaks on LF-only)
  - UTF-8 BOM removed (`@echo off` was showing `?@echo off`)
  - All 4 steps now execute correctly in sequence: [1/4] Restore → [2/4] Build → [3/4] Publish → [4/4] Test → pause on exit
  - `--verbosity normal` + `echo ^>` command echo on all steps
  - `pause` before every `exit` (in error paths) + `pause >nul` at end

## 2026-07-11 — Connection defaults from game template analysis + UI restructure

### Analysis
- Scanned 68 game `map_templates` (`*.rmg.json`) = 1746 connections at `E:\SteamLibrary\...\StreamingAssets\map_templates`
- Confirmed real field names; `guardRandomization` on connections: 0.1/0.15/0.2/0.25 (most common 0.15); on **zones**: **0.05** (1014×)
- `guardWeeklyIncrement`: 0.2 (920×), 0.1 (347×), 0.15 (195×), 0.05 (10×), 0.25 (2×)
- `gatePlacement`: "Center" (224×) + "NearZone" (4×, only `Sand Clover.rmg.json`)
- `gatePlacementArgs` (List<string>) exists only in Sand Clover — zone connector names, only when `gatePlacement="NearZone"`
- `guardEscape`: 100% false; `simTurnSquad`: 100% true

### Added
- **`Connection.cs`** — `GatePlacementArgs` (`List<string>?`, JSON `gatePlacementArgs`)
- **`KnownValues.cs`** — `GatePlacements` now `["Center", "NearZone"]`
- **`Strings.cs`** — `S.EC.GatePlacementArgs` ("Зоны размещения ворот" / "Gate placement zones"), `S.EC.Advanced` ("Дополнительно" / "Advanced")
- **Default values** applied when a user creates a new connection (TemplateEditorWindow connect handler):
  `GuardValue=25000`, `GuardWeeklyIncrement=0.15`, `GuardRandomization=0.05`, `Length=1`, `GuardEscape=false`, `SimTurnSquad=true`, `GatePlacement="Center"`
- **`ConnectionSettingsWindow.xaml`** — `LstGatePlacementArgs` (multi-select ListBox, visible only for `NearZone`) + `Expander` "Дополнительно" wrapping `GuardZone`/`GuardMatchGroup` with tooltip "во всех шаблонах пустая если знаете что ставить ставьте =)"
- **`TemplateEditorWindow.xaml.cs`** — connection inspector: moved `GuardZone` + `GuardMatchGroup` into an "Дополнительно" `Expander` (tooltip as above); added `GatePlacementArgs` multi-select ListBox (filtered to zones excluding the connection's from/to), shown only when `gatePlacement == "NearZone"`

### Changed
- **`ConnectionSettingsWindow.xaml.cs`** — load/save `GatePlacementArgs`; gate placement combo toggles `GatePlacementArgsPanel` visibility; guardZone/guardMatchGroup now populate inside the Expander
- **`ConnectionSerializationTests`** — round-trip now covers `gatePlacementArgs`; `JsonPropertyNames_MatchGameFormat` asserts `gatePlacementArgs`; `NewConnectionKeysExist` includes new keys

### Notes
- Defaults are NOT in the `Connection` constructor (kept null-by-default for clean serialization of loaded templates); only new UI-created connections get them
- `guardZone` / `guardMatchGroup` remain null (empty) by default per request

## 2026-07-11 (позже) — Defaults tweak + SimTurnSquad moved to Advanced + tooltips

### Changed
- **`TemplateEditorWindow.xaml.cs`** — `GuardValue` default for new connections: **25000 → 3000**
- **`TemplateEditorWindow.xaml.cs`** — `SimTurnSquad` ("одновременный отряд") removed from main panel, moved into "Дополнительно" `Expander` (hidden by default)
- **`ConnectionSettingsWindow.xaml`** — `ChkSimTurnSquad` moved into "Дополнительно" `Expander`
- **`TemplateEditorWindow.xaml.cs`** — `AddComboField` now accepts optional `tooltip` param; `GuardZone`/`GuardMatchGroup`/`SimTurnSquad` inside Advanced get hover tooltips
- **`ConnectionSettingsWindow.xaml`** — `CmbGuardZone`/`TxtGuardMatchGroup`/`ChkSimTurnSquad` inside Advanced get hover tooltips
- **`Strings.cs`** — added `S.EC.GuardZoneTip`, `S.EC.SimTurnSquadTip`, `S.EC.GuardMatchGroupTip` (RU+EN)

## 2026-07-11 (ещё позже) — Remove tooltips, add static note, drop keys

### Removed
- All tooltip code: `AddComboField` tooltip param reverted; `ToolTip` attributes removed from `ConnectionSettingsWindow.xaml` (CmbGuardZone/TxtGuardMatchGroup/ChkSimTurnSquad); tooltip args removed from inspector
- Localization keys `S.EC.GuardMatchGroup`, `S.EC.SimTurnSquad` (RU+EN) and the three `*Tip` keys — removed
- `L("S.EC.GuardMatchGroup")`/`L("S.EC.SimTurnSquad")` in inspector replaced with hardcoded RU labels

### Changed
- `S.EC.GuardZone` value: RU "Зона охраны" → "появление соединение около зоны"; EN "Guard zone" → "Connection appearance near zone"
- `ConnectionSettingsWindow.xaml` — GuardZone label hardcoded to "появление соединение около зоны"
- Both Advanced expanders (inspector + settings window) now end with a read-only note: "во всех шаблонах пустая если знаете что ставить ставьте =)"
- `UnitTest1.cs` — `NewConnectionKeysExist` no longer asserts removed keys





## 2026-07-11 — User: доработка зеркального режима (хаб, близнецы-соединения, клэмп оси, авто-логика)

### Added
- `MirrorSettingsWindow.xaml/.cs`: кнопка "Создать хаб" -> создаёт неподвижную центральную зону "hub" (не зеркалится)
- `TemplateEditorWindow.xaml.cs`: `CreateHubZone`/`CreateHubZoneExternally`, множество `_lockedZones`
- `_connectionMirrorMap` + `MirrorConnectionMarkDirty`: правки настроек соединения отзеркаливаются в близнец
- `ApplyAutoLogicToTwin`: авто-переименование зоны при спавне наследуется близнецом
- `Strings.cs`: `S.EC.MirrorHub`, `S.EC.HubCreated` (RU+EN)

### Changed
- `MirrorZoneProperties` теперь делает глубокую копию (близнец получает независимые MainObjects) — исправлено отсутствие отзеркаливания добавлений
- `EnableMirrorMode` пропускает уже сопоставленные и заблокированные зоны (исправлено дублирование при повторном включении)
- `DisableMirrorMode` сохраняет `_mirrorMap`/`_connectionMirrorMap`/`_lockedZones` (близнецы не удаляются)
- `BtnDelete_Click`: удаление зоны удаляет и близнеца; очистка карты зеркал соединений; удаление соединения чистит карту
- Перетаскивание: зона зажимается на своей стороне оси; хаб/заблокированные зоны неподвижны (нельзя пересечь ось)
- `RebuildMainObjectsList` + `RebuildAdditionalMainObjectsList`: все правки MainObject вызывают `MirrorMarkDirty(z)`
- Сеттеры инспектора соединения направлены в `MirrorConnectionMarkDirty(c)`


## 2026-07-11 — User: исправление багов зеркального режима (хаб, road, близнецы объектов)

### Fixed
- `MirrorConnectionMarkDirty`: больше НЕ копирует `From`/`To` (концы связи) — раньше близнец-соединение перезаписывал свои концы концами источника, из-за чего связь "переносилась" на ту, где подняли флаг road
- Переключение флага `Road`: авто-генерация дорог внутри зон теперь выполняется и для зон близнеца-соединения (а не только для одной из зон)
- `MirrorSettingsWindow`: установлен `Owner = this`, поэтому кнопка "Создать хаб" достигает редактора и создаёт зону (раньше `Owner` был null и хаб не создавался)
- Добавление MainObject в инспекторе зоны (`addAdditionalMoBtn`) теперь вызывает `MirrorMarkDirty(z)` вместо `MarkDirty()` — дополнительные объекты отзеркаливаются в близнец-зону


## 2026-07-11 — User: ещё баги зеркального режима (объекты, дороги, хаб-связи)

### Changed
- `_mirrorProperties` и `_mirrorConnections` теперь включены по умолчанию — раньше при включении зеркального режима обе под-опции были выключены, из-за чего объекты (MainObject) не отзеркаливались (нужно было вручную ставить галочку "Отзеркаливание свойств")
- `MirrorConnection` переписан: теперь зеркалирует связь даже если один из концов — хаб (не имеет близнеца). Связь близнец→хаб создаёт зеркальную связь исходная-зона→хаб (и наоборот)
- `RemapMirrorReferences`: у близнеца переназначаются кросс-ссылки — `PlacementArgs` MainObject (placement "Connection"/"NearZone") и дороги зоны (`Roads`) теперь указывают на зеркальные связи/зоны близнеца, а не на исходные (исправлено "авто-дороги использовали связь близнеца вместо реальной")


## 2026-07-11 — User: объекты вкладки "Объекты" не зеркалировались близнецу

### Fixed
- `RebuildContentObjectsList`: обработчики добавления и удаления контент-объекта (вкладка "Объекты") вызывали `MarkDirty()` вместо `MirrorMarkDirty(z)`, из-за чего `MandatoryContent` не отзеркаливался в близнец-зону. Заменено на `MirrorMarkDirty(z)` (строки ~2424 и ~2471)
- Примечание: зеркалирование объектов вкладки "Объекты" подчиняется галочке "Отзеркаливание свойств" (включена по умолчанию)

## 2026-07-11 18:51 — User: "обнови справку" (move help text, reorganize toolbar/canvas)

### Changed
- Moved the selection-help text S.Ed.011 ("Выберите зону или связь...") off the inspector hint and onto a top-right TextBlock overlay inside GraphCanvas (it overflowed where it was).
- Reordered toolbar buttons: Copy Zone + Paste Zone now follow Zone; Connection Manager follows Connect; JSON preview follows PNG.
- Removed the Mirror button from the toolbar and placed it as a bottom-right button inside the canvas (BtnMirror), alongside the existing top-left canvas hint.
- TemplateEditorWindow.xaml.cs: when nothing is selected, TxtInspectorHint is now cleared (text comes from canvas instead).
- Build green; 8/8 tests pass.

## 2026-07-11 19:24 — User: авто-выставление аргумента "владелец" (и Spawn) в зеркальном режиме должно соответствовать не-зеркальной логике

### Changed
- Added public static bool ResolvePlayerConflicts(Zone, HashSet<string> used) — pure (no UI) helper that re-points a zone's Owner/Spawn to free players, mirroring the inspector combo / PasteCopiedZone conflict-resolution: clears Owner for non-City and Spawn for non-Spawn, reassigns an already-taken player to the first free KnownValues.SpawnPlayers, applies side effects (RemoveGuardIfHasOwner=true, nulled guards, Faction→Match), updates used in place, returns whether anything changed.
- ApplyAutoLogicToTwin now calls ResolvePlayerConflicts(twin, GetUsedPlayers(twin.Name)) (excludes the twin, includes the source + other zones) so a mirrored twin no longer duplicates the source's Owner/Spawn; then runs AutoRenameZoneForSpawn per MainObject (zone rename preserved via RenameZone's mirror-map update).
- Covers both Owner and Spawn per user decision.
- Added 5 unit tests (PlayerConflictResolutionTests) for the helper; total 8 -> 13 pass.
- Build green (publish step OK); CRLF line endings preserved.

## 2026-07-11 19:47 — User: исправить ошибку в test11072026.rmg.json (дороги hub-зоны)

### Analyzed
- Compared generated 	est11072026.rmg.json against all ~80 templates in the game's StreamingAssets/map_templates folder.
- Confirmed 
ame is valid UTF-8 (console garble was PowerShell ANSI misdecode, not mojibake); owners Player1..Player4 distinct (validates earlier mirror fix); no dangling connection refs; Direct/zone_layout_* valid; empty contentPools + single variant match other project exports.

### Fixed
- AddRoadToZone castle-less branch (TemplateEditorWindow.xaml.cs) generated an invalid hub: a self-loop road (Direct-Zone-2-hub -> Direct-Zone-2-hub) plus 3 roads all anchored to the same connection (anchor was whatever connection got added first). Root cause: first connection got a self-loop, which then became the star anchor.
- Extracted public static Road? BuildCastleLessRoad(connectionName, incidentConnections) — builds a deterministic star among the zone's incident connections (anchor = first by ordinal name; one road per other incident connection; never a self-loop). A single incident connection keeps the legacy self-loop; zero incident connections → no road.
- AddRoadToZone now computes the zone's incident connections from Connections and delegates to the helper.
- Added 5 unit tests (CastleLessRoadTests): 2-incident -> 1 road no self-loop; anchor returns null; 4-incident -> spokes from anchor, no self-loop; single -> self-loop; none -> null. Total 13 -> 18 pass.
- Build green (publish step OK after freeing a locked running exe); CRLF line endings preserved.

## 2026-07-11 20:13 � User: "��� ����������� road � ���������� ������ ���� road ������������ � �� ������ �����, ��������� �� ��� �� ����"

### Fixed
- AddRoadToZone castle-less (hub) branch now builds the star only from connections with Road==true (new public static RoadIncidentConnections(zone, connections)). Previously it used ALL incident connections, so toggling one hub connection created a phantom anchor road that SyncConnectionRoadFlags then promoted to Road on reload � the flag bled to sibling connections of the same zone (most visible on the hub between mirror halves).
- Road checkbox OFF branch now fully reversible: deletes roads referencing the connection (both endpoint zones + mirror twin via _connectionMirrorMap) and rebuilds the castle-less star from surviving Road==true connections (new helpers RemoveConnectionRoads + RebuildCastleLessStar).
- Added 4 unit tests (RoadIncidentConnections: only road-flagged siblings participate / two road-flagged / none; one road-flagged hub -> self-loop, no sibling promotion). Total 18 -> 22 pass.
- build.bat green (22/22 tests, publish OK); CRLF line endings preserved.

## 2026-07-12 — User: "убрать сериализацию orientation.zeroAngleZone и border если не нажата кнопка «макет и границы»; переименовать в «ориентация и граница»; добавить Spawn как отдельный выбор над «владелец»; убрать автоматизацию выставления owner; уникальность spawn (9 значений) живо и при сохранении"

### Changed — serialization / orientation window
- Removed the seeded `Orientation = {Mode="MinimalBoundingSquare"}` and `Border = {...}` defaults from `NewEmptyTemplate()` so a brand-new template no longer serializes `orientation`/`border` unless the user actually opens the window and presses Apply.
- `OrientationWindow.BtnApply_Click` now writes `ZeroAngleZone` ONLY from `CmbZeroAngleZone.SelectedItem` (was `?? Text.Trim()`, the source of bogus free-text like "Spawn-A"); `Border` is created only when the new `ChkUseBorder` checkbox is checked, otherwise `_variant.Border = null`; `Mode` index 2 keeps `Orientation = null`. Added `CheckBox ChkUseBorder` to OrientationWindow.xaml; `LoadCurrentValues` initializes it from whether a Border already exists.
- Localization `S.Ed.016`: "Макет и границы" -> "Ориентация и граница" (RU) / "Layout and borders" -> "Orientation and border" (EN). `S.EC.OrientationApplied` messages updated to "ориентации и границы" / "Orientation and border".
- Added `S.EC.MoSpawn` = "Спавн" / "Spawn".

### Changed — Spawn selector + owner sync (first main object)
- `RebuildMainObjectEditor` now shows a **Spawn** selector ABOVE the Owner row, but only for the FIRST main object of a `City` zone. Selecting Spawn keeps `Owner` in sync (`Owner = Spawn`); editing Owner keeps `Spawn` in sync for the first City MO. Both live-refresh the open combo boxes.
- Removed the auto-owner automation: deleted `AutoRenameZoneForSpawn` and its calls (type-combo handler, `ApplyAutoLogicToTwin`); removed the interactive duplicate-owner `MessageBox` conflict blocks in both owner handlers (primary + `RebuildAdditionalMainObjectsList`); removed the Owner branch of `ResolvePlayerConflicts` (kept the Spawn dedup branch) and the Owner dedup block in `PasteCopiedZone` (kept Spawn dedup). Re-added the `UniqueZoneName(string)` overload that a surviving caller needs.

### Added — spawn uniqueness
- New public static `EnsureUniqueSpawns(RmgTemplate)` / `EnsureUniqueSpawns(IEnumerable<Zone>)`: enforces unique non-empty `spawn` across all zones from the canonical 9-value set (empty + Player1..Player8); duplicates are reassigned to the first free Player, the 9th collides to empty; the first main object's `Owner` is synced to its (possibly reassigned) Spawn. Called live from the spawn/owner handlers and once in `BtnSave_Click` before serialization.
- Replaced `PlayerConflictResolutionTests` (owner auto-assignment removed) with `SpawnUniquenessTests` (5 tests: distinct players across zones; 9 zones -> Player1..8 + empty; empty/unset untouched; owner sync on first MO; duplicate on non-first MO reassigned). Total 22 pass.
- build.bat green (22/22 tests, publish OK); CRLF line endings preserved.

## 2026-07-12 — User: "убрать из функции отзеркаливания свойств зон отзеркаливание spawn т.к. это уникальный аргумент"

### Fixed
- `MirrorZoneProperties` (TemplateEditorWindow.xaml.cs) no longer mirrors `Spawn`: it captures the twin's own `Spawn` values before the reflection copy and restores them by index after `ApplyAutoLogicToTwin` + `RemapMirrorReferences` (so the twin keeps its unique spawn, not the source's). The first `City` main object's `Owner` is re-synced to the restored `Spawn` to preserve the spawn↔owner invariant.
- `CloneZone` (initial mirror creation) now clears `Spawn` (and the first `City` main object's `Owner`) on the new twin so a freshly mirrored zone does not duplicate the source's unique `Spawn`; it gets a unique one via `EnsureUniqueSpawns` on save.
- build.bat green (22/22 tests, publish OK after stopping a locked running exe); CRLF line endings preserved.

## 2026-07-12 — User: "если выставляется owner отдельно от spawn не применять логику переназначения; если после этого выставляется spawn — логика должна сработать; не использовать логику переназначения owner для mainobject, кроме первого"

### Changed — односторонняя синхронизация спавн → владелец
- Owner handler (первый MO, `RebuildMainObjectEditor`): убрана обратная синхронизация `mo.Spawn = mo.Owner` и вызов `EnsureUniqueSpawns(Zones)`. Владелец, выставленный отдельно от спавна, больше не переназначает спавн/владельца.
- Owner handler в `RebuildAdditionalMainObjectsList`: убран вызов `EnsureUniqueSpawns(Zones)` (последующие MO не должны задействовать логику переназначения владельца).
- Spawn handler (первый MO): поменян порядок — сначала `EnsureUniqueSpawns(Zones)`, затем синхронизация `mo.Owner = mo.Spawn` по **финальному** (после дедупликации) спавну, чтобы владелец совпадал с итоговым значением. Логика срабатывает именно при выставлении спавна.
- `EnsureUniqueSpawns`: синхронизация владельца защищена условием `isFirst && finalSpawn != null && string.IsNullOrEmpty(mo.Owner)` — явно выставленный (непустой) владелец больше не затирается при сохранении, пустой владелец по-прежнему подтягивается к спавну.
- build.bat green (22/22 tests, publish OK); CRLF line endings preserved.

## 2026-07-12 11:09 — User: "убери синхронизацию owner в зеркальном режиме, добавь флаг «Одинаковые владелец» вверху вкладки «основное», добавь синхронизацию spawn -> zone_layout_spawn"

### Changed
- MirrorZoneProperties (зеркальный режим): убрана синхронизация owner — у двойника больше не принудительно выставляется владелец первого City равным отзеркаленному spawn. Spawn по-прежнему не отзеркаливается (уникальный аргумент).
- Zone: добавлен UI-флаг [JsonIgnore] public bool SyncOwners (не сериализуется).
- BuildInspector: в самом верху вкладки «Основное» добавлен чекбокс «Одинаковые владелец» (S.EC.SameOwner). При включении (и при каждом изменении owner/spawn первого main object) все main object зоны получают owner первого main object (SyncOwnersInZone).
- Добавлена синхронизация spawn -> layout: при установке любого аргумента Spawn (непустой, кроме « ») зоне выставляется Layout = «zone_layout_spawn»; при очистке spawn Layout возвращается в null только если он равен «zone_layout_spawn» (SyncZoneLayoutForSpawn). Применяется в обоих обработчиках spawn (первый City и Spawn-тип) и в обработчике owner первого main object.
- Strings.cs: добавлены ключи S.EC.SameOwner («Одинаковые владелец» / «Same owner»).

### Verified
- dotnet build (0 ошибок), dotnet test 22/22, build.bat -> release/OldenEraTemplateGenerator.exe (залоченный exe остановлен перед публикацией).
## 2026-07-12 11:09 — User: "при включении зеркального создания канвас полностью очищался вместе с содержащейся информацией"

### Changed
- BtnMirror_Click: при включении зеркального режима канвас теперь полностью очищается (старое поведение клонировало все зоны в зеркальные двойники, из-за чего старая информация «вылазила» позже).
- Добавлен метод ClearCanvasForMirror(): удаляет разделитель, очищает Zones, _positions, _mirrorMap, _connectionMirrorMap, _lockedZones, Variant.Connections и GraphCanvas.Children.
- Удалён метод EnableMirrorMode (массовое клонирование существующих зон при входе в режим). Зеркальное создание НОВЫХ зон по-прежнему работает (CloneZone используется в AddZone/перемещении).

### Verified
- dotnet build (0 ошибок), dotnet test 22/22, build.bat -> release/OldenEraTemplateGenerator.exe.
## 2026-07-12 — User: "реализуй (исправить загрузку GameData.json в single-file exe)"

### Fixed
- GameData.json теперь встраивается как EmbeddedResource (csproj: <EmbeddedResource Include=\"GameData.json\" /> вместо Content+CopyToOutputDirectory). В single-file публикации Content-файлы не извлекаются на диск, из-за чего пулы не грузились («⚠ Файл не найден…»).
- GamePoolDataLoader.Load(): добавлен fallback на встроенный ресурс, если файл на диске не найден. Поиск на диске остаётся первым (дропнутый GameData.json рядом с exe по-прежнему имеет приоритет). _dataPath при загрузке из ресурса = «<embedded: …>».
- Добавлено свойство [JsonIgnore] public Dictionary<string, string>? MetaData в Zone (используется H3TParser.cs, который был добавлен как неотслеживаемый файл и ломал сборку — ссылался на отсутствующий Zone.MetaData). JsonIgnore — чтобы не засорять сериализуемый шаблон.

### Tests
- Добавлен GameDataEmbeddedResourceTests.Load_ReadsGameDataFromEmbeddedResource (проверяет загрузку из встроенного ресурса, т.к. на диске в тестовом выводе файла нет).

### Verified
- dotnet build (0 ошибок), dotnet test 23/23, build.bat -> release/OldenEraTemplateGenerator.exe. Встроенный ресурс подтверждён в бандле (PE-ридер: Olden_Era___Template_Editor.GameData.json присутствует).
## 2026-07-12 — User: "изменить визуальное отображение mainobject внутри zone (показать spawn), спавн только для типа Spawn, HoldCityWinCon чекбокс перекрывает локализацию — подвинуть"

### Changed
- DrawNode (визуализация зоны на холсте): добавлено отображение mainobject типа Spawn внутри узла — строка «⚑{count}» зелёным (SpawnBorder), рядом с «🏰{castles}».
- RebuildMainObjectEditor: убран селектор «Спавн» над полем Owner для первого City (firstSpawnCombo/firstSpawnPanel). Теперь спавн-поле («Спавн игрока», playerCombo/spawnPanel) показывается ТОЛЬКО для mainobject типа Spawn. Удалены все ссылки на firstSpawnCombo; isFirstMainObject оставлен (используется в SyncOwners).
- RebuildMainObjectEditor: чекбокс «Условие победы (удержание)» (MoHoldCity / HoldCityWinCon) перенесён в конец секции mainobject (после placement-полей), чтобы не перекрывать блок типа/владельца/спавна.

### Verified
- dotnet build (0 ошибок), dotnet test 23/23, build.bat -> release/OldenEraTemplateGenerator.exe.
## 2026-07-12 — User: "Hold-city чекбокс в самый верх; spawn добавляет +1 к отображению city; локализация SameOwner -> «одинаковые владельцы для всех создаваемых city в этой зоне»"

### Changed
- RebuildMainObjectEditor: чекбокс «Условие победы (удержание)» (HoldCityWinCon) перенесён в САМЫЙ ВЕРХ секции mainobject (сразу после panel.Children.Clear()).
- DrawNode: отображение spawn-объекта теперь прибавляется к текущему счётчику городов — показывается «🏰{castles + spawns}» (отдельная строка ⚑ убрана).
- Strings.cs: S.EC.SameOwner (RU) = «Одинаковые владельцы для всех создаваемых city в этой зоне»; (EN) = «Same owners for all created cities in this zone».

### Verified
- dotnet build (0 ошибок), dotnet test 23/23, build.bat -> release/OldenEraTemplateGenerator.exe.
## 2026-07-13 — User: "изменить систему так что бы автоматическое размещение зон учитывало связи между зонами лучше (force-directed для Default/HubAndSpoke/Chain/SharedWeb)"

### Changed
- TemplatePreviewPngWriter.LayoutZones: диспетчеризация для Default/HubAndSpoke/Chain/SharedWeb теперь вызывает новый LayoutZonesForceDirected вместо LayoutZonesRing (чистый круг, игнорировавший связи). Random/Balanced/Lanes/MultiHub НЕ изменены.
- Добавлен LayoutZonesForceDirected: детерминированный (без RNG) force-directed (Fruchterman-Reingold) — посев на окружности, притяжение только вдоль связей, отталкивание между всеми парами, затем CorrectOverlapsAndFit.
- Добавлен общий CorrectOverlapsAndFit (Pass A: мин. центр-центр дистанция; Pass B: очистка от ребёр; финальный fit/центровка/масштаб под холст). Используется новым методом (существующий Random/Balanced-путь оставлен нетронутым ради безопасности).
- Proximity/Portal связи исключаются из графа (как и в остальных layout-методах).
- LayoutZonesRing сохранён для Lanes-fallback и MultiHub.

### Impact
- Загрузка шаблонов («загрузка шаблонов») тоже затрагивается: LoadTemplate принудительно ставит _topology = Default и вызывает ComputePositions, поэтому загруженные шаблоны теперь раскладываются force-directed (раньше — чистый круг). Координаты зон не сериализуются, так что перекладка при загрузке происходила всегда; поведение стало лучше, регрессий нет.

### Tests
- Добавлен ForceDirectedLayoutTests: ForceDirected_NoOverlappingZones (Theory для Default/HubAndSpoke/Chain/SharedWeb — круги не пересекаются), ForceDirected_ConnectedZonesAreCloserThanUnconnected, ForceDirected_IsDeterministic.

### Verified
- dotnet test 29/29 (было 23), build.bat -> release/OldenEraTemplateGenerator.exe (0 ошибок).
## 2026-07-13 — User: "добавить в сериализатор указание координат зон (блок AuroraRMG) чтобы при импорте зоны выставлялись по сохранённым координатам"

### Added
- Модель `AuroraRmgCoords` / `ZoneCoord` (OldenEraTemplateEditor.Models): `AuroraRmgCoords.Zones` = список `{ name, x, y }`.
- Свойство `RmgTemplate.AuroraRmg` ([JsonPropertyName("AuroraRMG")]), объявлено ПЕРЕД `Name`, поэтому сериализуется первым (сразу после `{`, перед `"name"`).
- `TemplateEditorWindow.ApplyAuroraRmgPositions(RmgTemplate, Dictionary<string,Point>)` (public static): перезаписывает `_positions` зон, присутствующих в блоке; зоны без координат в блоке сохраняют авто-раскладку.

### Changed
- `BtnSave_Click`: перед `File.WriteAllText(... Serialize(_template))` заполняет `_template.AuroraRmg` из текущих `_positions` (только для зон, реально присутствующих в шаблоне) — сохраняются и ручные перетаскивания.
- `LoadTemplate`: после `ComputePositions()` вызывает `ApplyAuroraRmgPositions(loaded, _positions)`, так что повторный импорт восстанавливает точную раскладку по сохранённым координатам (x/y — логические координаты холста, тот же формат, что у авто-раскладки).

### Format
- JSON: `{ "AuroraRMG": { "zones": [ { "name": "<ZoneName>", "x": 0, "y": 0 }, … ] }, "name": "…", … }`. Якоря "start"/"stop" из исходного описания заменены единым валидным объектом-обёрткой (по согласованию с пользователем — дубликат ключа "AuroraRMG" невалиден в JSON).

### Tests
- AuroraRmgCoordTests: Serialize_WritesAuroraRmgBlockBeforeName (блок раньше "name" + round-trip координат), ApplyAuroraRmgPositions_OverridesMatchedZones_KeepsOthers, ApplyAuroraRmgPositions_NullBlock_NoOp.

### Verified
- dotnet test 32/32, build.bat -> release/OldenEraTemplateGenerator.exe (0 ошибок).
## 2026-07-13 — User: "декодировать размер импортируемого h3t по таблице кодов и подбирать ближайший размер при сериализации; переименовать ключи координат zones/name во избежание конфликта с загрузкой шаблона"

### Added
- `H3TParser.DecodeH3TMapSize(int code)` (public static): таблица HotA кодов размера → реальный размер поверхности. Коды 1/2→36, 4/8→72, 9/18→108, 16/32→144, 25/50→180, 36/72→216, 49/99→252. Флаг подземелья игнорируется (редактор моделирует только поверхность). Неизвестный код → 160.
- `KnownValues.NearestMapSize(int size)`: ближайший из `AllMapSizes` (офиц. 64..240 + эксперим. 256..512); при точном равенстве расстояний — округление вверх.

### Changed
- `H3TParser.Parse`: вместо прямого использования закодированного поля `FIELD_MAX_SIZE` (field 17 карты) как SizeX, теперь декодирует код через `DecodeH3TMapSize` и ставит `SizeX = SizeZ = decoded` (квадрат, формат "*x*").
- `BtnSave_Click`: перед сериализацией `SizeX`/`SizeZ` округляются до ближайшего поддерживаемого размера через `KnownValues.NearestMapSize` (напр. 36→64, 72→80, 108→112, 144→144, 180→176, 216→208, 252→256). Безопасно для шаблонов, размер которых уже в допустимом наборе (no-op).
- `AuroraRmgCoords.Zones` JSON-ключ переименован `"zones"` → `"coords"`; `ZoneCoord.Name` JSON-ключ переименован `"name"` → `"id"`. Причина: ключи `"zones"`/`"name"` конфликтуют со схемой игры (`variants[].zones[]`, имя зоны/шаблона) и ломали загрузку шаблона. C#-имена свойств (`Zones`, `Name`) и вся логика сопоставления не изменились. Формат блока: `{ "AuroraRMG": { "coords": [ { "id": "<ZoneName>", "x": 0, "y": 0 }, … ] }, "name": "…", … }`.

### Tests
- H3TAndMapSizeTests: DecodeH3TMapSize_DecodesToSurfaceSize (15 cases, включая fallback для неизвестных кодов), NearestMapSize_SnapsToSupportedSize (9 cases, включая tie 72→80 и неположительный → 64).
- AuroraRmgCoordTests.Serialise_WritesAuroraRmgBlockBeforeName дополнен проверкой наличия ключей `"coords"`/`"id"`.

### Verified
- dotnet test 57/57, build.bat -> release/OldenEraTemplateGenerator.exe (0 ошибок).

### Note
- Блок `AuroraRMG` — editor-only метаданные. .rmg.json также читается движком игры; если игровой ридер не игнорирует неизвестные ключи, может понадобиться отдельный "export for game" путь без этого блока (не реализовано — требует подтверждения, что файл должен читаться и игрой).

## 2026-07-14 — User: "задай mandatory тег mandatory, content_limits тег content_limits чтобы они отображались в категориях 'содержимое пулов' (просмотр, без редактирования); перемести эти два блока в конец файла при сохранении"

### Added
- `GamePool.Tag` (string?, Models/GamePoolData.cs): тег для синтетических/шаблонных пулов (реальные GameData-пулы оставляют null).
- `PoolItemInfo.Detail` (string?): свободное текстовое поле настроек (isMine/isGuarded/rules или maxCount) для шаблонных пулов.
- `TemplatePoolBuilder.Build(List<MandatoryContentGroup>?, List<ContentCountLimit>?)` (static, Models/GamePoolData.cs): строит синтетические `GamePool` (имя = SID пула, Tag = "mandatory"/"content_limits") и словарь строк отображения `Dictionary<GamePool, List<PoolItemInfo>>`. Для mandatory — по строке на ContentItem (sid + Detail с isMine/isGuarded/rules); для limits — по строке на ContentSidLimit (sid + Detail "maxCount: N", Weight = maxCount).

### Changed
- `ContentPoolViewerWindow`: конструктор принимает `mandatory`/`limits`; синтетические пулы добавляются в `_allPools` и в словарь `_templateItems`; `ShowPool` отдаёт строки из словаря, иначе `GamePoolDataLoader.GetPoolItems`. Категории комбо-бокса дополнены "Mandatory" и "Content limits"; `MatchesCategory` маршрутизирует их по `pool.Tag` (с запасным совпадением по имени).
- `ContentPoolViewerWindow.xaml`: добавлена колонка "Настройки" (Binding=Detail).
- `TemplateEditorWindow.xaml.cs` (~:782): при открытии "📋 Просмотр содержимого пулов" передаётся `_template.MandatoryContent` и `_template.ContentCountLimits`.
- `RmgTemplate.cs`: свойства `MandatoryContent` и `ContentCountLimits` перенесены в конец объявлений (после `contentPools`/`contentLists`), чтобы сериализовались в конец .rmg.json — как в игровых шаблонах. `AuroraRMG`/`name` остаются первыми.

### Tests
- TemplatePoolsViewerTests.TemplatePoolBuilder_TagsMandatoryAndContentLimitPools: теги "mandatory"/"content_limits", строки с sid + Detail (isMine/Crossroads/maxCount).
- TemplatePoolsViewerTests.Serialize_WritesMandatoryContentAndLimitsAtEnd: round-trip сохраняет items/limits; порядок — mandatoryContent и contentCountLimits пишутся после contentLists (и после variants).

### Verified
- dotnet test 59/59, build.bat -> release/OldenEraTemplateGenerator.exe (0 ошибок). Просмотр только (без редактирования). .h3t-импорт изменений не требовал (по запросу).
## 2026-07-14 20:07 — User: (debug) catalog generator produced wrong file path + malformed class; fix and make real MC/CL generation green

### Fixed
- Replaced scripts/UpdateGameContentCatalog.ps1 with a C# generator scripts/GenCatalog/ (Program.cs + GenCatalog.csproj) using System.Text.Json. Reason: PowerShell 5.1 ConvertTo-Json/ConvertFrom-Json collapse single-element arrays into scalars (["0"]->"0", [{}]->{}), so the generated .cs could not be deserialized by STJ. The C# generator normalizes bare-object/single-string array fields (ArrayFields: mandatoryContent, content, rules, args, includeLists, contentCountLimits, limits) into JSON arrays via JsonNode deep clones.
- scripts/GenCatalog/Program.cs: fixed the output path. FindRepoRoot() now walks up from the current directory to locate the repo (previously Directory.GetCurrentDirectory() was combined with ..\\.., writing the file to a phantom G:\table\Olden Era - Template Editor\...\ instead of G:\table\AuroraRMG-main\...\), so the real Models\Generated\GameContentCatalog.generated.cs was never updated.
- Fixed malformed generated class: Header previously contained the full class body AND its closing brace, which emitted DataJson outside the class. Restructured so Header only opens the class, DataJson is the first member, and Footer holds CatalogDoc/helpers and closes the class once (braces now balanced: 3088/3088).
- uild.bat [0/5] now runs dotnet run --project "%~dp0scripts\GenCatalog" --verbosity quiet (ps1 removed).

### Changed
- 	ests/.../UnitTest1.cs: relaxed CatalogContent_ResolvesRolesToRealContent and Generator_EmitsNonEmptyMandatoryContentAndLimits to accept content items that use includeLists instead of a sid (real game data has e.g. mandatory_content_blue items with only includeLists).

### Verified
- dotnet test 63/63 pass (4 GameContentCatalogTests now green: catalog loads 78 MC + 64 CL, source-file annotation, role resolution, non-empty MC/CL emit).
- uild.bat -> elease/OldenEraTemplateGenerator.exe, 0 errors (80 pre-existing nullable/CA1416 warnings only). Generator emits 78 mandatoryContent and 64 contentCountLimits pools from the 69 game .rmg.json templates.

## 2026-07-14 21:00 вЂ” User: "РѕСЃС‚Р°РІСЊ Р°РІС‚РѕРІС‹СЃС‚Р°РІР»РµРЅРёРµ РїСѓР»РѕРІ РІ С‚РµС… Р»РѕРіРёРєР°С… РІ РєРѕС‚РѕСЂС‹С… РѕРЅ СѓР¶Рµ РµСЃС‚СЊ; РІ РїСѓР»С‹ РІС‹Р±РѕСЂ РєР°Р¶РґРѕРіРѕ РїСѓР»Р° РѕС‚РґРµР»СЊРЅРѕ; СЂРµР°Р»РёР·СѓР№ (СЏРІРЅС‹Р№ РІС‹Р±РѕСЂ Р±Р°Р·РѕРІРѕРіРѕ С€Р°Р±Р»РѕРЅР° РІ UI)"

### Changed
- **РџСѓР»С‹ С…СЂР°РЅСЏС‚СЃСЏ РѕС‚РґРµР»СЊРЅРѕ РїРѕ РєР°Р¶РґРѕРјСѓ С€Р°Р±Р»РѕРЅСѓ** (Р±РµР· СЃР»РёСЏРЅРёСЏ РїРѕ РёРјРµРЅРё): scripts/GenCatalog/Program.cs С‚РµРїРµСЂСЊ РїРёС€РµС‚ `GameContentCatalog` РєР°Рє `{ templates: { "<key>": { mandatoryContent, contentCountLimits } } }`, РіРґРµ key = РёРјСЏ С„Р°Р№Р»Р° Р±РµР· `.rmg.json`. РџСЂРёС‡РёРЅР°: РѕРґРёРЅ Рё С‚РѕС‚ Р¶Рµ pool name РЅРµСЃС‘С‚ СЂР°Р·РЅС‹Р№ РєРѕРЅС‚РµРЅС‚ РІ СЂР°Р·РЅС‹С… С€Р°Р±Р»РѕРЅР°С… (РїСЂРѕРІРµСЂРµРЅРѕ: 36/36 mandatoryContent-РёРјС‘РЅ РІ >1 С„Р°Р№Р»Рµ СЂР°Р·Р»РёС‡Р°СЋС‚СЃСЏ, 38/41 contentCountLimits).
- `Models/Generated/GameContentCatalog.generated.cs` (СЂРµРіРµРЅРµСЂРёСЂРѕРІР°РЅ, 69 С€Р°Р±Р»РѕРЅРѕРІ): РЅРѕРІС‹Р№ API вЂ” `TemplateNames`, `Templates` (Dictionary<string,TemplateEntry>), `GetMandatoryContent(key)`, `GetContentCountLimits(key)`, `TryFindMandatoryContent(key, out, params names)`, `TryFindContentCountLimits(key, out, params names)`, `TemplateFile(key)`. РЈРґР°Р»РµРЅС‹ `MandatoryContentGroups`/`ContentCountLimits`/`MandatoryContentByName`/`ContentCountLimitsByName`/`SourceFilesFor`.
- `Services/ContentManagement/CatalogContent.cs` (РЅРѕРІС‹Р№): С…СЂР°РЅРёС‚ `_templateKey`; `UseTemplate(key)` РІС‹Р±РёСЂР°РµС‚ Р±Р°Р·РѕРІС‹Р№ С€Р°Р±Р»РѕРЅ; `ResolveBaseTemplate(preferred)` вЂ” best-effort (exact -> substring -> first) РґР»СЏ РїСЂРµСЃРµС‚РѕРІ/РїСЂРѕСЃС‚РѕРіРѕ СЂРµР¶РёРјР°; `McName`/`ClName`/`TryGetMc(role)`/`TryGetCl(role)` СЂРµР·РѕР»РІСЏС‚ РїСѓР» Р’РќРЈРўР Р РІС‹Р±СЂР°РЅРЅРѕРіРѕ С€Р°Р±Р»РѕРЅР°; `Clone<T>` Р»РѕРєР°Р»СЊРЅРѕ (Р±РµР· РєСЂРѕСЃСЃ-РЅРµР№РјСЃРїРµР№СЃРЅРѕРіРѕ `JsonExport`).
- `Models/Generator/GeneratorSettings.cs`: РґРѕР±Р°РІР»РµРЅРѕ `BaseTemplate` (РєР»СЋС‡ Р±Р°Р·РѕРІРѕРіРѕ РёРіСЂРѕРІРѕРіРѕ С€Р°Р±Р»РѕРЅР°). `Models/Generator/SettingsFile.cs`: `baseTemplate` (JSON). `Models/Generator/Presets.cs`: РєРѕРїРёСЂСѓРµС‚ `BaseTemplate`.
- `Services/TemplateGenerator.cs` `Generate`: РІР°Р»РёРґРёСЂСѓРµС‚ `settings.BaseTemplate` (Р±СЂРѕСЃР°РµС‚, РµСЃР»Рё null/РїСѓСЃС‚Рѕ/РЅРµС‚ РІ РєР°С‚Р°Р»РѕРіРµ) Рё РІС‹Р·С‹РІР°РµС‚ `CatalogContent.UseTemplate(baseTemplate)`. РЎСѓС‰РµСЃС‚РІСѓСЋС‰Р°СЏ Р°РІС‚Рѕ-СЂР°Р·РґР°С‡Р° РїСѓР»РѕРІ (СЂРѕР»Рё) СЂР°Р±РѕС‚Р°РµС‚ РїСЂРѕР·СЂР°С‡РЅРѕ С‡РµСЂРµР· `CatalogContent`.
- MainWindow РіРµРЅРµСЂР°С†РёСЏ (MainWindow.Shoot.cs `GenerateReadyMaps`, editor demo; MainWindow.xaml.cs `BtnSimpleGenerate_Click`) С‚РµРїРµСЂСЊ: `settings.BaseTemplate = CatalogContent.SelectedBaseTemplateKey ?? CatalogContent.ResolveBaseTemplate(...)` вЂ” СЏРІРЅС‹Р№ РІС‹Р±РѕСЂ РёР· РІРєР»Р°РґРєРё РџСѓР»С‹ РёРјРµРµС‚ РїСЂРёРѕСЂРёС‚РµС‚, РёРЅР°С‡Рµ auto (РїСЂРµСЃРµС‚С‹/РїСЂРѕСЃС‚РѕР№ СЂРµР¶РёРј СЃРѕС…СЂР°РЅСЏСЋС‚ auto).
- `scripts/GenCatalog/Program.cs`: normalize С‚РµРїРµСЂСЊ РїСЂРёРІРѕРґРёС‚ С‡РёСЃР»РѕРІС‹Рµ СЌР»РµРјРµРЅС‚С‹ СЃС‚СЂРѕРєРѕРІС‹С… СЃРїРёСЃРєРѕРІ (`args`, `includeLists`, РЅР°РїСЂ. `[0]`) Рє СЃС‚СЂРѕРєР°Рј, С‚.Рє. РјРѕРґРµР»СЊ РєР°С‚Р°Р»РѕРіР° С‚РёРїРёР·РёСЂСѓРµС‚ РёС… РєР°Рє `List<string>`.

### Added (UI)
- Р’РєР»Р°РґРєР° В«РџСѓР»С‹В» (`TemplateEditorWindow.xaml` TabPools): РІРІРµСЂС…Сѓ РґРѕР±Р°РІР»РµРЅ ComboBox В«Р‘Р°Р·РѕРІС‹Р№ С€Р°Р±Р»РѕРЅ РёРіСЂС‹В» (CmbBaseTemplate), Р·Р°РїРѕР»РЅСЏРµС‚СЃСЏ РёР· `GameContentCatalog.TemplateNames`. Р’С‹Р±РѕСЂ РІС‹Р·С‹РІР°РµС‚ `CatalogContent.SelectBaseTemplate(key)` (Рё `UseTemplate`) вЂ” РїСѓР»С‹ Рё Р°РІС‚Рѕ-РЅР°Р·РЅР°С‡РµРЅРёРµ Р·РѕРЅ Р±РµСЂСѓС‚СЃСЏ РёР· РІС‹Р±СЂР°РЅРЅРѕРіРѕ С€Р°Р±Р»РѕРЅР°; РІС‹Р±РѕСЂ СЃРѕС…СЂР°РЅСЏРµС‚СЃСЏ РІ `CatalogContent.SelectedBaseTemplateKey` Рё РІР»РёСЏРµС‚ РЅР° РіРµРЅРµСЂР°С†РёСЋ. РџРµСЂ-РїСѓР»РѕРІС‹Р№ РІС‹Р±РѕСЂ РІ РёРЅСЃРїРµРєС‚РѕСЂРµ Р·РѕРЅС‹ СЃРѕС…СЂР°РЅС‘РЅ (С‡РёС‚Р°РµС‚ `GamePoolDataLoader.GetAllPools()`).
- `CatalogContent.SelectBaseTemplate(key)` + РїСѓР±Р»РёС‡РЅРѕРµ `SelectedBaseTemplateKey`.

### Fixed
- `ContentPoolViewerWindow.xaml.cs`: СѓР±СЂР°РЅ РІС‹Р·РѕРІ СѓРґР°Р»С‘РЅРЅРѕРіРѕ `GameContentCatalog.SourceFilesFor` (РїСѓР»С‹ РїСЂРёС…РѕРґСЏС‚ Р±РµР· РєР°С‚Р°Р»РѕРіР°; Р°РЅРЅРѕС‚Р°С†РёСЏ Р±С‹Р»Р° РёРЅРµСЂС‚РЅРѕР№).
- РўРµСЃС‚С‹ `GameContentCatalogTests` (UnitTest1.cs) РїРµСЂРµРїРёСЃР°РЅС‹ РїРѕРґ per-template API; `Clone` Р»РѕРєР°Р»СЊРЅРѕ РІРѕ РёР·Р±РµР¶Р°РЅРёРµ РЅРµРґРѕСЃС‚СѓРїРЅРѕСЃС‚Рё РЅРµР№РјСЃРїРµР№СЃР° `Old_Era___Template_Editor` РІ WPF temp-compile.

### Verified
- dotnet test 63/63 (4 GameContentCatalogTests Р·РµР»С‘РЅС‹Рµ). build.bat -> release/OldenEraTemplateGenerator.exe, 0 РѕС€РёР±РѕРє (pre-existing CA1416 warnings). Р“РµРЅРµСЂР°С‚РѕСЂ РІС‹РґР°С‘С‚ СЂРµР°Р»СЊРЅС‹Рµ mandatoryContent/contentCountLimits РёР· РІС‹Р±СЂР°РЅРЅРѕРіРѕ Р±Р°Р·РѕРІРѕРіРѕ С€Р°Р±Р»РѕРЅР°.


## 2026-07-14 21:30 вЂ” User: "РїСЂРё РЅР°Р¶Р°С‚РёРё РЅР° РєРЅРѕРїРєСѓ СЃРѕР·РґР°С‚СЊ С€Р°Р±Р»РѕРЅ РїСЂРѕРіСЂР°РјРјР° РєСЂР°С€РёС‚СЃСЏ"

### Fixed (crash on "РЎРѕР·РґР°С‚СЊ С€Р°Р±Р»РѕРЅ" / Generate)
- Root cause: `MainWindow.BuildSettings()` (MainWindow.xaml.cs:2705) never set `GeneratorSettings.BaseTemplate`. `TemplateGenerator.Generate` had been changed to **throw** `InvalidOperationException` when `BaseTemplate` is null/empty/not-in-catalog, so the main Generate button (label `S.Btn.Generate` = "РЎРѕР·РґР°С‚СЊ С€Р°Р±Р»РѕРЅ", calls `BuildSettings()`) hit the throw в†’ unhandled в†’ app crash.
- `Services/TemplateGenerator.cs` `Generate`: replaced the hard `throw` with a graceful fallback chain вЂ” `settings.BaseTemplate` в†’ `CatalogContent.SelectedBaseTemplateKey` (Pools-tab ComboBox) в†’ `CatalogContent.ResolveBaseTemplate(null)` (auto). Throws only if the catalog is genuinely empty (never at runtime вЂ” embedded 69 templates). Then `settings.BaseTemplate = resolvedKey; CatalogContent.UseTemplate(resolvedKey);`. Honors "keep auto-resolution" while the explicit Pools-tab pick still wins.
- `MainWindow.xaml.cs` `BuildSettings()`: now also sets `BaseTemplate = CatalogContent.SelectedBaseTemplateKey ?? CatalogContent.ResolveBaseTemplate(TxtTemplateName.Text.Trim())` for consistency with the other 3 generate paths.

### Added (tests)
- `GameContentCatalogTests.Generator_DoesNotThrowWithoutExplicitBaseTemplate`: Generate with `BaseTemplate` left null must not throw and must still emit pools (regression guard for the crash).

### Verified
- dotnet test 64/64 pass. build.bat -> release/OldenEraTemplateGenerator.exe, 0 errors.


## 2026-07-14 23:20 � User: "���� content_limits � mandatory_content ����� ������� �� ����������� �� �������, � ��������� �������� � ���������� ������� ���� ... ������� �� ������������� � ���������� ����� ... � ������� ����������� �� �������� � ��������������� ����������. ����� �� ���������� �����: � 1 � 2 ������: guarded, unguarded, random, specific-template, ���������; � ��� ��������: resources"

### Changed
- Removed the per-template `������� ������ ����` ComboBox (and `TxtBaseTemplateHint`) from the Pools tab; removed `InitBaseTemplatePicker` / `CmbBaseTemplate_SelectionChanged` in TemplateEditorWindow.
- Removed `CatalogContent.SelectedBaseTemplateKey` and `SelectBaseTemplate`; `TemplateGenerator.Generate` now auto-resolves the base template via `CatalogContent.ResolveBaseTemplate(TemplateName/null)` instead of a user-gated selection. MainWindow/MainWindow.Shoot `BaseTemplate` assignments drop the `?? SelectedBaseTemplateKey` part.
- Added `GamePool.SourceTemplate` (catalog key of the originating game template) and `GamePoolDataLoader.GetAllPoolsWithTemplates(out Dictionary<GamePool,List<PoolItemInfo>>)` which returns the full universe: game pools + every one of the 69 game templates' `mandatoryContent` / `contentCountLimits` pools (built via `TemplatePoolBuilder.Build`, each tagged with its source template).
- ContentPoolViewerWindow now loads that full universe by default (view mode) and gained a selection mode (`selectionMode`, `allowedCategories`, `SelectedPools`): categories include a new `Specific-template` (matches `SourceTemplate != null`); shows the source template under each pool; double-click / "�������� ��������� ���" adds to the selection; OK returns chosen pool names.
- Pools-tab pickers (Guarded, Unguarded, Resources, MandatoryContent, ContentCountLimits) converted to category-based selection via new `AddCategoryPicker`, which opens the viewer in selection mode restricted to the requested categories: p1/p2 = guarded/unguarded/random/specific-template/created; p3 = resources; p4/p5 = specific-template/created.

### Verified
- dotnet build (Debug) clean; dotnet test 64/64 pass; build.bat -> release/OldenEraTemplateGenerator.exe, 0 errors (includes catalog regen).

## 2026-07-14 23:55 � User: "���������� ���� mandatory/content_limits � ��������� ��������� '������������ �������' � '������ ���������� ��������' (�� specific-template), �������� ��� ������� � �������� (������ UI); ������������� ������� '���'->'�������', '���������'->'������������ ����������', ���������� ' * ' ������ 'maxCount:*'; ������ specific-template �� ������� ��� ������������ �������/������"

### Changed
- `GamePool.ToString()` now appends "  �  <SourceTemplate>" for template-sourced pools (display only; stored/serialized value stays the bare `Name`).
- `ContentPoolViewerWindow`: replaced the generic "Specific-template" category with two dedicated categories � "������������ �������" (tag=="mandatory") and "������ ���������� ��������" (tag=="content_limits"); removed the now-unused `Specific-template` option and `GetSourceTemplate` helper.
- DataGrid column headers renamed: "���" -> "�������", "���������" -> "������������ ����������".
- `TemplatePoolBuilder.Build` now sets content-limit row `Detail` to " * " (display-only marker) instead of "maxCount: *"; the real `MaxCount` is still carried in `Weight` (and serialized from `ContentCountLimit.Limits`, untouched).
- Pools-tab pickers: p1/p2 now use "Template-specific" (template_pool_ game pools) instead of the removed "Specific-template"; p4 (mandatory) uses ["������������ �������","���������"]; p5 (limits) uses ["������ ���������� ��������","���������"]. "specific-template" is no longer offered for the mandatory/limits pickers.

### Verified
- dotnet test 64/64 pass (updated TemplatePoolBuilder_TagsMandatoryAndContentLimitPools assertion to the new " * " display). build.bat -> release/OldenEraTemplateGenerator.exe, 0 errors.

## 2026-07-15 00:15 � User: "� 'maxCount:*' ������ ������ �������� ������ *; ��� mandatory_content ������������� ������� � '�������������', �������: ��� �������� -> �����, ������� -1 -> '-1'"

### Changed
- `TemplatePoolBuilder.Build` content-limit rows now set `Detail` to the real max-count value (`sl.MaxCount.ToString()`) instead of a literal " * " � the " * " was a placeholder for that value.
- `ContentPoolViewerWindow` DataGrid now binds to a display projection (`PoolRow`): "�������" (was "���") and "�������������/������������ ����������" (was "���������") columns use pre-formatted strings.
  - For mandatory-content pools the "�������" column shows nothing when weight is 0 (no variant) and "-1" when weight is -1; the detail column header becomes "�������������".
  - For content-limits pools the detail column header is "������������ ����������" and shows the max-count value.
  - For all other pools the header stays "���������" and weight is shown as-is.
- Updated `TemplatePoolBuilder_TagsMandatoryAndContentLimitPools` assertion to the new `Detail == "1"` (the max-count value).

### Verified
- dotnet test 64/64 pass. build.bat -> release/OldenEraTemplateGenerator.exe, 0 errors (stopped the locked running exe first so the single-file publish could overwrite).

## 2026-07-14 00:00 � User: ��� includeLists / content_limit ����� (SID = ��� include-������, ������������ variant/maxCount)

### Fixed
- PoolItemInfo: added Variant (int?) and MaxCount (int) fields.
- TemplatePoolBuilder.Build:
  - Mandatory items: Sid falls back to joined IncludeLists when Sid is absent; Variant column = item.Variant (null -> empty, -1 -> -1).
  - Content-limit items: Sid resolves to sl.Sid -> joined IncludeLists -> first nested content Sid; Variant = sl.Variant (null -> empty); Extra = maxCount value, empty when maxCount is 0/absent.
- ContentPoolViewerWindow.ShowPool: for mandatory/limits pools the ""�������"" column uses VariantText(int? v) (null -> "", -1 -> "-1"); for limits the Extra column shows MaxCount (empty when 0). Game pools keep raw Weight logic and still expand include-lists.
- Model unchanged (ContentSidLimit.MaxCount stays int) so serialization is unaffected; absent maxCount is treated as 0 -> empty cell.

### Added
- Test TemplatePoolBuilder_DisplaysIncludeListsAndOptionalVariantMaxCount covering includeLists Sid, optional variant, and absent maxCount.

### Verified
- dotnet test 65/65 pass. build.bat -> release/OldenEraTemplateGenerator.exe, 0 errors.

## 2026-07-14 — User: "при импорте шаблона или создании шаблона нужно в главном окне оставлять возможность редактировать некоторые данные (название/режим игры/описание/условие победы/правила карты), но блокировать вкладку наполнение зон, вкладку доп. наполнение, игроков и вид карты"

### Added — импорт шаблона + режим "Изменить базовые настройки" в главном окне
- MainWindow.xaml: кнопка "Импортировать шаблон" на тулбаре; кнопка "Создать заново" (BtnRecreate) рядом с BtnEditor в StackPanel; x:Name для блокируемых элементов TabMapZones, TabExtraContent, LblPlayers, LblMapView; выявленный ComboBox режима игры (CmbGameModeEdit) рядом с названием; TextBox описания (TxtTemplateDescription); TextBox условия победы (TxtWinConditionDetail).

### Changed
- MainWindow.xaml.cs: состояние _editingTemplate / _editingTemplatePath / _editMode. BtnPreview_Click разделён на GenerateTemplate() (оригинальная генерация) и EditBasicSettings() (патч метаданных+правил, сохранение). После генерации автоматически EnterEditMode(), чтобы сразу можно было править базовые настройки.
- BtnNew_Click вызывает ExitEditMode() (сброс UI к режиму создания).
- BuildSettings переименован в BuildGameRulesFromUi; добавлен ApplyTemplateToUi(RmgTemplate) — обратный маппинг метаданных + правил в UI (Name, GameMode, Description, DisplayWinCondition/WinConditionDetail, sliders, чекбоксы, value overrides, bans, light/locked/obelisk чекбоксы, NeutralTowns).
- SetEditModeUi(bool): скрывает TabMapZones/TabExtraContent/LblPlayers/LblMapView в режиме правки; BtnPreview меняет контент на "Изменить базовые настройки", BtnEditor/BtnRecreate — IsEnabled=false.
- PatchMetadataAndRules(target, source) — чистый метод: патчит только Name/GameMode/Description/DisplayWinCondition/GameRules (включая Bonuses и ValueOverrides из target), остальное (zones/content/variants) не трогает.

### Added — публичные билдеры правил
- TemplateGenerator.BuildGameRules, BuildValueOverrides, BuildGlobalBans сделаны public (используются для пересборки правил при сохранении патча).

### Added — тесты
- TemplateGeneratorRulesTests: BuildGameRules (valueOverride HeroCountMax=4 + бан TownMilitia -> HeroCountMax/ValueOverrides/GlobalBans), BuildValueOverrides (true->"On", false->empty, число->строка), BuildGlobalBans (true->id, false->пропуск).

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 ошибок. dotnet test: 67 pass, 1 fail — RuAndEnHaveSameKeys (RU=687, EN=683) падает из-за 4 ключей S.OR.* присутствующих в RU, но отсутствующих в EN (параллельный агент чинит локализацию, мои добавленные ключи сбалансированы 2х2). build.bat (release, single-file publish) 0 ошибок по основному проекту; единственный упавший тест — локализация (не моя зона ответственности).

### Limitations
- Бонусы не раунд-трипятся в UI при импорте (raw Bonus->BonusEntry неоднозначен), но сохраняются как есть в PatchMetadataAndRules (EditBasicSettings не теряет бонусы). Вид карты/зоны не рендерятся из импортированного шаблона (только из сгенерированного).

## 2026-07-14 — User: "при нажатии на кнопку пересоздать шаблон состоянии генератора должно сбрасываться до состоянии как будто он только что был запущен"

### Changed — кнопка "Создать заново" теперь сбрасывает генератор к старту
- Добавлен `ResetGeneratorToDefaults()`: сбрасывает активный вид (Advanced -> ApplySettings(new SettingsFile()); Simple -> ResetSimpleToDefaults()), очищает _currentSettingsPath/_isDirty, а также сгенерированное состояние — _generatedTemplate=null, _generatedTopology=default, _templateOutdated=false, ImgPreview.Source=null, BtnSaveGenerated.Visibility=Collapsed, lblNoPreview.Content=null, UpdateBalanceReport() (очищает отчёт при null-шаблоне), UpdateOutdatedWarning(), ExitEditMode(), UpdateTitle().
- BtnNew_Click теперь оставляет свой диалог подтверждения, но делегирует сброс в ResetGeneratorToDefaults() (заодно теперь очищает устаревший превью/анализ — консистентно со стартом).
- BtnRecreate_Click ("Создать заново") вместо повторной генерации вызывает ResetGeneratorToDefaults() без подтверждения. Видимость кнопки не изменена — видна только в режиме правки (SetEditModeUi), после сброса возвращается в обычный режим и скрывается.

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 ошибок. dotnet publish -c Release win-x64 single-file -> release/OldenEraTemplateGenerator.exe, 0 ошибок. dotnet test 68/68 pass (в т.ч. RuAndEnHaveSameKeys — параллельный агент починил локализацию).

## 2026-07-14 — User: "при импорте rmg.json шаблона из главного окна не происходит построение канваса; импорт singlehero ставит лимит 2/1 вместо 1/0; при singlehero блокировать лимиты на 1/0/1 и авто-вкл 'режим одного героя' с запретом выключения; после создания/импорта прятать шанс перехода нейтралов, блокировать 'поражение при потере стартового города' и 'победа за удержание нейтр города', прятать весь блок 'окружение и встречи' и флаг разрешить обход охраны"

### Fixed — канвас при импорте
- EnterEditMode теперь после ApplyTemplateToUi выставляет _generatedTemplate=template, _generatedTopology=default и рендерит ImgPreview через TemplatePreviewPngWriter.Render (с default-топологией, т.к. реальная не хранится в .rmg.json) + UpdateBalanceReport(). EditBasicSettings уже перерисовывает.

### Fixed — SingleHero импорт/выбор
- ApplyTemplateToUi при GameMode=="SingleHero": вместо пересчёта лимитов вызывает ApplySingleHeroLock(true) (min=max=1, increment=0) и ChkSingleHeroMode.IsEnabled=false (нельзя выключить режим одного героя, пока шаблон SingleHero).
- Извлечён ApplySingleHeroLock(bool): общая логика блокировки ( IsEnabled=false для SldHeroMin/Max/Increment + TxtHero*, ChkLostStartHero.IsChecked=true и IsEnabled=false, значения 1/1/0). Используется и в ChkSingleHeroMode_Changed, и в ApplyTemplateToUi, и в CmbGameModeEdit_SelectionChanged.
- MainWindow.xaml: CmbGameModeEdit получил SelectionChanged="CmbGameModeEdit_SelectionChanged". При выборе "SingleHero" автоматически ChkSingleHeroMode.IsChecked=true + ApplySingleHeroLock(true) + ChkSingleHeroMode.IsEnabled=false; при другом режиме — IsEnabled=true и снимается блокировка.

### Fixed — блокировка лишних настроек после создания/импорта
- Добавлен SetLockedExtras(bool): скрывает PnlEnvironment (весь блок "Окружение и встречи", включая ChkEncounterHoles — обход охраны) и PnlDiplomacy (шанс перехода нейтралов, SldDiplomacy/S.M.038), а также ChkLostStartCity.IsEnabled=false и ChkCityHold.IsEnabled=false. Вызывается из SetEditModeUi(true), снимается в SetEditModeUi(false).
- MainWindow.xaml: добавлены x:Name="PnlEnvironment" (StackPanel блока окружения/встреч) и x:Name="PnlDiplomacy" (DockPanel шанса перехода нейтралов).

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 ошибок. dotnet publish -c Release win-x64 single-file -> release/OldenEraTemplateGenerator.exe, 0 ошибок. dotnet test 68/68 pass.

## 2026-07-17 — Fix: NullReferenceException в BonusPickerWindow при открытии (кнопка «Добавить бонусы»)

### Root cause
- Глобальный `ComboBoxBehavior` (`SyncTextWithSelectedItem=True` из App.xaml) на каждый `SelectionChanged` выполняет `cb.Text = cb.SelectedItem?.ToString()`. В конструкторе `BonusPickerWindow` установка `CmbType.SelectedIndex = 0` вызывает `SelectionChanged` → поведение проставляет `Text` → `ComboBox.TextUpdated` реентерабельно вызывает `SelectionChanged` ещё раз → `CmbType_Changed` → `SelectedType` → `(ComboBoxItem)CmbType.SelectedItem` равен null в этот момент → NullReferenceException (get_SelectedType, строка 39).
- Это НЕЗАВИСИМЫЙ от favicon краш: favicon-ный XamlParseException (в InitializeComponent) маскировал этот NRE; после удаления favicon-ссылок NRE стал реальным падением окна при открытии.

### Fixed
- `BonusPickerWindow.xaml.cs` `SelectedType` переписан как свойство с null-safe разбором: если `CmbType.SelectedItem` не ComboBoxItem/Tag — возвращает `BonusPresetType.TownPortalFree` (безопасный дефолт) вместо NRE.
- `SelectedReceiver` аналогично: null-safe, дефолт `"start_hero"`.
- `CmbType_Changed` добавлена early-return при `CmbType.SelectedItem == null` (защита от реентерабельной селекции во время Text-sync/конструкции).

### Verified
- Репродьюс через реальный EXE + временный `--bonustest` (показ окна ShowDialog + клик BtnAdd_Click) писал crash в %TEMP%\aurora_crash.log; после фикса crash-лог пустой, окно не падает. Временный хук и DispatcherUnhandledException-обработчик из App.xaml.cs/MainWindow.xaml.cs удалены.
- dotnet build (Debug, PublishTrimmed=false) 0 ошибок. dotnet test 68/68 pass (удалён временный TmpBonusRepro2.cs, падавший на DialogResult-артефакте тест-харнеса). Запуск EXE без аргументов — LAUNCH OK.

## 2026-07-17 — Fix: в окне «Добавить бонус» выбор 2+ заклинаний/артефактов не показывался в строке

### Root cause
- В `BonusPickerWindow.BtnPickSpell_Click`/`BtnPickItem_Click` при выборе >1 элемента в SpellPickerWindow/ItemPickerWindow окно бонуса сразу закрывалось (DialogResult=true), а выбранные id НЕ записывались в `TxtSpell`/`TxtItem` — пользователь не видел свой выбор в строке UI.

### Fixed
- Мульти-выбор больше не закрывает окно. `TxtSpell`/`TxtItem` заполняются списком выбранных id через `", "`, так что выбор виден в строке.
- `Results` заполняется всеми выбранными записями сразу (как было), но окно не закрывается — пользователь видит и подтверждает.
- `BtnAdd_Click`: для Spell/Item, если `Results` уже содержит записи нужного типа (заполнены пикером), добавляет их через новый `AddPickedResults` (прогоняет проверку на дубликаты и переприменяет текущий Receiver), не перезаписывая joined-текст одной записью. Одиночный ручной ввод по-прежнему работает.

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 ошибок. dotnet test 68/68 pass.

## 2026-07-14 — User: "импорт через визуальный редактор зон не применяет то же правило что и основное окно; версия 3.0; кнопка 'изменить базовые настройки' только замена метаданных без сохранения"

### Fixed — импорт через визуальный редактор зон возвращает шаблон в основное окно с блоками
- TemplateEditorWindow.xaml.cs: добавлен публичный event TemplateImported (Action<RmgTemplate>); поднимается в BtnLoad_Click после LoadTemplate (только при реальном импорте .rmg.json/H3T/картинки, не при JSON-правке из JsonPreviewWindow).
- MainWindow.xaml.cs BtnOpenEditor_Click: подписывается на editor.TemplateImported += OnZoneEditorTemplateImported; обработчик вызывает EnterEditMode(loaded, path:null) — то есть применяет те же ограничения, что и прямой импорт через основное окно (ApplyTemplateToUi: SingleHero-блокировка + HeroHireBan; SetEditModeUi/SetLockedExtras: скрытие "Окружение и встречи"/"Дипломатия", блокировка "потеря стартового города"/"захват нейтрального города").

### Fixed — версия 3.0
- AssemblyInfo.cs: AssemblyVersion и AssemblyFileVersion подняты 2.0.0.0 -> 3.0.0.0 (пред. версия была 2.0, не 1.0). В заголовке окна отображается "v3.0".
- ВНИМАНИЕ: нельзя ставить <AssemblyVersion> в .csproj при GenerateAssemblyInfo=false — конфликтует с AssemblyInfo.cs и ломает загрузку WPF-ресурсов (BAML ссылается на версию сборки). Это и было причиной "приложение не запускается" (FileNotFoundException 'OldenEraTemplateGenerator, Version=3.0.0.0'). Исправлено: версия только в AssemblyInfo.cs.

### Fixed — 'изменить базовые настройки' только замена метаданных, без сохранения
- MainWindow.xaml.cs EditBasicSettings: убран вызов SaveEditingTemplate() (и SaveEditingTemplateAs). Теперь кнопка только применяет PatchMetadataAndRules к _editingTemplate и обновляет превью; сохранение — отдельной командой пользователя.

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 ошибок. dotnet test 68/68 pass.

## 2026-07-14 — User: "проверь сериализацию valueOverrides против игровых map_templates"

### Findings (сверка с E:\...\StreamingAssets\map_templates\*.rmg.json, ~120 файлов)
- Игровой формат valueOverrides = { "sid", "variant", "guardValue" } (camelCase, 2-space indent). JsonExport.Options (WriteIndented, WhenWritingNull, JsonPropertyName) воспроизводит точно — совпадение.
- variant в играх = 0..3 (конкретный подъобъект) или -1 (любой). Код ставит Variant=-1 = "любой подъобъект" — семантически верно, НЕ баг. Оставлено как есть.
- В играх НЕТ поля "value" в valueOverrides (0 совпадений). Поле value не имеет схемы игры → не сработает в игре.

### Changed — поле value объектов заблокировано (по решению юзера)
- Models/Unfrozen/Miscellaneous.cs: у ValueOverride.Value убран [JsonPropertyName("value")] — поле больше НЕ сериализуется в .rmg.json (остаётся только для неактивного UI).
- MainWindow.xaml: секция "Перераспределение value объектов" (S.M.115) обёрнута в StackPanel IsEnabled=False Opacity=0.5 — неактивна/затемнена. Добавлена подсказка S.M.117 (RU+EN) "поле неактивно: в схеме игры нет value".
- Strings.cs: добавлен ключ S.M.117 (RU+EN), баланс RU/EN сохранён.
- Guard-поле (S.M.114) полностью рабочее и корректно сериализуется в игровую схему.

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 ошибок. dotnet test 68/68 pass. value не имеет JsonProperty -> не попадает в вывод.

## 2026-07-14 — User: "поле Перераспределение силы охраны переделать под UI как у пулы; добавить Перераспределение value объектов; локализацию имён из mapObjects.json"

### Changed — UI полей value-override переделан под чипы (как пулы)
- MainWindow.xaml: секция S.M.088 (Переопределение силы охраны) перестала быть свободным TextBox. Вместо него — два блока чипов в стиле пулов визуального редактора:
  - GuardOverrideChips (WrapPanel) + BtnAddGuardOverride (S.M.114 "Переопределение силы охраны", S.M.116 "Добавить объект…").
  - ValueOverrideChips (WrapPanel) + BtnAddValueOverride (новое поле S.M.115 "Перераспределение value объектов").
  - Чип = локализованное имя объекта + raw sid + кнопка "✕" (удаляет только своё значение; если у объекта осталось только одно из двух — запись не дублируется).
- MainWindow.xaml.cs: _valueOverrides (List<ValueOverride>) — единый источник правды для обоих полей. UpsertValueOverride/RemoveGuardOverride/RemoveValueOverride объединяют sid в ОДНУ запись (value затем guardValue). RebuildValueOverrideChips строит чипы через MakeOverrideChip (тот же стиль BrushInput/BrushBorder, что у пулов). BtnAddGuardOverride_Click / BtnAddValueOverride_Click открывают ValueOverridePickerWindow в режиме Guard/Value. Загрузка/сохранение (_valueOverrides <-> GeneratorSettings.ValueOverridesText) обновлены; сброс через ResetGeneratorToDefaults -> ApplySettings. OnLanguageChanged: ObjectNameResolver.Reset() + RebuildValueOverrideChips().
- ValueOverridePickerWindow.xaml/.cs: добавлен PickMode (Guard/Value); для Value — отдельное поле TxtObjectValue (S.Vov.004/005), GuardRow/ValueRow переключаются по режиму. Список объектов теперь привязан к ObjectItem{Sid, Display}, где Display = локализованное имя из ObjectNameResolver (поиск фильтрует и по имени). Результат: "sid=G" (guard) или "sid=,V" (value).
- Models/Unfrozen/Miscellaneous.cs: ValueOverride добавлено свойство [JsonPropertyName("value")] int? Value.
- Services/TemplateGenerator.cs: BuildValueOverrides парсит формат "sid=G", "sid=,V", "sid=G,V" (обратно совместимо со старым "sid=G"); добавлен ValueOverridesToText (сериализация в тот же формат). В RmgTemplate.ValueOverrides теперь пишется один entry на sid с обоими полями.
- Services/GameData/ObjectNameResolver.cs (NEW): читает Lang/<lang>/texts/mapObjects.json из Core.zip (ключ <sid>_name -> text) для RU/EN, кэширует, Resolve(sid) возвращает локализованное имя или сам sid. Reset() очищает кэш при смене языка.
- Localization/Strings.cs: добавлены ключи (RU+EN, баланс сохранён): S.M.114, S.M.115, S.M.116, S.Vov.004, S.Vov.005.

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 ошибок. dotnet test 68/68 pass (RuAndEnHaveSameKeys зелёный). Игровой Core.zip и Lang/russian/texts/mapObjects.json присутствуют по указанному пути.

## 2026-07-14 — User: "в UI не видно заданное значение; добавить в окно подсказку про множественный выбор"

### Fixed
- MainWindow.xaml.cs MakeOverrideChip: теперь принимает valueText и показывает заданное значение внутри чипа ("Лесопилка (mine_wood) = 5000"). RebuildValueOverrideChips передаёт o.GuardValue / o.Value.
- ValueOverridePickerWindow.xaml: добавлена подсказка S.Vov.006 "Можно выбрать несколько объектов — для всех выбранных значение будет одинаковым." (RU+EN) под полем значения.
- Strings.cs: добавлен ключ S.Vov.006 (RU+EN).

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 ошибок. dotnet test 68/68 pass.

## 2026-07-14 — User: "в ImageImportWindow сломанные локализации — проверь ключи и локализацию самих ключей"

### Root cause
- В ImageImportWindow.xaml/.cs использовались ключи S.EI.*, но в словаре Strings.cs существовал только блок статусных/сообщений (SelectRmg, SelectH3T, ReadingRmg, ParseFail, FileInfo, RmgSummary, RmgLoaded, H3TParsed, DetectedZones, DetectedConns, Parsing, Error). UI-ключи (Title, Placeholder, Select, Zones, Import, Close) и кодовые (SelectTitle, Analyzing, Done) отсутствовали → DynamicResource/L возвращали сырой ключ (ложная локализация). Кроме того часть текста была захардкожена на RU (BtnSelectRmg, BtnSelectH3T, заголовки DetectedZones/DetectedConns, RmgFileInfo).

### Fixed
- Strings.cs (RU ~705-716, EN ~1444-1455): добавлены недостающие ключи в оба словаря (пара RU=EN по количеству, тест RuAndEnHaveSameKeys зелёный):
  S.EI.Title, S.EI.Placeholder, S.EI.Select, S.EI.Zones, S.EI.Import, S.EI.Close, S.EI.SelectTitle, S.EI.Analyzing, S.EI.Done.
- ImageImportWindow.xaml: RmgFileInfo, BtnSelectRmg, BtnSelectH3T переведены на DynamicResource S.EI.SelectRmg/.SelectH3T; заголовки H3T-панелей на S.EI.DetectedZones/.DetectedConns. Все кнопки (BtnSelectRmg, BtnSelect, BtnSelectH3T, BtnImport, BtnClose) и заголовки теперь локализованы через DynamicResource.

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 ошибок. dotnet test 68/68 pass. RuAndEnHaveSameKeys green.

## 2026-07-14 — User: "там же остались s.ei.ready и s.ei.hint"

### Fixed
- Strings.cs: добавлены пропущенные ключи S.EI.Ready и S.EI.Hint в оба словаря (RU+EN). Они использовались в ImageImportWindow.xaml (StatusText, HintText) и .cs (ResetState/SwitchTo), но отсутствовали в словаре → показывался сырой ключ.

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 ошибок. dotnet test 68/68 pass. RuAndEnHaveSameKeys green.

## 2026-07-14 — User: "кнопка импорта в основном меню должна копировать функционал кнопки 'загрузить .rmg.json'"

### Changed — импорт в главном окне использует тот же ImageImportWindow
- MainWindow.xaml.cs BtnImportTemplate_Click теперь открывает ImageImportWindow (тот же, что и кнопка "загрузить .rmg.json" в визуальном редакторе зон, TemplateEditorWindow.BtnLoad_Click): поддерживает импорт .rmg.json, PNG-картинки (ImageAnalyzer) и .h3t (H3TParser) с превью/статусом и внутренней обработкой ошибок. Результат передаётся в EnterEditMode(template, path: null) (path=null → при сохранении SaveAs, как в BtnLoad_Click где _currentPath=null).

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 ошибок. dotnet publish -c Release win-x64 single-file -> release/OldenEraTemplateGenerator.exe, 0 ошибок. dotnet test 68/68 pass.

## 2026-07-14 — User: "локализация 'окружение и встречи' не пропадает вместе с функциями; при singlehero выставлять и блокировать 'запрет найма героев'; у кнопки 'зеркало' неотображаемый квадратик; заменить локализацию 'зеркало' на 'режим зеркального построения'"

### Fixed — заголовок "Окружение и встречи" скрывается вместе с блоком
- MainWindow.xaml: убран лишний x:Name="PnlEnvironmentHeader"; TextBlock заголовка S.Hdr.Environment перенесён ВНУТРЬ StackPanel x:Name="PnlEnvironment". Теперь при SetLockedExtras(true) скрывается и заголовок, и все функции блока.

### Fixed — SingleHero выставляет и блокирует "Запрет найма героев"
- ApplySingleHeroLock(true) теперь так же ChkHeroHireBan.IsChecked=true и ChkHeroHireBan.IsEnabled=false; в ветке else оба чекбокса (LostStartHero и HeroHireBan) снова IsEnabled=true. Работает для импорта (ApplyTemplateToUi), выбора режима в комбо (CmbGameModeEdit_SelectionChanged) и ручного чекбокса (ChkSingleHeroMode_Changed).

### Fixed — локализация кнопки "зеркало"
- Strings.cs: S.EC.Mirror (RU) "🪞 Зеркало" -> "Режим зеркального построения" (убран неотображаемый эмодзи-квадратик); S.EC.Mirror (EN) "🪞 Mirror" -> "Mirror build mode".

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 ошибок. dotnet publish -c Release win-x64 single-file -> release/OldenEraTemplateGenerator.exe, 0 ошибок. dotnet test 68/68 pass (RuAndEnHaveSameKeys зелёный — RU/EN сбалансированы).

## 2026-07-14 — User: "s.e.hint в локализации при импорте из окна визуальный редактор зон"

### Fixed — подсказка холста (S.Ed.CanvasHint) не показывается после импорта
- TemplateEditorWindow.xaml.cs: добавлен UpdateCanvasHintVisibility() — TextBlock TxtCanvasHint (подсказка "Wheel — zoom · ... Del — delete · Esc — cancel", ключ S.Ed.CanvasHint) виден только для пустого нового шаблона (Zones.Count==0) и скрывается (Visibility.Collapsed), как только в шаблоне есть зоны. Вызывается в Loaded (после RebuildGraph) и в LoadTemplate (после импорта/загрузки .rmg.json). Ранее подсказка была всегда видна поверх импортированной схемы зон.

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 ошибок. dotnet publish -c Release win-x64 single-file -> release/OldenEraTemplateGenerator.exe, 0 ошибок. dotnet test 68/68 pass.

## 2026-07-17 — Fix: слетел ключ локализации S.EC.SameOwner + аудит всех ключей + автотесты

### Root cause
- Код `TemplateEditorWindow.xaml.cs:693` ссылался на `L("S.EC.SameOwner")`, но ключа не было ни в RU, ни в EN таблице Strings.cs. `LocalizationManager.Get` возвращает сам ключ при отсутствии → в UI показывался сырой `S.EC.SameOwner`.

### Audit (скрипт поиска всех referenced ключей в *.cs/*.xaml)
- Найдено ещё 4 реально отсутствующих ключа (показывались бы сырыми): `S.CPV.List` (ContentPoolViewerWindow.xaml:76), `S.EC.MirrorOn`/`S.EC.MirrorOff` (TemplateEditorWindow.xaml.cs:3874), `S.EC.HubCreated` (TemplateEditorWindow.xaml.cs:4259). `S.X.Y` — плейсхолдер только в doc-comment, не считается.

### Fixed — добавлены недостающие ключи в обе таблицы (RU + EN)
- `S.EC.SameOwner` = "Тот же владелец" / "Same owner"
- `S.CPV.List` = "Список" / "List"
- `S.EC.MirrorOn` = "Режим зеркалирования: вкл" / "Mirror mode: ON"
- `S.EC.MirrorOff` = "Режим зеркалирования: выкл" / "Mirror mode: OFF"
- `S.EC.HubCreated` = "Создан центр: {0}." / "Hub created: {0}."

### Added — автотест на полную локализацию
- `UnitTest1.cs` / `LocalizationKeysTests.EveryReferencedKeyExistsInRuAndEn`: сканирует дерево исходников редактора (исключая bin/obj) на предмет всех ключей, упомянутых в коде (L/T/Get) и XAML (DynamicResource/StaticResource S.*), и утверждает, что каждый есть в RU и EN таблицах. Постоянный guard от регрессии «слетевшего» ключа. `S.X.Y` (doc-плейсхолдер) исключён.

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 ошибок. dotnet test 69/69 pass (новый аудит-тест зелёный, реальных missing-ключей 0).

## 2026-07-17 — Fix: иконка кнопки «развернуть» на topbar превращалась в «??» после 1 нажатия

### Root cause
- `MainWindow.xaml.cs` `Window_StateChanged` (строки 557-566) в ОБОИХ ветках (Maximized и Normal) выставлял `BtnMaximize.Content = "??"`. Поэтому после любого переключения состояния окна иконка заменялась на два знака вопроса.

### Fixed
- Maximized → `Content = "🗗"` (восстановить), Normal → `Content = "🗖"` (развернуть). ToolTip по-прежнему локализован через S.CB.Restore / S.CB.Maximize. XAML-значение по умолчанию (MainWindow.xaml:115) = "🗖".

### Verified
- dotnet build 0 ошибок; логика ветвления корректна (иконка меняется между 🗖/🗗 по состоянию).

## 2026-07-17 — Fix: после импорта шаблона кнопка «сохранить .rmg.json» была недоступна

### Root cause
- После импорта вызывается `EnterEditMode` (MainWindow.xaml.cs), который выставляет `_generatedTemplate = template`, но НЕ обновлял состояние кнопки `BtnSaveGenerated` (сохраняет .rmg.json). Кнопка оставалась `IsEnabled=False` (дефолт из XAML), поэтому после импорта пользователь не мог сохранить шаблон как .rmg.json, хотя сама кнопка видима. После «Создать шаблон» кнопка включалась (GenerateTemplate вызывает UpdateOutdatedWarning), но после импорта — нет.

### Fixed
- `EnterEditMode` теперь вызывает `UpdateOutdatedWarning()` перед `SetEditModeUi(true)`, что выставляет `BtnSaveGenerated.IsEnabled = true` (т.к. `_generatedTemplate != null` и не outdated). Теперь кнопка «сохранить .rmg.json» доступна и после создания, и после импорта шаблона.
- Функционал сохранения (BtnSaveGenerated_Click) уже пишет `_generatedTemplate` в .rmg.json через SaveFileDialog; после «Изменить базовые настройки» `_generatedTemplate` синхронизируется с отредактированным `_editingTemplate` (EditBasicSettings, строка 2443), так что сохраняется актуальная версия.

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 ошибок. dotnet test 69/69 pass.

## 2026-07-18 � Fix: ����������� ��������������� .rmg.json �� �������� � ���� (��� ������ mandatoryContent/contentCountLimits/zoneLayouts, ������ displayWinCondition, ������ AuroraRMG)

### Root cause
- ������ �� �������� ���� `mandatory_content_*` / `content_limits_*` / `zone_layout_*` ��� � �� �������� ������ ������ ������ � �����. ���� ��������� (`TemplateGenerator.Generate`) ��������� ��� ����� ����� `BuildAllMandatoryContent` / `ZoneContentManager.BuildAllContentCountLimits` / `BuildZoneLayouts`, �� ���� ������� (H3T/�������� .rmg.json) �������� �� �������, � ���� ���� ��������� �� SID (����. `mandatory_content_spawns`, `content_limits_spawns`, `zone_layout_spawn`). ���� �� ����� ��������� ������ > ������ �� ��������.
- `displayWinCondition` � ��������������� �������� ������ (��� �������� ������� ���������� `win_condition_1/3/4/5/6`).
- �������� ����� editor-only ���� `AuroraRMG` � ���� (�� ������ ���������� ������ � ������ ���������, �� � ����������� .rmg.json).

### Fixed
- `GameContentCatalog.generated.cs`: ��������� �����-��������� ���������� `TryFindMandatoryContentAny(name)` / `TryFindContentCountLimitsAny(name)`, ���������� ��� �� ������� ����� ����� ���� ������������ ������� ��������.
- `Services/ContentManagement/CatalogContent.cs`: ��������� `TryGetMcByName(name)` / `TryGetClByName(name)`, ������������ ������������� �������� ����������� ���� (������ ������� �� �������� ������� ��������).
- `Services/TemplateGenerator.cs`: ��������� `NormalizeForExport(RmgTemplate)` (additive: ��������� ����������� top-level `mandatoryContent`/`contentCountLimits`/`zoneLayouts` �� SID, �� ������� ��������� ����; ������ `contentPools=[]`/`contentLists=[]`; �������� `displayWinCondition="win_condition_1"`; �� �������� ��� ����������� ����������� �����) � `StripAndNormalizeForSave(template, options)` (�������� null-�� `AuroraRMG`, �����������, ����������� � JSON, ��������������� `AuroraRMG` � ������). ����� �������� `DefaultZoneLayout(name)` (���������� fallback-������).
- ��� ����� ���������� ������ ���������� `StripAndNormalizeForSave`:
  - `MainWindow.BtnSaveGenerated_Click` (MainWindow.xaml.cs:3119)
  - `MainWindow.SaveEditingTemplate` (MainWindow.xaml.cs:2479)
  - `TemplateEditorWindow.BtnSave_Click` (TemplateEditorWindow.xaml.cs:4887)
- ����� �������������� ������ `StripAndNormalize` �� MainWindow.

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 ������. dotnet test 71/71 pass (��������� `ExportNormalizationTests`: `NormalizeForExport_FillsReferencedBlocks_StripsAuroraRmg`, `NormalizeForExport_KeepsExistingBlocks_AddsMissing`).

## 2026-07-18 � Fix: H3T-������ ����� �������������� ���� `content_pool_default_*` > ���� ������ ��� ��������� �����

### Root cause
- `Services/H3TParser.cs` `AssignDefaultPools(zone, layout)` (����� `zone_layout_sides`/`zone_layout_spawn`, ������ ~643-648) ����� ��������� � spawn/side-���� ���� `"content_pool_default_guarded"`, `"content_pool_default_unguarded"`, `"content_pool_default_resource"`.
- ��� SID �� ���������� � �� ���� � �� ���������� �� � ����� �� 71 �������� ������� (���������: `contentPools` ���� � ����; `content_pool_default_*` ������ ����� �� �����������, ����� ���� H3T-������� ������� � `1deaL.rmg.json` � `DiamondH3T.rmg.json`). ������� ������� ���������� template-����������� ����� (`content_pool_general_resources_*`, `content_pool_template_*`, `classic_template_pool_random_*`).
- ��� ���� (`Player.log`) ����� ������������ �������:
  ```
  [MapGen] Generating map: Template hash: 26d3a9cb17c3960c531250d25c1a0d23
  [Config Error] [MapGen] Couldn`t find content pool `content_pool_default_resource`.
  NullReferenceException ... bkb..ctor (Hex.MapGenerator.ContentPoolConfig ...)
    at bnr.myt (System.String a)   // ����� ���� �� SID
    at bld.Generate (System.String templateJson, ...)
  ```
  ������������� ��� > null > NRE � ������������ `ContentPoolConfig` > ��������� ����� �������� ����������� > ������ �� �����������.

### Fixed
- `Services/H3TParser.cs` (����� `zone_layout_sides`/`zone_layout_spawn`): �������� ��� SID �� �������� ��������� ���� `jebus_cross` (treasure-����� ��� ������������ �������� `content_pool_template_kerberos_*`):
  - `content_pool_default_guarded`   > `content_pool_guarded_objects_start_zone_jebus_cross`
  - `content_pool_default_unguarded` > `content_pool_unguarded_objects_start_zone_jebus_cross`
  - `content_pool_default_resource`  > `content_pool_resources_start_zone_jebus_cross`
  - `mandatory_content_spawns` / `content_limits_spawns` � ���� �� ����� ������� (����������� ����� �������) � ��������� ��� ����.
- `Services/TemplateGenerator.cs` `NormalizeForExport`: ��������� ��������� `RewriteInvalidContentPools(zone)` � �������������� ����� SID `content_pool_default_*` (guarded/unguarded/resources) � ��������������� `jebus_cross`-���. ��� ������������� ����� ��� ����������� ����� ����� (`1deaL.rmg.json`, `DiamondH3T.rmg.json`) ��� ��������� ���������� ����� `StripAndNormalizeForSave`.

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 ������. dotnet test 73/73 pass (��������� `ExportNormalizationTests`: `NormalizeForExport_RewritesInvalidContentPools`, `NormalizeForExport_KeepsValidContentPools`).
- ������������: ������������� `1deaL.rmg.json`/`DiamondH3T.rmg.json` ����� �������� (������������ ��������� ����) � ��������� � ���� ���� ���������� `Couldn`t find content pool 'content_pool_default_resource'`.
## 2026-07-18 - H3T spawn Player1 sequential + auto-PNG on save + remove Same owner + UI reorders

### Changed (H3T import - Services/H3TParser.cs)
- Human-start zones get zone_layout_spawn with Spawn=Player1 placeholder in ParseZone; real sequential Player assignment done in new post-processing pass AssignSequentialSpawns(zones) (called after the second-pass zone loop). It walks every zone_layout_spawn zone in document order and assigns Player1..PlayerN regardless of the H3T ownership field, and forces each spawn MainObject to RemoveGuardIfHasOwner=true and Placement=Uniform.
- Spawn MainObject in ParseZone now also sets Placement=Uniform.

### Changed (auto PNG preview on every .rmg.json save)
- MainWindow.BtnSaveGenerated_Click: PNG sidecar ALWAYS written next to the saved .rmg.json via TemplatePreviewPngWriter.GetSidecarPath (checkbox gate removed).
- MainWindow.SaveEditingTemplate: now also writes a PNG sidecar (MapTopology.Default).
- TemplateEditorWindow.BtnSave_Click: now also writes a PNG sidecar using editor topology _topology.

### Removed - tot zhe vladelts / Same owner feature
- TemplateEditorWindow.xaml.cs: removed Same owner AddCheckField, handler branches, and SyncOwnersInZone method.
- Models/Unfrozen/Zone.cs: removed SyncOwners property.
- Localization/Strings.cs: removed S.EC.SameOwner (RU + EN).

### Changed (inspector UI reorder - TemplateEditorWindow.xaml.cs)
- Tip obekta (object type) label + typeCombo moved BELOW the Ubrat okhranu pri vladenii (RemoveGuardIfHasOwner) checkbox.

### Changed (JSON template viewer - JsonPreviewWindow.xaml)
- JSON shablona window now opens maximized (WindowState=Maximized, ResizeMode=CanResize).

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 errors.
- dotnet test 73/73 pass.

## 2026-07-18 - Add dedicated Save button in edit mode (main window)

### Added
- MainWindow.xaml: new BtnSaveEditing button (Grid.Row=13, hidden by default) with content S.Btn.SaveEditing ("Сохранить шаблон (редактирование)"), mirroring the existing .rmg.json save flow.
- MainWindow.xaml.cs: BtnSaveEditing_Click -> SaveEditingTemplate() (which writes the .rmg.json + auto PNG sidecar).
- Localization: added S.Btn.SaveEditing (RU + EN).
- SetEditModeUi(edit): in edit mode BtnSaveEditing becomes visible and the generated-template BtnSaveGenerated is hidden (avoids ambiguity); both revert when leaving edit mode.

### Verified
- dotnet build (Debug, PublishTrimmed=false) 0 errors.

## 2026-07-18 - Fix H3T connection parsing (zones imported but never connected)

### Root cause
Services/H3TParser.cs resolved connection endpoints via zoneIds keyed by the zone Id field (field[28]). In HotA .h3t files the Id field is offset (every template starts at 2), while the connection Zone 1/Zone 2 fields are **1-based zone ordinals** (the order zones appear). So a connection index of 1 looked up zoneIds[1] (empty) and fell back to a non-existent "Zone-1". Every connection dangled -> the editor showed all zones as unconnected. This affected ALL h3t imports (only the content-pool tests exercised the parser before, so the latent bug was never caught).

Secondary bug: connections were parsed in the SAME loop that created zones, so a zone-row connection referencing a zone defined LATER in the file failed to resolve (the ordinal map was incomplete at that point).

### Fixed
- H3TParser.Parse: zoneIds is now keyed by **1-based zone ordinal** (ordinal counter, one per real zone row) instead of the Id field. ParseConnection is unchanged and now resolves to the real Zone-2..Zone-N names.
- Connection resolution moved to a **separate pass after all zones are created**, so references to later zones resolve correctly regardless of file order.
- Added NormalizeConnections(raw): structural, name-agnostic de-duplication that handles every H3T connection layout — connections embedded in each zone row AND/OR a trailing standalone block (sometimes duplicated / reversed):
  - drops self-loops (From == To);
  - merges by unordered zone pair {min,max} (so A->B and B->A are one edge and reverse-order duplicates collapse);
  - when several occurrences share a pair, keeps the most-specific one (Road==true, else a non-Default ConnectionType, else first) so duplicated trailing blocks never discard road/portal info.
  - stable sort by (From, To).

### Verified
- spider.h3t end-to-end: 24 zones, 32 connections, 0 dangling endpoints, 6 roads preserved (was 0 connected).
- New regression test H3TAndMapSizeTests.H3T_ConnectionsResolveByOrdinal_AndNormalizeDuplicates: synthetic structural .h3t (offset Id start=2, embedded + duplicated/reversed standalone connections) asserts 4 canonical edges, all endpoints are real zone names, no self-loops, no duplicate unordered pairs, and the only road-carrying edge keeps Road=true.
- dotnet build (Debug, PublishTrimmed=false) 0 errors. dotnet test 74/74 pass.

## 2026-07-18 - Import-time zone placement algorithms (spectral / MDS / hierarchical)

### User request
Add three new zone auto-placement algorithms for imported templates — (1) spectral analysis, (2) multidimensional scaling, (3) hierarchical radial-tree layout — and let the user choose which to use on import.

### Added
- Models/Generator/ImportLayoutAlgorithm.cs: enum ImportLayoutAlgorithm { Force, Spectral, Mds, Hierarchical }. Force is the historical Fruchterman-Reingold default; the other three are new.
- Services/LinearAlgebra.cs: dependency-free symmetric eigen-decomposition (cyclic Jacobi) + Floyd-Warshall all-pairs shortest path, used by the new algorithms.
- Services/TemplatePreviewPngWriter.cs:
  - New LayoutZonesSpectral — embeds via the two smallest-nonzero eigenvectors of the graph Laplacian (per connected component).
  - New LayoutZonesMds — classical MDS on the all-pairs shortest-path distance matrix (double-centred inner product, projected onto leading eigenvectors).
  - New LayoutZonesHierarchical — minimum spanning tree over the graph, laid out as a radial tree (root at centre, children fanned by depth); multiple components become separate radial trees.
  - Shared BuildDirectAdjacency (Direct connections only) + FitUnitToCanvas (normalise → canvas → overlap/fit pass) used by all three.
  - ComputeLayout / Render / DrawPreview / LayoutZones overloads that thread an ImportLayoutAlgorithm through; for Random/Balanced the chosen algorithm still overrides the generator stamps when non-default. CorrectOverlapsAndFit coincidence-push now uses a deterministic per-pair direction so symmetric/stacked points fan out instead of drifting together, and re-enforces the minimum separation after the final fit-shrink.
- ImageImportWindow.xaml(.cs): added a shared CmbLayout ComboBox (localised names, SelectedItem, Force default) shown in all import modes. On import the chosen algorithm is applied to the result via ApplyLayoutToResult, which stamps normalized [0,1]² GeneratorPosition hints (Y-flipped) onto every zone so the editor reproduces the exact layout through the existing pipeline.
- Localization S.EI.Layout (+ .Force/.Spectral/.Mds/.Hierarchical) in RU/EN (Localization/Strings.cs).

### Tests
- 	ests/.../UnitTest1.cs new ImportLayoutAlgorithmTests (MemberData over all 4 algorithms): AllZonesPositioned, NoOverlappingZones (>=4px separation), IsDeterministic, and ConnectedCloserThanUnconnected (restricted to the connection-aware local embeddings Force/Hierarchical, since Spectral/MDS preserve global distances, not local edge lengths).

### Verified
- spider.h3t (24 zones / 32 connections) lays out under all four algorithms with no dangling endpoints; build 0 errors; full test suite 88/88 pass.

### Notes / bug fixes found while implementing
- MDS double-centring had grand summed inside the i,j loop (n^4 blow-up) → all points collapsed; moved to a single pre-pass.
- CorrectOverlapsAndFit shrink step could crush separated zones back together; added a post-shrink re-enforcement pass.

## 2026-07-18 - Import-time layout: Centrality algorithm, ComboBox contrast, spectral crash fix

### User request
- Add a combined-centrality placement algorithm (PageRank + betweenness) for imported templates and wire it into the layout selector; lighten the import-window ComboBox for readability; fix a Spectral-method crash on multi-component templates.

### Added
- Models/Generator/ImportLayoutAlgorithm.cs: new enum value `Centrality`.
- Services/TemplatePreviewPngWriter.cs:
  - LayoutZonesCentrality — computes PageRank (power iteration, damping 0.85) + Brandes betweenness, combines and normalises both, places zones in concentric rings by combined-centrality tier (most central near centre) with angular spread by node degree.
  - Wired `Centrality` into the LayoutZones switch; added S.EI.Layout.Centrality RU/EN in Localization/Strings.cs.
  - ImageImportWindow.xaml.cs LayoutKeys now includes Centrality; ComboBox binding unchanged (SelectedItem).
- ImageImportWindow.xaml: CmbLayout lightened — Background="#EDEDF2", Foreground="#000000"; label Foreground="#CCC" for readability on the dark background.

### Fixed (spectral crash — root cause)
- LinearAlgebra.JacobiEigen eigenvector ordering: eigenvectors are now stored column-major as `vecs[eigenRank][node]` = eigenvector #order[rank] at node. LayoutZonesSpectral previously read `vecs[node][col]` (row/column scramble) which produced degenerate / NaN coordinates on some templates and crashed the WPF preview. Fixed to read `vecs[xRank][a]` / `vecs[yRank][a]`. Added an AreParallel guard plus degeneracy fallbacks (m==2 / collinear → synthesise a perpendicular 2nd axis; non-finite → fallback) and a FitUnitToCanvas guard that routes any non-finite/zero-span result to the Force-Directed layout so the renderer never receives NaN/Infinity.
- LayoutZonesSpectral `local` index array was sized `m` (component count) but indexed by global zone indices (which can exceed m for later components, e.g. a second size-2 component with global indices ≥2) → IndexOutOfRange on templates like twoPairs. Sized it to `n` (global zone count) instead.

### Tests
- ImportLayoutAlgorithmTests.AllAlgorithms now built from Enum.GetValues so Centrality is auto-included.
- Added DegenerateTemplates member data (chain / star / twoPairs / edge / complete K5) driving Spectral_NoNonFiniteCoordinates and Centrality_NoNonFiniteCoordinates, asserting every produced coordinate is finite (catches the index-out-of-range / eigenvector-scramble failures).

### Verified
- dotnet build (PublishTrimmed=false) 0 errors; dotnet test 101/101 pass (was 100 — +1 from auto-included Centrality in AllAlgorithms).

## 2026-07-18 - Network-logic layout, persisted algorithm, editor grid controls

### User request
- Add a "Сетевая логика" (Network logic) import-time zone-layout algorithm.
- Make the selected layout algorithm persist across import-window close/reopen.
- In the main editor, move the "Snap to grid" button below the "Hotkeys" button.
- Add a control to resize the canvas grid visualization (zoom the grid).

### Added — Network logic algorithm
- Models/Generator/ImportLayoutAlgorithm.cs: new enum value `Network`.
- Services/TemplatePreviewPngWriter.cs: new LayoutZonesNetwork — layered radial flow. Picks the highest-degree hub (ties broken by neighbours' degrees) as the centre, BFS-layers the rest into concentric rings; within a ring, nodes are ordered by local cluster strength (sum of neighbours' degrees) so tightly-coupled sub-networks stay grouped. Per connected component becomes its own cluster placed on a meta-circle. Routes through the LayoutZones switch (case Network) and reuses BuildDirectAdjacency + FitUnitToCanvas.
- Localization: S.EI.Layout.Network (RU "Сетевая логика" / EN "Network logic") in Strings.cs; wired into ImageImportWindow.LayoutKeys.

### Added — persisted algorithm
- Services/GameData/AppSettings.cs: new `ImportLayout` string property (JSON "importLayout", default "Force").
- ImageImportWindow.xaml.cs: ImageImportWindow_Loaded restores the last choice via ParseLayout (unknown value → Force); CmbLayout_SelectionChanged and BtnImport_Click persist the choice through SaveLayoutChoice → AppSettings.Current.ImportLayout. BtnClose (cancel) does not persist.

### Changed — main editor (TemplateEditorWindow)
- Grid snap button (BtnGridSnap) repositioned from floating-bottom to top-left, directly below the "Горячие клавиши" (Hotkeys) button (Margin 12,58).
- Grid visualization is now resizable: GridSize const → instance field `_gridSize` (20–200 px, step 10). Snap (SnapToGrid / SnapAllPositionsToGrid) now honours `_gridSize` so snapping tracks the drawn grid. Two toolbar buttons "Сетка +"/"Сетка −" (BtnGridBigger / BtnGridSmaller) adjust it and call RebuildGraph(); a TxtGridSize label shows the current cell size (S.EC.GridHint). DrawGrid now tiles at `_gridSize`.
- Localization: S.EC.GridBigger / S.EC.GridSmaller / S.EC.GridHint (RU/EN).

### Verified
- dotnet build (PublishTrimmed=false) 0 errors; dotnet test 104/104 pass (AllAlgorithms theory now auto-includes Network; the two NoNonFinite theory cases also cover Network).

## 2026-07-18 - Fix: crash on "Создать пул" (open ContentPoolCreatorWindow)

### Root cause (two XAML errors in ContentPoolCreatorWindow.xaml)
- `AvailableLists` ListBox set BOTH `DisplayMemberPath="Name"` and an `ItemTemplate` → WPF throws `InvalidOperationException: Cannot set both DisplayMemberPath and ItemTemplate`.
- The DataGrid columns referenced `StaticResource DataGridCellStyle`, but DataGrid columns are not in the visual tree, so `StaticResource` cannot resolve the merged theme dictionary at load → `XamlParseException`. The style had to be referenced via `DynamicResource` (resolved at runtime against Application.Resources).

### Fixed
- ContentPoolCreatorWindow.xaml: removed `DisplayMemberPath="Name"` from AvailableLists (ItemTemplate already binds `Name`).
- Themes/MedievalTheme.xaml: added the missing `DataGridCellStyle` style (TextBlock, BrushText, 11px, wrap).
- ContentPoolCreatorWindow.xaml: changed the two column `ElementStyle` references from `StaticResource` to `DynamicResource DataGridCellStyle`.

### Verified
- Added a temporary STA repro test that instantiates + shows the window; it now opens without throwing. Full suite 104/104 pass; build 0 errors.

## 2026-07-18 - ContentPoolCreatorWindow: white/black styling + button layout

### User request
- In "Создать новый пул" window make the available-list and the in-pool table white background with black text.
- Move "Add all" above "Remove all".
- Put Add/Remove as a horizontal pair (add left, remove right) ABOVE "Add all"; change their icons to + and −.

### Changed (ContentPoolCreatorWindow.xaml)
- AvailableLists ListBox: Background="White", Foreground="Black"; item TextBlock Foreground="Black".
- SelectedLists DataGrid: Background="White", Foreground="Black"; cells use a new local BlackCellText style, headers use new BlackHeaderText style (both defined in Window.Resources).
- Button StackPanel reordered: [ + | − ] (horizontal, Add left / Remove right) → AddAll → RemoveAll. The single add/remove buttons now show "+" and "−" (were ">" / "<").

## 2026-07-18 - Inspector gating + pool serialization schema

### User request
- Connection / NearZone fields must appear ONLY after Placement is set (already fixed for the primary MainObject; extend the same gating to additionally-added MainObjects).
- Created custom pools must serialize to the real GameData schema: one group per selected list, group `weight` = the weight written for that list at creation, `includeLists` = the list name, plus a hardcoded `valueDistribution` block.

### Fixed — MainObject argument gating (TemplateEditorWindow.xaml.cs)
- `RebuildMainObjectEditor`: extracted the placement-visibility logic into a local `UpdatePlacementVisibility(string? sel)` (sets placeArgsPanel / connArgsPanel / nearZonePanel + auto-assigns Connection args). `placeCombo.SelectionChanged` now delegates to it. `UpdateFieldVisibility` no longer force-uncollapses `connArgsPanel`/`nearZonePanel` (it only toggles the Placement section group by type, then calls `UpdatePlacementVisibility(mo.Placement)`), so Connection/NearZone stay collapsed until Placement = "Connection"/"NearZone".
- `RebuildAdditionalMainObjectsList`: applied the SAME fix (was the identical bug — its inner `UpdateFieldVisibility` did `foreach (field in placementFields) Visible`, force-showing Connection/NearZone). Now gates via `UpdatePlacementVisibility` using `placementFields` list indices (avoids capturing panels declared later in the method; the owner-combo handler runs before those declarations).

### Added — pool serialization (Models/GamePoolData.cs, TemplateEditorWindow.xaml.cs)
- Added `ValueDistribution` model class (`PriceBounds`, `Weights`) and `GamePool.ValueDistribution` property (was missing — created pools dropped the block).
- `CreatePool_Click` caller now builds ONE `PoolGroup` PER selected list: `Weight = <that list's weight at creation>`, `IncludeLists = [list name]` (previously a single group with `Content` item-sids). The pool also gets the hardcoded `ValueDistribution` `{ priceBounds:[3999,6999,12999,15999], weights:[6,8,10,6,0] }`.
- `SaveCustomPools` now serializes with `PropertyNamingPolicy = CamelCase` so on-disk JSON matches the real schema (name / groups / includeLists / valueDistribution / priceBounds / weights). Loader is already case-insensitive, so reload still binds.

### Tests
- New `CreatedGamePoolSerializationTests.CreatedPool_Serializes_ExpectedSchema`: builds a pool in the new shape, round-trips it (camelCase serialize + case-insensitive deserialize) and asserts name, valueDistribution (priceBounds + weights), per-group weight + includeLists, and that the JSON contains `valueDistribution`/`includeLists`.

### Verified
- dotnet build (PublishTrimmed=false) 0 errors; dotnet test 105/105 pass.

## 2026-07-18 — Localization/help cleanup + connection-properties hotkeys

### User request
- Change `S.Ed.Help` localization value to "Справка".
- Inside the help window remove both texts "Визуальный редактор зон" and "графовый редактор шаблонов...".
- For the "Копировать свойства связи" button add a default hotkey `ctrl+shift+Z`.
- For the "Вставить свойство" button add a default hotkey `ctrl+Z`.
- Add both buttons to the editable hotkeys.
- Add a hint inside both buttons on the bottom edge (hotkey label).

### Changed
- `Localization/Strings.cs`: `S.Ed.Help` → "Справка" (RU) / "Help" (EN). Added `S.EC.CopyConnProps`, `S.EC.PasteConnProps`, `S.EC.SelectConnFirst`, `S.EC.NothingCopied` (RU+EN).
- `EditorHelpWindow.xaml`: removed the title TextBlock ("▎Визуальный редактор зон") and the subtitle TextBlock ("Графовый редактор шаблонов RMG ...").
- `TemplateEditorWindow.xaml.cs`:
  - `GetDefaultHotkeys`: added `["CopyConnectionProps"]="Ctrl+Shift+Z"` and `["PasteConnectionProps"]="Ctrl+Z"`; freed `Ctrl+Z` from `CopyConnections` (now `""`) to avoid a clash.
  - `Window_KeyDown` switch: added `case "CopyConnectionProps"` and `case "PasteConnectionProps"`.
  - Refactored the inspector copy/paste buttons (`_selected is Connection`) to call new `CopyConnectionProps()` / `PasteConnectionProps()` methods; button content is now built via `MakeHotkeyButtonContent(label, action)` which shows a bottom-edge hotkey hint. `CopyConnectionProps` refreshes the inspector so the Paste button enables immediately.
- `Services/ConfigJson.cs`: `Hotkeys` default map gained `CopyConnectionProps`/`PasteConnectionProps`; `CopyConnections` default set to `""`.
- `HotkeySettingsWindow.xaml.cs`: both new actions added to the editable hotkey list (labels `L("S.EC.CopyConnProps")` / `L("S.EC.PasteConnProps")`, defaults `Ctrl+Shift+Z` / `Ctrl+Z`).
- `tests/.../UnitTest1.cs`: the STA `Editor_SingleUniformMainObject_GetsRoadToConnection` test now loads `Themes/MedievalTheme.xaml` into `Application.Current.Resources` before constructing `TemplateEditorWindow` (the window's XAML references `BrushPanel` etc., which are only defined in the app theme).

### Verified
- dotnet build (PublishTrimmed=false) 0 errors; dotnet test 107/107 pass.

## 2026-07-18 - Add visible "Справка" (help) button in zone editor + duplicate in main window

### User request
- In the visual zone editor the help (справка) button was not visible — add it next to the "−" (zoom-out) button.
- Add a duplicate of that help button in the main window (MainWindow).

### Added
- `TemplateEditorWindow.xaml`: added `BtnHelp` (Content="?", ToolTip `S.Ed.Help`) to the right-aligned toolbar, placed immediately next to `BtnZoomOut` ("−") before `BtnRelayout`. The `BtnHelp_Click` handler (opens `EditorHelpWindow`) already existed in code-behind but had no matching XAML button, so it was never reachable.
- `MainWindow.xaml`: added `BtnMainHelp` ("?") in the header file-actions stack, before the language buttons.
- `MainWindow.xaml.cs`: added `BtnMainHelp_Click` handler that opens `new EditorHelpWindow()`.
- `Localization/Strings.cs`: added `S.Ed.Help` = "Справка по редактору зон" (RU) / "Zone editor help" (EN).

### Verified
- dotnet build (PublishTrimmed=false) 0 errors.

## 2026-07-18 - Roads reach every MainObject on road-flagged connections (h3t import + editor)

### User request
- On h3t import, roads were NOT created for a MainObject if it was Uniform and the zone was connected by a road-flagged connection.
- Also: in the editor, a road set on a connection before the MainObject existed would never produce a road to the MainObject.
- Fix BOTH paths, connect ALL MainObjects (not just MainObject[0]), and relax the `Count <= 1` guard (Variant A: skip only empty zones).

### Changed — H3TParser.cs (h3t import)
- `AddRoadToZone` (`:924`) rewritten: for the given road connection it now adds a `MainObject[i] → Connection[connectionName]` road for EVERY MainObject in the zone (new `AddRoadToZoneMainObject` + `RoadExists` helpers), instead of only `MainObject[0]` for castle zones. Castle-less zones additionally keep the engine `Connection → Connection` star (via `BuildCastleLessRoad`) unchanged. This makes Uniform / Center / Connection / any-placed MOs all get a road reaching them — previously castle-less Uniform zones emitted only `Connection→Connection` and never referenced the MainObject.

### Changed — TemplateEditorWindow.xaml.cs (in-editor)
- `RebuildConnectionRoads` guard `:3242` changed from `MainObjects.Count <= 1` to `== 0` (Variant A): empty zones are still skipped, but a zone with exactly 1 MainObject now proceeds (fixes "set road first, add MainObject later" — road now appears).
- Added **Part C2** after Part C: for each road connection incident to the zone (`conn.Road == true` and `conn.From/To == zone.Name`) and for EACH MainObject index `i` whose `Placement != "Connection"` (Connection-placed MOs are still handled by the existing per-MO loop), calls `AddUniqueRoad(zone, "MainObject", [i], "Connection", [conn.Name])`. Mirrors the h3t parser so a Uniform MainObject gets a road to the connection.

### Changed — AssemblyInfo.cs
- `InternalsVisibleTo` corrected from `"Olden Era - Template Editor.Tests"` (spaces) to `"OldenEraTemplateEditor.Tests"` (actual test assembly name, no spaces) so the internal `RebuildGraph()` is reachable from the test project.

### Tests — UnitTest1.cs (new class `RoadToMainObjectTests`)
- `H3T_CastleLessZone_ConnectsAllUniformMainObjectsToRoad`: synthetic .h3t with a castle-less zone holding 3 Uniform `Spawn` MOs + a `Road=+` connection → asserts `Zone.Roads` contains `MainObject[0|1|2] → Connection` (all three). Uses a local `H3TParser_AutoGenerateRoads` helper that mirrors the (private) parser auto-generation via the public API.
- `Editor_SingleUniformMainObject_GetsRoadToConnection`: builds a template (1 Uniform MainObject + incident `Road=true` connection), constructs `TemplateEditorWindow` on an STA thread, calls internal `RebuildGraph()`, asserts a `MainObject[0] → Connection` road exists (covers the `== 0` guard + connect-all branch).

### Verified
- dotnet build (PublishTrimmed=false) 0 errors; dotnet test 105/105 pass (2 new).

## 2026-07-18 - Auto-generated roads must set Road.Type = "Stone"

### User request
- For auto-generated roads the `From` endpoint auto-fills `Args=["0"]` but its `Type` shows `?` (null); it should be `Stone`. So every generated `Road` must carry `Type = "Stone"` (the engine's road type) instead of leaving it null.

### Changed
- `Services/H3TParser.cs`: added `Type = "Stone"` to all three `new Road` sites — `AddRoadToZoneMainObject` (`MainObject[i]→Connection`, `:965`), and both branches of `BuildCastleLessRoad` (`Connection→Connection` self-loop `:997` and star spoke `:1011`). Previously only the editor set `Type="Stone"`, so h3t-imported roads serialized without a `type` field.
- `TemplateEditorWindow.xaml.cs`: added `Type = "Stone"` to both `new Road` branches of the editor's `BuildCastleLessRoad` (`public static`, `:3430` and `:3444`) so castle-less in-editor roads also carry the type. (The editor's `AddUniqueRoad` at `:3410` already set it.)
- `tests/.../UnitTest1.cs`: the `H3TParser_AutoGenerateRoads` mirror helper's `AddRoadToZoneMainObject` now sets `Type = "Stone"` too, matching the parser.

### Verified
- dotnet build (PublishTrimmed=false) 0 errors; dotnet test 105/105 pass.
