# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] - 2026-10-19

### Added

- Comprehensive Community FAQ ([`docs/faq.md`](docs/faq.md)) addressing modded faction compatibility (Vanilla Factions Expanded, Medieval Overhaul), tactical combat overhauls (Combat Extended, CAI 5000), Rim War architectural comparisons, and 1.6 backport evaluations.
- Steam Workshop Collection badges and cross-links across project documentation.
- Architectural specifications for 3-tier visibility modes (Planetary Surveillance, Classic Haze of War, Omniscient) and 5-tier contact intelligence progression in [`docs/modules/geoscape-and-radar.md`](docs/modules/geoscape-and-radar.md).
- Decoupled internal namespace design rules (`World`, `Sensory`, `Tactical`) in [`docs/architecture/modular-system.md`](docs/architecture/modular-system.md) to guarantee clean future standalone extraction.
- Dynamic sensory tracking fidelity distinction: discrete 250-tick sweep jumps for radar/sonar blips vs smooth 60 FPS spherical interpolation (`Vector3.Slerp`) for direct optical vision in [`docs/modules/geoscape-and-radar.md`](docs/modules/geoscape-and-radar.md).
- Planetary simulation lifecycle optimisations: complete off-world render skipping (`WorldRendererUtility.WorldRenderedNow == false`), peacetime sweep early-exit when zero incursions are active, stationary sensor caching, and Omniscient bypass.
- Baseline Scout Horizon lookout system providing line-of-sight early warning for colonies (~5 hexes) and caravans (~3 hexes).
- Two-Stage Early Warning Escalation: Removed internal faction cheat so all incoming forces (hostile raids, trade caravans, visitors) generate an **Unidentified Contact** alert (`ThreatSmall`) and amber blip at the outer 5-hex perimeter, escalating to a **Classified Confirmation** (`ThreatBig` for raids, `PositiveEvent` for friendly caravans) once within close visual range (~2.2 hexes) or field reconnaissance.
- **Tactical Ingress Diversification & Anti-Funnel Pathing:** Implemented off-road approach waypoints (~4–7 hexes outside the target colony) and spherical compass quadrants (`Left`, `Right`, `Rear`) in `IncursionOriginResolver` to eliminate highway magnetism and prevent raiders from funnelling down predictable road corridors.
- **Faction Movement Doctrines:** Differentiated pathing doctrines across factions—friendly trade caravans stick to highways (0% flank), Mechanoids march in direct geodesic lines (0%), Outlanders/Pirates flank occasionally (50%), and Neolithic Tribal Warbands prioritise wilderness cover and flanking vectors (80%).
- **Multi-Vector Compass Pincers:** Enforced distinct compass approach quadrants for multi-column assaults to execute coordinated pincer strikes around the settlement.
- **Calm Tactical Amber Pulse:** Replaced high-frequency 5Hz triangle strobe with a smooth 2.5s sinusoidal breathing beacon for Unidentified Contacts across both world mesh rendering and expanding icons.
- **Native Planetary Haze of War Mesh (`WorldDrawLayer_HazeOfWar`):** Implemented native 3D mesh overlay on the RimWorld 1.7 multi-layer planetary grid (`PlanetLayer.WorldDrawLayers`) for Classic Haze mode. Triangulates sector fan geometry over altitude `0.042f` with `WorldMaterials.VertexColorTransparent`, drawing 100% opaque obsidian shroud over unexplored sectors, translucent slate-grey haze (~55% opacity) over unmonitored sectors, and crystal-clear cutouts for active sensory perimeters. Features zero-allocation submesh reuse and instant single-pass mesh finalisation.
- **WorldObject Fog Occlusion & Information Security:** Added Harmony patches (`WorldObject_Visibility_Patch`) across `ExpandableWorldObjectsUtility.HiddenByRules`, `WorldObject.SelectableNow`, `WorldObjectSelectionUtility.VisibleToCameraNow`, and `GenWorldUI.WorldObjectsUnderMouse`. Non-player outposts, cities, settlements, quest sites, and ruins in unexplored shrouded sectors have their 2D expanding icons hidden, mouse hover tooltips suppressed, and selection blocked. Explored static settlements remain visible through the translucent haze veil.
- **Instant World Load & Zero-Flicker Initialization:** Eagerly initialized the exploration bitmask and executed an initial sensory sweep during `SensoryManager.FinalizeInit(bool fromLoad)` before the globe renders. Defaulted unallocated bitset queries to shrouded (`false`), eliminating the initial flash where all sectors briefly appeared grey before turning black.
- **Soft Compatibility with Community Fog Mods:** Implemented `ExternalFogCompatibility` soft reflection bridge detecting external planetary fog mods (such as D4egon's _World Fog of War_ `#3765828104`, _Rimworld Exploration Mode_, and VEF-compatible fog grids) without hard assembly dependencies on `VanillaExpandedFramework.dll`. Bi-directionally reads external revealed tile state and propagates scout/radar reveals into external grids.
- **Dynamic Incursion Classification & Information Security:** Differentiated planetary contacts into tactical groups (`HostileWarband`, `TradeCaravan`, `Visitors`, `Travelers`, `TributeCollector`, `Pilgrims`, `Beggars`, `AlliedReinforcements`). Concealed underlying entity types and def descriptions when at `Contact` intelligence tier, presenting a neutral label ("Unidentified Contact") and atmospheric reconnaissance text on inspect and Info cards, while unlocking tailored labels upon classification.
- **Empty Settlement Arrival Notifications:** Handled friendly visitors and caravans arriving at an evacuated or unoccupied colony (zero conscious colonists present). Instead of silently vanishing due to vanilla visitor job-giver failures, a historical notification informs the player that the travellers arrived, found no colonists present, and departed safely.
- **Tech-Level-Aware Sensory Narrative & Vocabulary Adaptation:** Implemented `SensoryNarrativeUtility` dynamically altering world map labels, inspect telemetry, and early warning letters based on the player colony's technology tier (`TechLevel`) and the detecting sensor's nature (`IsTechnological`). Tribal and Medieval colonies receive immersive low-tech vocabulary ("Approaching Strangers", settlement watchers/spotters, horizon dust, and warrior battle preparations) rather than sci-fi techno-babble ("radar telemetry", "long-range sensors"). Industrial and Spacer factions continue to receive modern tactical alerts.
- **Click & Marquee Selection Lockout for Shrouded & Undetected Objects:** Sealed selection exploits by patching `WorldSelector.Select` and `WorldObjectSelectionUtility.MultiSelectableWorldObjectsInScreenRectDistinct` as well as overriding `WorldObject_Incursion.NeverMultiSelect`, preventing players from clicking or drag-selecting unrevealed world objects or undetected incursions.
- **Nighttime Visual Horizon & Alertness Simulation:** Naked-eye optical lookouts on colonies and caravans suffer a 50% detection radius penalty during planetary darkness (`GenCelestial.CurCelestialSunGlow < 0.35f`). If 100% of conscious colonists on the map are asleep in bed, vision collapses to an unattended 1.0-hex perimeter. Dynamic sensor labels update to "Night Watchers", "Sleeping Lookouts", or "Asleep in Camp".
- **Biological Incursion Night Resting & Campfires:** Living mortal factions (Tribals, Outlanders, Pirates, Empire, neutral traders, visitors) halt overland movement and make camp between 22:00 and 06:00 local solar time using `CaravanNightRestUtility.RestingNowAt(Tile)`. Mechanoids and anomaly horrors march relentlessly 24/7. Contacts camped for the night display stationary status, dawn resumption countdowns, and campfire observations.
- **Watertight Shroud & Info Card Button Suppression:** Patched `WorldInspectPane.DoInspectPaneButtons` to suppress the Info Card ('i') button when clicking unexplored shroud tiles in Classic Haze of War, preventing biome, rock type, and terrain leakages.
- **Cell Inspector Shrouding & Loss-of-Contact Deselection:** Patched `CellInspectorDrawer.DrawWorldInspector` to block Left Alt inspection on unexplored shroud hexes, and implemented instant auto-deselection and travel-line overlay suppression when contacts move outside sensor range.
- **X-COM Style Persistent Tracking IDs:** Implemented monotonic sequential tracking IDs allocated on first detection (`Contact-1`, `Contact-2` for Industrial/Spacer; `Strangers #1`, `Strangers #2` for Tribal/Medieval). Target IDs persist across the incursion's entire lifespan, preserving continuity through radar dead zones, night rest, and classification escalations (`Hostile Warband [Contact-1]`), serialized into `SensoryManager` and `WorldObject_Incursion` save data.

### Changed

- Scoped CI spellcheck workflow (`.github/workflows/spellcheck.yml`) and `cspell.json` to target documentation and prevent false positives on C# engine symbols and scripts.

## [0.1.0] - 2026-10-18

### Added

- **Physical Incursion Engine:** Replaced instantaneous map-edge pawn materialisation with real-time planetary transit across the `WorldGrid` for walk-in enemy raids (`IncidentWorker_RaidEnemy`) and friendly trade caravans (`IncidentWorker_TraderCaravanArrival`).
- **Dynamic Origin Resolution:** Added `IncursionOriginResolver` to locate nearest faction settlements or establish procedural wilderness staging sites based on terrain accessibility.
- **Realistic Transit Simulation:** Implemented `IncursionMovementHandler` computing tile-by-tile pathing, biome travel costs, and live ETAs (hours to days depending on terrain).
- **Tactical Map Rendering:** Added composite dual-layer world rendering in `WorldObject_Incursion`:
  - Quad alignment fixed to point naturally toward planetary North at all camera zoom levels.
  - Distinct IFF colouring: Alert Crimson Red for hostile raids and Cool Cyan for friendly/neutral caravans.
  - Caravan marching silhouette underlay combined with an upright, prominent faction crest overlay.
- **Interactive Incursion Telemetry:** Added custom world inspect strings displaying originating faction, remaining distance, live ETA, and combat threat rating.
- **Storyteller & Cooldown Integration:** Added `IncursionArrivalManager` to trigger incidents upon physical hex arrival, with configurable options to reset `lastThreatBigTick` to prevent storyteller threat stacking.
- **Save/Load State Resilience:** Built full Scribe serialization (`Scribe_Values`, `Scribe_References`, `Scribe_Deep`) so ongoing incursions persist across game save cycles.
- **In-Game Settings Dialogue:** Added `GeoscapeMod` and `GeoscapeSettings` allowing players to configure threat cooldown resets and transit speed multipliers.
- **Architectural Specifications & Master Vision:** Comprehensive technical documentation for Geoscape & Radar, Interception, Containment & Biology, Invasion Campaign, Aquatic Operations (TftD), and DLC integrations.
