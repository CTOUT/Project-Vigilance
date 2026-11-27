# Implementation Roadmap & TODO

## Active Roadmap: Phase 2 — Geoscape & Radar Grid (Standalone Framework)

### Milestone 1.5: Foundation Framework, Modular Architecture & Player Configuration [COMPLETED & VERIFIED]

- [x] **Stream 1: Modular Architecture & Collection Packaging Review:**
  - **Modular Separation of Concerns:** Defined formal boundaries between `ProjectVigilance.Core` (foundation bitmasks, 3D world draw layers, Keplerian orbit solver, shared contracts) and pluggable domain modules (`ProjectVigilance.Geoscape`, `ProjectVigilance.Aquatics`, `ProjectVigilance.Battlescape`) in `docs/architecture/modular-system.md`.
  - **Dynamic Unified Mod Options Interface (`ISettingsTabProvider`):** Implemented `ISettingsTabProvider` and thread-safe `VigilanceSettingsRegistry` displaying a single unified `"Project Vigilance"` entry in RimWorld's native Mod Options list with dynamically registered sub-module tabs, redirection support, and isolated persistence.
  - **Packaging & Multi-Assembly Strategy:** Monorepo multi-assembly compilation paired with `LoadFolders.xml` for flexible Steam Workshop collection distribution.
- [x] **Stream 2: RimWorld Keyed Localisation Support (`Languages/English/Keyed/*.xml`):**
  - **Structured Reference Dictionaries:** Created clean, documented reference XML files in `Geoscape/1.7/Languages/English/Keyed/`:
    - `Geoscape_Letters.xml`: Early warning alerts, threat escalation, encounter aftermath, quest breadcrumbs.
    - `Geoscape_Dialog.xml`: Campaign rules wizard, first-time mod adoption modal, contact telemetry.
    - `Geoscape_Inspect.xml`: Satellite inspect panes, incursion telemetry, terrain masking alerts, listening post status.
    - `Geoscape_FloatMenus.xml`: World right-click menus, outpost deployment, repair/salvage actions, caravan interception choices.
    - `Geoscape_Settings.xml`: Mod settings tab labels, toggle labels, slider headers, CRT scope preview legends.
  - **Parameter Tokens & Formatting:** Enforced proper replacement tokens (`{0}`, `{1}`) and RimWorld grammar helpers (`CapitalizeFirst`, gender, and pluralisation rules).
- [x] **Stream 3: Mod Settings & Sensory Cosmetic Customisation Review:**
  - **Curated Theme Dropdown with Custom Override:**
    - Curated palette presets: _Tactical Cyan_ (Default), _Amber CRT_ (1980s Retro), _Emerald Phosphor_ (Military Surplus), _Crimson Alert_ (High Urgency), _Ghost White_ (Minimalist Monochromatic).
    - Accessibility presets: _Protanopia / Deuteranopia_ (Red-Green Safe), _Tritanopia_ (Blue-Yellow Safe), and _High-Contrast Tactical_.
    - Optional `"Use Custom Palette Overrides"` toggle revealing granular RGB/HSV sliders for Radar, Identification Horizon, Optical Scout Horizon, and Coastal Sonar.
  - **Feature Toggles & Modular Gating:** Audited and exposed granular toggles for core mechanics (`enableThermalIntegrityAudit`, `incursionsRestAtNight`, `enableTacticalIngressFlanking`, `startingSiteSelection`, `optimisticCaravanDetours`, `enableInterFactionCollisions`).
  - **Live CRT Scope Preview Integration:** Updated the `180x180 px` `RadarScopePreview` widget to display simulated Radar, Sonar, and Optical horizons with real-time palette updates.
- [x] **Stream 4: Modder Extensibility Hooks & Foundation Library Architecture:**
  - **XML `DefModExtension` Integration:** Enabled external mod developers to configure custom structures or vehicles as sensors (`DefModExtension_Sensor`) or trackable targets (`DefModExtension_Detectable`) without hard compile-time code dependencies.
  - **Public Sensor & Target Provider API:** Exposed and documented formal registration interfaces (`ISensorProvider`, `IDetectableTarget`) with dynamic registration on `SensoryManager` (`RegisterDynamicSensor`, `UnregisterDynamicSensor`, `RegisterDynamicTarget`, `UnregisterDynamicTarget`).
  - **Incursion & Tactical Event Lifecycle Hooks:** Exposed public static C# event delegates (`OnIncursionSpawned`, `OnIncursionDeparted`, `OnContactDetected`, `OnContactClassified`, `OnContactLost`, `OnIncursionIntercepted`, `OnAmbushSprung`, `OnCaptiveIncarcerated`).
- [x] **Stream 5: Agent Skill & Engineering Rule Enshrinement:**
  - Updated `GEMINI.md` and `.agents/rules/rimworld-development.md` with durable post-retrofit compliance rules:
    - _Rule 8:_ Keyed Localisation Mandatory (`"PV_*".Translate()`, zero hardcoded English literals in UI).
    - _Rule 9:_ Mod Settings & Feature Gating Required (all new mechanics expose toggles/settings).
    - _Rule 10:_ Extensibility & Modder Hooks Mandatory (`DefModExtension` and public lifecycle events).
    - _Rule 11:_ Sensory Palette Customisation & A11y (curated themes, colourblind presets, WCAG AA contrast).
    - _Rule 12:_ Modular Separation & Loose Coupling (interface contracts, soft dependencies).
- [ ] **Follow-Up Branch (`refactor/wave-4-i18n-and-decomposition` — Post-1.5 Audit Wave 4):**
  - Wire the 51 orphaned XML keys (`Geoscape_Inspect/Letters/Dialog/FloatMenus.xml`), add `Orbital`/`Aquatics` `Keyed/*.xml`, and complete remaining Rule 8 raw English UI extraction (`I18N-01`).
  - Execute low-risk `partial class` / domain splits for `WorldObject_Incursion`, `SensoryManager`, `WorldObject_Visibility_Patch`, and `GeoscapeSettings` tabs per [`docs/audits/2026-11-26-comprehensive-audit.md`](docs/audits/2026-11-26-comprehensive-audit.md).

---

### Milestone 1.6: Planetary Expedition & World Integrity [ACTIVE ROADMAP]

- [ ] **Planetary Expedition Phase & Orbital Landing Reserves (Civilization Settler Paradigm):**
  - **Pre-Settlement Survey Phase:** Implement pre-settlement expedition phase for uncharted rim worlds, allowing players to traverse and survey regional geography, waterways, and mountain passes before committing to a permanent settlement hex.
  - **Off-Map Orbital Staging Reserves:** Keep heavy starting supplies (bulk steel, food crates, components, heavy weaponry) off-map in orbital staging pods, preventing caravan encumbrance during initial land exploration.
  - **Colony Beacon Site Claiming:** Dedicated deployable structure (`Geoscape_ColonyBeacon`) carried by expedition party. Activating "Stake Colony Claim" creates player colony map and triggers precision drop pods delivering orbital staging cargo.
  - **Fail-Safe & Anti-Soft-Lock Guards:** Auto-deploy emergency drop pods if expedition party suffers catastrophic defeat, or allow secondary beacon fabrication at crafting spots if beacon is lost.
  - **Scenario Opt-In Architecture:** Configurable via `ExpeditionStartMode` (`Disabled`, `ScenarioDefault`, `ForceExpedition`) with XML `DefModExtension` support, automatically bypassing orbital/space starts (Odyssey, GravShips, SOS2).
- [ ] **Blind Landing Drop-Zone Biome Filter:** Add an optional biome whitelist filter in `GeoscapeSettings` / `StartingSiteFeedbackUtility` for blind `FogOfWar` random landings so players can exclude extreme biomes (e.g. Sea Ice) while keeping exact coordinates, neighbours, and map previews strictly hidden ([`docs/all_source_intelligence_and_exploration_plan.md`](docs/all_source_intelligence_and_exploration_plan.md)).
- [ ] **Odyssey DLC Synergy & Non-Destructive Gravcore Takeoff:**
  - Hook into 1.6 gravship components and orbital layer transit (`PlanetLayerDefs`).
  - Patch gravcore launch sequence to eliminate permanent world tile destruction, preserving planetary terrain integrity during orbital deployments without external mod dependencies.
- [ ] **Codebase & Naming Terminology Alignment:**
  - Audit legacy internal identifiers, enums, comments, and XML keys (e.g. `ClassicHazeOfWar`, `HazeOfWar`, `Classic`, `HazeReconnaissanceManager`) against current product terminology (`Fog of War`, `Planetary Surveillance`, `Omniscient`).
  - Plan safe renames and Scribe backwards-compatibility aliases (`BackCompatibilityConverter_VigilanceModular` / enum parsing shims) so existing saves load cleanly with zero regressions.
- [ ] **Environmental Thermal Dynamics & Material Phase States (Future Feature):**
  - **Device Operational Temperature Envelopes:**
    - Sub-zero degradation (< -30°C): Sensor antenna rotation slowdown, battery brownout risk, sweep cadence penalty, and mechanical freezing.
    - Extreme heat (> +60°C): Thermal throttling, heat sink burnout, component breakdown, and emergency failsafe shutdown.
  - **Cryogenic Ductile-to-Brittle Transition:**
    - Under severe sub-zero cold (< -40°C), structural metals, plasteel, and stonework do not passively decay, but suffer an embrittlement multiplier (+50% to +100% damage taken from blunt concussive impacts, artillery shock waves, and explosive breaches).
  - **Superheated Kiln / Oven Room Structural Degradation:**
    - High-temperature ambient chambers (> 400°C) cause progressive structural damage and structural collapse to low-melting-point materials and electrical wiring, preventing indefinite exploit cheese while preserving realistic thermal resistance.

---

### Milestone 1.7: Theatre Coordination & Aquatic Operations

- [ ] **Intelligent Multi-Base Strike Coordination:**
  - Replace disconnected per-colony storyteller triggers with theatre-aware campaign logic.
  - Coordinate diversionary raids, staggered multi-base assaults, or synchronised offensive campaigns across player settlements based on proximity and strategic threat level.
- [ ] **Modular Separation — Aquatic Operations Extraction (`Vigilance.Aquatics`):**
  - Migrate Coastal Sonar (`SonarTraversalUtility`, `PlaceWorker_CoastalSonar`, `Geoscape_SonarArray`, `Vigilance_ListeningPost_Sonar`) out of `ProjectVigilance.Geoscape` into standalone `Vigilance.Aquatics` module.
  - Establish soft contract boundaries via `ISensorProvider` and lay groundwork for oceanic bathymetry.
- [ ] **Elevation-Aware Visual Observation:**
  - Evaluate whether `tile.elevation` should provide additional vantage bonus for optical observation systems (Lookout Watchtowers, Signal Beacons, Caravan Lookouts) beyond existing `Hilliness`-based modifiers; high-altitude mountain tiles (> X metres) receiving additional observation bonus. Avoid applying to radar systems unless testing demonstrates benefit.

---

### Milestone 1.8: Classic X-COM Geoscape Command Deck & Diegetic Experience

- **Architectural Design Blueprint:** [`docs/archive/Classic_XCom_Geoscape_Blueprint.md`](docs/archive/Classic_XCom_Geoscape_Blueprint.md)
- **Interactive Visual Mockup & Art Prototype:** [`docs/archive/Mockups/geoscape_command_deck_mockup.html`](docs/archive/Mockups/geoscape_command_deck_mockup.html)
- [ ] **Temporal "Contact Drop" Engine & CRT Telemetry Modal (`Dialog_ContactDetected`):** Automatic time deceleration to Normal (1x / 60 TPS) or Pause upon radar boundary ingress; monospace dot-leader telemetry readout with single-click `[CENTRE ON BOGEY — TIME = 5 SECS]` camera jump.
- [ ] **Diegetic Command Deck Sidebar & Harmony Non-Collision Architecture:** 220px classic right-hand operations sidebar with live seconds LED mission clock, discrete time compression buttons, and 3D trackball gimbal; Harmony offset for `LetterStack` (leftwards cascade) and conditional suppression of vanilla `WorldGlobalControls`.
- [ ] **Outpost Siting Hologram Preview Dome ("Select Site for New Base"):** Full-screen planetary survey mode replacing float menus with dynamic topographic vantage bonus overlay (+1 to +2 rings on hills/mountains) and blind-spot calculation.
- [ ] **TFTD Oceanic Bathymetry & Underwater USOs:** 3-tier depth world mesh using negative elevation data (`tile.elevation < 0`), acoustic sonar propagation occluding on landmasses, and abyssal trench submarine evasion.
- [ ] **Atmospheric Immersion & Cosmic Skybox:** Shaders for Classic Violet Nebula (1994) and Apocalyptic Crimson Abyss (TFTD 1995), Fresnel atmospheric limb glow, and sharp day/night terminator.

---

### Milestone 1.9: Imperfect Intelligence, Curiosity & Living Shroud

- **Architectural Design Blueprint:** [`docs/all_source_intelligence_and_exploration_plan.md`](docs/all_source_intelligence_and_exploration_plan.md)
- [ ] **Signal Confidence, Bearing Cones & Interferometric Triangulation:** Attach explicit confidence tags (`Unconfirmed`, `Bearing Only`, `Clutter`, `Confirmed`) and error arcs/strength brackets to marginal contacts; sharpen fixes automatically when two or more sensor stations overlap.
- [ ] **Explored Haze Staleness & Off-Screen World Drift:** Track `Last Observed` timestamps across unmonitored Explored Haze tiles and settlements so off-screen skirmishes, collapse, or territorial turnover cause historical map memory to go stale honestly rather than updating magically.
- [ ] **Tiered Allied Shared Vision & Early-Warning Relays:** Grant static settlement `Haze` visibility and forwarded hostile incursion alerts at Allied goodwill ($\ge +75$) with a powered `CommsConsole` (tech-tiered from courier dispatches to radio/Spacer telemetry data-links), while explicitly rejecting uncapped pawn `Sight`-stat scaling so radar infrastructure stays meaningful.
- [ ] **All-Source Field Intelligence (HUMINT, DOCEX, Regional Cartography & TECHINT):** Add tradeable Regional Charts (`Haze`-only `8–12` hex sectors with staleness timestamps), raided camp stashes, commander corpse logs, hacked terminals, and salvaged convoy transponders routed through `Geoscape_SigintConsole` to publish `?` site pins or uncertain search ellipses via `GeoscapeIntelLead` — strictly revealing leads or `Haze`, never free live `Clear` vision.
- [ ] **Non-Cheap Frontier Curiosity Hooks:** Surface indirect evidence along explored edges — roads/waterways cutting into Shroud, downstream river pollution/ash plumes, displaced stragglers escaping uncharted conflict, partial VHF radio intercepts, and hearsay-graded traveller rumours with varying reliability.
- [ ] **Physically Grounded Noise, Clutter & Enemy Deception:** Introduce traceable false positives and misreadings (solar flare radar ghosts, biological echoes on early `Geoscape_SonarArray`, nocturnal/weather optical distortion, decoy campfires, spoofed caravan transponders, and hostile ECM split-blips countered via `Geoscape_SigintConsole`).
- [ ] **Signal History & Cumulative Intelligence Dossier:** Maintain a non-quest historical signal journal logging recurring tracks (such as `Repeated Contact #12` with `First Observed`, `Observations`, `Confidence`, `Classification`, and `Last Seen`), integrating time-decayed uncertainty ellipses, low-confidence track correlation/splitting ambiguity, and historical sighting overlays in `[ INTEL ARCHIVE ]`.

---

### Milestone 1.10: Tactical Orbital Warfare, Targeted Probes & ASAT Defence

- **Architectural Design Blueprint:** [`docs/tactical_orbital_warfare_plan.md`](docs/tactical_orbital_warfare_plan.md)
- [ ] **Expendable Tactical Probes (Recon & Navigation):** Targeted hex deployment via transport pods or launch silos; finite operational lifespans (5–10 in-game days) with battery/fuel decay and automatic atmospheric de-orbit burnup.
- [ ] **Strategic Orbital Platforms & Ground Uplink Bandwidth:** High-orbit geostationary military and comms platforms bounded by colony Comms Console and Phased Array uplink channel capacity (X-COM style).
- [ ] **Military Satellite Strike Restrictions:** Finite kinetic payload (3 strikes total), 3-day capacitor recharge cooldown, and 2,000W ground targeting lock requirement.
- [ ] **Hostile Anti-Satellite (ASAT) Countermeasures:** Surface-to-orbit missile silos at pirate outposts intercepting low-orbit assets; electronic warfare / SIGINT jamming bubbles on advanced mech incursions.

---

## Phase 3: Tactical Battlescape & Subterranean Operations (Standalone Framework)

### Milestone 2.0: Battlescape Fog of War & Directional Sensory

- [ ] **3-Mode Battlescape Visibility & Directional Line-of-Sight (`MapComponent_TacticalVisibility`):**
  - **Epistemic Information Policy:** Exact Battlescape analogue to Geoscape Fog of War ("You don't know what you don't know"). Players only see active sightlines of friendly pawns; terrain uncovers through physical exploration; out-of-sight cells drop into stale memory haze.
  - **Mode 1: Classic Haze of War:**
    - _Obsidian Shroud (Tier 0):_ Completely unmapped black pitch across unexplored sectors.
    - _Slate Memory Haze (Tier 1):_ Discovered terrain and structures remain visible as last observed (honest staleness); dynamic pawns, items, and changes are masked.
    - _Live Line-of-Sight (Tier 2):_ Dynamic raycast field-of-view updated via pawn facing and optical range.
  - **Mode 2: Tactical Site Surveillance:** Pre-charted structural blueprints with masked dynamic hostiles.
  - **Mode 3: Omniscient:** Vanilla spectator fallback.
- [ ] **Zero-Allocation Spatial Cell Bitmasks:** Implement `ulong[] cellExploredBitset` and `ulong[] cellVisibleBitset` on `.NET 10` for maps up to 100,000 cells without GC allocations.
- [ ] **Multi-Tier Sensory Modelling:**
  - **Colonists & Biological Pawns:** 120° forward optical vision cone (~30 cells day, ~14 cells night, weather penalties) + 360° acoustic hearing horizon (~6–8 cells). Bionic and Archotech eye enhancements.
  - **Pets & War Beasts:** 360° olfactory (scent) and acute acoustic detection aura (~18–22 cells) detecting lurking hostiles behind thin cover and walls.
  - **Mechanoids:** Multi-spectral optics and phased radar arrays; night/smoke immunity; chassis-calibrated envelopes (Scyther wide-arc, Lancer 50-cell sniper tunnel vision, Centipede 360° radar).
- [ ] **Tactical Field Equipment:**
  - Handheld Motion Scanner with 250-tick acoustic sonar pulses emitting blips through walls.
  - Directional rail flashlights, deployable spotlight structures, illumination flares, and smoke/chaff screens.
- [ ] **Geoscape-to-Battlescape Integration:**
  - Radar intel tier dictates insertion Fog of War state and landing zone (LZ) security.
  - Live tactical countdown timers linked to inbound Geoscape reinforcements (mission doom clock).

---

### Milestone 2.1: Guerrilla & Pod AI (Anti-Cheese Combat)

- [ ] **Overhaul Suicide/Kamikaze Raids:**
  - Replace reckless headlong rushes with tactical squad behaviour (stealth, cover-to-cover movement, coordinated flanking).
- [ ] **Anti-Cheese & Anti-Trap Pathfinding:**
  - Threat-evaluation heuristics that detect high-density trap corridors, superheated/freezer death traps, and IED bait mazes, actively choosing alternative approach vectors or siege standoff.
- [ ] **Sapper & Perimeter Breaching Doctrines:**
  - Sapper squads target structural perimeter weaknesses with breach axes and explosives rather than filtering through fatal funnels.
- [ ] **Fireteam Pod Cohesion & Overwatch:**
  - Fireteam pods remain in dormant patrol/overwatch states until alerted, preventing whole-map swarms.
- [ ] **Suppress & Peel Retreats:**
  - Battered squads execute covering fire, smoke deployment, and peeling retreats to link with reserve pods rather than fighting to the last man.

---

### Milestone 2.2: Subterranean Operations & X-COM Base Engineering

- [ ] **Subterranean Map Generation (`PocketMapUtility`):**
  - Reuse multi-map pocket layer architecture (Anomaly Pit Gates, Odyssey underground dungeons) to generate a subterranean bedrock map layer beneath the player's colony.
- [ ] **Buildable Base Access Elevator & Freight Shaft (`Building_BaseElevator`):**
  - Surface-to-subterranean transit structure providing power, thermal conduit, and pawn transport between floors.
- [ ] **Bidirectional NPC Traversal (Anti-Bunker Cheese):**
  - Custom transfer job driver (`JobDriver_UseElevator`) allowing pawns, livestock, and enemy raiders to traverse between surface and underground maps.
  - Hostile forces actively pursue fleeing colonists into subterranean levels; eliminates invulnerable bunker exploits.
- [ ] **Subterranean Base Infrastructure & Hazards:**
  - Structural support struts, cave-in risks, subterranean ventilation/heat exchange, and excavation of alien relics or deep-dwelling infestations.

---

## Phase 4: Module 2 — Interception & Rapid Response (Standalone Framework)

- [ ] Define craft stats (speed, fuel, range, armament slots).
- [ ] Build tactical air interception dialogue UI.
- [ ] Implement crash site generation (`SiteDef` with wreckage, debris, and survivor spawns).
- [ ] Create tactical deployment and extraction drop-zone mechanics.
- [ ] Build amphibious transport capabilities (Triton analogue for aquatic sites).
- [ ] **Orbit-Layer Craft Visibility Policy (4-Tier Domain Rule):** Expose Phase 4 Air & Sub-Orbital craft (interceptors, Skyranger dropships, atmospheric UFOs) alongside Space/Orbital entities (`IOrbitalEntity`) and strategic colonies (`MapParent`) on the `Orbit` camera layer, keeping tactical ground foot/animal caravans and unmanned outposts culled to the `Surface` layer.
- [ ] **Aerial Recon Sweeps & External Craft Sensor Hooks:** Grant interceptors, Skyranger dropships, and external vehicle/shuttle integrations (Odyssey shuttles, Vehicles Framework via `PatchOperationFindMod` + `DefModExtension_Sensor`) bounded corridor recon along flight paths gated by fuel, altitude, and airspace SAM/ASAT risk ([`docs/all_source_intelligence_and_exploration_plan.md`](docs/all_source_intelligence_and_exploration_plan.md)).
- [ ] **Standalone Test:** Validate interception combat and dropship deployment against standard pirate shuttles without Module 1.

---

## Phase 5: Module 3 — Alien Containment & Biology (Standalone Base System)

- [ ] Implement stasis pods and containment cell comps.
- [ ] Hook into Anomaly DLC study mechanics (with standalone fallback for non-DLC).
- [ ] Create autopsy bills and surgical extraction recipes.
- [ ] Implement interrogation mechanics with mental resistance tracking.
- [ ] **Item-Triggered Research Discovery:** Build "Fog of Research" system unlocking tech upon physical salvage, autopsy, or interrogation.
- [ ] **BIOINT & Forensic Geoscape Triangulation:** Emit soft-hooked `GeoscapeIntelLead` discoveries (search ellipses, pheromone/migration vectors, and `?` pins) via `ProjectVigilance.Core` from autopsies, Anomaly entity studies, tissue/spore tracing, and interrogations across aliens, human-alien hybrids, anomalies, insectoid hives, and sentient animal lairs without hard-coupling to `Geoscape` ([`docs/all_source_intelligence_and_exploration_plan.md`](docs/all_source_intelligence_and_exploration_plan.md)).
- [ ] Design reverse-engineering knowledge tree (Alloys, Elerium-115, Weapon Fragments).
- [ ] **Standalone Test:** Validate containment pods and autopsy bills on vanilla pawns/creatures in isolation.

---

## Phase 6: Module 4 — Invasion Campaign & Panic (Dependent Campaign Engine)

- [ ] **Dual Storyteller & Campaign Progression Architecture:**
  - **Universal Campaign Threat Governor (Any Storyteller):**
    - Intercept and throttle alien incursion generation across any storyteller (Cassandra, Randy, modded).
    - Hard-gate craft classes and alien tiers (Small Scout -> Medium Scout -> Harvester -> Terror Ship -> Battleship) by elapsed campaign days, panic thresholds, and tech milestones.
    - Prevents premature lethal encounters (e.g. day-6 battleships under Randy Random).
  - **Dedicated Thematic Storyteller (`StorytellerDef_XCOM`):**
    - Authentic 1994 pacing: monthly Council report cards, calm research intervals, and clustered multi-wave strategic operations.
- [ ] Build Council of Planetary Factions funding and monthly evaluation ledger.
- [ ] Implement regional panic tracking per territory.
- [ ] Implement Alien Strategic AI (mission generation: Scout, Harvest, Infiltration, Terror, and coordinated multi-base planetary offensives).
- [ ] Create global Invasion Doom Clock and Dark Events counter-operations.
- [ ] Implement faction defection / alien pact mechanics.

---

## Phase 7: Module 5 — Alien Arsenal & Factions (Thematic Content Pack)

- [ ] Implement alien pawn kinds (Sectoids, Mutons, Chryssalids, Aquatoids, Deep Ones).
- [ ] Create weapon sets: Laser, Plasma, Sonic, and Harpoon/Gauss.
- [ ] Create armour sets: Kevlar, Carapace, Titan, and Diving/Aquatic suits.
- [ ] Integrate sound effects and visual FX faithful to the franchise.

---

## Completed Milestones

<details>
<summary><b>v0.4.1 — Tooling, Visual Assets &amp; Structural Materials (Shipped 2026-11-18)</b></summary>

### Milestone 1.4: Tooling, Visual Assets & Structural Materials

- [x] **Stream 1: Tooling & CI/CD — Mock Reference Assemblies & Headless Tests:**
  - **Game Assembly Stubs (`refasmer`):** Generated stripped public API reference assemblies for RimWorld 1.7 Waylith core assemblies (`Assembly-CSharp.dll`, `RimWorld.dll`, `UnityStubs.dll`, `Waylith.UnityCompat.dll`). Stored stubs in `lib/ref/` and added generation script ([`scripts/generate-reference-assemblies.ps1`](scripts/generate-reference-assemblies.ps1)).
  - **Automated Pull Request CI (`dotnet-ci.yml`):** Configured GitHub Actions workflow ([`.github/workflows/dotnet-ci.yml`](.github/workflows/dotnet-ci.yml)) running `dotnet restore` and `dotnet build` with fallback to `lib/ref/` reference assemblies.
  - **Zero-Warning Enforcement:** Integrated `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in project files and patched obsolete `Harmony.PatchAll()` call.
- [x] **Stream 2: Custom Visual Asset Pipeline & Faction Livery Tinting (Functional Placeholders):**
  - [x] **Orbital Infrastructure (`Textures/ProjectVigilance/Geoscape/World/Orbital/`):**
    - Authored custom 128×128 `RGBA32` functional placeholder sprites with neutral white/grayscale palette masks for clean `Faction.Color` tinting via [`scripts/generate-orbital-icons.ps1`](scripts/generate-orbital-icons.ps1).
    - Persistent Orbital Logistics Hub (`WorldObject_OrbitalStation`): Spoke-and-hub modular space station and orbital dock with 4 radial truss spokes, outlying pods, and solar wings ([`OrbitalHub.png`](Geoscape/1.7/Textures/ProjectVigilance/Geoscape/World/Orbital/OrbitalHub.png)).
    - Populated Coverage Anchoring: Restrict station and satellite spawning to active world coverage and populated hexes (`FindPopulatedCoverageAnchorTile`).
    - Altitude Stratification & Hard Orbit Ceiling: Banded visual altitude mapping (`LEO` 4–10, `MEO` 10–24, `GEO` 24–34) and hard render limits (`MaxOrbitalRenderRadius = 135f` in `OrbitalFlightModel` and `145f` in `WorldObject_DrawPos_Patch`), with restored `MapParent`/`Site` Odyssey altitude telemetry, Orbit-layer surface clutter culling (`IsSurfaceAnchorVisibleFromOrbit`), and deep-orbit raycast click projection (`TryProjectRayToPlanetDirection`).
    - Specialized Satellite Chassis (`WorldObject_Satellite`): Functional placeholder sprites for Reconnaissance ([`Satellite_Recon.png`](Geoscape/1.7/Textures/ProjectVigilance/Geoscape/World/Orbital/Satellite_Recon.png)), Trade ([`Satellite_Trade.png`](Geoscape/1.7/Textures/ProjectVigilance/Geoscape/World/Orbital/Satellite_Trade.png)), Navigation ([`Satellite_Nav.png`](Geoscape/1.7/Textures/ProjectVigilance/Geoscape/World/Orbital/Satellite_Nav.png)), Communication ([`Satellite_Comms.png`](Geoscape/1.7/Textures/ProjectVigilance/Geoscape/World/Orbital/Satellite_Comms.png)), and Military ([`Satellite_Military.png`](Geoscape/1.7/Textures/ProjectVigilance/Geoscape/World/Orbital/Satellite_Military.png)).
  - [x] **Lightweight Geoscape Outposts (`Textures/ProjectVigilance/Geoscape/World/Outposts/`):**
    - Authored custom 128×128 `RGBA32` functional placeholder icons with neutral white/grayscale palette masks via [`scripts/generate-outpost-icons.ps1`](scripts/generate-outpost-icons.ps1).
    - Dedicated world map markers for Automated Listening Posts ([`Outpost_Radar.png`](Geoscape/1.7/Textures/ProjectVigilance/Geoscape/World/Outposts/Outpost_Radar.png)), Signal Beacons ([`Outpost_Beacon.png`](Geoscape/1.7/Textures/ProjectVigilance/Geoscape/World/Outposts/Outpost_Beacon.png)), and Automated Sonar Stations ([`Outpost_Sonar.png`](Geoscape/1.7/Textures/ProjectVigilance/Geoscape/World/Outposts/Outpost_Sonar.png)).
    - Encounter Aftermath ([`EncounterAftermath.png`](Geoscape/1.7/Textures/ProjectVigilance/Geoscape/World/Outposts/EncounterAftermath.png)): Smouldering battlefield wreckage and crater debris site replacing `Sites/ItemStash`.
  - [x] **Battlescape Sensory Buildings (`Textures/ProjectVigilance/Geoscape/Buildings/Sensory/`):**
    - Lookout Watchtower: Dedicated 2×3 footprint timber/stone observation tower with staircase and covered viewing platform, replacing `SteleLarge` ([`LookoutTower.png`](Geoscape/1.7/Textures/ProjectVigilance/Geoscape/Buildings/Sensory/LookoutTower.png)).
    - Colony Sensory Infrastructure: Functional placeholder orthographic graphics for Early-Warning Radar Dome (3×3) ([`RadarDomeSmall.png`](Geoscape/1.7/Textures/ProjectVigilance/Geoscape/Buildings/Sensory/RadarDomeSmall.png)), Spacer Phased Array (4×4 with 4-way rotation) ([`PhasedArrayLarge_south.png`](Geoscape/1.7/Textures/ProjectVigilance/Geoscape/Buildings/Sensory/PhasedArrayLarge_south.png)), SIGINT Decryption Console (3×2 with 4-way rotation) ([`SigintConsole_south.png`](Geoscape/1.7/Textures/ProjectVigilance/Geoscape/Buildings/Sensory/SigintConsole_south.png)), and Coastal Hydrophone Station (3×3) ([`SonarStation.png`](Geoscape/1.7/Textures/ProjectVigilance/Geoscape/Buildings/Sensory/SonarStation.png)).
  - [x] **Dynamic Faction Colour Shifting Specification:**
    - All world entity icons and satellite sprites authored as neutral grayscale / white silhouette masks with transparent backgrounds (`RGBA32`).
    - Clean runtime multiplication with `Faction.Color` via `MaterialPool.MatFrom(..., ShaderDatabase.WorldOverlayTransparentLit, Faction.Color)` and `ExpandingIconColor` without hue clash, muddy saturation, or artifacting across all planetary factions.
    - Note: Assets serve as verified functional placeholders; dedicated high-fidelity art overhaul and custom `<shadowData>` tuning scheduled for visual polish milestone.
- [x] **Stream 3: Programmatic Thermal Integrity Post-Def Audit Engine:**
  - **Programmatic Post-Def Processing:** Implemented `ThermalIntegrityPostDefProcessor` executing at startup (`[StaticConstructorOnStartup]`) to programmatically enforce non-combustible properties across core, DLC, and mod defs without fragile manual XML patches.
  - **Structural & Precious Metals:** Set raw item and Stuff factor `Flammability = 0` on `Steel`, `Plasteel`, `Gold`, `Silver`, and `Uranium`, preventing metal walls, doors, barricades, and resource stacks from burning.
  - **Infrastructure & Sensory Weatherproofing:** Set `Flammability = 0` on `Geoscape_RadarSmall`, `Geoscape_RadarLarge`, `Geoscape_SigintConsole`, and `Geoscape_SonarArray`.
  - **Bridge Flammability Alignment:** Ensured Core wooden `Bridge` has 100% flammability (`1.0`) while Odyssey steel `HeavyBridge`, `AncientHeavyBridge`, and `Substructure` are set to `0`.
  - **Gravship Hull Dynamic Stuff Calibration:** Set `GravshipHull` base flammability to `1.0` so material Stuff properties govern (Bioferrite hulls burn at 75% flammability factor, metal hulls remain 0% immune).
  - **Automated All-Metal & Stone Machine Audit:** Automated classification setting fixed-recipe metal/stone structures (`ElectricSmelter`, `ElectricSmithy`, `TableMachining`, `FabricationBench`, `PowerSwitch`, `AutocannonTurret`, `Hopper`, `SignalJammer`) to `0` flammability while preserving `Battery` flammability (thermal runaway).
  - **Explicit Non-Combustibles & Archotech:** Set `0` flammability for `ReinforcedBarrel`, mechanoid boss chips (`SignalChip`, `PowerfocusChip`, `NanostructuringChip`), gravtech components (`Gravcore`, `GravlitePanel`, `ShuttleEngine`), archotech artifacts/bionics, and pure metal bionics.
- [x] **Stream 4: Standalone Verification:**
  - Validated all 7 Milestone 1.4 test cases (`TC-01`–`TC-07` in [`docs/archive/milestone_1_4_test_plan.md`](docs/archive/milestone_1_4_test_plan.md)) on clean RimWorld 1.7 installation.
  - Verified 441/441 unit tests passing with zero warnings under `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
  - Confirmed clean formatting (`prettier`) and British English spell checking (`cspell`).

</details>

<details>
<summary><b>v0.4.0 — Radar Infrastructure, Advanced Early Warning & Inter-Faction Encounters (Shipped 2026-11-16)</b></summary>

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
  - `WorldObject_ListeningPost` (Zero-TPS global entity, 100% unmanned, automated solar relay, coastal sonar station, and medieval stone/wood beacon; no WorldPawns garrisons).
  - **Civilization-Style Right-Click Hex Interaction (`FloatMenuMakerWorld`):**
    - _"Establish Automated Listening Post (100 Steel, 4 Components)"_, _"Deploy Automated Sonar Station (120 Steel, 4 Components)"_, and _"Construct Signal Beacon (150 Wood/Stone)"_ deployed directly from carried caravan inventory.
    - **Unified Labels & Builder-Scaled Assembly (`CaravanExpeditionUtility.GetListeningPostKindLabel`, `OutpostAssemblySpeedUtility`):** Standardised labels (`"Automated Listening Post"`, `"Automated Sonar Station"`, `"Signal Beacon"`) across menus, dialogues, inspect panes, and estimates. Assembly duration scales by lead builder speed (100%) plus assistants (50% each), clamped between `2,500` and `40,000` ticks with circadian shift awareness.
    - **Material Tracking & Exploit Prevention:** Signal Beacons track the specific consumed material (`stuffDef`), updating labels (e.g. `Granite blocks Signal Beacon`) and inspect strings. Dismantling recovers 75% of the matching material (112 items for beacons, 75 Steel + 3 Components for relays, 90 Steel + 3 Components for sonar stations), cleanly partitioned into valid item stacks respecting `stackLimit` (e.g. 75 + 37 items) to prevent infinite wood transmutation and engine stack overflow glitches.
    - _"Dismantle Outpost (Recover 75% Materials)"_ and _"Repair Sabotaged Station"_ (`25 Steel + 1 Component`, `30 Steel + 1 Component`, or `30 Wood/Stone`) actions on existing outposts.
  - Outpost vulnerability & sabotage events from passing hostile incursions.
  - **Automated Unit Testing & Verification (`Geoscape.Tests`):** Full MSTest suite validating radii scaling, capped relay vantage, full beacon vantage, `stuffDef` tracking, stack partitioning, builder speed scaling, and 75% salvage economics. Zero compilation warnings on .NET 10 / Waylith.
- [x] **Waylith Native DeepProfiler Instrumentation, Zero-Allocation Policy & Performance Benchmarking:**
  - `Verse.DeepProfiler.Start` / `Verse.DeepProfiler.End` native Waylith profiling blocks wrapping all sensory sweeps (`ProjectVigilance.PerformSensorySweep`), vision updates (`ProjectVigilance.SensorySweep.VisionUpdates`), target evaluations (`ProjectVigilance.SensorySweep.TargetEvaluations`), collision sweeps (`ProjectVigilance.InterFactionCollisions`), and background skirmish resolutions (`ProjectVigilance.SkirmishResolution`).
  - **Single-Pass Traversal Optimization:** Unified `ISensorProvider.TryGetDetectionDistance(target, out int distance)` evaluates sensor range and traversal distance in a single BFS pass, eliminating redundant graph queries per sensor/target pair on the sweep hot path.
  - Strict zero-allocation policy on `TickRare` (250 ticks) with pre-allocated and pooled dictionary buckets, list pools, query lists, and static predicates.
  - `PerformanceAndStressTests.cs`: comprehensive MSTest benchmark and stress suite simulating high-load scenarios (20 active radar installations and 100 concurrent world incursions) verifying the $\le 1.0\text{ ms}$ execution budget and the strict 0-byte GC allocation rule (`GC.GetAllocatedBytesForCurrentThread()`).
- [x] **Coastal Sonar Traversal, Automated Sonar Station & Placement Validation (`SonarTraversalUtility`, `PlaceWorker_CoastalSonar`, `Vigilance_ListeningPost_Sonar`):**
  - Added standalone **Automated Sonar Station** (`ListeningPostKind.AutomatedSonar` / `Vigilance_ListeningPost_Sonar`: 120 Steel + 4 Components; repair 30 Steel + 1 Component; salvage 90 Steel + 3 Components; 18 detection rings, 8 identification rings).
  - Enforced direct coastal access (at least one adjacent `WaterCovered` hex via `SonarTraversalUtility.HasCoastalAccess` and `PlaceWorker_CoastalSonar`) for both settlement `Geoscape_SonarArray` facilities and standalone Automated Sonar Stations.
  - Originated sonar propagation from adjacent `WaterCovered` tiles outward in directional V-shaped sectors (`DirectionalSectorCosThreshold = 0.45f`) strictly through connected `WaterCovered` tiles, treating land as an acoustic barrier and keying `SensoryManager` stationary caches by `(tileId, isSonar)` so Radar and Sonar overlap cleanly on water tiles without conflict.
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
- [x] **Simulated Inter-Faction Trade Routes, Grounded Encounters, Route Masking & Pursuit (`GroundedCaravanMeetingHelper`, `IncidentWorker_Ambush_Patch`, `IncursionInterceptionUtility`, `IncursionTacticalRetreatUtility`):**
  - **Organic Trade Route Network:** Physical, persistent NPC trade caravans (`WorldObject_Incursion`) travel between faction settlements with real cargo, merchant inventories, and overland travel schedules.
  - **Grounding Storyteller Caravan Meetings & Ambushes:** Harmony patches on `IncidentWorker_CaravanMeeting`, `IncidentWorker_CaravanDemand`, and `IncidentWorker_Ambush` (`IncidentWorker_Ambush_EnemyFaction` via `GroundedCaravanMeetingHelper.TryInterceptAmbush`) ground storyteller encounters into physical, travelling `WorldObject_Incursion` entities with persistent pawn rosters, cargo, and travel routes (leaving manhunter packs vanilla).
  - **Post-Retreat Hostile Pursuit (`IncursionTacticalRetreatUtility.ResumeIncursionAfterCaravanRetreat`, `Pawn_ExitMap_Patch`):** When a player caravan retreats from a tactical encounter map, able-bodied hostile forces ($\ge 50\%$ fit survivors, `PursuitMinimumFitFraction = 0.5f`) actively give chase on the Geoscape (`SetPursuitTarget`), while battered or non-hostile groups resume their route or return home.
  - **Caravan Route Visibility Masking (`WorldObject_Incursion.DrawVisibilityClippedSegment`, `GroundedCaravanMeetingHelper.GetTileDestinationLabel`):** Clips incursion path segments at the active vision perimeter in `ClassicHazeOfWar` and masks unexplored destination settlement names as `"an uncharted destination"`.
  - **Random Caravan Trade Loop Fix (`IncursionInterceptionUtility.ConsumeInterceptOrder`):** Consumes pending `CaravanArrivalAction_InterceptIncursion` directives on interception (`StopDead()`) and enforces encounter cooldown timers (`IsCaravanOnCooldown`) on `1a` interception checks while clearing any stale cooldown (`ResetCaravanCooldown`) on manual `"Hail & Trade"`.
  - **Orbital Faction Grounding Guard:** Explicitly filters candidate overland meeting factions to require active trader group makers and merchant cargo (`caravanTraderKinds.Count > 0`), safely substituting orbital-only factions (such as TradersGuild) or converting them to peaceful visits to prevent null-inventory crashes.
- [x] **Sensory Model Refinements & Vision Consistency:**
  - **Signal Beacon 0 Identification Radius:** Unmanned timber and stone signal beacons provide early-warning detection (Contact Tier) across 6 rings, but feature strictly 0 identification range, eliminating premature intel leakage without human or technological sensors.
  - **Terrain Masking Vision Horizon Alignment:** Corrected `SensoryManager.PopulateActiveVisionTiles` so masked mountainous hexes are not erroneously painted into the yellow identification horizon when viewed by non-penetrating sensors. Proximity sightline logic preserves direct optical observation for immediately adjacent hexes ($\le 1$ tile).
  - **Actionable Masking Telemetry:** Added clear, contextual feedback to incursion inspect panes explaining when heraldry and weapon details are obscured by mountain terrain masking.
- [x] **Detection-Layer Site Masking, Anomalous Landmarks & Optimistic Fog-of-War Exploration:**
  - **Detection-Layer Site Masking:** Non-player settlements, outposts, and camps emerging from the black shroud into the outer Detection ring ($R_{detect}$) are masked as "Unidentified site" contacts with gold question-mark mystery icons and concealed faction heraldry until brought within the inner Identification ring ($R_{id}$) or visited.
  - **Anomalous Landmark Exploration (`LandmarkSensoryUtility`, `WorldSearchVisibilityUtility`, `TileHighlightVisibilityUtility`):** Anomalous structures, monoliths, ruins, and insectoid mega-hives inside $R_{detect}$ render distinct cyan `'!'` discovery blips, while world search entries and single-tile hover/selection highlights are gated by exploration status under `ClassicHazeOfWar`.
  - **Optimistic Fog-of-War Travel Routing & Autonomous Obstacle Circumvention (`OptimisticPathingUtility`, `WorldPathing_FindPath_Patch`, `Dialog_FormCaravan_Patches`):** Assumes unexplored black hexes are traversable until uncovered, evaluating known barriers via `UnitTraversalCapability` (`LandCaravan`, `NavalUnit`, `Aircraft`) and `ExpeditionDetourState` break-even budgets (`MaxDetourMultiplier = 2.0f`). Viable detours emit a soft advisory notification (`"Caravan has encountered an obstacle and is searching for an alternative route."`), while unviable routes halt with a 4-option high-priority hard-stop prompt (`Select a new destination`, `Authorise a wider search radius`, `Cancel the expedition`, `Continue manual exploration`).
  - **Pre-Entry Impassable Tile Safeguards & Unexplored Road Masking:** `Caravan_PathFollower_SetupMoveIntoNextTile_Patch` and `Caravan_PathFollower_TryEnterNextPathTile_Patch` validate `IsTileActuallyImpassable`, uncover the single blocked tile via `MarkSurroundingTilesExplored(tile, 0f)` (`maxSteps = 0`), invoke `NotifyCaravanVisionUpdated`, and halt via `StopDead()` before stepping onto impassable tiles even after authorising a wider search radius; `WorldGrid_GetRoadMovementDifficultyMultiplier_Patch` returns `1.0f` on unexplored tiles so routes cannot trace undiscovered roads in the shroud.
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
  - **Automated Unit Testing & Verification (`OrbitalLogisticsTests.cs`):** 19 automated test cases validating ground track drift, Keplerian velocities, station service area transitions, trade contracts, caravan navigation bonuses, and constellation management.
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
  - **Forensic Audit Ledger & Cryptographic Savegame Integrity (`RuleChangeRecord`, `RuleLedgerSigner`, `GeoscapeCampaignRules.ReconcileLedgerOnLoad`):** Capped at 100 entries, recording escalations, unsafe transitions, and dev override toggles with pinned Day-0 rule snapshots (`CampaignCreated`), mod adoption provenance (`ModAdoption`), and legacy migration records (`LegacyMigration`). Secured with chained HMAC-SHA256 record hashing and non-destructive load-time replay reconciliation.
  - **DevMode-Gated Session-Only Override:** `allowUnsafeRuleTransitions` requires active `Prefs.DevMode`, resets on game exit, and clears automatically when DevMode is turned off.
  - **Legacy Save Migration:** Adopt current ModSettings defaults on first load of pre-1.3 saves, opening a 1-day review window with a deferred notification letter.
- [x] **5-Tab Mod Settings UI, Live CRT Scope Preview & Mountain Shadow Visualisation (`GeoscapeSettings`, `RadarScopePreview`, `RadarTerrainShadowStyle`):** Reorganised Mod Settings into five latched tabs (`Sensory`, `Radar`, `Incursions`, `Geoscape`, `Diagnostics`) featuring a live $180\times180\text{ px}$ CRT `RadarScopePreview` HUD and configurable Mountain Radar Shadow styles (`None`, `DashedContours`, `CellHatching`, `Both`).
- [x] **Environmental Sensory Disruption (`SensoryDisruptionUtility`):** Split environmental disruption by power and sensor tier: `IsSolarFlareActive` (`SolarFlare`, `EMIField`) disrupts colony `CompRadar` installations, `WorldObject_Satellite` orbital satellites, and electronic outposts (`AutomatedRelay`, `AutomatedSonar`), whereas `IsEclipseOrProlongedDarknessActive` (`Eclipse`, `VolcanicWinter`, `UnnaturalDarkness`, `SunBlocker`) specifically takes solar-powered `AutomatedRelay` and `AutomatedSonar` outposts `Offline` without interrupting grid-powered colony `CompRadar` installations or satellites; primitive optical `SignalBeacon` bonfires remain unaffected.
- [x] **Cross-Platform Linux & Windows Deployment Pipeline:** Added native Bash deployment automation script ([`scripts/deploy-geoscape.sh`](scripts/deploy-geoscape.sh)) and cross-platform PowerShell ([`scripts/deploy-geoscape.ps1`](scripts/deploy-geoscape.ps1)) supporting Linux / CachyOS (native Steam, Snap, and Flatpak Steam path auto-detection, Flatpak sandbox permission checks and read-only overrides, symlinks, `--skip-build` / `--copy` modes) and Windows (NTFS junctions). Implemented cross-platform MSBuild OS detection and non-blocking assembly rotation across game launches.
- [x] **Overlapping Entity Disambiguation Menu (X-COM Style Tactical Selector):**
  - Harmony prefixes on `WorldSelector.SelectUnderMouse` and `WorldSelector.SelectFirstOrNextFrom` intercepting multi-entity clicks on co-located world objects, settlements, and caravans.
  - Presents an X-COM style contact disambiguation float menu with faction crests, contact status descriptions, member counts, and sector selection (`Select Tile X`), eliminating click ambiguity while preserving modifier Shift-click selection and sensory masking.
- [x] **Smart Source-Agnostic Contextual Dispatch & Civilization-Style Settlement Staging (`WorldSelector_HandleWorldClicks_Patch`, `CaravanArrivalAction_HoldPosition`):**
  - Enhanced right-click interaction in `WorldSelector.HandleWorldClicks` eliminating the requirement to pre-select an origin settlement or caravan.
  - Dynamically discovers player caravans and colonies, sorted by proximity to target, presenting clear contextual options (_"Move caravan [CaravanName] to [Target] ([N] tiles away)"_ and _"Send caravan from [ColonyName] to [Target] ([N] tiles away)"_).
  - Integrates Civilization-style settlement staging (`CaravanArrivalAction_HoldPosition`: hold position outside settlements without entering or merging), listening post construction/dismantling expeditions, caravan formation, and fog-of-war exploration routing.
- [x] **WorldGrid Distance & Sensor Footprint Mathematical Alignment (`OrbitalFlightModel`):** Replaced degree-to-hex constant approximations (`1.8f`) with native `Find.WorldGrid.ApproxDistanceInTiles(angularDistanceRadians)`, guaranteeing 1:1 mathematical alignment with `GenDraw.DrawWorldRadiusRing` flood-fill geometry.
- [x] **Comms Console Overhead Trade Deduplication & Operational Guards (`Building_CommsConsole_GetCommTargets_Patch`):** Guarded target enumeration with local `HashSet<string>` and validated `sat.CanTradeNow`, preventing duplicate entries or invalid contact yields.
- [x] **Orbital Visuals & Dedicated Sprites (`WorldObjects_Orbital.xml`):** Replaced watchtower placeholder icons with dedicated satellite and settlement platform sprites (`World/WorldObjects/Expanding/Satellite`).
- [x] **Debug Actions & Testing Hygiene (`DebugActionsVigilance`):** Added unique suffix identifiers (tick timestamp) to spawned orbital assets and introduced bulk clean-up actions (`Orbital: Clear All Satellites`, `Orbital: Despawn Overhead Satellites`).
- [x] **Milestone Scope Gating & Content Stabilization:** Formally gated experimental features (coastal sonar array, non-standard satellites: Recon, Nav, Comms, Military) to `Prefs.DevMode` and debug menus for future dedicated milestones.
- [x] **Headless Unit Test Safe Lifecycle (`WorldObject_Satellite`):** Guarded against uninitialized `Find.WorldObjects` and `Find.FactionManager` singletons in `SetFactionSafe` and sensor status checks.
- [x] **Milestone 1.3 Automated Verification (`Geoscape.Tests`):** Full MSTest suite passing (**425 unit tests**) covering campaign rules, HMAC integrity reconciliation, coastal sonar traversal, optimistic pathing safeguards, outpost assembly, orbital logistics, grounded encounters, idle map component tick performance, and sensory masking.

</details>

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
