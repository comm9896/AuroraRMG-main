# Руководство разработчика AuroraRMG (CommFork)

> **Версия**: CommFork 2.8+ (форк AuroraRMG v1.6)  
> **Платформа**: .NET 10, WPF, Windows  
> **Язык**: C#  
> **IDE**: Visual Studio 2022+ / Rider

---

## Оглавление

1. [Архитектура приложения](#архитектура-приложения)
2. [Структура проекта](#структура-проекта)
3. [Модели данных](#модели-данных)
4. [Сервисы](#сервисы)
5. [Визуальный редактор](#визуальный-редактор)
6. [Генератор шаблонов](#генератор-шаблонов)
7. [Локализация](#локализация)
8. [Сериализация JSON](#сериализация-json)
9. [Тестирование](#тестирование)
10. [Сборка и публикация](#сборка-и-публикация)
11. [Соглашения по коду](#соглашения-по-коду)

---

## Архитектура приложения

```
┌──────────────────────────────────────────────────────────┐
│                      MainWindow                          │
│  (Simple Mode / Advanced / Bans / Bonuses / Presets)     │
├──────────────────────┬───────────────────────────────────┤
│                      │                                   │
│  TemplateGenerator   │  TemplateEditorWindow             │
│  (генерация)         │  (визуальный редактор графа)      │
│                      │       │                           │
│  RandomTemplateBuilder│  ├── ConnectionManagerWindow     │
│  (Simple Mode)       │  ├── ConnectionSettingsWindow     │
│                      │  ├── JsonPreviewWindow            │
│  H3TParser           │  ├── OrientationWindow            │
│  (импорт HotA)       │  ├── MirrorSettingsWindow         │
│                      │  ├── EditorHelpWindow             │
│  ImageAnalyzer       │  ├── ContentPoolViewerWindow      │
│  (анализ рисунков)   │  └── ContentPoolCreatorWindow     │
│                      │                                   │
└──────────────────────┴───────────────────────────────────┘
         │                         │
         ▼                         ▼
┌──────────────────────────────────────────────────────────┐
│                   Модели (Models/)                       │
│  RmgTemplate / Zone / Connection / MainObject / GameRules│
└──────────────────────────────────────────────────────────┘
         │                         │
         ▼                         ▼
┌──────────────────────────────────────────────────────────┐
│                   Сервисы (Services/)                    │
│  JsonExport / H3TParser / GameCatalog / Localization     │
│  ZoneContentManager / ZoneGraphValidator / UpdateService │
└──────────────────────────────────────────────────────────┘
```

### Поток данных

```
GeneratorSettings ──► TemplateGenerator ──► RmgTemplate
       ▲                      │                   │
       │                      ▼                   ▼
  MainWindow ────► TemplateEditorWindow ──► .rmg.json
                         │
                         ▼
              JsonPreviewWindow (round-trip)
```

---

## Структура проекта

```
G:\table\AuroraRMG-main\
├── Olden Era - Template Editor\     # Основной проект WPF
│   ├── App.xaml / App.xaml.cs       # Точка входа, инициализация
│   ├── MainWindow.xaml / .cs        # Главное окно (настройки генерации)
│   ├── MainWindow.Shoot.cs          # Режим скриншотов
│   ├── TemplateEditorWindow.xaml/.cs # Визуальный редактор зон (~4900 строк)
│   │
│   ├── Models\                      # Модели данных
│   │   ├── Unfrozen\                # Модели .rmg.json
│   │   │   ├── RmgTemplate.cs       # Корневой контейнер шаблона
│   │   │   ├── Zone.cs              # Зона (layout, биомы, MO, дороги)
│   │   │   ├── Miscellaneous.cs     # ValueOverride, GlobalBans, GameRules
│   │   │   ├── KnownValues.cs       # Все константы и справочники
│   │   │   └── AuroraRmgCoords.cs   # Координаты зон для редактора
│   │   ├── Generator\               # Параметры генерации
│   │   │   ├── GeneratorSettings.cs # Все настройки генерации
│   │   │   ├── SettingsFile.cs      # Файл настроек .oetgs
│   │   │   ├── Presets.cs           # 35+ встроенных пресетов
│   │   │   └── ImportLayoutAlgorithm.cs # Алгоритм размещения зон
│   │   ├── GamePoolData.cs          # Данные пулов контента
│   │   └── Generated\               # Авто-сгенерированные файлы
│   │
│   ├── Services\                    # Сервисы
│   │   ├── H3TParser.cs             # Парсер .h3t (HotA)
│   │   ├── TemplateGenerator.cs     # Генератор RmgTemplate (~3665 строк)
│   │   ├── JsonExport.cs            # Сериализация JSON
│   │   ├── ImageAnalyzer.cs         # Анализ изображений схем
│   │   ├── LinearAlgebra.cs         # Математика для графа
│   │   ├── TemplatePreviewPngWriter.cs # Рендер PNG предпросмотра
│   │   ├── ZoneGraphValidator.cs    # Валидация графа зон
│   │   ├── Generation\              # Движок случайной генерации
│   │   │   ├── RandomTemplateBuilder.cs # Simple Mode (детерминированный)
│   │   │   ├── Biomes.cs            # Биомы и террейн
│   │   │   └── GuardReaction.cs     # Реакция охраны
│   │   ├── GameData\               # Загрузка игровых данных
│   │   │   ├── GameCatalog.cs       # Каталог героев
│   │   │   ├── GameCatalogService.cs# Сервис загрузки Core.zip
│   │   │   ├── AppSettings.cs       # Настройки приложения
│   │   │   ├── IconResolver.cs      # Разрешение иконок
│   │   │   └── ObjectNameResolver.cs# Разрешение имён объектов
│   │   ├── ContentManagement\       # Управление контентом
│   │   │   ├── ZoneContentManager.cs# Менеджер контента зон
│   │   │   ├── ZoneContent.cs       # Контент зон (T2-T5)
│   │   │   ├── CatalogContent.cs    # Каталог контента
│   │   │   ├── ContentPresets.cs    # Пресеты контента
│   │   │   ├── RulePresets.cs       # Пресеты правил
│   │   │   └── SidMapping.cs        # Маппинг SID
│   │   ├── Localization\            # Локализация
│   │   │   └── LocalizationManager.cs # Менеджер языков
│   │   └── Update\                  # Обновления
│   │       └── UpdateService.cs     # Сервис обновлений
│   │
│   ├── Localization\                # Таблицы локализации
│   │   └── Strings.cs               # 546+ ключей (RU + EN)
│   │
│   ├── Resources\GameData\          # Встроенные игровые данные
│   │   └── content_pools\           # Пулы контента для шаблонов
│   │
│   ├── Themes\                      # WPF темы
│   │   └── MedievalTheme.xaml       # Тема «Средневековье»
│   │
│   └── *.xaml / *.xaml.cs           # Окна (21 файл)
│
├── tests\OldenEraTemplateEditor.Tests\  # Тесты
│   ├── UnitTest1.cs                 # Основные тесты
│   ├── DbgMdsTest.cs                # Тест генерации шаблонов
│   └── OldenEraTemplateEditor.Tests.csproj
│
├── scripts\GenCatalog\              # Генератор каталога
├── release\                         # Готовая сборка
├── build.bat / build.ps1            # Скрипты сборки
├── global.json                      # Версия SDK
└── GameData.json                    # Игровой каталог (embedded)
```

---

## Модели данных

### RmgTemplate — корневой контейнер

```csharp
public class RmgTemplate
{
    public AuroraRmgCoords? AuroraRmg { get; set; }  // редакторские координаты
    public string Name { get; set; }                  // имя шаблона
    public string? GameMode { get; set; }              // Classic / SingleHero
    public int SizeX, SizeZ { get; set; }              // размер карты
    public GameRules? GameRules { get; set; }          // правила игры
    public List<ValueOverride>? ValueOverrides { get; set; }
    public GlobalBans? GlobalBans { get; set; }
    public List<Variant>? Variants { get; set; }       // варианты (зоны + связи)
    public List<object>? ContentPools { get; set; }
    public List<object>? ContentLists { get; set; }
    public List<MandatoryContentGroup>? MandatoryContent { get; set; }
    public List<ContentCountLimit>? ContentCountLimits { get; set; }
}
```

### Zone — зона

```csharp
public class Zone
{
    public string Name { get; set; }
    public double? Size { get; set; }
    public string? Layout { get; set; }        // zone_layout_*
    public double? GuardCutoffValue { get; set; }
    public double? GuardRandomization { get; set; }
    public double? GuardMultiplier { get; set; }
    public double? GuardWeeklyIncrement { get; set; }
    public List<int>? GuardReactionDistribution { get; set; }
    public List<string>? GuardedContentPool { get; set; }
    public List<string>? UnguardedContentPool { get; set; }
    public List<string>? ResourcesContentPool { get; set; }
    public List<MainObject>? MainObjects { get; set; }  // города, спавны
    public BiomeSelector? ZoneBiome { get; set; }
    public BiomeSelector? ContentBiome { get; set; }
    public BiomeSelector? MetaObjectsBiome { get; set; }
    public List<Road>? Roads { get; set; }
}
```

### Key Design Decisions

1. **AuroraRmgCoords** — редакторские координаты зон (position на холсте). Не являются частью игрового формата, сериализуются первым полем для удобства просмотра.

2. **Variant** — контейнер для набора зон + связей. Шаблон может иметь несколько вариантов (например, для разных размеров карты).

3. **List<object>** — ContentPools и ContentLists сериализуются как `object` (игровой формат не типизирован строго). Обработка через кастомные конвертеры.

---

## Сервисы

### JsonExport — сериализация

Главная проблема: игровой JSON-ридер **не поддерживает** `\uXXXX` escape-последовательности и **не поддерживает** строгую типизацию массивов.

```csharp
public static readonly JsonSerializerOptions Options = new()
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,  // UTF-8 без экранов
    Converters =
    {
        new StringOrStringArrayConverter(),     // одно значение → массив
        new SingleObjectOrArrayConverterFactory(), // один объект → массив
    },
};
```

**StringOrStringArrayConverter**: читает список строк как одно значение (строка/число) или массив. Пишет всегда как массив.

**SingleObjectOrArrayConverterFactory**: читает список объектов как один объект (например, `"bonuses": {...}`) или массив.

### H3TParser — импорт HotA

Парсит .h3t формат (табулированные значения, 140+ полей на строку):

- Строка 1: зоны (type, name, size, treasure, monsters, etc.)
- Строка 2+: связи (Zone1 → Zone2, guard value, road flag)
- Маппинг кодов зон (0-12) → zone_layout_* имена
- Декодирование кодов размера карты

### TemplateGenerator — генерация

Главный алгоритм (~3665 строк):

1. **BuildNeutralZonePlan** — план нейтральных зон
2. **BuildTopologyAdjacency** — топология графа
3. **LayoutZonesInSpace** — размещение зон (концентрические кольца / random)
4. **BuildConnections** — создание связей между зонами
5. **BuildGameRules** — правила игры
6. **BuildMandatoryContent** — обязательный контент

Поддерживаемые топологии: Ring, Balanced, Hub & Spoke, Chain, Lanes, Random, SharedWeb.

### RandomTemplateBuilder — Simple Mode

Детерминированный генератор настроек из высокоуровневых опций:

- Фиксированный seed → всегда одинаковый результат
- Случайные параметры в безопасных диапазонах
- Сигнатурные достопримечательности (landmarks) на neutral зонах

### ImageAnalyzer — анализ изображений

Использует WPF WriteableBitmap для обнаружения зон на изображении:

- Анализ цветовых кластеров
- Определение связей по линиям
- Дефолтные пулы контента на основе layout зоны
- Коэффициенты распределения контента (на основе 68 официальных шаблонов, 1014 зон)

### ZoneGraphValidator — валидация

Чистая функция для проверки графа зон:

- Пустые имена зон
- Дубликаты имён
- Висячие соединения (несуществующие зоны)
- Самопетли (Zone → Zone)
- Дубликаты имён связей
- Изолированные зоны

---

## Визуальный редактор

### Архитектура TemplateEditorWindow

**Основные структуры данных:**

```csharp
private readonly Dictionary<string, Point>   _positions;     // позиции зон
private readonly Dictionary<string, Shape>    _nodeShapes;   // визуальные узлы
private readonly Dictionary<string, FrameworkElement> _nodeLabels; // метки
private readonly List<(Line Line, Connection Conn)> _edges;  // рёбра
private readonly Dictionary<Connection, double> _edgeFanOffsets; // fan-out
```

**Отрисовка графа (RebuildGraph):**

1. Очистка всех элементов с холста
2. Расчёт позиций зон (из AuroraRmgCoords или LayoutZonesRing)
3. Отрисовка соединений (линии от центра к центру с fan-out)
4. Отрисовка узлов (квадраты зон поверх линий)
5. Отрисовка меток и легенды
6. Синхронизация дорог и Placement Args

**Fan-out рёбер:**
```
offset = (groupIndex - (groupCount - 1) / 2.0) * fanSpread;
offset = Clamp(offset, -maxPerp, maxPerp);
где maxPerp = min(rFrom, rTo) * 0.85
```

**Система зеркального копирования:**

```csharp
private bool _mirrorMode;
private bool _mirrorProperties;   // копировать свойства на зеркальную зону
private bool _mirrorConnections;  // дублировать соединения
private readonly Dictionary<string, string> _mirrorMap; // zone → twin
```

### Инспектор свойств

**BuildInspector()** — перестраивает панель свойств для выбранной зоны:

1. Основные параметры (имя, размер, layout)
2. Биомы (zoneBiome, contentBiome, metaObjectsBiome)
3. Главные объекты (основной + дополнительные)
4. Охрана (cutoff, randomization, multiplier, increment)
5. Контент (пулы guarded/unguarded/resources)
6. Дороги (список дорог)
7. Пул контента (зонные пулы)

### Placement Args UI

Три режима размещения:

| Режим | Описание | UI |
|-------|----------|-----|
| **Center** | В центре зоны | Нет аргументов |
| **Connection** | На линии связи | ComboBox выбора связи |
| **NearZone** | Рядом с зоной | Dropdown выбора зоны |
| **Uniform** | Равномерно | Текстовое поле для аргументов |

### Система дорог

```csharp
// Авто-генерация дорог для Connection MO
RebuildConnectionRoads():
  для всех зон:
    для каждого MainObject[i > 0] с Placement = "Connection":
      создай MainObject[0] → Connection[name]
      создай MainObject[i] → Connection[name]

// Синхронизация флагов дорог
SyncConnectionRoadFlags():
  для всех дорог с Type = "Connection":
    установи conn.Road = true

// Коррекция при удалении MO
AdjustRoadIndicesAfterRemoval(Zone, int removedIndex):
  удали дороги с участием MainObject[removedIndex]
  декрементируй индексы > removedIndex
```

---

## Генератор шаблонов

### Алгоритм TemplateGenerator.Generate()

```
1. Разрешение базового шаблона (каталог контента)
2. Инициализация RNG (seed / time-based)
3. BuildNeutralZonePlan — расчёт нейтральных зон
4. BuildTopologyAdjacency — матрица смежности
5. LayoutZonesInSpace — физическое размещение
6. BuildConnections — связи между зонами
7. BuildGameRules — правила игры
8. BuildMandatoryContent — контент зон
9. Сборка финального RmgTemplate
```

### Топологии

**Ring:** зоны на кольце, центр внутри, спавны равномерно распределены.

**Balanced:** смешанная топология с зонами разных типов, спавны и сокровищницы.

**Hub & Spoke:** все зоны соединены с центральным хабом, игроки не граничат напрямую.

**Chain:** линейная цепь зон от игрока к игроку.

**Lanes:** параллельные полосы, каждая своя иерархия зон.

**Random:** случайный граф с соблюдением базовых инвариантов.

### Содержимое зоны по тиражам

| Тираж | Зоны | Контент |
|-------|------|---------|
| T2 | Side, Bronze | Базовые предметы, слабая охрана |
| T3 | Treasure, Silver | Средние предметы, банки юнитов |
| T4 | Super Treasure, Gold | Эпические предметы, сильная охрана |
| T5 | Center, Hub | Легендарный контент, драконы |

---

## Локализация

### Архитектура

```csharp
public sealed class LocalizationManager
{
    public AppLanguage CurrentLanguage { get; private set; }
    public string Get(string key, params object[] args);
    public static string T(string key, params object[] args);
    public void SetLanguage(AppLanguage lang);
    public void Toggle();
    public event EventHandler? LanguageChanged;
}
```

**Таблицы**: `Localization/Strings.cs` содержит два статических словаря `Ru` и `En` с 546+ ключами каждая.

**XAML**: локализация через `{DynamicResource S.X.Y}` (DynamicResource, а не StaticResource — позволяет переключать язык в рантайме).

**Code-behind**: через `L("S.X.Y")` или `LocalizationManager.T("S.X.Y")`.

**Инициализация** (App.xaml.cs):
1. CLI параметр `--lang`
2. Сохранённая настройка
3. Язык системы Windows

---

## Сериализация JSON

### Особенности формата .rmg.json

- **UTF-8 без BOM** — `File.WriteAllText` + `UnsafeRelaxedJsonEscaping`
- **Nullable поля с WhenWritingNull** — отсутствующие поля не сериализуются
- **Lenient чтение** — StringOrStringArrayConverter, SingleObjectOrArrayConverterFactory
- **Порядок полей** — `AuroraRMG` блок пишется первым, `mandatoryContent`/`contentCountLimits` последними

### Кастомные конвертеры

```csharp
// Читает: ["a", "b"] или "a" или 42 → ["42"]
public sealed class StringOrStringArrayConverter : JsonConverter<List<string>>

// Читает: [{...}, {...}] или {...} → [{...}]
public sealed class SingleObjectOrArrayConverterFactory : JsonConverterFactory
```

---

## Тестирование

### UnitTest1.cs

Основной файл тестов. Тестирует:

- **Сериализация/десериализация** RmgTemplate
- **H3TParser** — парсинг корректных и битых файлов
- **TemplateGenerator** — генерация с разными параметрами
- **ZoneGraphValidator** — валидация графа
- **KnownValues** — консистентность констант
- **RandomTemplateBuilder** — детерминизм по seed
- **Connection creation** — уникальность имён, флаги дорог
- **Локализация** — полнота ключей

### DbgMdsTest.cs

Генерация всех встроенных пресетов для отладки. Использует `--gen-readymaps` режим.

### Тестовый проект

- .NET 10, Windows
- Пакеты: NUnit, FluentAssertions
- Parallelizable (через [ThreadStatic] RNG)

---

## Сборка и публикация

### Параметры .csproj

```xml
<TargetFramework>net10.0-windows</TargetFramework>
<UseWPF>true</UseWPF>
<AssemblyName>OldenEraTemplateGenerator</AssemblyName>
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

### Скрипты сборки

```bash
# build.bat (cmd)
build.bat Release    # или Debug
# build.ps1 (PowerShell)
.\build.ps1 -Configuration Release
```

Результат: `build/Release/OldenEraTemplateGenerator.exe` (single-file, ~30 MB с компрессией).

### Embedded Resources

`GameData.json` — встроенный ресурс с каталогом игровых объектов.
`Resources/GameData/` — 78 JSON файлов с пулами контента как Embedded Resources.

---

## Соглашения по коду

### ComboBox Usage

```csharp
// Всегда используйте SelectedItem, НЕ Text
combo.SelectedItem = value;
var value = combo.SelectedItem as string;
```

Глобальный стиль в App.xaml синхронизирует Text с SelectedItem через `ComboBoxBehavior.SyncTextWithSelectedItem`.

### Изменения

Все изменения кода фиксируются в `AGENTS_changes.md` с:
- Датой и временем
- Описанием запроса пользователя
- Списком изменений

### MCP: code-review-graph

Проект использует граф знаний для анализа кода. Рекомендуемый порядок исследования:

1. `query_graph` или `semantic_search_nodes` (поиск сущностей)
2. `get_impact_radius` (понимание влияния изменений)
3. `get_architecture_overview` (обзор архитектуры)
4. Только затем — чтение файлов

---

## Ключевые окна

| Окно | Файл | Назначение |
|------|------|------------|
| MainWindow | MainWindow.xaml/.cs | ~3546 строк — главное окно с настройками |
| TemplateEditorWindow | TemplateEditorWindow.xaml/.cs | ~4935 строк — визуальный редактор графа |
| ConnectionManagerWindow | ConnectionManagerWindow.xaml/.cs | Таблица всех связей |
| ConnectionSettingsWindow | ConnectionSettingsWindow.xaml/.cs | Редактор одной связи |
| JsonPreviewWindow | JsonPreviewWindow.xaml/.cs | JSON редактор |
| OrientationWindow | OrientationWindow.xaml/.cs | Настройки ориентации/границ |
| MirrorSettingsWindow | MirrorSettingsWindow.xaml/.cs | Настройки зеркального режима |
| ImageImportWindow | ImageImportWindow.xaml/.cs | Импорт изображения шаблона |
| BonusPickerWindow | BonusPickerWindow.xaml/.cs | Выбор бонусов |
| SpellPickerWindow | SpellPickerWindow.xaml/.cs | Выбор заклинаний |
| ItemPickerWindow | ItemPickerWindow.xaml/.cs | Выбор предметов |
| ContentPoolCreatorWindow | ContentPoolCreatorWindow.xaml/.cs | Создание пулов контента |
| ContentPoolViewerWindow | ContentPoolViewerWindow.xaml/.cs | Просмотр пулов контента |

---

## Расширение функциональности

### Добавление нового типа зоны

1. Добавьте layout имя в `KnownValues.ZoneLayouts`
2. Добавьте цвета в `TemplateEditorWindow` (SpawnFill, SpawnBorder и т.д.)
3. Добавьте маппинг layout → цвет в `GetZoneColors()`
4. Добавьте в легенду `KnownValues.ZoneLayoutLegend`
5. Добавьте pools в `ImageAnalyzer.DefaultPools`

### Добавление новой топологии

1. Добавьте значение в `MapTopology` enum (GeneratorSettings.cs)
2. Реализуйте логику в `TemplateGenerator`:
   - `BuildTopologyAdjacency` — матрица смежности
   - `LayoutZonesInSpace` — размещение зон
3. Добавьте опцию в `MainWindow` (TopologyOptions)

### Добавление новой локализации

1. Добавьте ключ в `Strings.Ru` и `Strings.En`
2. Используйте `{DynamicResource S.X.Y}` в XAML
3. Используйте `L("S.X.Y")` в code-behind
4. Добавьте проверку в тест `StringsTest()`