using System.Collections.Generic;
using System.Linq;

namespace OldenEraTemplateEditor.Models
{
    /// <summary>
    /// Dictionary of .rmg.json field descriptions sourced from oldeneramaps.com/reference.
    /// Each entry: (FieldName, Category, DescriptionRU, DescriptionEN).
    /// </summary>
    public static class RmgFieldDictionary
    {
        public record FieldEntry(string Field, string Category, string DescRu, string DescEn);

        public static readonly List<FieldEntry> Entries = new()
        {
            // ── Template Root ────────────────────────────────────────────
            new("name",             "Template",     "Имя шаблона, отображаемое в игре. Используйте понятное название.", "Template name shown in game. Use a clear map-style name."),
            new("sizeX",            "Template",     "Ширина карты в тайлах. Больше — дольше матч, больше зон, медленнее игра.", "Map width and height in tiles. Bigger numbers usually mean longer travel, more room for zones, and a slower match."),
            new("sizeZ",            "Template",     "Высота карты в тайлах.", "Map height in tiles."),
            new("description",      "Template",     "Необязательные заметки о шаблоне. Используется для описания происхождения.", "Optional notes about the template. The app uses this to describe how a generated template was made."),
            new("gameMode",         "Template",     "Режим игры. Официальные быстрые форматы используют SingleHero.", "Game mode label. Official fast formats can use values such as SingleHero."),
            new("displayWinCondition", "Template",  "Бейдж условия победы в списке шаблонов (classic, arena, cityHold и т.д.).", "The win-condition badge the game shows in the template list, such as classic play, arena, or City Hold."),
            new("variants",         "Template",     "План карты: расположение зон, связи, правила границ. Приложение экспортирует один вариант.", "The actual map plan: where areas are, how they connect, and what border/orientation rules apply. This app currently exports one variant."),
            new("zoneLayouts",      "Template",     "Повторяемые рецепты форм зон. Зона ссылается на такой рецепт вместо инлайн-описания.", "Reusable shape recipes for zones. A zone points to one of these instead of writing all layout details inline."),
            new("mandatoryContent", "Template",     "Именованные пакеты обязательных объектов (города, рудники, награды).", "Named bundles of important objects the zone should include, such as towns, mines, or required rewards."),
            new("contentCountLimits", "Template",   "Именованные ограничения на количество объектов каждого типа.", "Named caps that stop a zone from getting too many copies of certain object types."),
            new("contentPools",     "Template",     "Продвинутые блоки списков объектов. Обычно пустые.", "Advanced object-list blocks. The current generator usually leaves them empty unless a future feature needs them."),
            new("valueOverrides",   "Template",     "Продвинутые настройки именованных значений (сила охраны и т.д.).", "Advanced adjustments for named game values, often used to tune guard or object values."),
            new("globalBans",       "Template",     "Списки героев, предметов или магии, которые не должны появляться.", "Lists of heroes, items, or magic that should not appear in the generated match."),

            // ── gameRules ────────────────────────────────────────────────
            new("heroCountMin",     "GameRules",    "Минимальное количество героев игрока.", "The fewest heroes a player can have."),
            new("heroCountMax",     "GameRules",    "Максимальное количество героев игрока. Увеличение поддерживает больше разведки.", "The most heroes a player can have. Raising the max supports more scouting and expansion."),
            new("heroCountIncrement", "GameRules",  "На сколько замки повышают лимит героев. Больше = больше награда за города.", "How much owning castles can raise the hero cap. Higher values reward taking extra towns."),
            new("heroHireBan",      "GameRules",    "Когда true — запрещает нанимать доп. героев. Делает разведку строже.", "When true, blocks hiring extra heroes. This makes scouting and chaining much stricter."),
            new("encounterHoles",   "GameRules",    "Включает поведение encounter holes для нейтральных боёв.", "Enables encounter-hole behavior for neutral fights when the template uses it."),
            new("tournamentRules",  "GameRules",    "Отмечает турнирные правила. Поля дней и очков живут в winConditions.", "Marks tournament-style rules as active. The actual day and score fields live under winConditions."),
            new("valueOverrides",   "GameRules",    "Продвинутые настройки именованных значений (сила охраны и т.д.).", "Advanced adjustments for named game values, often used to tune guard or object values."),

            // ── winConditions ────────────────────────────────────────────
            new("classic",          "WinConditions", "Обычная победа: победить соперников и захватить контроль.", "Normal Heroes-style victory: defeat opponents and take control in the usual way."),
            new("lostStartCity",    "WinConditions", "Управляет, может ли потеря стартового города выбить игрока.", "Controls whether losing the starting city can eliminate a player."),
            new("lostStartCityDay", "WinConditions", "День, начиная с которого потеря стартового города выбивает игрока.", "After what day losing the starting city eliminates a player."),
            new("lostStartHero",    "WinConditions", "Управляет, может ли потеря стартового героя выбить игрока.", "Controls whether losing the starting hero can eliminate a player."),
            new("cityHold",         "WinConditions", "Включает режим City Hold.", "Turns on City Hold mode."),
            new("cityHoldDays",     "WinConditions", "Сколько дней нужно удерживать отмеченный город.", "How many days the marked city must be held."),
            new("gladiatorArena*",  "WinConditions", "Тайминги арены: регистрация, задержка, дни боя, выбор чемпиона.", "Arena timing fields: registration, fight start delay, fight day count, and champion selection behavior."),
            new("tournament*",      "WinConditions", "Турнирные поля: активные дни, дни объявления, очки для победы.", "Tournament fields: active days, announcement days, points needed to win, and whether armies are saved."),
            new("desertionDay",     "WinConditions", "День начала давления быстрого формата.", "When desertion starts in fast-format templates."),
            new("desertionValue",   "WinConditions", "Порог значения для давления быстрого формата.", "The value threshold attached to desertion in fast-format templates."),

            // ── orientation ──────────────────────────────────────────────
            new("orientation.mode", "Orientation",  "Как весь граф зон вращается перед генерацией.", "How the whole zone graph is rotated or arranged before the map is generated."),
            new("orientation.zeroAngleZone", "Orientation", "Зона-якорь вращения. Полезно, когда одна область должна смотреть в предсказуемом направлении.", "The zone used as the rotation anchor. Useful when one area should face a predictable direction."),
            new("orientation.baseAngleMin", "Orientation", "Минимальный допустимый начальный угол.", "Minimum allowed starting angle for the layout."),
            new("orientation.baseAngleMax", "Orientation", "Максимальный допустимый начальный угол.", "Maximum allowed starting angle for the layout."),
            new("orientation.randomAngleAmplitude", "Orientation", "Сколько случайного вращения можно добавить.", "How much random rotation can be added."),
            new("orientation.randomAngleStep", "Orientation", "Шаг случайности вращения.", "The step size used for that randomness."),

            // ── border ───────────────────────────────────────────────────
            new("border.cornerRadius", "Border",    "Насколько закруглена игровая граница карты.", "How rounded the playable map edge is."),
            new("border.obstaclesWidth", "Border",  "Толщина полосы препятствий вокруг игровой области.", "Thickness of obstacle bands around the playable area."),
            new("border.waterWidth", "Border",       "Толщина водной полосы вокруг игровой области.", "Thickness of water bands around the playable area."),

            // ── Zone Fields ──────────────────────────────────────────────
            new("zone.name",        "Zone",         "Уникальное имя зоны. Связи и правила размещения ссылаются на этот текст.", "Unique zone name. Connections and placement rules refer to this exact text."),
            new("zone.size",        "Zone",         "Относительный размер зоны. Больше — больше пространства и контента.", "Relative zone size. Larger zones take more map space and can hold more content."),
            new("zone.layout",      "Zone",         "Форма и стиль содержимого зоны (spawn, treasure, center и т.д.).", "The zone's shape and internal road/content style, such as spawn-like or center-like layout."),
            new("zone.guardCutoffValue", "Zone",    "Встречи ниже этого значения могут обрабатываться иначе.", "Small encounters below this value may be treated differently by the generator."),
            new("zone.guardRandomization", "Zone",  "Насколько сила охраны может варьироваться.", "How much guard strength can vary from the listed value."),
            new("zone.guardMultiplier", "Zone",      "Широкий множитель сложности для охранённого контента.", "Broad difficulty multiplier for guarded content in the zone."),
            new("zone.guardWeeklyIncrement", "Zone", "Насколько охрана растёт каждую неделю.", "How much guards grow over time."),
            new("zone.guardReactionDistribution", "Zone", "Веса реакции охраны. Продвинутая настройка боевого поведения.", "Weights for how guards react. Treat this as advanced combat behavior tuning."),
            new("zone.diplomacyModifier", "Zone",    "Настраивает поведение дипломатии для встреч в зоне.", "Adjusts diplomacy behavior for encounters in the zone."),
            new("zone.guardedContentPool", "Zone",   "Разрешённые случайные пулы охранённых наград/объектов.", "Allowed random guarded reward/object pools for this zone."),
            new("zone.unguardedContentPool", "Zone",  "Разрешённые случайные пулы неохранённых наград/объектов.", "Allowed random unguarded reward/object pools for this zone."),
            new("zone.resourcesContentPool", "Zone",  "Разрешённые пулы ресурсов.", "Allowed resource pickup pools for this zone."),
            new("zone.guardedContentValue", "Zone",   "Общий бюджет для охранённых объектов.", "Total budget for guarded objects in the zone."),
            new("zone.guardedContentValuePerArea", "Zone", "Бюджет для охранённых объектов на единицу площади.", "Area-based budget for guarded objects."),
            new("zone.unguardedContentValue", "Zone", "Общий бюджет для неохранённых объектов.", "Total budget for free or lightly protected objects in the zone."),
            new("zone.unguardedContentValuePerArea", "Zone", "Бюджет для неохранённых объектов на единицу площади.", "Area-based budget for free or lightly protected objects."),
            new("zone.resourcesValue", "Zone",      "Общий бюджет для ресурсов.", "Total budget for loose resources and similar pickups in the zone."),
            new("zone.resourcesValuePerArea", "Zone", "Бюджет для ресурсов на единицу площади.", "Area-based budget for loose resources and similar pickups."),
            new("zone.zoneBiome",   "Zone",         "Селектор основного биома зоны.", "Selector for the ground terrain of the zone."),
            new("zone.contentBiome", "Zone",        "Селектор биома для размещения контента.", "Selector for terrain-compatible content placement inside the zone."),
            new("zone.metaObjectsBiome", "Zone",    "Селектор биома для мета-объектов и декораций.", "Selector for special objects and decoration-like map elements."),
            new("zone.crossroadsPosition", "Zone",  "Где должна находиться точка перекрёстка зоны.", "Where the zone's internal crossroads point should sit."),
            new("zone.mainObjects", "Zone",         "Основные фиксированные объекты: spawn, город, руины, цель City Hold.", "Major fixed objects in the zone, for example a player spawn, city, ruins, or City Hold target."),
            new("zone.mandatoryContent", "Zone",    "Обязательные пакеты контента для этой зоны.", "Required content bundles for this zone. Think of these as must-have ingredients."),
            new("zone.contentCountLimits", "Zone",  "Ограничения повторяющегося контента. Не дают зоне переполниться.", "Limits for repeated content. These keep the generator from overcrowding a zone."),

            // ── Connection Fields ────────────────────────────────────────
            new("conn.name",        "Connection",   "ID связи. Дороги и правила размещения ссылаются на него.", "Connection id. Roads and placement rules can point to it, so keep names stable."),
            new("conn.from",        "Connection",   "Имя исходной зоны.", "The source zone name."),
            new("conn.to",          "Connection",   "Имя конечной зоны. Порталы travel from→to; добавьте обратный портал.", "The destination zone name. Portal connections travel from→to; add a reverse portal for return travel."),
            new("conn.connectionType", "Connection", "Direct — обычный маршрут, Portal — телепорт в одну сторону, Proximity — сближение.", "Direct is a normal border route, Portal is a one-way teleporter-style route, Proximity is adjacency guidance."),
            new("conn.guardZone",   "Connection",   "Какая зона владеет охраной на этом маршруте.", "Which zone owns the border guard for this route."),
            new("conn.guardEscape", "Connection",   "Продвинутый флаг поведения охраны.", "Advanced guard behavior flag."),
            new("conn.simTurnSquad","Connection",   "Отряд симуляции хода.", "Sim-turn squad for guard behavior."),
            new("conn.guardValue",  "Connection",   "Сила охраны на связи.", "Border guard strength."),
            new("conn.guardRandomization", "Connection", "Разброс силы охраны.", "Guard strength variance."),
            new("conn.guardWeeklyIncrement", "Connection", "Еженедельный рост охраны.", "Guard growth over time."),
            new("conn.guardMatchGroup", "Connection", "Группа для сопоставления охраны. Связанные границы остаются сопоставимыми.", "Groups similar guards so related borders can stay comparable."),
            new("conn.road",        "Connection",   "Нужна ли дорога на этой связи.", "Whether the connection should receive road support."),
            new("conn.gatePlacement","Connection",  "Продвинутая подсказка размещения ворот.", "Advanced placement hint for connection gates."),
            new("conn.length",      "Connection",   "Длина связи (только для Proximity).", "Distance hint for the route (Proximity only)."),

            // ── MainObject Fields ────────────────────────────────────────
            new("mo.type",          "MainObject",   "Тип объекта: Spawn, City, Ruins и т.д.", "Major object kind, such as Spawn, City, or Ruins."),
            new("mo.spawn",         "MainObject",   "ID спавна игрока (Player1, Player2 и т.д.).", "Player spawn id for Spawn objects."),
            new("mo.owner",         "MainObject",   "Владелец объекта, если он должен быть начальным.", "Player owner for an object when it should start owned."),
            new("mo.faction",       "MainObject",   "Селектор фракции: Match — копировать, Random/FromList — из разрешённых.", "Faction selector. Match means copy from another object; Random or FromList chooses from allowed factions."),
            new("mo.guardChance",   "MainObject",   "Шанс охраны объекта и приблизительная сила.", "Chance the object is guarded and the approximate guard strength."),
            new("mo.guardValue",    "MainObject",   "Сила охраны объекта.", "Guard strength for the object."),
            new("mo.buildingsConstructionSid", "MainObject", "Пресет построек города (бедный, богатый, ультрабогатый).", "Town building preset, such as poor, rich, or ultra-rich construction."),
            new("mo.placement",     "MainObject",   "Где и как объект размещается внутри зоны.", "Where and how the object is placed inside the zone."),
            new("mo.holdCityWinCon","MainObject",   "Отмечает этот город как цель City Hold.", "Marks this city as the City Hold objective."),

            // ── ContentPools / ContentLists ──────────────────────────────
            new("pool.name",        "ContentPool",  "Имя группы контента.", "Group name referenced by zones."),
            new("pool.content",     "ContentPool",  "Обязательные объекты или вложенные контент-записи.", "The required objects or nested content entries in that group."),
            new("pool.sid",         "ContentPool",  "Конкретный ID объекта (mine_wood, pandora_box и т.д.).", "Specific object id, such as mine_wood, market, watchtower, or pandora_box."),
            new("pool.includeLists","ContentPool",  "Именованные игровые списки, раскрывающиеся в возможные объекты.", "Named game lists that expand into possible objects."),
            new("pool.isGuarded",   "ContentPool",  "Флаг охранённости объекта.", "Marks whether the object is guarded."),
            new("pool.isMine",      "ContentPool",  "Флаг: объект является рудником.", "Marks whether the object is treated as a mine."),
            new("pool.soloEncounter","ContentPool", "Просит генератор разместить объект как отдельную встречу.", "Asks the generator to place the object as its own encounter."),
            new("pool.weight",      "ContentPool",  "Вес правила размещения — насколько сильно влияет на позицию.", "How strongly the rule should influence placement."),
            new("pool.rules",       "ContentPool",  "Правила размещения контента.", "Placement rules for where the content can appear."),
            new("pool.targetMin",   "ContentPool",  "Минимальное расстояние от цели.", "Minimum distance from the target."),
            new("pool.targetMax",   "ContentPool",  "Максимальное расстояние от цели.", "Maximum distance from the target."),

            // ── Layout (ZoneLayout) Fields ───────────────────────────────
            new("layout.obstaclesFill", "Layout",    "Какая часть зоны заполнена блокирующим рельефом.", "How much of the zone is filled with blocking terrain."),
            new("layout.obstaclesFillVoid", "Layout", "Какая часть зоны — пустые карманы.", "How much of the zone is empty pockets."),
            new("layout.lakesFill", "Layout",         "Какая часть зоны — озёра.", "How much lake terrain appears."),
            new("layout.minLakeArea", "Layout",       "Минимальный допустимый размер озера.", "How small lakes are allowed to be."),
            new("layout.elevationClusterScale", "Layout", "Насколько широкие области высот.", "How broad elevation areas are."),
            new("layout.elevationModes", "Layout",    "Взвешенные варианты долей низких/высоких area.", "Weighted choices for low/high elevated area fractions."),
            new("layout.roadClusterArea", "Layout",   "Насколько зона поощряет кластеризацию дорог.", "How much road-like clustering the layout encourages."),
            new("layout.guardedEncounterResourceFractions", "Layout", "Как ресурсы распределяются вокруг охранённых встреч.", "How resources are split around guarded encounters."),
            new("layout.ambientPickupDistribution", "Layout", "Как свободные подборы распространяются вокруг дорог и препятствий.", "How loose pickups spread around roads, obstacles, and each other."),

            // ── ContentCountLimit ────────────────────────────────────────
            new("limit.name",       "ContentLimit", "Имя группы ограничений, на которое ссылаются зоны.", "Limit group name referenced by zones."),
            new("limit.playerMin",  "ContentLimit", "Мин. число игроков, при котором действуют ограничения.", "Player-count minimum where the limits apply."),
            new("limit.playerMax",  "ContentLimit", "Макс. число игроков, при котором действуют ограничения.", "Player-count maximum where the limits apply."),
            new("limit.limits",     "ContentLimit", "Ограничения объектов внутри группы.", "The object limits inside this group."),
            new("limit.limits[].sid", "ContentLimit", "ID ограниченного объекта.", "Object id being limited."),
            new("limit.limits[].maxCount", "ContentLimit", "Максимальное допустимое количество.", "Maximum number allowed for that object."),

            // ── ValueOverride ────────────────────────────────────────────
            new("override.sid",     "ValueOverride", "Именованное значение для переопределения.", "Named value being overridden."),
            new("override.variant", "ValueOverride", "Индекс варианта (если применимо).", "Optional variant index the override applies to."),
            new("override.guardValue", "ValueOverride", "Переопределение значения охраны.", "Guard value override for the named entry."),

            // ── GlobalBans ──────────────────────────────────────────────
            new("bans.items",       "GlobalBans",   "ID предметов, которые не должны появляться.", "Item ids that should not appear."),
            new("bans.heroes",      "GlobalBans",   "ID героев, которые не должны появляться.", "Hero ids that should not appear."),
            new("bans.magics",      "GlobalBans",   "ID заклинаний/магии, которые не должны появляться.", "Magic or spell ids that should not appear, such as neutral_magic_town_portal."),

            // ── Connections Placement ────────────────────────────────────
            new("connectionsPlacement", "Advanced",  "Продвинутая подсказка для размещения ворот связи.", "Advanced placement hint that can influence how connection gates are arranged."),
            new("obstaclesNoise",   "Advanced",      "Списки записей шума для естественных границ.", "Lists of noise entries that make border bands more natural."),
            new("waterNoise",       "Advanced",      "Списки записей шума для водных границ.", "Lists of noise entries for water border bands."),
            new("amp",              "Advanced",      "Сила шума. Больше — граница сильнее волнистая.", "Noise strength. Larger values make the border wiggle more."),
            new("freq",             "Advanced",      "Частота шума. Больше — более частые изменения.", "Noise frequency. Larger values create tighter, more frequent changes."),
            new("waterType",        "Advanced",      "Тип водного рельефа для водных границ.", "The water terrain type used when the border includes water."),

            // ── Gameplay Tips ────────────────────────────────────────────
            new("tip.longerMatch",  "Советы",        "Чтобы матч был дольше: увеличьте размер карты, добавьте нейтральные зоны, усложните ключевые связи.", "To make a match longer, increase map size, add more neutral zones, or make key connections harder to break through."),
            new("tip.fasterExpand", "Советы",        "Чтобы расширение быстрее: уменьшите охрану рядом, добавьте дороги, приблизьте ранние зоны к стартам.", "To make expansion faster, lower nearby guard strength, add roads, or keep early zones closer to player starts."),
            new("tip.centerMatters","Советы",        "Чтобы центр имел значение: дайте хабу важный контент, награды, цель City Hold или несколько охранённых связей.", "To make the center matter, give the hub important content, stronger rewards, a City Hold target, or several guarded connections."),
            new("tip.balanced",     "Советы",        "Для баланса: каждая стартовая зона должна иметь ту же раскладку, похожий обязательный контент и сопоставимую сложность связей.", "To keep players balanced, make each spawn zone use the same layout, similar mandatory content, and comparable connection difficulty."),

            // ── Containers (structural) ──────────────────────────────────
            new("gameRules",        "Container",     "Правила всего матча: лимиты героев, бонусы, условия победы, тайминги режимов.", "Match-wide rules: hero limits, movement bonuses, victory conditions, and special mode timing."),
            new("winConditions",    "Container",     "Блок условий победы (classic, cityHold, gladiatorArena, tournament и т.д.).", "Which win rules are active, such as normal defeat-all play, City Hold, Gladiator Arena, or Tournament timing."),
            new("orientation",      "Container",     "Как весь граф зон вращается/выстраивается перед генерацией.", "How the whole layout is rotated. This changes where zones appear without changing the matchup design."),
            new("border",           "Container",     "Что окружает игровую карту: полосы препятствий и воды.", "What surrounds the playable map edge, usually obstacle and water bands."),
            new("zones",            "Container",     "Старты игроков, нейтральные зоны, хабы и целевые области. Здесь живёт большая часть настроек.", "The player starts, neutral areas, hubs, and objective areas. Most gameplay tuning lives here."),
            new("connections",      "Container",     "Пути между зонами. Решают, кто может дойти до кого, охраняется ли граница, дорога или портал.", "The paths between zones. These decide who can reach whom, whether a border is guarded, and whether a road or portal is used."),
            new("layout",           "Container",     "Форма зоны и внутренний стиль дорог/контента (spawn-like, center-like и т.д.).", "The zone's shape and internal road/content style, such as spawn-like or center-like layout."),
            new("mainObjects",      "Container",     "Крупные фиксированные объекты зоны: спавн игрока, город, руины, цель City Hold.", "Major fixed objects in the zone, for example a player spawn, city, ruins, or City Hold target."),

            // ── Missing gameRules fields ─────────────────────────────────
            new("bonuses",          "GameRules",     "Глобальные бонусы шаблона. Сейчас используется для настройки бонуса движения.", "Global bonuses applied by the template. The builder currently uses this for the movement bonus setting."),
            new("factionLawsExpModifier", "GameRules", "Меняет опыт от эффектов законов фракций. Больше — быстрее прокачка героев.", "Changes experience from faction law effects. Higher values speed up hero development from that source."),
            new("astrologyExpModifier", "GameRules",  "Меняет опыт от астрологических эффектов. Больше — быстрее прокачка героев.", "Changes experience from astrology effects. Higher values speed up hero development from that source."),
            new("desertion",        "GameRules",     "Специальное правило давления быстрых форматов. См. desertionDay/desertionValue.", "Special pressure rules seen in fast official templates. Treat these as advanced mode-specific timing controls."),
            new("heroLighting",     "GameRules",     "Специальное правило давления быстрых форматов (освещение героев).", "Special pressure rules seen in fast official templates. Treat these as advanced mode-specific timing controls."),

            // ── Road fields (inside zone) ────────────────────────────────
            new("zone.roads",       "Zone",         "Дорожные сегменты внутри зоны, обычно связывающие главные объекты с выходами.", "Road segments inside the zone, usually linking main objects to connections."),
            new("road.type",        "Road",         "Стиль или тип дороги (если задан).", "Road style or route type when specified."),
            new("road.from",        "Road",         "Начальная точка сегмента дороги.", "Endpoints for the road segment."),
            new("road.to",          "Road",         "Конечная точка сегмента дороги.", "Endpoints for the road segment."),
            new("road.endpoint.type", "Road",       "Что является целью конечной точки (главный объект или связь).", "What the road endpoint targets, such as a main object or connection."),
            new("road.endpoint.args", "Road",       "Аргументы цели (например, индекс объекта 0 или имя связи).", "Arguments for the endpoint target, such as object index 0 or a connection name."),

            // ── MainObject placement fields ──────────────────────────────
            new("mo.placementArgs", "MainObject",   "Дополнительные аргументы размещения объекта.", "Arguments for where and how the object is placed inside the zone."),

            // ── ContentPool additional fields ────────────────────────────
            new("pool.designatedEncounter", "ContentPool", "Отмечает специальную встречу в контенте из примеров.", "Marks a special encounter in example-backed content."),
            new("pool.rules.type",  "ContentPool",  "Тип правила размещения (Crossroads, MainObject и т.д.).", "Rule kind, such as Crossroads, MainObject, or another game-supported placement target."),
            new("pool.rules.args",  "ContentPool",  "Дополнительные аргументы цели правила.", "Extra target arguments for the rule."),
            new("pool.rules.target","ContentPool",  "Одно целевое значение, когда диапазон не используется.", "Single target value when a range is not used."),
            new("pool.target",      "ContentPool",  "Одно целевое значение расстояния.", "Single target value when a range is not used."),
            new("limit.limits[].includeLists", "ContentLimit", "Списки, объекты которых входят в лимит.", "Lists whose objects are included in the limit."),
        };

        /// <summary>Возвращает RU-описание поля по его ключу. Null если не найдено.</summary>
        public static string? GetDesc(string field)
            => Entries.FirstOrDefault(e => e.Field == field)?.DescRu;

        /// <summary>Возвращает EN-описание поля по его ключу. Null если не найдено.</summary>
        public static string? GetDescEn(string field)
            => Entries.FirstOrDefault(e => e.Field == field)?.DescEn;
    }
}
