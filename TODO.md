# Implementation Roadmap & TODO

## Active Roadmap: Phase 2 — Geoscape & Radar Grid (Standalone Framework)

### Milestone 1.3: Radar Infrastructure, Advanced Early Warning & Inter-Faction Encounters

- [x] **Tactical Radar Coverage & Perimeter Outlines (`WorldDrawLayer_RadarCoverage`):** Implemented native 3D mesh overlay rendering glowing perimeter boundary rings and hexagonal holographic honeycomb grid over active sensor envelopes on the planetary globe. Configurable styles (Boundary & Grid, Boundary Only, None), 5 color palettes (Tactical Cyan, Surveillance Amber, Emerald Phosphor, Crimson Alert, Ghost White), adjustable opacity, and optional background surveillance dimming (Option 3 Hybrid). Zero-allocation hot path with dynamic sensor movement tracking.
- [x] **Colony Technological Radar Infrastructure:**
  - `CompProperties_Radar` and `CompRadar` with dynamic standby vs active power scaling (`standbyPowerDraw` / `activePowerDraw`) and explicit integer ring fields (`detectionRadius = 24`, `identificationRadiusDish1 = 10`, `identificationRadiusDish2 = 17`, `identificationRadiusDish3 = 22`).
  - **Multi-Dish Settlement Stacking (Interferometry & Identification Expansion):** Traversal ring distance (Ring 0 = origin, each ring $n$ adds $6 \times n$ tiles). Maximum detection radius $R_{detect}$ stays stable at **24 rings**, while secondary dishes expand the inner **Identification / Classification Horizon ($R\_{id}$)** outward from 10 rings (~40%) $\rightarrow$ 17 rings (~70%) $\rightarrow$ 22 rings (~92%) at a locked 250-tick sweep cadence, and provides fail-safe redundancy.
  - **Sensor Terrain Model (Locked Model B+: "Vantage & Masking"):**
    - _Vantage Bonus (Origin Tile):_ Optical lookouts gain $+1$ to $+3$ rings on hills/mountains; outposts gain $+1$ to $+2$ rings. High-tech radars retain fixed $R_{detect}$ with $+1$ to $+2$ rings to $R_{id}$.
    - _Terrain Masking (Target Tile):_ Incursions in `Mountainous` tiles delay classification to Contact Tier amber blips until exiting into open terrain. Spacer Phased Array + SIGINT Console penetrates terrain masking.
    - Zero raycasting; zero radar shadows; zero mesh deformation.
  - **Sensory Building Defs & Research Projects:**
    - `Geoscape_LookoutTower` (Neolithic/Medieval, 8 rings + vantage, 0 W passive optical observation, Contact Tier).
    - `Geoscape_RadarSmall` (Industrial Early-Warning Dome, 14 rings, 350W/750W, Contact Tier).
    - `Geoscape_RadarLarge` (Spacer Phased Array, 24 rings, 800W/1,800W, Classified Tier).
    - `Geoscape_SigintConsole` (Ultra SIGINT Decryption Console, `CompFacility` linkable, 700W/1,400W, upgrades Phased Array to Tier 4 Analysed; penetrates terrain masking).
    - `Geoscape_SonarArray` (Industrial Coastal Hydrophone Station, 18 rings water/coast; future note: bathymetry/depth).
  - **Automated Unit Testing & Verification (`Geoscape.Tests`):** Full MSTest test suite verifying `CompProperties_Radar` validation rules, multi-dish interferometry progression, and terrain masking gating. Zero compilation errors or warnings on .NET 10 / Waylith.
- [x] **Lightweight Geoscape Outposts & World Map Strategic Context Actions:**
  - `WorldObject_ListeningPost` (Zero-TPS global entity, 100% unmanned, automated solar relay vs medieval stone beacon; no WorldPawns garrisons).
  - **Civilization-Style Right-Click Hex Interaction (`FloatMenuMakerWorld`):**
    - _"Establish Automated Listening Post (100 Steel, 4 Components)"_ and _"Construct Signal Beacon (150 Wood/Stone)"_ deployed directly from carried caravan inventory.
    - **Material Tracking & Exploit Prevention:** Signal Beacons track the specific consumed material (`stuffDef`), updating labels (e.g. `Granite blocks Signal Beacon`) and inspect strings. Dismantling recovers 75% of the matching material (112 items), cleanly partitioned into valid item stacks respecting `stackLimit` (e.g. 75 + 37 items) to prevent infinite wood transmutation and engine stack overflow glitches.
    - _"Dismantle Outpost (Recover 75% Materials)"_ and _"Repair Sabotaged Station"_ actions on existing outposts.
  - Outpost vulnerability & sabotage events from passing hostile incursions.
  - **Automated Unit Testing & Verification (`Geoscape.Tests`):** Full MSTest suite validating radii scaling, capped relay vantage, full beacon vantage, stuffDef tracking, stack partitioning, and 75% salvage economics. Zero compilation warnings on .NET 10 / Waylith.
- [x] **Waylith Native DeepProfiler Instrumentation, Zero-Allocation Policy & Performance Benchmarking:**
  - `Verse.DeepProfiler.Start` / `Verse.DeepProfiler.End` native Waylith profiling blocks wrapping all sensory sweeps (`ProjectVigilance.PerformSensorySweep`), vision updates (`ProjectVigilance.SensorySweep.VisionUpdates`), target evaluations (`ProjectVigilance.SensorySweep.TargetEvaluations`), collision sweeps (`ProjectVigilance.InterFactionCollisions`), and background skirmish resolutions (`ProjectVigilance.SkirmishResolution`).
  - **Single-Pass Traversal Optimization:** Unified `ISensorProvider.TryGetDetectionDistance(target, out int distance)` evaluates sensor range and traversal distance in a single BFS pass, eliminating redundant graph queries per sensor/target pair on the sweep hot path.
  - Strict zero-allocation policy on `TickRare` (250 ticks) with pre-allocated and pooled dictionary buckets, list pools, query lists, and static predicates.
  - `PerformanceAndStressTests.cs`: comprehensive MSTest benchmark and stress suite simulating high-load scenarios (20 active radar installations and 100 concurrent world incursions) verifying the $\le 1.0\text{ ms}$ execution budget and the strict 0-byte GC allocation rule (`GC.GetAllocatedBytesForCurrentThread()`).
- [x] **Oceanic Sonar Detection Layer:** Built coastal hydrophone station (`Geoscape_SonarArray`) and `isOceanicSonar` support in `CompProperties_Radar` / `ISensorProvider`, restricting maritime acoustic tracking strictly to water-covered hexes (`tile.WaterCovered`).
- [x] **Two-Stage Early Warning Dispatcher & Non-Peeking Escalation:**
  - `EarlyWarningDispatcher`: Stage 1 Outer Boundary Alert (`LetterDefOf.ThreatSmall` / `PV_TacticalEncounter_ThreatSmall`, amber pulsing contact blip, masked faction & threat points, hidden path) and Stage 2 Identification Threshold (`LetterDefOf.ThreatBig` hostile / `LetterDefOf.PositiveEvent` friendly caravan, red/cyan blip with faction crest, Model B+ terrain masking gating).
  - Identification preservation and direct high-spec detection bypass without alert replay.
- [x] **Concealed Dynamic Sites & Reconnaissance in the Haze (`HazeReconnaissanceManager`):**
  - **Haze Concealment for Dynamic Camps:** Dynamic world sites (work sites, logging/mining camps, bandit camps, quests) in unmonitored Haze sectors have announcement letters suppressed until uncovered by radar sweeps, scout caravans, or rumours.
  - **Despawn & Removal Lifecycle Hooking:** Harmony patch on `WorldObject.PostRemove` and `CheckUncoveredSites` stale purging ensure expired, cancelled, or destroyed dynamic sites are immediately purged from `concealedSites`, preventing save-game bloat and missing cross-reference serialization errors.
  - **Quest Timeout Clock Suspension:** Harmony prefix on `TimeoutComp.CompTickInterval` suspending timeout countdown while site is concealed in Haze so quests do not expire invisibly.
  - **Breadcrumb Rumours & Traveller Intelligence (`BreadcrumbRumourUtility`):** Arriving friendly trade caravans or visitors share intelligence on unmapped sites (_"Travellers from the [Faction] report sighting an active [Site Type] [X] hexes to the [Direction]."_), uncovering target hexes on the Geoscape.
  - **Automated Unit Testing & Verification (`Geoscape.Tests`):** Full MSTest suite covering alert dispatching, stage escalation, Harmony patch targets, TimeoutComp suspension, stale site cleanup, and 8-cardinal compass calculations.
- [x] **Forward Staging Ground Detection & Concealment:** Dynamic hostile staging camps and bandit outposts spawned in unmonitored Haze sectors remain concealed until charted by active radar sweeps, mobile scout caravans, or friendly traveller rumours.
- [x] **Dynamic Inter-Faction Encounters (`InterFactionEncounterManager`):**
  - **Tile-Bucketed Incursion Collisions ($O(M)$):** Evaluates collisions on `TickRare` (250 ticks) using pre-allocated, recycled tile buckets (`Dictionary<int, List<WorldObject_Incursion>>`), triggering algebraic background skirmishes strictly when hostile or rival forces occupy identical tiles.
  - **Algebraic Background Skirmishes:** Pure algebraic resolution taking into account combat points, tech level multipliers, and tactical variance without spawning a tactical map.
  - **Encounter Aftermath Sites (`WorldObject_EncounterAftermath`):** Spawns persistent aftermath sites on the world map containing salvageable trade loot and wounded survivor pawns harvested directly from the clash.
- [x] **Simulated Inter-Faction Trade Routes & Storyteller Caravan Encounter Grounding:**
  - **Organic Trade Route Network:** Physical, persistent NPC trade caravans (`WorldObject_Incursion`) travel between faction settlements with real cargo, merchant inventories, and overland travel schedules.
  - **Grounding Storyteller Caravan Encounters:** Harmony patches on `IncidentWorker_CaravanMeeting` and `IncidentWorker_CaravanDemand` via `GroundedCaravanMeetingHelper` ground storyteller encounters into physical, travelling world incursions with persistent pawn rosters and cargo rather than spawning ephemeral pawns.
  - **Persistent Traversal & Aftermath:** Meeting participants continue marching along their planetary route after interactions, remaining vulnerable to raider ambushes and contributing to living-world emergent events.
  - **Orbital Faction Grounding Guard:** Explicitly filters candidate overland meeting factions to require active trader group makers and merchant cargo (`caravanTraderKinds.Count > 0`), safely substituting orbital-only factions (such as TradersGuild) or converting them to peaceful visits to prevent null-inventory crashes.
- [x] **Sensory Model Refinements & Vision Consistency:**
  - **Signal Beacon 0 Identification Radius:** Unmanned timber and stone signal beacons provide early-warning detection (Contact Tier) across 6 rings, but feature strictly 0 identification range, eliminating premature intel leakage without human or technological sensors.
  - **Terrain Masking Vision Horizon Alignment:** Corrected `SensoryManager.PopulateActiveVisionTiles` so masked mountainous hexes are not erroneously painted into the yellow identification horizon when viewed by non-penetrating sensors. Proximity sightline logic preserves direct optical observation for immediately adjacent hexes ($\le 1$ tile).
  - **Actionable Masking Telemetry:** Added clear, contextual feedback to incursion inspect panes explaining when heraldry and weapon details are obscured by mountain terrain masking.
- [x] **Detection-Layer Site Masking & Optimistic Fog-of-War Exploration:**
  - **Detection-Layer Site Masking:** Non-player settlements, outposts, and camps emerging from the black shroud into the outer Detection ring ($R_{detect}$) are masked as "Unidentified site" contacts with gold question-mark mystery icons and concealed faction heraldry until brought within the inner Identification ring ($R_{id}$) or visited.
  - **Optimistic Fog-of-War Travel Routing:** Caravan right-click travel directives assume unexplored black hexes are traversable until actively uncovered, re-routing locally if impassable terrain is discovered without leaking map knowledge ahead of time.
  - **Camera-Aligned World Outpost & Sensor Icons:** Fixed rotated world icons when zoomed in, ensuring outpost, beacon, and sensor markers remain upright and camera-aligned regardless of zoom level or globe latitude.
- [x] **Orbital Logistics, Persistent Stations & Drifting Satellite Infrastructure Framework:**
  - **Drifting Orbital Mechanics Engine (`OrbitalFlightModel` & `OrbitalParameters`):** Replaced fixed repeating ground tracks with physically consistent, closed-form analytical Keplerian mechanics ($O(1)$ computation, zero heap allocations). Incorporates planetary oblateness nodal precession ($\dot{\Omega}$, $J_2$ perturbation) and incommensurate orbital periods ($T_{\text{orbit}} \nmid 60{,}000$), ensuring satellite ground tracks smoothly and continuously drift across planetary longitudes.
  - **Persistent Orbital Station Hubs (`WorldObject_OrbitalStation`):** Stable orbital operational centres for orbital factions (such as `TradersGuild` / _Merchant's Bazaar_). Manages satellite constellations, handles telemetry sync, and executes automated fabrication queues for lost satellites. Monitors a $35^\circ$ angular service corridor where satellites transition to `In Service Area` for maintenance and cargo restock.
  - **Five Distinct Satellite Mission Roles (`SatelliteRole`, `WorldObject_Satellite`, `Satellite_Trade`):**
    - _Trade Satellites:_ Implements `ICommunicable`, `ITrader`, and `IThingHolder`. Dynamic orbital comms trade window when passing overhead colonies with powered `Building_CommsConsole`, dropping cargo via drop pods.
    - _Reconnaissance Satellites:_ High-inclination polar sweeps ($88^\circ$), implements `ISensorProvider` (`IsTechnological = true`, `PenetratesTerrainMasking = true`, `MaxIntelligenceTier = Analysed`), clearing Haze/Fog of War across all latitudes.
    - _Military Satellites:_ High-inclination tactical surveillance platforms providing fire control and instant incursion analysis in contested sectors.
    - _Navigation Satellites:_ Medium-orbit positioning constellations ($12{,}000\text{ km}$). Surface caravans passing under the satellite's footprint receive a 15% overland movement speed acceleration (`CaravanTicksPerMoveUtility_GetTicksPerMove_Patch`).
    - _Communication Satellites:_ High-apogee relays ($35{,}786\text{ km}$) bridging remote, unmanned listening posts across mountain chains and oceans to the player's colony sensor network.
  - **Extensibility for Future Orbital Vessels (`IOrbitalEntity`):** Common contract unifying stations, satellites, and future mobile actors (gravships, shuttles, interceptors, UFOs) without modifying station or satellite logic.
  - **Orbital Ecosystem Manager (`OrbitalManager`):** Global `WorldComponent` tracking orbital infrastructure and automatically deploying stations and initial constellations for orbital factions lacking ground bases.
  - **Automated Unit Testing & Verification (`OrbitalLogisticsTests.cs`):** 15 automated test cases validating ground track drift, Keplerian velocities, station service area transitions, trade contracts, caravan navigation bonuses, and constellation management. All 125 unit tests in `Geoscape.Tests` passing.
- [x] **Campaign Rules, Mutability Framework, Campaign Wizard & Blind Landing Feedback:**
  - **Epistemic Principle Alignment:** Embed twin core principles—_"You don't know what you don't know"_ and _"Information must be earned, not guessed"_—into the save architecture. Establish **Fog of War as the recommended default visibility mode**.
  - **Two-Axis Mutability Engine (`ProjectVigilance.Geoscape.Rules`):** Decouple lifecycle constraints (`Mutable`, `Escalatable`, `Immutable`) from transition side-effects (`RebuildWorldLayers`, `RescanSensors`, `ReleaseConcealedSites`, `RepathPlayerCaravans`).
  - **Authoritative Persistence (`GeoscapeCampaignRules` & `GeoscapeRules`):** Persist `GeoscapeRuleSet` in save data (`.rws`) via `WorldComponent`. Expose zero-allocation `GeoscapeRules` facade routing all production queries to the save, falling back to defaults in tests and at the main menu.
  - **First-Class Campaign Creation Wizard Page (`Page_GeoscapeRules`):** Native campaign wizard page stitched between Storyteller selection and World Parameters via `PageUtility_StitchedPages_Patch`, displaying $2 \times 2$ profile cards (**Fog of War**, **Planetary Surveillance**, **Hardcore Vigilance**, **Omniscient**) and launching dedicated Anomaly-style modal dialog (`Dialog_GeoscapeCampaignRules`) for granular customisation without card clipping.
  - **First-Time Mod Adoption Lifecycle (`Dialog_GeoscapeModAdoption`):** Modal dialog on loading pre-existing vanilla saves offering non-destructive Planetary Surveillance default, 1-day grace period, or Fog of War blackout challenge.
  - **Starting Site Selection Feedback & Blind Touchdown (`StartingSiteFeedbackUtility` & `StartingSiteSelectionFeedback_Patch`):**
    - _Ambiguous Rejection on Physically Impossible Sites:_ Replaces explicit ocean and mountain errors on unexplored hexes under Fog of War with `"Unviable landing coordinates: Selected sector cannot support a colony drop."`, eliminating sonar-probing.
    - _Blind Touchdown on Environmental Hazards:_ Suppresses Biotech pollution, extreme climate, and harsh biome confirmation dialogue prompts so players land blind into uncharted sectors.
    - _Faction Proximity Intelligence Masking:_ Silences proximity diplomatic penalty warnings near hidden faction bases in the shroud.
    - _Uncharted Inspect Telemetry:_ Masks detailed geology, fertility, elevation, and biome survey readouts on unexplored hexes.
  - **Forensic Audit Ledger (`RuleChangeRecord`):** Capped at 100 entries, recording escalations, unsafe transitions, and dev override toggles. Includes pinned Day-0 rule snapshot (`CampaignCreated`), mod adoption provenance (`ModAdoption`), and legacy migration records (`LegacyMigration`).
  - **DevMode-Gated Session-Only Override:** `allowUnsafeRuleTransitions` requires active `Prefs.DevMode`, resets on game exit, and clears automatically when DevMode is turned off.
  - **Legacy Save Migration:** Adopt current ModSettings defaults on first load of pre-1.3 saves, opening a 1-day review window with a deferred notification letter.
- [x] **Cross-Platform Linux & Windows Deployment Pipeline:** Added native Bash deployment automation script ([`scripts/deploy-geoscape.sh`](scripts/deploy-geoscape.sh)) and cross-platform PowerShell ([`scripts/deploy-geoscape.ps1`](scripts/deploy-geoscape.ps1)) supporting both Linux / CachyOS (symlinks, `~/.local/share` paths, `--skip-build` / `--copy` modes) and Windows (NTFS junctions). Implemented cross-platform MSBuild OS detection and non-blocking assembly rotation across game launches.
- [x] **Overlapping Entity Disambiguation Menu (X-COM Style Tactical Selector):**
  - Harmony prefix on `WorldSelector.SelectFirstOrNextFrom` intercepting multi-entity clicks on co-located world objects, settlements, and caravans.
  - Presents an X-COM style contact disambiguation float menu with faction crests, contact status descriptions, member counts, and sector selection (`Select Tile X`), eliminating click ambiguity while preserving modifier Shift-click selection and sensory masking.
- [x] **Smart Source-Agnostic Contextual Dispatch:**
  - Enhanced right-click interaction in `WorldSelector.HandleWorldClicks` eliminating the requirement to pre-select an origin settlement or caravan.
  - Dynamically discovers player caravans and colonies, sorted by proximity to target, presenting clear contextual options (_"Move caravan [CaravanName] to [Target] ([N] tiles away)"_ and _"Send caravan from [ColonyName] to [Target] ([N] tiles away)"_).
  - Integrates listening post dismantle expeditions, caravan formation, and fog-of-war exploration routing.

---

### Milestone 1.4: Planetary Coordination & Version Support

- [ ] **Planetary Expedition Phase & Orbital Landing Reserves (Civilization Settler Paradigm):**
  - Implement the pre-settlement expedition phase for uncharted rim worlds, allowing players to survey regional geography, waterways, and mountain passes before committing to a permanent base location.
  - **Landing Cargo / Mission Assets:** Keep heavy starting resources (bulk steel, food crates, components) off-map in orbital staging pods, deploying via drop pods only when the player stakes their colony claim via Colony Beacon.
  - **Scenario Opt-In Architecture:** Configurable via `ExpeditionStartMode` (`Disabled`, `ScenarioDefault`, `ForceExpedition`) with XML `DefModExtension` support, automatically bypassing orbital/space starts (Odyssey, GravShips, SOS2).
- [ ] **Intelligent Multi-Base Strike Coordination:** Replace disconnected per-colony storyteller triggers with theatre-aware campaign logic capable of orchestrating diversionary raids, staggered multi-base assaults, or synchronised offensive campaigns across player settlements.
- [ ] **Community Request: RimWorld 1.6 Legacy Backport Assessment:** Investigate compilation of `ProjectVigilance.Geoscape` for RimWorld 1.6 targeting `.NET Framework 4.7.2` / `.NET Standard 2.1` to support players on the previous release.
- [ ] **Standalone Test:** Validate radar detection, listening posts, and fog of war in vanilla RimWorld 1.7 with zero other Vigilance mods loaded.
- [ ] **Future Investigation: Elevation-Aware Visual Observation:**
  - Evaluate whether `tile.elevation` should provide an additional vantage bonus for optical observation systems (Lookout Watchtowers, Signal Beacons, Caravan Lookouts) beyond the existing `Hilliness`-based modifiers.
  - Consider whether exceptionally high-altitude mountain tiles (> X metres) should receive an additional observation bonus.
  - Do not apply to radar systems unless future gameplay testing demonstrates a clear benefit.

---

### Future Milestone: Classic X-COM Geoscape Command Deck & Diegetic Experience

- **Architectural Design Blueprint:** [`Documentation/Design/Classic_XCom_Geoscape_Blueprint.md`](Documentation/Design/Classic_XCom_Geoscape_Blueprint.md)
- **Interactive Visual Mockup & Art Prototype:** [`Documentation/Design/Mockups/geoscape_command_deck_mockup.html`](Documentation/Design/Mockups/geoscape_command_deck_mockup.html)
- [ ] **Temporal "Contact Drop" Engine & CRT Telemetry Modal (`Dialog_ContactDetected`):** Automatic time deceleration to Normal (1x / 60 TPS) or Pause upon radar boundary ingress; monospace dot-leader telemetry readout with single-click `[CENTRE ON BOGEY — TIME = 5 SECS]` camera jump.
- [ ] **Diegetic Command Deck Sidebar & Harmony Non-Collision Architecture:** 220px classic right-hand operations sidebar with live seconds LED mission clock, discrete time compression buttons, and 3D trackball gimbal; Harmony offset for `LetterStack` (leftwards cascade) and conditional suppression of vanilla `WorldGlobalControls`.
- [ ] **Outpost Siting Hologram Preview Dome ("Select Site for New Base"):** Full-screen planetary survey mode replacing float menus with dynamic topographic vantage bonus overlay (+1 to +2 rings on hills/mountains) and blind-spot calculation.
- [ ] **TFTD Oceanic Bathymetry & Underwater USOs:** 3-tier depth world mesh using negative elevation data (`tile.elevation < 0`), acoustic sonar propagation occluding on landmasses, and abyssal trench submarine evasion.
- [ ] **Atmospheric Immersion & Cosmic Skybox:** Shaders for Classic Violet Nebula (1994) and Apocalyptic Crimson Abyss (TFTD 1995), Fresnel atmospheric limb glow, and sharp day/night terminator.

---

### Future Milestone: Imperfect Intelligence, Curiosity & Living Shroud

- [ ] **Signal Confidence, Bearing Cones & Interferometric Triangulation:** Attach explicit confidence tags (`Unconfirmed`, `Bearing Only`, `Clutter`, `Confirmed`) and error arcs/strength brackets to marginal contacts; sharpen fixes automatically when two or more sensor stations overlap.
- [ ] **Explored Haze Staleness & Off-Screen World Drift:** Track `Last Observed` timestamps across unmonitored Explored Haze tiles and settlements so off-screen skirmishes, collapse, or territorial turnover cause historical map memory to go stale honestly rather than updating magically.
- [ ] **Non-Cheap Frontier Curiosity Hooks:** Surface indirect evidence along explored edges — roads/waterways cutting into Shroud, downstream river pollution/ash plumes, displaced stragglers escaping uncharted conflict, partial VHF radio intercepts, and hearsay-graded traveller rumours with varying reliability.
- [ ] **Physically Grounded Noise, Clutter & Enemy Deception:** Introduce traceable false positives and misreadings (solar flare radar ghosts, biological echoes on early `Geoscape_SonarArray`, nocturnal/weather optical distortion, decoy campfires, spoofed caravan transponders, and hostile ECM split-blips countered via `Geoscape_SigintConsole`).
- [ ] **Signal History & Cumulative Intelligence Dossier:** Maintain a non-quest historical signal journal logging recurring tracks (such as `Repeated Contact #12` with `First Observed`, `Observations`, `Confidence`, `Classification`, and `Last Seen`), integrating time-decayed uncertainty ellipses, low-confidence track correlation/splitting ambiguity, and historical sighting overlays in `[ INTEL ARCHIVE ]`.

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
