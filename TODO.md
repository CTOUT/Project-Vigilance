# Implementation Roadmap & TODO

## Active Roadmap: Phase 2 — Geoscape & Radar Grid (Standalone Framework)

### Milestone 1.3: Radar Infrastructure, Advanced Early Warning & Inter-Faction Encounters

- [x] **Tactical Radar Coverage & Perimeter Outlines (`WorldDrawLayer_RadarCoverage`):** Implemented native 3D mesh overlay rendering glowing perimeter boundary rings and hexagonal holographic honeycomb grid over active sensor envelopes on the planetary globe. Configurable styles (Boundary & Grid, Boundary Only, None), 5 color palettes (Tactical Cyan, Surveillance Amber, Emerald Phosphor, Crimson Alert, Ghost White), adjustable opacity, and optional background surveillance dimming (Option 3 Hybrid). Zero-allocation hot path with dynamic sensor movement tracking.
- [ ] **Colony Technological Radar Infrastructure:**
  - `CompProperties_Radar` and `CompRadar` with dynamic standby vs active power scaling (`standbyPowerDraw` / `activePowerDraw`).
  - **Multi-Dish Settlement Stacking (Interferometry & Identification Expansion):** Centred hexagonal geometry ($\text{Total} = 1 + 3n(n+1)$). Maximum detection radius $R_{detect}$ stays stable (e.g. 24 rings, $1,801$ tiles), while secondary dishes expand the inner **Identification / Classification Horizon ($R_{id}$)** outward from ~40% (10 rings) $\rightarrow$ ~70% (17 rings) $\rightarrow$ ~92% (22 rings), accelerates sweep cadence (250 ticks $\rightarrow$ 150 ticks $\rightarrow$ 75 ticks), and provides fail-safe redundancy.
  - **Sensory Building Defs & Research Projects:**
    - `Geoscape_LookoutTower` (Neolithic/Medieval, 8 hexes, unpowered/manned, Contact Tier).
    - `Geoscape_RadarSmall` (Industrial Early-Warning Dome, 14 hexes, 350W/750W, Contact Tier).
    - `Geoscape_RadarLarge` (Spacer Phased Array, 24 hexes [$1,801$ tiles], 800W/1,800W, Classified Tier).
    - `Geoscape_SigintConsole` (Ultra SIGINT Decryption Console, `CompFacility` linkable, 700W/1,400W, upgrades Phased Array to Tier 4 Analysed).
    - `Geoscape_SonarArray` (Industrial Coastal Hydrophone Station, 18 hexes water/coast).
- [ ] **Lightweight Geoscape Outposts & World Map Strategic Context Actions:**
  - `WorldObject_ListeningPost` (Zero-TPS global entity, automated solar relay vs manned lookout garrison).
  - **Civilization-Style Right-Click Hex Interaction (`FloatMenuMakerWorld`):**
    - *"Dispatch Expedition Here..."* (pre-populates and locks destination tile in `Dialog_FormCaravan`).
    - *"Establish Outpost / Relay Here..."* (instant deploy if caravan on tile; prompts outpost kit caravan from settlement if remote).
    - *"Reclaim Garrison / Dismantle / Repair"* actions on existing outposts.
  - Outpost vulnerability & sabotage events from passing hostile incursions.
- [ ] Build `RadarPulseHandler` utilizing `TickRare` (250 ticks) for efficient spatial distance checks.
- [ ] Build Oceanic Sonar detection layer for water hexes.
- [ ] Two-Stage Early Warning Dispatcher: generate advance warning alerts and reveal telemetry based on radar tier.
- [ ] **Concealed Dynamic Sites & Reconnaissance in the Haze:**
  - **Haze Concealment for Dynamic Camps:** When storyteller/quests spawn work sites, hunting camps, bandit outposts, or pirate forward staging bases in unmonitored sectors (Haze), suppress instant global announcement letters and map markers until an active player sensor or scout caravan enters visual range.
  - **Breadcrumb Rumours & Traveller Intelligence:** Visiting trade caravans, friendly guests, or allied patrols arriving at player settlements can share intelligence on unmapped sites they encountered en route (_"We passed a hostile logging camp about 5 hexes north-west"_), creating narrative reconnaissance leads and map reveals.
  - **Forward Staging Ground Detection:** Hostile raid preparations can establish temporary forward staging camps in the Haze that can be pre-emptively raided or dispersed before the main assault launches.
- [ ] **Dynamic Inter-Faction Encounters (Deferred from Milestone 1.2):** Enable roaming NPC incursions and trade caravans to interact when their routes cross—simulating background skirmishes between warring factions, ambush aftermath sites, and trade rendezvous.
- [ ] **Simulated Inter-Faction Trade Routes & Storyteller Caravan Encounter Grounding (Deferred from Milestone 1.2):**
  - **Organic Trade Route Network:** Generate physical, persistent NPC trade caravans (`WorldObject_Incursion`) travelling between NPC faction bases, outposts, and allied settlements along planetary roads and mountain passes, establishing organic background commerce rather than solely player-directed traffic.
  - **Grounding Storyteller Caravan Encounters:** Hook abstract storyteller incident events (`IncidentWorker_CaravanMeeting`, `IncidentWorker_CaravanDemand`) into physical world map incursions. When a player caravan meets another group on the road, ground the event in an actual travelling incursion with persistent inventory, pawn rosters, and origin/destination telemetry rather than spawning an ephemeral caravan out of thin air.
  - **Persistent Traversal & Aftermath:** Following a roadside meeting (trading, paying demands, or moving on), the NPC incursion continues moving along its route toward its destination instead of despawning into the void. Enables hostile raiders to intercept or ambush NPC merchant routes, spawning dynamic distress calls or salvageable aftermath sites.

---

### Milestone 1.4: Planetary Coordination & Version Support

- [ ] **Intelligent Multi-Base Strike Coordination:** Replace disconnected per-colony storyteller triggers with theatre-aware campaign logic capable of orchestrating diversionary raids, staggered multi-base assaults, or synchronised offensive campaigns across player settlements.
- [ ] **Community Request: RimWorld 1.6 Legacy Backport Assessment:** Investigate compilation of `ProjectVigilance.Geoscape` for RimWorld 1.6 targeting `.NET Framework 4.7.2` / `.NET Standard 2.1` to support players on the previous release.
- [ ] **Standalone Test:** Validate radar detection, listening posts, and fog of war in vanilla RimWorld 1.7 with zero other Vigilance mods loaded.

---

### Milestone 1.5: Foundation Framework, Modding Extensibility & Community Localisation

- [ ] **RimWorld Keyed Localisation Support (`Languages/English/Keyed/*.xml`):**
  - **Externalise Hardcoded Phraseology:** Extract hardcoded English string literals across C# classes (notification letters, dialogue trees, float menu options, inspect strings, narrative logs, and settings window tabs) into RimWorld's standard translation lookup pattern (`"Key".Translate(...)`).
  - **English Reference Dictionary:** Structure clean, documented reference XML files in `Languages/English/Keyed/` (`Geoscape_Letters.xml`, `Geoscape_Dialog.xml`, `Geoscape_Inspect.xml`, `Geoscape_Settings.xml`) so community translation teams can readily author and maintain non-English translations without modifying compiled C# assemblies.
  - **Parameter Tokens & Formatting:** Use proper replacement tokens (`{0}`, `{1}`) and RimWorld grammar helpers (`CapitalizeFirst`, gender, and pluralisation rules) to ensure clean grammar and flexibility in all destination languages.
- [ ] **Modder Extensibility Hooks & Foundation Library Architecture:**
  - **DefModExtension & XML Registration:** Enable external mod developers to configure custom structures as radar stations or sensor nodes via XML `DefModExtension` without requiring hard compile-time code dependencies.
  - **Public Sensor & Target Provider API:** Expose formal registration interfaces (`ISensorProvider`, `IDetectableTarget`) so third-party mods (e.g. satellite systems, psychic reconnaissance, aircraft, or custom vehicle frameworks) can register dynamic sensors or detectable overland craft directly with `SensoryManager`.
  - **Incursion & Tactical Event Lifecycle Hooks:** Expose public C# event delegates and callbacks across key operational milestones:
    - Incursion inception, routing, and departure (`OnIncursionSpawned`, `OnIncursionDeparted`).
    - Contact detection, classification, and loss-of-contact (`OnContactDetected`, `OnContactClassified`, `OnContactLost`).
    - Global hex interception, forward ambush triggering, and tactical disengage (`OnIncursionIntercepted`, `OnAmbushSprung`, `OnTacticalDisengage`).
    - Colony arrival, physical retreat initiation, and captive incarceration (`OnIncursionArrived`, `OnIncursionRetreatStarted`, `OnCaptiveIncarcerated`).
  - **Foundation Library Extraction Evaluation:** Assess isolating core spatial bitmask grids, native 3D mesh world layers, overland waypoint pathing, and abstract sensor/incursion contracts into a standalone foundation framework (`ProjectVigilance.Core` or `VigilanceFoundation`) that other planetary or tactical mods can depend on as a lightweight base library.

---

## Phase 3: Module 2 — Interception & Rapid Response (Standalone Framework)

- [ ] Define craft stats (speed, fuel, range, armament slots).
- [ ] Build tactical air interception dialogue UI.
- [ ] Implement crash site generation (`SiteDef` with wreckage, debris, and survivor spawns).
- [ ] Create tactical deployment and extraction drop-zone mechanics.
- [ ] Build amphibious transport capabilities (Triton analogue for aquatic sites).
- [ ] **Odyssey DLC Synergy:** Hook into 1.6 gravship components and orbital layer transit (`PlanetLayerDefs`).
- [ ] **Standalone Test:** Validate interception combat and dropship deployment against standard pirate shuttles without Module 1.

---

## Phase 3.5: Tactical Battlescape & Sensory (Standalone Framework)

- [ ] **3-Mode Battlescape Visibility (`MapComponent_TacticalVisibility`):**
  - **Mode 1: Classic Haze of War:** Obsidian Shroud (Tier 0) → Slate Memory Haze (Tier 1) → Live Line-of-Sight (Tier 2).
  - **Mode 2: Tactical Site Surveillance:** Pre-charted terrain/structure footprints with masked dynamic hostiles.
  - **Mode 3: Omniscient:** Vanilla spectator fallback.
- [ ] **Zero-Allocation Spatial Cell Bitmasks:** Implement `ulong[] cellExploredBitset` and `ulong[] cellVisibleBitset` on `.NET 10` for maps up to 100,000 cells without GC allocations.
- [ ] **Multi-Tier Sensory Modelling:**
  - **Colonists & Biological Pawns:** 120° forward optical vision cone (~30 cells day, ~14 cells night, weather penalties) + 360° acoustic hearing horizon (~6–8 cells). Bionic and Archotech eye enhancements.
  - **Pets & War Beasts:** 360° olfactory (scent) and acute acoustic detection aura (~18–22 cells) detecting lurking hostiles behind thin cover and walls.
  - **Mechanoids:** Multi-spectral optics and phased radar arrays; night/smoke immunity; chassis-calibrated envelopes (Scyther wide-arc, Lancer 50-cell sniper tunnel vision, Centipede 360° radar).
- [ ] **Tactical Field Equipment:**
  - Handheld Motion Scanner with 250-tick acoustic sonar pulses emitting blips through walls.
  - Directional rail flashlights, deployable spotlight structures, illumination flares, and smoke/chaff screens.
- [ ] **Guerrilla & Pod AI (Anti-Killbox Doctrines):**
  - Fireteam pod cohesion (dormant patrol states vs. localized alert without whole-map swarms).
  - Ambush and fatal-funnel crossfire setups holding fire until optimal range.
  - Peeling fallback and suppressive covering fire retreats linking up with rear pods.
  - Perimeter breaching with sappers/explosives avoiding turret killboxes.
- [ ] **Geoscape-to-Battlescape Integration:**
  - Radar intel tier dictates insertion Fog of War state and landing zone (LZ) security.
  - Live tactical countdown timers linked to inbound Geoscape reinforcements (mission doom clock).

---

## Phase 4: Module 3 — Alien Containment & Biology (Standalone Base System)

- [ ] Implement stasis pods and containment cell comps.
- [ ] Hook into Anomaly DLC study mechanics (with standalone fallback for non-DLC).
- [ ] Create autopsy bills and surgical extraction recipes.
- [ ] Implement interrogation mechanics with mental resistance tracking.
- [ ] **Item-Triggered Research Discovery:** Build "Fog of Research" system unlocking tech upon physical salvage, autopsy, or interrogation.
- [ ] Design reverse-engineering knowledge tree (Alloys, Elerium-115, Weapon Fragments).
- [ ] **Standalone Test:** Validate containment pods and autopsy bills on vanilla pawns/creatures in isolation.

---

## Phase 5: Module 4 — Invasion Campaign & Panic (Dependent Campaign Engine)

- [ ] Build Council of Planetary Factions funding and monthly evaluation ledger.
- [ ] Implement regional panic tracking per territory.
- [ ] Implement Alien Strategic AI (mission generation: Scout, Harvest, Infiltration, Terror, and coordinated multi-base planetary offensives).
- [ ] Create global Invasion Doom Clock and Dark Events counter-operations.
- [ ] Implement faction defection / alien pact mechanics.

---

## Phase 6: Module 5 — Alien Arsenal & Factions (Thematic Content Pack)

- [ ] Implement alien pawn kinds (Sectoids, Mutons, Chryssalids, Aquatoids, Deep Ones).
- [ ] Create weapon sets: Laser, Plasma, Sonic, and Harpoon/Gauss.
- [ ] Create armour sets: Kevlar, Carapace, Titan, and Diving/Aquatic suits.
- [ ] Integrate sound effects and visual FX faithful to the franchise.

---

## Completed Milestones

<details>
<summary><b>v0.3.0 — Ground Interception, Tactical Ambushes & Evacuated Colony Hierarchy (Shipped 2026-10-26)</b></summary>

### Milestone 1.2: Ground Interception & Forward Ambush

- [x] **Field Interception & Forward Ambush:** Allow player caravans (and rapid-response transports) to intercept approaching incursions on the global hex grid, generating an encounter map where raiders can be engaged, ambushed, or routed before reaching the colony. Right-click float menus (`Intercept`, `Hail & Trade`, `Warn Away / Turn Back`), dynamic target pursuit tracking, and X-COM "Contact Lost" telemetry dialog. Night camp ambush advantages and Night-Owl trait bonuses.
- [x] **Night Owl & Schedule-Aware Caravan Rest:** Replace hardcoded 22:00–06:00 player caravan rest lock with circadian and `Pawn_TimetableTracker` evaluation (`Caravan_NightResting_Patch`, `CaravanNightRestUtility_LeftRestTicksAt_Patch`). All-Night-Owl (or majority Night-Owl) squads automatically camp during daytime (11:00–18:00) and march through the night; mixed squads follow the majority shift; explicit `Sleep` or `Work` schedule assignments allow custom rest shifts or force-marching.
- [x] **Threat-Aware Tactical Retreat & Settlement Evacuation:** Evaluate known/visible hostile threats (raiders, mechanoids, insectoids, turrets) across all four map edges and verify `PawnPath` trajectory safety when retreating (`IncursionTacticalRetreatUtility`). Support emergency caravan evacuation from besieged home settlements without losing pawns to `WorldPawns`.
- [x] **Retreat, Pursuit & Kidnap Recovery:** When raiders flee, rout, or kidnap colonists, simulate their physical retreat across the world map back to their home settlement (maintaining the incursion's assigned persistent tracking ID, e.g. `Contact-3 [Retreating]` / `Contact-3 [Kidnappers]`). Fleeing pawns preserve 1:1 wounds, carried loot, and captive colonists. Incursions simulate overland physiological processes (bleeding, natural healing, field tending with carried medicine, night rest, and hunger). Intercepting them spawns the captive colonist on the battlefield for rescue; reaching the stronghold transfers captives cleanly into vanilla's `Faction.kidnapped` system.
- [x] **Roadside Trading & Persistent Stock:** Friendly trade caravans generate their trade stock once upon inception (`Dialog_Trade`). Roadside trading deducts bought goods and adds sold items to persistent stock, preventing double-dipping or item duplication upon colony arrival.
- [x] **Warn Away / Turn Back:** Friendly/neutral travellers approaching an active danger zone can be warned off by player caravans or radio, reversing course toward home with positive faction goodwill (`+5`).
- [x] **Abandoned Camp Fallback & Overland Track Pursuit:** When raiders or incursions arrive at an abandoned colony or vacated camp, replace instant disbanding with intelligent tactical behaviour:
  - **Ground Track Pursuit:** Evaluate recently evacuated player caravans from the tile. If the evacuation departed overland on foot or pack animals, roll a tactical tracking chance. After a delay searching the empty camp, reroute the incursion to actively pursue the caravan to its next destination or ambush it en route.
  - **Tactical Fallback & Retargeting:** If no tracks are discovered or the trail goes cold, raiders choose between marching back to their home base/nearest faction outpost, establishing an occupation camp, or opportunistically redirecting their assault toward an adjacent player settlement or ally.
- [x] **Physical Settlement Incarceration & Base Assault Rescue:** When retreating kidnappers reach their stronghold unintercepted, captive colonists are registered as incarcerated at that specific settlement tile (`IncursionRetreatManager.RecordSettlementIncarceration`). When the player mounts an assault on that enemy stronghold (`SettlementUtility.Attack`), the captured colonist is spawned inside the fortress as a prisoner, enabling direct base-liberation missions.

</details>

<details>
<summary><b>v0.2.0 — Sensory Architecture, Visibility Modes & Scout Horizon (Shipped 2026-10-19)</b></summary>

### Milestone 1.1: Sensory Architecture, Visibility Modes & Scout Horizon

- [x] **Decoupled Namespace Setup:** Establish `ProjectVigilance.Geoscape.Sensory` namespace and `ISensorProvider` / `IDetectableTarget` contracts.
- [x] **Zero-Allocation Spatial Bitmask State:** Implement compact 64-bit word bitsets (`isExploredBitset`, `isActiveVisionBitset`) in `.NET 10` for high-performance tile state tracking without GC churn.
- [x] **Configurable Visibility Modes (`GeoscapeSettings`):**
  - **Planetary Surveillance (Default / X-COM Style):** Static global geography (continents, oceans, cities) is known; moving incursions and clandestine threats are hidden outside sensor range.
  - **Classic Haze of War (3-Tier RTS Style):** Unexplored (Black Shroud) → Explored/Unmonitored (Translucent Grey Haze remembering static terrain) → Active Vision (Clear + live target reveal).
  - **Omniscient (Spectator Style):** All planetary movement immediately visible across the globe without fog or radar requirements.
- [x] **Natural Scout Horizon:** Implement baseline line-of-sight vision for player colonies (~5 tiles) and caravans (~3 tiles) via `ScoutHorizonSensor` so low-tech/tribal colonies receive early warning naturally.
- [x] **5-Tier Contact Classification & Incursion Masking:**
  - _Tier 0 (Undetected):_ Completely hidden on the world map and unselectable.
  - _Tier 1 (Suspected):_ Indirect narrative cues and sector rumours.
  - _Tier 2 (Contact):_ Pulsing amber blip without faction crest or destination vector.
  - _Tier 3 (Classified):_ Faction identity and broad warband type identified.
  - _Tier 4 (Analysed):_ Full telemetry (exact pawn count, weaponry, target colony, live ETA).
- [x] **Two-Stage Early Warning Escalation (Fog-of-War Parity):** Removed faction cheat. All approaching entities (hostile warbands, trade caravans, visitor groups) trigger an **Unidentified Contact** alert (`ThreatSmall`) at the outer 5-hex perimeter with an amber pulsing blip. Escalates to **Classified Confirmation** (`ThreatBig` for raids, `PositiveEvent` for friendly caravans) upon reaching close visual range (~2.2 hexes) or field scout interception.
- [x] **Sampled Radar Jumps vs. Optical Continuous Smoothing:** Discrete 250-tick sweep position updates for radar/contact blips vs smooth 60 FPS Slerp for direct optical sight.
- [x] **Off-World Render Gating & Peacetime Early-Exit:** Zero TPS impact while managing colonies, instant bypass when no incursions are active, and stationary sensor caching.
- [x] **Tactical Ingress Diversification & Anti-Funnel Pathing:**
  - **Approach Waypoint Offsets:** Eliminate highway magnetism by generating randomized flanking waypoints (~4–8 hexes outside colony) so raids from the same direction don't always funnel down the identical highway corridor.
  - **Faction Movement Doctrines:** Differentiate path preferences—civilian traders use highways, tribals favour wilderness cover to avoid lookouts, and mechanoids march in uncompromising geodesic lines.
  - **Multi-Vector Compass Pincers:** Enforce separate approach quadrants for multi-column assaults so they surround the colony in coordinated pincer strikes rather than arriving in a conga line.
- [x] **`WorldLayer_HazeOfWar` & `WorldDrawLayer_HazeOfWar`:** Render native 3D mesh overlay on the RimWorld 1.7 multi-layer planetary grid (`PlanetLayer.WorldDrawLayers`) for Classic Haze mode. Triangulates sector geometry using `WorldAltitudeOffsets.MiscTileOverlay` and `WorldMaterials.VertexColorTransparent`: deep obsidian shroud (~98% opacity) over unexplored tiles, translucent slate-grey haze (~55% opacity) over unmonitored sectors revealing underlying terrain, and crystal-clear cutouts for active sensory perimeters. Features zero-allocation submesh reuse and batched asynchronous regeneration.
- [x] **Soft Compatibility with Community Mods:** Implemented `ExternalFogCompatibility` soft reflection bridge detecting external planetary fog mods (such as D4egon's _World Fog of War_ `#3765828104`, _Rimworld Exploration Mode_, and VEF-compatible fog grids) without compile-time or runtime dependencies on `VanillaExpandedFramework.dll`. Bi-directionally reads external revealed tile state and propagates scout/radar reveals into external grids.
- [x] **Dynamic Incursion Classification & Atmospheric Masking:** Differentiated tactical incursion categories (`HostileWarband`, `TradeCaravan`, `Visitors`, `Travelers`, `TributeCollector`, `Pilgrims`, `Beggars`, `AlliedReinforcements`). Masked Def labels and Info cards as neutral "Unidentified Contact" at Tier 2 (`Contact`) while revealing tailored labels and tactical descriptions upon classification.
- [x] **Empty Settlement Incursion/Visitor Resolution:** Friendly visitors and caravans arriving at an evacuated settlement map (zero conscious colonists) gracefully notify the player and depart safely instead of vanishing silently due to vanilla targeting failures.
- [x] **Selection Lockout for Shrouded & Undetected Objects:** Sealed click and marquee box selection exploits for unrevealed world objects and undetected contacts via `WorldSelector` and `WorldObjectSelectionUtility` patches.
- [x] **Nighttime Visual Horizon & Alertness Simulation:** Naked-eye optical lookouts on colonies and caravans suffer a 50% detection radius penalty during darkness (`GenCelestial.CurCelestialSunGlow < 0.35f`). If 100% of conscious colonists on the map are asleep in bed, vision collapses to an unattended 1.0-hex perimeter. Dynamic sensor labels update to "Night Watchers", "Sleeping Lookouts", or "Asleep in Camp".
- [x] **Biological Incursion Night Resting & Campfires:** Living mortal factions (Tribals, Outlanders, Pirates, Empire, neutral traders, visitors) halt overland movement and make camp between 22:00 and 06:00 local solar time using `CaravanNightRestUtility.RestingNowAt(Tile)`. Mechanoids and anomaly horrors march relentlessly 24/7. Contacts camped for the night display stationary status, dawn resumption countdowns, and campfire observations.
- [x] **Watertight Shroud & Info Card Button Suppression:** Sealed biome and terrain info leaks by patching `WorldInspectPane.DoInspectPaneButtons` to suppress the Info Card ('i') button when clicking unexplored shroud tiles in Classic Haze of War.
- [x] **Cell Inspector Shrouding & Loss-of-Contact Deselection:** Patched `CellInspectorDrawer.DrawWorldInspector` to block Left Alt inspection on unexplored shroud hexes, and implemented instant auto-deselection and travel-line overlay suppression when contacts move outside sensor range.
- [x] **X-COM Style Persistent Tracking IDs:** Monotonic sequential tracking designation counter allocated on first detection (`Contact-1`, `Contact-2` for Industrial/Spacer; `Strangers #1`, `Strangers #2` for Tribal/Medieval). Designations persist for the object's entire lifespan across radar dead zones, night rest, and classification escalations (`Hostile Warband [Contact-1]`), serialized into `SensoryManager` and `WorldObject_Incursion` save data.

</details>

<details>
<summary><b>v0.1.0 — Architecture & Overland Incursions (Shipped 2026-10-18)</b></summary>

### Phase 1: Architecture & Foundation

- [x] Initialise git repository and enforce repository standards.
- [x] Define three-pillar architectural vision (1994 UFO Defense, Terror from the Deep, Modern Firaxis).
- [x] Establish decoupled modular system design and dependency matrix.
- [x] Author technical specifications for Module 1 (Geoscape & Radar).
- [x] Author technical specifications for Module 2 (Interception & Deployment).
- [x] Author technical specifications for Module 3 (Containment & Biology).
- [x] Author technical specifications for Module 4 (Campaign & Panic).
- [x] Document aquatic operations and Terror from the Deep integration.

### Phase 2: Module 1 (Shipped Components)

- [x] Create `Geoscape/` C# project (`Geoscape.csproj`) targeting RimWorld 1.7 (Waylith) and `.NET 10` (`net10.0`).
- [x] **Physical Incursion Engine (Milestone 1A):** Intercept walk-in raids (`RaidEnemy`), resolve origin bases/wilderness staging, and simulate real-time overland transit on the `WorldGrid`.
- [x] **Physical Incursion Engine (Milestone 1B):** Expand incursion routing to friendly caravans (`TraderCaravanArrival`) and roaming visitor/traveller groups.

</details>

#### See [CHANGELOG.md](CHANGELOG.md) for full release notes
