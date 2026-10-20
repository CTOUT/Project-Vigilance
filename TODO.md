# Implementation Roadmap & TODO

## Active Roadmap: Phase 2 — Geoscape & Radar Grid (Standalone Framework)

### Milestone 1.1: Sensory Architecture, Visibility Modes & Scout Horizon (Shipped in v0.2.0)

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

---

### Milestone 1.2: Ground Interception & Forward Ambush (Completed)

- [x] **Field Interception & Forward Ambush:** Allow player caravans (and rapid-response transports) to intercept approaching incursions on the global hex grid, generating an encounter map where raiders can be engaged, ambushed, or routed before reaching the colony. Right-click float menus (`Intercept`, `Hail & Trade`, `Warn Away / Turn Back`), dynamic target pursuit tracking, and X-COM "Contact Lost" telemetry dialog. Night camp ambush advantages and Night-Owl trait bonuses.
- [x] **Retreat, Pursuit & Kidnap Recovery:** When raiders flee, rout, or kidnap colonists, simulate their physical retreat across the world map back to their home settlement (maintaining the incursion's assigned persistent tracking ID, e.g. `Contact-3 [Retreating]` / `Contact-3 [Kidnappers]`). Fleeing pawns preserve 1:1 wounds, carried loot, and captive colonists. Incursions simulate overland physiological processes (bleeding, natural healing, field tending with carried medicine, night rest, and hunger). Intercepting them spawns the captive colonist on the battlefield for rescue; reaching the stronghold transfers captives cleanly into vanilla's `Faction.kidnapped` system.
- [x] **Roadside Trading & Persistent Stock:** Friendly trade caravans generate their trade stock once upon inception (`Dialog_Trade`). Roadside trading deducts bought goods and adds sold items to persistent stock, preventing double-dipping or item duplication upon colony arrival.
- [x] **Warn Away / Turn Back:** Friendly/neutral travellers approaching an active danger zone can be warned off by player caravans or radio, reversing course toward home with positive faction goodwill (`+5`).
- [ ] **Abandoned Camp Fallback & Overland Track Pursuit:** When raiders or incursions arrive at an abandoned colony or vacated camp, replace instant disbanding with intelligent tactical behaviour:
  - **Ground Track Pursuit:** Evaluate recently evacuated player caravans from the tile. If the evacuation departed overland on foot or pack animals, roll a tactical tracking chance. After a delay searching the empty camp, reroute the incursion to actively pursue the caravan to its next destination or ambush it en route.
  - **Tactical Fallback & Retargeting:** If no tracks are discovered or the trail goes cold, raiders choose between marching back to their home base/nearest faction outpost, establishing an occupation camp, or opportunistically redirecting their assault toward an adjacent player settlement or ally.
- [ ] **Dynamic Inter-Faction Encounters:** Enable roaming NPC incursions and trade caravans to interact when their routes cross—simulating background skirmishes between warring factions, ambush aftermath sites, and trade rendezvous.
- [ ] **Physical Settlement Incarceration (Future Follow-up):** If retreating raiders reach their home settlement unintercepted, associate the kidnapped colonists with that specific enemy settlement tile, allowing players to mount direct rescue assaults against that stronghold to liberate their people.

---

### Milestone 1.3: Radar Infrastructure & Advanced Early Warning

- [ ] Implement `CompProperties_Radar` and `CompRadar` building components.
- [ ] Implement `WorldObjectComp_Radar` for settlements and forward listening posts.
- [ ] Build `RadarPulseHandler` utilizing `TickRare` (250 ticks) for efficient spatial distance checks.
- [ ] Implement semi-transparent radar coverage overlay rendering on the world map.
- [ ] Build Oceanic Sonar detection layer for water hexes.
- [ ] Early Warning Dispatcher: generate advance warning alerts and reveal telemetry based on radar tier.

---

### Milestone 1.4: Planetary Coordination & Version Support

- [ ] **Intelligent Multi-Base Strike Coordination:** Replace disconnected per-colony storyteller triggers with theatre-aware campaign logic capable of orchestrating diversionary raids, staggered multi-base assaults, or synchronised offensive campaigns across player settlements.
- [ ] **Community Request: RimWorld 1.6 Legacy Backport Assessment:** Investigate compilation of `ProjectVigilance.Geoscape` for RimWorld 1.6 targeting `.NET Framework 4.7.2` / `.NET Standard 2.1` to support players on the previous release.
- [ ] **Standalone Test:** Validate radar detection, listening posts, and fog of war in vanilla RimWorld 1.7 with zero other Vigilance mods loaded.

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

#### See [CHANGELOG.md](CHANGELOG.md#010---2026-10-18) for full release notes
