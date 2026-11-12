# Project Vigilance — Milestone 1.3 In-Game Verification & Test Plan

This test plan provides comprehensive, step-by-step procedures to validate all systems implemented across **Milestone 1.3: Radar Infrastructure, Advanced Early Warning & Inter-Faction Encounters**.

It covers multi-dish radar interferometry, dynamic power scaling, Model B+ terrain vantage and mountain masking, SIGINT decryption penetration, coastal sonar arrays, holographic 3D radar coverage globe overlays, lightweight unmanned Geoscape listening posts, right-click caravan construction and salvage, two-stage early warning non-peeking escalation, concealed dynamic camps and breadcrumb rumours in the Fog of War, $O(M)$ tile-bucketed inter-faction collisions, algebraic background skirmishes, persistent encounter aftermath sites, grounded storyteller trade caravans, Keplerian orbital mechanics with storyteller-calibrated drift, dual-layer orbital rendering and faction colour tinting, native campaign creation wizard integration (`Page_GeoscapeRules`), Waylith native `DeepProfiler` telemetry, and Scribe save/load state resilience.

---

## Testing Priority & Focus Guide

To make testing as efficient as possible, every test case is categorised into a clear, colour-coded focus tier:

| Status Badge            | Priority Tier                         | Description & Player Action                                                                                                    |
| :---------------------- | :------------------------------------ | :----------------------------------------------------------------------------------------------------------------------------- |
| `🔴 Focus 1: Immediate` | **Priority 1: Active In-Game Focus**  | Brand-new systems and major wizard/visual overhauls requiring immediate hands-on verification in RimWorld.                     |
| `🟡 Focus 2: Playtest`  | **Priority 2: Core Colony Playtest**  | Key tactical, storyteller, and environmental mechanics to verify organically during an active colony play session.             |
| `🔵 Focus 3: Scenario`  | **Priority 3: Edge Case & Lifecycle** | Dedicated scenario setups (legacy save migration, first-time mod adoption, notification preference decoupling).                |
| `🟢 Automated Pass`     | **Priority 4: Unit-Test Verified**    | Backend architecture, mutability constraints, and performance budgets verified 100% passing in automated test suite (216/216). |

### Recommended Testing Sequence

1. **Step 1: Campaign Setup & Automated Landfall** (🟢 All Passed)  
   Start a new game $\rightarrow$ Verify `Page_GeoscapeRules` presets & modal dialog (🟢 **TC-17**), generate under Fog of War to verify black globe bypass into pawns/ideology (🟢 **TC-28**), and check the Day-0 audit ledger (🟢 **TC-18**).
2. **Step 2: Colony Radar Infrastructure & Holographic Visuals** (🟢 All Passed)  
   Switch to colony map $\rightarrow$ Construct Spacer Phased Arrays (🟢 Interferometry & Scaled SIGINT Passed — **TC-01**), verify the 3D honeycomb overlay and the overhauled 5-tab Mod Settings UI (🟢 All Passed — **TC-04**), and observe dual-layer orbit rendering with faction liveries, selection persistence across zoom, stratified orbital altitudes, and surface player visibility (🟢 All Orbital Passed — **TC-27**).
3. **Step 3: 4X Outposts & Early Warning Incursions** (🟢 All Passed)  
   Select colony on World Map $\rightarrow$ Right-click to dispatch an unmanned listening post expedition (🟢 Active Outpost Passed — **TC-05**), then spawn an incursion to observe Stage 1 amber alert $\rightarrow$ Stage 2 red identified alert (🟢 In-Game Pass — **TC-06**).
4. **Step 4: Contact Disambiguation & Contextual Dispatch** (🟢 All Passed)  
   Station a caravan at a colony or outpost $\rightarrow$ Click or Shift-click overlapping hex to verify the contact disambiguation menu and Civilization-style hold position outside colony (🟢 **TC-29**), deselect and right-click world hexes to verify source-agnostic contextual dispatch (🟢 **TC-30**), test savegame integrity replay reconciliation and HMAC-SHA256 tamper verification (🟢 **TC-31**), and explore anomalous landmarks with tactical cyan exclamation mark blips (🟢 **TC-32**).
5. **Step 5: Organic Colony Playtesting** (🟡 Next Up)  
   Run regular play at 1x–3x speed to observe Model B+ mountain masking (🟢 **TC-02**), coastal sonar (🟡 **TC-03**), dynamic camps (🟡 **TC-07**), and drifting satellites (🟡 **TC-13**).

---

## Test Matrix Overview

| Test Case | Feature Under Test                                                              | Expected Behaviour                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | Status                                    | Primary Screen / Tool                              |
| :-------- | :------------------------------------------------------------------------------ | :----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | :---------------------------------------- | :------------------------------------------------- |
| **TC-01** | Multi-Dish Radar & Interferometry Progression                                   | • 1 Dish: $R_{detect} = 24$ rings, $R_{id} = 10$ rings; 2 Dishes: $R_{id} = 17$ rings; 3 Dishes: $R_{id} = 22$ rings, locked 250-tick sweep cadence (🟢 In-Game Pass).<br>• Dynamic power scaling: 800W standby $\rightarrow$ 1,800W active when tracking targets.<br>• **Scaled SIGINT Bandwidth & Compound Sharing:** A single operational SIGINT Decryption Console (`Geoscape_SigintConsole`) linked to powered dishes within 12.9 cells shares decrypted telemetry (`MaxIntelligenceTier = Analysed`) and mountain terrain penetration across all phased array dishes in the colony compound up to connected dish capacity (1 dish: 10 rings, 2 dishes: 17 rings, 3 dishes: 22 rings) (🟢 In-Game Pass).<br>• **Strict Power Requirement:** Offline/unpowered arrays do not contribute to SIGINT correlator bandwidth or terrain penetration (🟢 In-Game Pass).<br>• **SIGINT Telemetry:** Console inspect pane reports operational status, linked dish count, and capabilities; radar dishes report `Directly Linked` or `Array Linked` status.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              | `🟢 In-Game Pass`                         | Colony Map / Power Grid / World Map                |
| **TC-04** | Tactical Radar Overlay & Globe UX                                               | • Native 3D mesh overlay renders glowing perimeter boundary rings and hexagonal holographic honeycomb grid on the globe (🟢 In-Game Pass).<br>• **Five-Tab Settings UI Architecture:** Mod Settings split into 5 cohesive, unclipped tabs (`Sensory`, `Radar`, `Incursions`, `Geoscape`, `Diagnostics`).<br>&nbsp;&nbsp;– _Latched Active Tab:_ Selected tab renders as a static neon-cyan panel with a 3px cyan bottom accent bar and single centred label (no duplicate ghost text).<br>&nbsp;&nbsp;– _Sensory Tab:_ Planetary visibility rules (Fog of War / Planetary Surveillance / Omniscient), Lookout horizons (Colony/Caravan), Night-time reduction toggle.<br>&nbsp;&nbsp;– _Radar Tab:_ Top header features a **Live Holographic Tactical HUD Scope Preview** (180×180px CRT scope with hex-aligned mountain ridge cluster inside the identification horizon, exposed-edge dashed amber contours, hex-clipped diagonal cell hatching, masked `?` contact inside the shadow, exterior-only surveillance dimming, and legend tooltip) alongside Overlay style selection and the `Surveillance Background Dimming` slider; measured 2-column layout below covers Outer Detection Perimeter and Inner Identification Horizon, followed by a 2×2 `Terrain Masking (Mountain Radar Shadows)` grid (`Both`, `Dashed Contours Only`, `Cell Hatching Only`, `None`).<br>• **Zero Clipping & Measured Heights:** `maxOneColumn = true` prevents hidden column wrapping; dynamic measured heights eliminate gaps and show a scrollbar whenever content overflows. | `🟢 In-Game Pass`                         | World Map / Mod Settings                           |
| **TC-05** | Lightweight Geoscape Outposts & 4X Context Actions                              | • **4X Colony Dispatch:** Right-clicking explored passable hex with Colony selected opens contextual dispatch menu (`Form caravan`, `Dispatch Construction Expedition: Automated Relay / Signal Beacon`).<br>• **Automated Supply Pre-Allocation:** Pre-selects exact build materials in `Dialog_FormCaravan` item list and locks destination route.<br>• **Distant Caravan Orders:** Right-clicking distant hex with caravan selected offers move and queued travel + construct.<br>• **Material Tracking:** Tracks consumed material (`stuffDef`), updating world label (e.g. `Granite blocks Signal Beacon`) and inspect string.<br>• **Mid-Build Departure Protection & Zero Ghost-Building:** Ordered departure mid-build intercepts with a 3-choice modal dialog (`Leave Part-Built`, `Cancel & Refund`, `Stay On-Site`). Leaving pauses progress while marching away without ghost-building; cancelling yields 100% exact material refund (`stuffDef` preserved); right-click options allow resuming paused construction locally or from afar.<br>• **Zero-TPS execution:** 100% unmanned, no `WorldPawns`, no map generated.<br>• **Incursion Sabotage & Salvage:** Hostile traversal sabotages sensor; caravan repair (25% cost) and dismantle (75% salvage partitioned into valid stacks respecting `stackLimit`, e.g. 75 + 37 items, preventing infinite wood transmutation).<br>• **Upright Icon Consistency:** Outpost and sensor icons remain upright and camera-aligned regardless of zoom level or latitude.                                       | `🟢 In-Game Pass (Core Outpost Verified)` | World Map / Colony & Caravan Float Menu            |
| **TC-06** | Two-Stage Early Warning & Non-Peeking Escalation                                | • Stage 1 ($R_{detect}$ boundary crossing): Amber alert letter (`ThreatSmall`), pulsing amber blip, masked faction, concealed threat points, hidden path.<br>• Stage 2 ($R_{id}$ boundary crossing): Red alert letter (`ThreatBig` hostile / `PositiveEvent` friendly), faction crest, known destination path.<br>• Direct high-spec entry: Incursions spawning within $R_{id}$ trigger Stage 2 directly without replaying Stage 1.<br>• Classification preservation: Re-entering mountainous masking does not regress known intelligence.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         | `🟢 In-Game Pass`                         | World Map / Letter Stack / Inspector               |
| **TC-27** | Dual-Layer Orbital Rendering & Faction Colour Tinting                           | • Satellites and orbital stations tagged with `<fullyExpandedInSpace>true</fullyExpandedInSpace>` render in both Surface and Odyssey Orbit layers when detected (🟢 In-Game Pass).<br>• 2D expanding icons and 3D mesh quads dynamically inherit faction livery colours (`Faction.Color`, e.g. Voidborn magenta, Pirate crimson, Player emerald green) (🟢 In-Game Pass).<br>• **Selection Persistence Across Zoom Layers:** Selecting a satellite or station and zooming between Surface and Orbit views seamlessly preserves active selection (`WorldSelector_SelectedLayer_Patch`) (🟢 In-Game Pass).<br>• **Logarithmic Orbital Altitude Scaling:** Clamped logarithmic scaling frames Geostationary stations (35,786 km) at visual radius $\approx 133$–$135$ alongside the planet rather than lost in deep space (🟢 In-Game Pass).<br>• **Deterministic Orbital Shell Stratification:** Odyssey asteroids and stations on the Orbit layer receive deterministic radial offsets ($-5$ to $+7$ units), creating natural multi-layer orbital shells rather than a single flat ring (`WorldObject_DrawPos_Patch`) (🟢 In-Game Pass).<br>• **Player Entity Orbital Visibility:** Player colonies (`MapParent`), listening posts (`WorldObject_ListeningPost`), and caravans (`Caravan`) remain visible and selectable from the Odyssey Orbit / space layer with horizon curvature culling (`WorldObject_Visibility_Patch`) (🟢 In-Game Pass).                                                                                                                    | `🟢 In-Game Pass`                         | World Map (Surface & Orbit Layers) / Inspector     |
| **TC-29** | Overlapping Entity Disambiguation Menu & Settlement Staging                     | • When multiple selectable world entities occupy identical/overlapping world coordinates or screen positions, left-clicking or cycling invokes an entity disambiguation float menu (🟢 In-Game Pass).<br>• Menu displays distinct entity entries with faction crests/colours, entity type labels, operational status, member count, and ground sector targeting (`Select Tile X`).<br>• Shift-clicking toggles multi-selection or single-entity deselection without closing the selector.<br>• Settings toggle `enableOverlappingEntitySelectionMenu` allows toggling back to vanilla cycling behaviour.<br>• Un-scanned or concealed entities outside radar identification horizons are cleanly masked as "Unidentified Contact".<br>• **Civilization-Style Settlement Staging (`CaravanArrivalAction_HoldPosition`):** Right-clicking an established colony hex with a caravan selected presents `"Move to hex (Hold position outside [Colony])"` alongside `"Enter Colony"`, allowing units to stage, camp, or fortify on the world hex without dissolving into the map (🟢 In-Game Pass). Redundant unmanned outpost construction options are suppressed on colony tiles.                                                                                                                                                                                                                                                                                                                                                                                      | `🟢 In-Game Pass`                         | World Map / Multi-Contact Stack                    |
| **TC-30** | Smart Source-Agnostic Contextual Dispatch                                       | • Right-clicking any explored world hex without selecting an origin entity presents intelligent contextual orders.<br>• Identifies player caravans and colonies, sorted by proximity to the target.<br>• Clamps top-level menu to nearest $N$ caravans (default 3) and $M$ colonies (default 2), preventing screen overflow.<br>• Provides `"Other caravans (N more)... >"` and `"Other colonies (N more)... >"` drilldown sub-menus with `< Back` navigation.<br>• Collapses multiple beacon building materials into a clean sub-menu drilldown.<br>• Generates clear directives: `"Move caravan [CaravanName] to [Target] ([N] tiles away)"` and `"Send caravan from [ColonyName] to [Target] ([N] tiles away)"`.<br>• Targeting player colonies provides local caravan formation and recall actions.<br>• Targeting active listening posts offers outpost dismantle expeditions.<br>• Classic Haze / Fog of War mode suppresses unmonitored terrain leaks and offers optimistic exploration routes.<br>• Settings toggle `enableIntelligentRightClick` allows reverting to vanilla right-click behaviour.                                                                                                                                                                                                                                                                                                                                                                                                                                                       | `🟢 In-Game Pass`                         | World Map / Contextual Float Menu                  |
| **TC-31** | Savegame Integrity Replay Reconciliation & Tamper Telemetry                     | • Deterministic transaction replay on save load: replays ledger records from the pinned Day-0 snapshot baseline against deserialised `<rules>`.<br>• Direct external XML modifications to rules without matching ledger entries are automatically detected, flagged as `ExternallyModified`, and recorded in `<ruleChangeLedger>` with exact key diffs.<br>• Cryptographic HMAC-SHA256 signature chain (`RuleLedgerSigner`) signs every ledger entry, preventing forged records or manual edits to baseline notes in raw XML.<br>• Mod Settings and About tabs display active governance badges (`Governed & Verified`, `Review Period Active`, `Developer Override Used`, `External Modification Detected`) and prominent warning banners upon tamper detection.<br>• Safe and non-destructive: never corrupts or invalidates modified saves.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     | `🟢 In-Game Pass`                         | Mod Settings / Save Load Telemetry                 |
| **TC-32** | Anomalous Landmark Exploration & Cyan Discovery Marker                          | • Distinctive exploration telemetry: Anomalous structures (Fallen Monolith, Ancient Complex, Ancient Ruins, Shrines, Hermetic Crates, Odyssey man-made structures) emerging from the black shroud into outer detection ($R_{detect}$) are masked with a distinctive **tactical cyan exclamation mark (`!`)** blip and label `"Unidentified landmark"`.<br>• Physical 3D ground meshes (`WorldDrawLayer_Landmarks`) and 2D expanding icons are completely concealed until brought within inner radar identification range ($R_{id}$) or visited.<br>• Distinguishes high-interest discovery points from hostile tactical threats (amber `?`).<br>• Mundane geographic features (lakes, ponds, oases, cliffs, valleys, dunes) remain unmasked upon terrain exploration.<br>• Entering inner radar identification range ($R_{id}$) or visiting via caravan permanently reveals true landmark identity, visuals, and inspect telemetry.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                | `🟢 In-Game Pass`                         | World Map / Landmark Exploration Blips             |
| **TC-02** | Model B+ Vantage & Mountain Masking                                             | • Origin Vantage: Lookouts gain $+1$ to $+3$ rings on hills/mountains; high-tech radars gain $+1$ to $+2$ rings to $R_{id}$.<br>• Target Terrain Masking: Incursions in `Mountainous` hexes remain held at Contact tier (amber blip) even within $R_{id}$.<br>• Terrain Exit: Moving into open plains immediately elevates to Classified/Analysed.<br>• SIGINT Console: Linked facility penetrates terrain masking and unlocks Tier 4 Analysed telemetry.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          | `🟢 In-Game Pass`                         | World Map / Inspector / Dev Console                |
| **TC-03** | Coastal Sonar Traversal & Automated Sonar Station                               | • Settlement Sonar Facility (`Geoscape_SonarArray`) and standalone Automated Sonar Station (`Vigilance_ListeningPost_Sonar`) require at least one adjacent `WaterCovered` world hex.<br>• Sonar originates from adjacent `WaterCovered` tiles, projecting outward in directional V-shaped sectors strictly through connected `WaterCovered` tiles.<br>• Land acts as an acoustic barrier, blocking propagation and preventing detection of disconnected water bodies across terrain.<br>• Overlapping sectors merge seamlessly from shoreline to shoreline; Radar and Sonar coverage overlap on water tiles without conflict.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           | `🟡 Focus 2: Playtest`                    | Colony Map / World Map / Coastal Tile              |
| **TC-07** | Haze Reconnaissance, Dynamic Camps & Rumours                                    | • Dynamic quest sites and bandit camps in unmonitored Haze sectors have announcement letters suppressed.<br>• Quest timeout clocks (`TimeoutComp`) are frozen while concealed so quests do not expire invisibly.<br>• **Detection-Layer Site Masking:** Non-player settlements, outposts, camps, and sites emerging from the black shroud into the outer Detection ring ($R_{detect}$) are masked as "Unidentified site" contacts with gold mystery icons, concealed factions, and suppressed interaction gizmos until brought within the inner Identification ring ($R_{id}$) or visited.<br>• Uncovering site via radar or scout caravan triggers deferred discovery letter and resumes timeout clock.<br>• Friendly caravan arrival dispatches Breadcrumb Rumour letter with 8-cardinal compass heading, uncovering target hex.<br>• Expired / Despawned Site Auto-Purge: Despawned or expired dynamic sites are automatically intercepted by Harmony patch on `WorldObject.PostRemove` and purged on the next sweep pass, preventing unbounded save game bloat.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                | `🟡 Focus 2: Playtest`                    | World Map / Letter Stack / Quest Tab               |
| **TC-08** | Inter-Faction Collisions & Background Skirmishes                                | • $O(M)$ tile-bucketed collision detection on `TickRare` (250 ticks).<br>• Hostile vs hostile/neutral collisions resolve purely algebraically without loading a tactical map.<br>• Clashes dispatch notification letter and spawn persistent `WorldObject_EncounterAftermath` at clash hex.<br>• Caravan arrival action `Scavenge Aftermath` cleanly transfers trade cargo and survivor pawns.<br>• 15,000-tick skirmish cooldown prevents repeat collision churn.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | `🟡 Focus 2: Playtest`                    | World Map / Letter Stack / Caravan Action          |
| **TC-09** | Grounded Storyteller Caravan Encounters                                         | • Storyteller caravan meetings and extortion incidents are grounded into physical `WorldObject_Incursion` entities with true settlement provenance (`origin -> shroud staging -> destination`).<br>• **Shroud Staging & Zero Modal Pop-ups:** Incursions spawn in unmonitored shroud (4–7 hexes away) or at origin settlement in Omniscient mode, dispatching a non-blocking sighting alert rather than forcing a modal trade window.<br>• **Faction & Dialogue Consistency:** Robust non-null faction resolution prevents `Tried to generate pawn kinds with null faction` errors; explicit group kind synchronisation ensures friendly merchants/travellers never generate contradictory "hostile warband" titles.<br>• Extortion demand dialog delays item deduction until player explicitly confirms acceptance.<br>• Caravans continue overland march across the globe post-encounter.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        | `🟡 Focus 2: Playtest`                    | World Map / Sighting Alert / Dev Incident          |
| **TC-12** | Optimistic Fog-of-War Travel Routing & Autonomous Obstacle Circumvention        | • Caravan travel directives assume unexplored black hexes are traversable without leaking impassability or hazard information ahead of time.<br>• Upon discovering an obstacle, the caravan automatically attempts to identify an alternative route and emits an advisory soft notification (`"Caravan has encountered an obstacle and is searching for an alternative route."`).<br>• Halts and prompts the player (`"Caravan cannot determine a viable route to its destination and has halted awaiting new orders."`) only when no viable route can be found or opportunistic pathfinding can no longer break even.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    | `🟡 Focus 2: Playtest`                    | World Map / Caravan Right-Click Orders             |
| **TC-13** | Drifting Orbital Mechanics & Non-Repeating Ground Tracks                        | • Satellites orbit along Keplerian paths with nodal precession ($\dot{\Omega}$) and storyteller-calibrated periods: Trade satellites (62,500 ticks / ~25.0 in-game hrs), Recon satellites (45,000 ticks / 18.0 in-game hrs), Military satellites (75,000 ticks / 30.0 hrs), Navigation satellites (120,000 ticks), Communication satellites (180,000 ticks).<br>• Ground tracks shift westward across successive orbits.<br>• Incommensurate periods ($T_{\text{orbit}} \nmid 60{,}000$) ensure sub-satellite position after 1 day (60,000 ticks) is distinctly displaced, avoiding artificial diurnal repeating clocks.<br>• Legacy save migration automatically upgrades pre-existing orbital periods below 30,000 ticks to the calibrated minimum threshold.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    | `🟡 Focus 2: Playtest`                    | World Map / Orbit Telemetry / Inspector            |
| **TC-14** | Persistent Orbital Stations & Constellation Service Zones                       | • Persistent orbital station hub (`WorldObject_OrbitalStation`) operates in stable high orbit for orbital factions (`TradersGuild`).<br>• Satellites entering the $35^\circ$ service corridor transition to `In Service Area`, syncing telemetry and restock inventory.<br>• Lost or destroyed satellites queue automated replenishment countdown, spawning replacements after fabrication.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        | `🟡 Focus 2: Playtest`                    | World Map / Station Inspector                      |
| **TC-15** | Satellite Mission Roles (Trade & Navigation Boost)                              | • **Trade Satellite:** Storyteller-calibrated orbital passes maintain an overhead communications window of ~3.8 in-game hours (~160s at 1x, ~27s at 3x) per pass; natural revisit cadence of once every 8–12 days across 2–3 active orbital trade vessels matches Cassandra Classic and Phoebe Chillax frequency; opens trade comms dialog with drop pod delivery.<br>• **Navigation Satellite:** Caravans under the satellite's 24-tile footprint receive 15% overland speed boost (0.85x ticks per move) with inspect string telemetry feedback.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | `🟡 Focus 2: Playtest`                    | Colony Comms Console / Caravan Inspector           |
| **TC-20** | Monotonic Visibility Escalation & Side-Effects Engine                           | • In-game escalation from `FogOfWar` $\rightarrow$ `PlanetarySurveillance` or `Omniscient` prompts permanent escalation confirmation dialog.<br>• Executing transition invokes `RuleTransitionExecutor`: releases all concealed dynamic sites and delivers deferred quest/announcement letters, invalidates sensory caches and sweeps vision, repaths active player caravans around discovered terrain, and rebuilds 3D GPU world meshes and feature labels.<br>• Ledger records `Escalation` transition with from/to labels.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      | `🟡 Focus 2: Playtest`                    | Mod Settings / World Map / Caravans                |
| **TC-22** | Developer-Mode Session Unsafe Override & Audit Trail                            | • With `Prefs.DevMode` active, About & Diagnostics tab exposes `Allow Unsafe Rule Transitions (Session Only)` checkbox.<br>• Enabling override unlocks immutable rules and reverse visibility transitions, displaying red warning banner and `(Unsafe)` badge.<br>• Executing unsafe transition prompts destructive warning modal; upon confirmation, forces transition and records `UnsafeChange` in ledger.<br>• Downgrading to `FogOfWar` triggers sensor radius sweep to re-flood fog of war around active lookouts.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           | `🟡 Focus 2: Playtest`                    | Mod Settings / About Tab / Dev Mode                |
| **TC-19** | Legacy Save Migration & Day-1 Review Grace Period                               | • Loading unversioned pre-1.3 save detects `schemaVersion == 0`.<br>• Adopts current `ModSettings` as baseline, sets 60,000-tick review window, and pins `LegacyMigration` record in ledger.<br>• Dispatches non-blocking explanation letter warning player of review period (including Fog of War exploration data notice if applicable).<br>• Review banner in Mod Settings displays remaining time and `[Confirm & Lock In Campaign Rules Now]` button.<br>• Confirming or window expiration locks immutable rules and records `ReviewWindowLocked`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            | `🔵 Focus 3: Scenario`                    | Legacy Save Load / Letter Stack                    |
| **TC-25** | Mutable Notification Preferences & ModSettings Decoupling                       | • In-game toggling of `EarlyWarningLetters` (`enableEarlyWarningLetters`) takes effect immediately without confirmation modal or ledger entry.<br>• In-game reset button (`Reset New Campaign Defaults to Factory Settings`) resets global preferences and factory defaults for future games, leaving active campaign rules and ledger completely untouched.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       | `🔵 Focus 3: Scenario`                    | Mod Settings / Early Warning Tab                   |
| **TC-26** | First-Time Project Vigilance Adoption & Save Provenance                         | • Loading established save never exposed to Project Vigilance (`schemaVersion == 0` and foundational components absent from save XML) triggers `DetermineEntryPath` $\rightarrow$ `ModAdoption`.<br>• Game pauses on load and presents `Dialog_GeoscapeModAdoption` modal.<br>• Choosing `Planetary Surveillance` adopts rules preserving charted geography, opens 1-day review window, and pins Day-0 `ModAdoption` origin snapshot in ledger.<br>• Choosing `Fog of War ("The Great Blackout")` displays explicit warning note that existing geographic knowledge will be lost, resets shroud around lookouts, and triggers optimistic caravan route recalculation.<br>• Dismissing modal via `Esc` or `Close (X)` defaults to `Planetary Surveillance` with active 1-day grace period.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          | `🔵 Focus 3: Scenario`                    | Mod Adoption Modal / Audit Ledger                  |
| **TC-10** | Waylith DeepProfiler & Zero-Allocation Verification                             | • Dev Mode DeepProfiler reveals native `ProjectVigilance.PerformSensorySweep`, `ProjectVigilance.SensorySweep.VisionUpdates`, `ProjectVigilance.InterFactionCollisions`, and `ProjectVigilance.SkirmishResolution` markers.<br>• Single-Pass Traversal: Unified `TryGetDetectionDistance(target, out int distance)` eliminates redundant graph traversals per sensor/target pair on the sweep hot path.<br>• Verification of $\le 1.0\text{ ms}$ execution budget and 0-byte GC allocation during steady-state `TickRare`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         | `🟢 Automated Pass`                       | Dev Actions Menu / Deep Profiler                   |
| **TC-11** | Scribe Save/Load & Outpost Removal Resilience                                   | • Saving and loading with active radars, outposts, incursions, aftermath sites, and concealed haze sites produces 0 Scribe errors and 0 duplicate IDs.<br>• Deleting an outpost via dev tools or external mod leaves no dangling references; next sweep clears entry cleanly.<br>• Outpost `stuffDef` is serialised via `Scribe_Defs.Look`, maintaining construction material fidelity across saves.<br>• Expired or despawned dynamic sites leave zero dangling `ConcealedSiteRecord` entries upon save/load.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     | `🟢 Automated Pass`                       | Save/Load Menu / Debug Log                         |
| **TC-16** | Cross-Campaign Save Isolation & Pinned Rules                                    | • Campaign A created under `FogOfWar`. Campaign B created under `Omniscient`.<br>• Reloading Campaign A restores `FogOfWar`; reloading Campaign B restores `Omniscient`.<br>• Global `ModSettings` defaults are untouched by save loads and do not cross-contaminate active saves.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | `🟢 Automated Pass`                       | World Map / Mod Settings / Save System             |
| **TC-17** | Native Campaign Creation Wizard Page (`Page_GeoscapeRules`) & Profile Selection | • Stitched seamlessly into the campaign creation sequence between Storyteller selection (`Page_SelectStoryteller`) and World Parameters (`Page_CreateWorldParams`) via `PageUtility_StitchedPages_Patch`.<br>• $2 \times 2$ profile card selection grid: **Fog of War** (Recommended default), **Planetary Surveillance**, **Hardcore Vigilance**, and **Omniscient**.<br>• Dedicated Anomaly-style modal window (`Dialog_GeoscapeCampaignRules`) launched via `Customise Individual Rules...` preserves full-height uncompressed profile cards, eliminates text truncation, and enables fine-grained rule tuning with live custom profile detection upon close.<br>• Standard Next/Back navigation links correctly forward to world generation and back to storyteller selection; consumes draft and commits Day-0 `CampaignCreated` ledger snapshot upon world generation.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       | `🟢 In-Game Pass`                         | Campaign Creation Wizard (`Page_GeoscapeRules`)    |
| **TC-18** | Default Campaign Creation & Day-0 Snapshot                                      | • Generating world without altering `Page_GeoscapeRules` adopts factory defaults (`FogOfWar`).<br>• Pins initial `CampaignCreated` Day-0 snapshot record in ledger with full rule state.<br>• Transient draft remains cleared.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     | `🟢 In-Game Pass`                         | World Generation / Audit Ledger                    |
| **TC-21** | Mutability Enforcement & Immutable Rule Locking                                 | • Once campaign starts and review window closes, all 7 Immutable rules (`colonyScoutRadius`, `caravanScoutRadius`, `enableNighttimeSensoryReduction`, `incursionsRestAtNight`, `synchronizeMultiVectorRaids`, `enableTacticalIngressFlanking`, `resetThreatCooldownOnArrival`) are greyed out with `(Locked)` suffix.<br>• Reverse visibility transitions (e.g. `PlanetarySurveillance` $\rightarrow$ `FogOfWar`) are greyed out with `(Locked - Cannot reverse)` suffix.<br>• Sliders and checkboxes reject interaction when DevMode is disabled; 0 changes recorded to ledger.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   | `🟢 Automated Pass`                       | Mod Settings / Incursions & Sensory Tabs           |
| **TC-23** | Automatic Override Invalidation on DevMode Toggle                               | • Toggling `Prefs.DevMode` off in Options immediately deactivates `UnsafeRuleOverride.Requested` and sets `Effective` to false.<br>• Active campaign ledger records `DevOverrideDisabled` with note `Developer mode switched off`.<br>• Mutability constraints immediately re-engage in Mod Settings.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              | `🟢 Automated Pass`                       | Options / Mod Settings / Audit Ledger              |
| **TC-24** | Override Session Ephemerality Across Loads & Restarts                           | • Enabling unsafe override and saving game does not persist override flag into save XML.<br>• Loading save while DevMode is active logs `DevOverrideEnabled` (`Override active when campaign was loaded`).<br>• Restarting RimWorld executable launches with override disabled by default, preventing accidental unintended rule mutations.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        | `🟢 Automated Pass`                       | Save/Load Menu / Game Restart                      |
| **TC-28** | Uncharted Globe Starting Site Selection & Automated Landfall Paradigm           | • **Automated Landfall (Recommended):** World generation under Fog of War and Hardcore Vigilance automatically designates a valid, habitable land sector via `TileFinder.RandomStartingTile()` upon clicking `Generate` on `Page_CreateWorldParams` (Step 5), completely skipping `Page_SelectStartingSite` and advancing directly to Ideology / Pawns.<br>• **Manual Globe Targeting Opt-In:** Players aiming for specific polar/equatorial latitudes can toggle `Manual Globe Targeting` in `Dialog_GeoscapeCampaignRules`; advancing without selecting a tile auto-picks a viable tile without vanilla errors.<br>• **Planetary Surveillance & Omniscient:** Retains full orbital cartography and manual site selection.<br>• **Ambiguous Rejection (Manual Mode):** Settle attempts on unexplored ocean or impassable mountains under Fog of War display `"Unviable landing coordinates: Selected sector cannot support a colony drop."` rather than explicit terrain leaks.<br>• **Blind Touchdown (Manual Mode):** Confirmation dialog prompts for toxic pollution (Biotech), extreme climate, and harsh biomes are suppressed under Fog of War, landing blind.<br>• **Faction Proximity Masking:** Proximity diplomatic warnings are suppressed so hidden bases are not revealed.<br>• **Step 3 Vanilla Compatibility:** `Page_SelectStoryteller` remains 100% vanilla, guaranteeing zero conflicts with storyteller and UI mods.                                                                                                                           | `🟢 In-Game Pass`                         | Campaign Creation Wizard / Starting Site Selection |

---

## Prerequisites & Test Environment Setup

1. **Deploy Latest Build to RimWorld 1.7:**
   - **Windows:** Execute [`scripts/deploy-geoscape.ps1`](scripts/deploy-geoscape.ps1) (or `npm run deploy:geoscape`) to compile `Release` binary and ensure the NTFS junction is linked to `RimWorld/Mods/ProjectVigilance-Geoscape`.
   - **Linux / CachyOS:** Execute [`scripts/deploy-geoscape.sh`](scripts/deploy-geoscape.sh) (or `npm run deploy:geoscape:linux`) to compile and link the symlink to `~/.local/share/Steam/steamapps/common/RimWorld/Mods/ProjectVigilance-Geoscape`.
2. **Launch RimWorld 1.7** with Project Vigilance loaded.
3. **Enable Development Mode** (`Options → Development mode`).
4. **Prepare a Recommended Test World:**
   - **Globe Coverage:** 30% or 50% (sufficient for multi-colony and long-range traversal testing).
   - **Colony Location:** Settle a colony on a flat or gently rolling tile that is:
     - Directly adjacent to at least one `Mountainous` or `LargeHills` tile (for Model B+ vantage and masking tests).
     - Within 15–18 tiles of an ocean coastline (for oceanic sonar tests).
5. **Initialise Colony Infrastructure (God Mode):**
   - Toggle **God Mode** on the top debug toolbar (`*`).
   - Spawn a stable power grid (e.g. 2–3 Vanometric Power Cells or Geothermal Generators with a battery bank).
   - Unlock all research via Dev Console: `Debug Actions Menu → General → Finish all research`.
   - Spawn carried resources: 1,000 Steel, 50 Components, 500 Wood, 500 Stone Blocks, Packaged Survival Meals, and Medicine.

---

## Step-by-Step Test Procedures

### Priority 1: Active In-Game Focus (Immediate) — 🔴 Focus 1

### Test 1: Multi-Dish Radar Construction & Interferometry Stacking (`TC-01`) — 🟢 In-Game Pass

_Objective: Validate multi-dish interferometry progression, locked 250-tick sweep cadence, dynamic power scaling, compound-wide SIGINT array sharing, and scaled bandwidth._

#### 1.1 Single Dish Baseline

1. On your colony map, navigate to `Architect → Security` (or `Structure / Vigilance`).
2. Construct one **Spacer Phased Array** (`Geoscape_RadarLarge`).
3. Connect power to the radar (requires 800W standby).
4. Select the radar building and inspect the left-hand inspect panel:
   - **Verify:** Power status displays `Standby: 800 W`.
   - **Verify:** Sensor status displays:
     - `Detection Radius: 24 rings`
     - `Identification Horizon: 10 rings (Tier 1 Baseline)`
     - `Sweep Cadence: 250 ticks (~4.1s)`
5. Switch to the World Map (`Tab`):
   - **Verify:** A glowing boundary ring appears at 24 traversal rings around your colony, and an inner identification circle appears at 10 rings.

#### 1.2 Dual-Dish Interferometry Expansion

1. In God Mode, construct a second **Spacer Phased Array** on the same colony map connected to the same power grid.
2. Select either dish and inspect the info panel:
   - **Verify:** Status updates immediately:
     - `Interferometry: 2 Dishes Linked (Sync Tier 2)`
     - `Detection Radius: 24 rings (Fixed)`
     - `Identification Horizon: 17 rings (~70% coverage)`
     - `Sweep Cadence: 250 ticks (~4.1s, locked)`
3. Switch to the World Map:
   - **Verify:** The inner identification boundary ring expands outward from 10 rings to 17 rings.

#### 1.3 Triple-Dish Maximum Stacking

1. Construct a third **Spacer Phased Array** on the same colony map.
2. Select any of the three dishes:
   - **Verify:** Status displays:
     - `Interferometry: 3 Dishes Linked (Sync Tier 3 - Maximum)`
     - `Detection Radius: 24 rings (Fixed)`
     - `Identification Horizon: 22 rings (~92% coverage)`
     - `Sweep Cadence: 250 ticks (~4.1s, locked)`
3. Switch to the World Map:
   - **Verify:** The inner identification horizon ring expands to 22 rings, almost matching the outer perimeter.

#### 1.4 Dynamic Power Scaling & Fail-Safe Redundancy

1. With no hostile contacts within 24 rings, verify total radar grid draw is $3 \times 800\text{ W} = 2,400\text{ W}$.
2. Trigger an incoming raid via Dev Mode:
   - `Debug Actions Menu → Incidents → Execute Incident... → RaidEnemy` (walk-in).
3. As the incursion enters the 24-ring detection perimeter:
   - **Verify:** Power consumption dynamically scales from 800W to **1,800W active** per dish.
4. While the target is being tracked, select Dish 3 and cut power (flick switch off or deconstruct a power conduit):
   - **Verify:** The colony radar array immediately and gracefully degrades to Tier 2 ($R_{id} = 17$ rings, locked 250-tick cadence).
   - **Verify:** No null-reference exceptions or UI glitches occur in the debug log (`~`).
5. Reconnect power to Dish 3:
   - **Verify:** The array restores Tier 3 synchronisation ($R_{id} = 22$ rings, locked 250-tick cadence) on the very next sweep.

#### 1.5 Scaled SIGINT Correlator Fidelity & Strict Power Re-Verification (Option 1)

1. On your colony map with 3 Spacer Phased Arrays constructed, construct one **SIGINT Decryption Console** (`Geoscape_SigintConsole`, Ultra tech).
2. Note that the console facility reach is **12.9 cells** (linking to up to 4 dishes). Place the console within 12.9 cells of Dish 1 (with Dish 2 and Dish 3 positioned outside the console's 12.9-cell radius).
3. Connect power to the SIGINT console (requires 700 W) and ensure Dish 1 is powered.
4. **Single-Dish Wired Baseline (10 Rings Bandwidth):**
   - Select the SIGINT console and inspect the info panel:
     - **Verify:** Status displays `SIGINT Status: Operational (Active Decryption)`.
     - **Verify:** Linked array count displays `Linked Radar Array: 1 operational dish`.
     - **Verify:** Bandwidth displays `Decryption Bandwidth: 10 tiles (Tier 4 Intel & Mountain Penetration)`.
   - Select Dish 1 (directly linked and powered):
     - **Verify:** Inspect panel displays `SIGINT Decryption: Directly Linked (10 tiles bandwidth, 1 active dish)`.
   - Select Dish 2 or Dish 3 (compound peers sharing telemetry):
     - **Verify:** Inspect panel displays `SIGINT Decryption: Array Linked (10 tiles bandwidth, 1 active dish)`.
   - Switch to the World Map (`Tab`) and observe mountainous terrain:
     - **Verify Terrain Penetration:** Masked mountain radar shadows (dashed contours and cell hatching) are eliminated across rings 2–10. Beyond 10 rings (steps 11–22), mountain shadows **remain visible**!
     - Incursions within 10 rings resolve Tier 4 (Analysed). Incursions between 11 and 22 rings fall back to Tier 3 (Classified) in open terrain, or Tier 2 (Contact) in mountain shadows.
5. **Multi-Dish Scaled Expansion (17 & 22 Rings Bandwidth):**
   - Relocate or construct conduits so Dish 2 is also within 12.9 cells of the SIGINT console (or link a second console):
     - **Verify:** Console displays `Linked Radar Array: 2 operational dishes` and `Decryption Bandwidth: 17 tiles (Tier 4 Intel & Mountain Penetration)`.
     - **Verify:** Mountain shadow elimination expands out to 17 rings.
   - Wire all 3 dishes to the console within 12.9 cells:
     - **Verify:** Console displays `Linked Radar Array: 3 operational dishes` and `Decryption Bandwidth: 22 tiles (Tier 4 Intel & Mountain Penetration)`.
     - **Verify:** Mountain shadows are completely eliminated across the entire 22-ring identification horizon.
6. **Strict Power Requirement & Graceful Degradation:**
   - With only Dish 1 wired to SIGINT, cut power to Dish 1:
     - **Verify:** Console immediately switches to `SIGINT Status: Standby (Connected Radar Array Unpowered)` and `Linked Radar Array: 1 dish (Offline)`.
     - **Verify:** All dishes immediately lose SIGINT decryption telemetry.
     - **Verify:** Mountain shadows immediately reappear on the World Map on the next sensory sweep.
   - Reconnect power to Dish 1:
     - **Verify:** Full decryption bandwidth restores seamlessly.
   - Turn off the SIGINT console power switch:
     - **Verify:** Console updates to `SIGINT Status: Offline (Switched Off)`.
     - **Verify:** Radar array cleanly reverts to baseline Classified tier and mountain shadows reappear.

> [!NOTE]
> **In-Game Verification (Pass):** Confirmed in-game with 3 operational Spacer Phased Arrays and 2 connected to SIGINT decryption console. Telemetry and Geoscape visual overlays correctly scale: 24 detection tiles, 22 identification tiles (3 dishes), with terrain penetration active up to 17 tiles across the compound while strictly respecting unpowered dish states.

---

### Test 4: Tactical Radar Coverage Overlay & Globe UX (`TC-04`) — 🟢 In-Game Pass

_Objective: Validate 3D native mesh rendering, boundary rings, holographic honeycomb grid, and the overhauled 5-tab user display settings._

#### 4.1 Holographic Globe Rendering (🟢 In-Game Pass)

1. Open the World Map (`Tab`) with active radar installations at your colony.
2. Observe the globe visual overlay:
   - **Verify Boundary Ring:** A glowing circular border encircles the maximum detection boundary (24 rings).
   - **Verify Holographic Honeycomb:** Inside the perimeter, a subtle hexagonal grid mesh is visible with an inward distance fade (glowing brightly at edge, fading toward center).

#### 4.2 Five-Tab Settings Menu Architecture & Controls (🟢 In-Game Pass)

1. Open `Options → Mod Settings → Project Vigilance: Geoscape`:
2. **Verify 5-Tab Architecture & Latched Active Tab:**
   - The settings window presents 5 dedicated, clearly labelled tabs:
     - `Sensory`: Planetary visibility rules (Fog of War / Planetary Surveillance / Omniscient), Lookout horizons (Colony/Caravan sliders), Night-time reduction.
     - `Radar`: Live Tactical HUD Scope Preview, Tactical Radar Overlay mode + Surveillance Background Dimming slider, measured 2-column layout (Outer Detection Perimeter / Inner Identification Horizon), and a 2×2 `Terrain Masking (Mountain Radar Shadows)` grid.
     - `Incursions`: Incursion movement & night rest rules, multi-vector raid synchronisation, faction emblems toggle.
     - `Geoscape`: Tactical Contact Disambiguation Menu, Intelligent Right-Click Dispatch & limit sliders, early warning letters, Epistemic Discovery breakdown.
     - `Diagnostics`: Campaign governance ledger status, developer mode overrides, factory defaults reset.
   - **Verify Latched Tab Highlight (No Ghost Text):** Clicking any tab immediately latches it as a dark-cyan panel with a 3px cyan bottom accent bar and a single centred cyan label (`#66F2FF`) with no faint duplicate background text. Inactive tabs remain standard brown buttons.
3. **Verify Live In-Menu Tactical Radar HUD Scope Preview:**
   - Switch to the `Radar` tab:
     - A 180×180px holographic CRT radar scope preview is rendered at the top left, alongside the Tactical Radar Overlay mode options and the `Surveillance Background Dimming` slider.
     - **Real-Time Responsiveness:** Moving sliders or changing colour swatches updates the preview instantly:
       - **Mode Toggles:** Switching between `Boundary Ring & Honeycomb Grid`, `Boundary Ring Only`, and `None` toggles the axial honeycomb hex matrix and perimeter boundary in the preview.
       - **Perimeter & Honeycomb Intensity:** Adjusting `Perimeter Intensity` and `Honeycomb Grid Intensity` dynamically scales the outer ring and hex grid alpha.
       - **Identification Horizon:** Toggling `Show Identification Horizon` and sliding `Identification Intensity` dynamically controls the inner concentric horizon ring and gates terrain shadow markings.
       - **Terrain Masking (Mountain Radar Shadows):** A 4-hex brown mountain ridge aligned with the honeycomb grid sits inside the identification horizon:
         - `Both`: Renders both hex-clipped diagonal cell hatching and a dashed amber outer contour around the exposed edges of the 4-hex mountain cluster.
         - `Dashed Contours Only`: Renders the dashed amber outer contour around the mountain cluster without interior hatching.
         - `Cell Hatching Only`: Renders diagonal scanline hatching clipped inside each mountain hex without outer contour dashes.
         - `None (Undecorated)`: Shows plain brown mountain hexes without amber markings.
       - **Exterior Surveillance Dimming:** Adjusting `Surveillance Background Dimming` dims the area outside the outer detection perimeter while keeping the active radar envelope clear.
       - **Telemetry Contacts & Legend Tooltip:** Displays a central colony core beacon, an identified contact `TR-1` on open ground inside the identification ring, an unclassified `?` contact in the outer detection zone, and a masked `?` contact inside the mountain ridge (demonstrating why mountain shadows conceal contacts within identification range). Hovering the scope displays a descriptive legend tooltip.
4. **Verify Consistent In-Place Greying Out & Horizontal Baseline Alignment Across All Tabs:**
   - Every control across all 5 tabs stays locked at a fixed position rather than hiding/collapsing when parent options change:
     - **Radar Tab:**
       - `Identification Intensity` in the right column aligns horizontally on the exact same baseline as `Honeycomb Grid Intensity` in the left column.
       - Selecting `Boundary Ring Only` greys out `Honeycomb Grid Intensity` in-place.
       - Unchecking `Show Identification Horizon` greys out `Use Custom Identification Colour`, its colour row, `Identification Intensity`, and the 2×2 `Terrain Masking` radio grid (both text labels and radio icons) in-place.
       - Selecting `None (Undecorated Globe)` greys out `Surveillance Background Dimming`, `Outer Detection Perimeter`, `Inner Identification Horizon`, and `Terrain Masking` in-place so an undecorated globe has zero overlay or exterior dimming.
     - **Sensory Tab & World-Gen Rules Dialog:**
       - `Colony scout horizon` and `Caravan scout horizon` sliders snap strictly to integer hex counts (e.g., `5 hexes`, `3 hexes`).
       - Selecting `Omniscient (Spectator / Living Map)` greys out `Lookout & Scouting Horizons` (`Colony Lookout Horizon`, `Caravan Scouting Horizon`, `Nighttime Sensory Reduction`) in-place instead of collapsing the section.
     - **Geoscape Tab:**
       - Unchecking `Intelligent Geoscape Right-Click Dispatch` greys out `Max Contextual Caravans Displayed` and `Max Contextual Colonies Displayed` in-place instead of hiding the sliders.
   - If the window or resolution is smaller than the content, a vertical scrollbar appears so every control remains reachable.
5. **Verify Label Clean-up:**
   - Confirm `MOUNTAIN RADAR SHADOWS (MODEL B+)` is replaced by `TERRAIN MASKING (MOUNTAIN RADAR SHADOWS)` with a plain-English explanation, and all obsolete `(X-COM Style)` and `(Preview Style)` labels remain removed.
6. **In-Game Verification (`TC-04`):**
   - Five-tab settings architecture (`Sensory`, `Radar`, `Incursions`, `Geoscape`, `Diagnostics`) validated in-game.
   - Latched active tab highlighting, live CRT radar scope preview, horizontal baseline alignment, and in-place control greying out verified clean with zero clipping or layout collapse.

---

### Test 5: Lightweight Geoscape Outposts & Context Actions (`TC-05`) — 🟢 In-Game Pass (Deployment & Active Operations Verified)

_Objective: Validate zero-TPS unmanned outposts, right-click caravan construction, incursion sabotage, repair, and salvage economics._

#### 5.1 4X Colony Dispatch, Queued Construction & Material Tracking

##### 5.1.1 Colony Right-Click Geoscape Dispatching

1. Select your Colony Settlement on the Geoscape globe.
2. **Right-Click** any explored, passable hex 8–15 tiles away:
   - **Verify Context Menu:** The following options appear:
     - `Form caravan to Tile [TileID]`
     - `Dispatch Construction Expedition: Automated Listening Post (100 Steel, 4 Components)`
     - `Dispatch Construction Expedition: Signal Beacon (150 Wood/Stone)`
3. Select `Dispatch Construction Expedition: Automated Listening Post`:
   - **Verify Dialog Open:** Vanilla's `Dialog_FormCaravan` opens with the destination route locked to the target hex.
   - **Verify Automated Material Pre-allocation:** In the Items tab, 100 Steel and 4 Components (or 150 Wood/Stone for Beacons) are automatically pre-selected.
   - **Verify Automated Food Pre-allocation:** In the Travel Supplies tab, non-perishable travel provisions (Packaged Survival Meals / Pemmican / Meals) are pre-allocated based on estimated round-trip travel time + on-site construction time + safety buffer.
   - **Verify Construction Capability Gate:** If only colonists incapable of Construction are selected, clicking **Send** blocks departure with an explanatory warning (`Cannot dispatch outpost expedition: No colonist capable of Construction is selected`).
   - **Verify Unified Deployment Confirmation:** Upon clicking **Send** with at least one capable builder:
     - A single consolidated confirmation dialog appears displaying:
       - Target Tile & Structure name.
       - Lead Builder (with individual Construction Speed percentage).
       - Assistants providing a team synergy bonus.
       - Effective Team Assembly Speed and estimated on-site assembly hours.
       - Estimated round-trip transit time and total mission duration.
       - Food provisions sufficiency assessment (e.g. `Food Provisions: 3.5 days (SUFFICIENT for ~0.8 days mission)` or warning in red if insufficient).
     - Clicking **Confirm** forms and dispatches the caravan immediately without triggering vanilla's generic `< 5.0 days` food warning popup.
     - (Note: Normal, non-outpost caravans remain 100% vanilla and continue to use standard vanilla warnings).
4. Select colonist(s) and confirm departure:
   - Once the colonists exit the map boundary, inspect the caravan on the world map.
   - **Verify Caravan Status:** Status displays `travelling to establish automated listening post`.
   - Fast-forward time until the caravan reaches the destination hex.
   - **Verify Autonomous Deployment & Timed Construction:** Upon stepping onto the hex, the caravan consumes the 100 Steel and 4 Components, placing the **Automated Listening Post** in an `Under Construction` state.
   - **Verify Construction Progression:** Inspecting the outpost displays `Status: Under Construction (X%, ~X.Xh remaining)`. Time advances as the caravan works on-site based on the team's calculated construction speed.
   - If the caravan moves away, verify the status switches to `Status: Construction Paused`. When the caravan returns, construction resumes.
   - Upon completion, a positive alert sounds, status switches to `Operational`, and the 12-ring sensory envelope activates on the Geoscape!
   - **Verify Faction Tinting & Upright Orientation:** The outpost symbol renders upright on the world sphere and is tinted with the player faction's colour.

##### 5.1.2 Distant Caravan Right-Click Orders & Local Deployment

1. With an active player caravan selected on the Geoscape carrying building materials:
2. **Right-Click** a distant passable hex:
   - **Verify Context Menu:** Options appear:
     - `Move to Tile [TileID]`
     - `Travel and deploy Automated Listening Post (100 Steel, 4 Components)`
     - `Travel and construct Signal Beacon (150 Wood/Stone)`
3. Select `Travel and construct Signal Beacon (150 Granite blocks)`:
   - Caravan marches to the hex, displaying `travelling to construct signal beacon (Granite blocks)`.
   - Upon arrival, the Signal Beacon deploys automatically with material tracking (`Granite blocks Signal Beacon`).
4. While a caravan is stationed on a hex without an outpost, **Right-Click** its current hex:
   - **Verify Local Deployment:** Provides immediate local deployment options (`Establish Automated Listening Post` / `Construct Signal Beacon`).

##### 5.1.3 Mid-Build Departure Interception, Ghost-Building Prevention & Material Preservation

1. **Stationary Assembly Requirement (Zero "Ghost-Building"):**
   - Start constructing or dismantling an outpost with a stationary caravan.
   - Note the current construction progress percentage (e.g. `Status: Under Construction (35%, ~3.2h remaining)`).
   - Order the caravan to move to another hex.
   - **Verify Movement Interception Dialog (`Dialog_MessageBox`):**
     - Caravan departure is immediately intercepted by Harmony patch on `Caravan_PathFollower.StartPath`.
     - A 3-choice modal dialog appears:
       > **Construction in Progress**  
       > _"Construction of [Outpost Name] is not finished ([X]% complete). Are you sure you wish to depart?"_  
       > Buttons: **Leave Part-Built** | **Cancel & Refund** | **Stay On-Site**
2. **Choice A: Leave Part-Built (Safe Pause, Zero Resource Duplication):**
   - Click **Leave Part-Built**:
     - The caravan begins moving towards its destination.
     - **Verify Construction Paused:** The outpost status switches to `Status: Construction Paused ([X]% complete)` with a world message (`Construction of [Outpost Name] paused`).
     - **Verify Zero Ghost-Building:** While the caravan marches across the origin hex and toward its destination, progress ticks freeze completely (enforced by `!c.pather.Moving`).
     - **Verify Material Commitment:** Materials remain committed to the outpost world object. No resources are refunded to the caravan's inventory (preventing resource duplication).
3. **Choice B: Cancel & Refund (100% Exact Material Reclamation):**
   - In another test run with an unfinished outpost, order the caravan to depart and select **Cancel & Refund**:
     - **Verify Outpost Deletion:** The unfinished outpost is immediately removed from the world map.
     - **Verify 100% Exact Material Refund to Caravan Inventory:**
       - For an **Automated Listening Post**: Exactly 100 Steel and 4 Industrial Components are refunded into the caravan inventory.
       - For a **Signal Beacon**: Exactly 150 of the consumed `stuffDef` material (e.g. 150 Wood logs, 150 Granite blocks, 150 Marble blocks, or 150 Sandstone blocks) is refunded into the caravan inventory.
       - Verify no transmutation occurs (marble yields marble, not wood or slate).
     - The caravan continues its movement to the ordered destination uninterrupted.
4. **Choice C: Stay On-Site (Safe Cancel):**
   - Order a constructing caravan to move and select **Stay On-Site** (or press `Escape`):
     - The movement order is cancelled (`pather.StopDead()`).
     - Caravan remains stationed on the outpost tile.
     - Construction continues advancing smoothly without losing elapsed ticks.
5. **Resuming Paused Outpost Construction:**
   - With an outpost in `Status: Construction Paused`:
   - **Local Caravan Option:** With a caravan stationed on the paused outpost tile, right-click:
     - Verify option: `Halt & resume construction of [Outpost Name] ([X]% complete)`.
     - Selecting it resumes construction; `Status: Under Construction` appears and progress ticks advance.
   - **Distant Caravan Option:** With a distant caravan selected on the world map, right-click the paused outpost tile:
     - Verify option: `Travel and resume construction of [Outpost Name] ([X]% complete)`.
     - Selecting it commands the caravan to march to the site. Upon arrival, construction resumes automatically.
   - **Dismantling / Cancelling Unfinished Construction:**
     - Right-clicking an outpost still under construction (from afar, locally, or via colony dispatch) provides options to dismantle/cancel with **100% material recovery** (e.g. Travel and Dismantle [Outpost Name] (Refund 100% Materials) / Dispatch caravan from [Colony] to dismantle [Outpost Name] (Refund 100% Materials)).
     - When the caravan arrives on-site, 100% of initial construction materials are immediately refunded into the caravan's inventory (not the 75% salvage rate reserved for completed operational installations), and the unfinished site is cleared.
     - Float menu options are deduplicated across all sources so no duplicate cancel options appear.

##### 5.1.4 Circadian Work Cycles, Rest Periods & Night Owl Outpost Construction

1. **Standard Colonist Day Shift & Night Rest Pause:**
   - Station a caravan containing a single normal colonist builder (no NightOwl trait, standard timetable) at an unfinished outpost site.
   - At midday (e.g. 12:00 local solar time), observe construction progress:
     - **Verify Daytime Assembly:** The colonist is awake; progress ticks advance at full baseline speed (1.0).
     - **Verify Telemetry:** Inspect string displays: Status: Under Construction ([X]%, ~[H]h remaining).
   - Fast-forward to midnight (e.g. 02:00 local solar time):
     - **Verify Night Rest Pause:** Colonists rest during standard night hours (22:00–06:00). Construction progress pauses completely; ticks remaining freeze.
     - **Verify Telemetry Feedback:** Inspect string displays: Status: Construction Paused ([X]% complete - Resting - Night/Sleep Period).
   - Advance time to dawn (06:00 local solar time):
     - **Verify Morning Resumption:** Construction automatically resumes as soon as the colonist awakens.

2. **Night Owl Nocturnal Shift & Daytime Sleep Pause:**
   - Station a caravan containing a single colonist builder with the NightOwl trait at an unfinished outpost site.
   - Fast-forward to midnight (02:00 local solar time):
     - **Verify Night Assembly:** The Night Owl colonist is actively awake and working. Construction progress ticks advance smoothly through the night.
     - **Verify Telemetry:** Inspect string displays: Status: Under Construction ([X]%, ~[H]h remaining).
   - Fast-forward to afternoon (14:00 local solar time):
     - **Verify Daytime Sleep Pause:** The Night Owl colonist sleeps during circadian rest hours (11:00–18:00). Construction progress halts completely; ticks remaining freeze.
     - **Verify Telemetry Feedback:** Inspect string displays: Status: Construction Paused ([X]% complete - Resting - Daytime Sleep Period).
   - Advance time to 18:00 local solar time:
     - **Verify Evening Resumption:** As the Night Owl awakens, construction automatically resumes for the nocturnal shift.

3. **Mixed Squad 24/7 Continuous Construction & Dynamic Shift Scaling:**
   - Form an assembly squad consisting of 1 normal colonist builder and 1 Night Owl colonist builder.
   - Station the caravan at an outpost site and commence construction:
     - **Daytime Window (11:00–18:00):** Normal builder is awake; Night Owl is resting. Construction continues at single-worker speed (ratio 1.0 / 1.5). Inspect string displays: Status: Under Construction ([X]%, ~[H]h remaining - Reduced Shift (1/2 awake)).
     - **Nighttime Window (22:00–06:00):** Night Owl is awake; normal builder is resting. Construction continues through the night at single-worker speed. Inspect string displays: Status: Under Construction ([X]%, ~[H]h remaining - Reduced Shift (1/2 awake)).
     - **Crossover Windows (06:00–11:00 and 18:00–22:00):** Both builders are awake on-site. Construction advances at full combined team speed (1.5). Inspect string displays: Status: Under Construction ([X]%, ~[H]h remaining).
     - **Verify 24/7 Continuity:** The outpost constructs continuously around the clock without ever entering a fully stalled state.

4. **Dismantling Sleep Schedule Respect:**
   - Station a caravan on an outpost and begin dismantling (isDismantling).
   - When night falls (or day sleep for Night Owls), verify dismantling halts during rest hours, displaying Status: Dismantling Paused ([X]% complete - Resting - Night/Sleep Period).

#### 5.2 Hostile Incursion Traversal & Sabotage

1. Using Dev Mode, spawn a hostile incursion on a trajectory that passes directly through the outpost hex:
   - `Debug Actions Menu → Incidents → Execute Incident... → RaidEnemy` (or spawn incursion on adjacent tile).
2. Fast-forward time at `Speed 3`.
3. When the enemy incursion steps onto the outpost hex:
   - **Verify High-Priority Alert:** A letter arrives:
     > **Listening Post Sabotaged: [Outpost Name]**  
     > _"A hostile force from [Faction Name] has traversed the sector containing [Outpost Name] and sabotaged its sensor array! The station is offline until repaired."_
   - **Verify Sensor Shutdown:** The outpost's sensory envelope collapses and disappears from the world map.
   - **Verify Outpost State:** Selecting the outpost displays status: `SABOTAGED (Sensor Offline - Requires Repairs)`.

#### 5.3 Field Repair & Dismantle Operations (Material Refund & Stack Partitioning)

1. Order your caravan to march to the sabotaged outpost tile.
2. Ensure the caravan carries repair materials (at least 25 Steel and 1 Component for relays, or 30 Wood/Stone for beacons).
3. Right-click the sabotaged outpost:
   - **Verify Context Option:** `Repair Sabotaged Station (25 Steel, 1 Component)` or `Repair Sabotaged Station (30 Wood/Stone)`.
4. Click `Repair Sabotaged Station`:
   - **Verify:** Repair materials are deducted from caravan inventory.
   - **Verify:** Outpost status restores to `Operational`.
   - **Verify:** Sensory envelope reappears on the world map immediately.
5. **Dismantle Dispatch from Colony Settlement:**
   - Select your Colony Settlement on the Geoscape globe.
   - Right-click an existing listening post or signal beacon:
     - **Verify Context Option:** `Dispatch Caravan to Dismantle [Outpost Name] (Recover 75% Materials)`.
   - Select option: Dialog_FormCaravan opens with route waypoint locked to the outpost.
   - **Carry Capacity & Pack Animal Validation Gate:**
     - Upon opening Dialog_FormCaravan, non-perishable travel provisions are pre-allocated for the round-trip mission.
     - If insufficient colonists or pack animals are selected to carry the anticipated salvage mass (e.g. attempting to recover 112–150 kg stone blocks or 39.3–52.4 kg steel/components with an unburdened solo colonist), clicking **Send** blocks departure with an error dialog detailing the shortfall and recommending pack animals (Muffalos, Dromedaries, Elephants).
     - When sufficient capacity is present, clicking **Send** presents a consolidated confirmation dialog showing target location, expected salvage mass, spare caravan capacity, round-trip transit time, and food sufficiency.
   - Confirming departure launches the caravan with status ravelling to dismantle [outpost name].
6. **Dismantle Orders from Caravan:**
   - With a distant caravan selected, right-click the outpost:
     - **Verify Option:** `Travel and Dismantle [Outpost Name] (Recover 75% Materials)`.
   - With a caravan on the same hex as the outpost, right-click the outpost:
     - **Verify Option:** `Dismantle [Outpost Name] (Takes time, recover 75% materials)`.
7. **Timed Deconstruction & Material Salvage:**
   - When dismantling begins, the outpost switches to `Status: Dismantling (X%, ~X.Xh remaining)`.
   - The caravan works on-site for ~36 game minutes (1,500 ticks). If the caravan leaves, dismantling pauses.
   - Upon completion:
     - **Verify Outpost Removal:** The outpost is safely removed from the world map.
     - **Automated Relay Salvage:** Exactly 75 Steel and 3 Components (75% of construction cost) are added to caravan inventory.
     - **Signal Beacon Salvage & Transmutation Prevention:**
       - For a beacon built with Granite blocks: caravan receives **112 Granite blocks** (75% of 150), **NOT wood logs**.
       - **Stack-Limit Partitioning:** Inspect caravan inventory: 112 stone items split into **two valid item stacks** (75 and 37) respecting `stackLimit`, with zero errors.
       - For a beacon built with Wood logs: recovers 112 Wood logs (75 and 37).

#### 5.4 Consistent Upright Zoomed-In Outpost Icons

1. Construct or locate an Automated Listening Post or Signal Beacon on the Geoscape.
2. Zoom the globe camera all the way out:
   - **Verify:** The 2D expanding icon displays upright facing North.
3. Slowly zoom the camera all the way in until the transition into 3D world mesh occurs:
   - **Verify Upright Orientation:** The 3D outpost icon remains perfectly upright facing North, matching the expanding icon orientation without tilting or rotating 90 degrees sideways.

#### 5.5 Authentic Fog-of-War Optimistic Pathfinding & In-Transit Route Adjustment (`TC-05.5`)

_Objective: Validate that unexplored sectors in the black unknown are assumed traversable during path calculation, and caravans dynamically adjust routes only when newly uncovered tiles reveal impassable obstacles._

1. **Ordering Movement into Black Unknown:**
   - Select a player caravan or settlement on the Geoscape.
   - Right-click an unexplored sector shrouded in pitch-black fog-of-war behind potential oceans or mountain ranges.
   - **Verify Context Option:** Displays `Travel into unexplored sector (Tile [ID])` (or `Form expedition towards unexplored sector (Tile [ID])`).
   - **Verify No Impassable Leaks:** The menu never mentions "impassable", ocean, or mountains, and does not alter the destination to a revealed passable sector.
2. **Straight Optimistic Path Calculation:**
   - Confirm the travel order.
   - **Verify Optimistic Route:** The caravan charts a straight path directly through the black shroud toward the destination, treating uncharted sectors as optimistically traversable with standard baseline movement difficulty (1.0).
   - The pathfinder does **not** omnisciently detour around mountains or bodies of water concealed beneath the fog.
3. **In-Transit Obstacle Revelation & Dynamic Recalculation:**
   - Fast-forward time at `Speed 2` or `Speed 3` as the caravan marches into the unknown.
   - When the caravan steps onto a subsequent hex and its scout vision uncovers an impassable tile (mountain peak or ocean) directly on its planned route:
     - **Verify Sensory Reveal:** The fog lifts over the obstacle tile, exposing the true impassable terrain.
     - **Verify Dynamic Recalculation:** The caravan detects that the route is no longer viable through the newly revealed obstacle and recalculates its path **around** that specific tile.
     - **Verify Optimistic Continuity:** Any subsequent unexplored black sectors further along the route continue to be assumed traversable.
     - **Verify Player Alert:** A notification appears:
       > _"Caravan [Name] encountered [impassable terrain / mountains / ocean] in sector [ID] and adjusted its route."_
4. **Destination Sector Impassability Fallback:**
   - Order a caravan directly towards an unexplored sector that happens to be an ocean or mountain summit.
   - As the caravan approaches and its vision radius illuminates the destination sector:
     - If an adjacent passable shore or valley exists: the caravan automatically reroutes to the adjacent sector and alerts the player.
     - If no adjacent passable route exists: the caravan halts safely at the boundary and notifies the player:
       > _"Caravan [Name] reached the edge of sector [ID]; further passage is blocked by [impassable terrain]. Caravan halted."_

#### 5.6 Environmental & Planetary Disruptions: Electronic Radars vs Primitive Lookouts (TC-05.6)

_Objective: Validate that electronic radar installations (Automated Sensor Relays, colony radar dishes, and reconnaissance satellites) are vulnerable to Solar Flares, Eclipses/Prolonged Darkness, and EMP, while primitive Signal Beacons remain completely immune._

1. **Solar Flare Disruption:**
   - With an active Automated Listening Post and colony CompRadar dish on the world map:
   - Trigger a Solar Flare event via Dev Mode: Debug Actions Menu → Incidents → Do Incident... → SolarFlare.
   - **Verify Automated Relay Shutdown:**
     - The outpost's sensory envelope collapses and disappears from the Geoscape.
     - Inspecting the outpost displays Status: Offline (Solar Flare Disruption) and telemetry text: Intense coronal radiation has knocked out electronic telemetry.
     - World object label displays Automated listening post (Offline: Solar Flare).
   - **Verify Colony Radar Dish Shutdown:**
     - Ground radar status displays Operational Status: Offline (Solar Flare Disruption) and power drops to 0 W.
   - **Verify Signal Beacon Immunity:**
     - Inspect an active Signal Beacon on the Geoscape:
     - **Verify:** The 6-ring detection perimeter remains active and fully functional.
     - Status remains Status: Operational. Bonfire lookouts are unaffected by coronal mass ejections.
   - When the Solar Flare ends:
     - Automated Relays and colony radar dishes automatically reactivate and restore their sensory envelopes.

2. **Solar Eclipse & Prolonged Darkness (Photovoltaic Power Loss):**
   - With an active Automated Listening Post on the Geoscape:
   - Trigger an Eclipse event via Dev Mode: Debug Actions Menu → Incidents → Do Incident... → Eclipse (or Unnatural Darkness / Volcanic Winter).
   - **Verify Photovoltaic Depletion:**
     - Automated Relays rely on photovoltaic arrays and emergency buffer batteries. Occluded sunlight causes shutdown.
     - Outpost status displays Status: Offline (Solar Eclipse / Darkness) and Photovoltaic arrays offline due to obscured sunlight.
     - Outpost radar rings deactivate.
   - **Verify Signal Beacon Immunity:**
     - Signal Beacons remain completely operational throughout the eclipse. Watchtowers and fire beacons ignore darkness.
   - When sunlight returns, Automated Relays recharge and resume operations immediately.

3. **EMP Disruption:**
   - Hit a colony CompRadar installation with an EMP grenade, mortar shell, or EMP blast.
   - **Verify Radar Disruption:** While stunned, the radar dish deactivates and reports Operational Status: Offline (EMP Disrupted / Stunned).
   - Once the stun duration expires, the dish returns online.

---

### Test 6: Two-Stage Early Warning & Non-Peeking Escalation (`TC-06`) — 🟢 In-Game Pass

_Objective: Validate Stage 1 outer boundary alert, Stage 2 inner identification threshold, non-peeking escalation, and direct entry bypass._

#### 6.1 Two-Stage Escalation Sequence

1. Ensure your colony has an operational Spacer Phased Array ($R_{detect} = 24$, $R_{id} = 10$).
2. Trigger an incoming hostile raid at a distant origin (26–30 hexes away).
3. Watch the incursion march toward the colony at `Speed 3`:
4. **Stage 1 Trigger (Crossing Ring 24):**
   - As the incursion touches Ring 24:
     - **Letter Stack:** An amber notification arrives:
       > **Early Warning: Unidentified Contact [Contact-X]**  
       > _"Long-range sensors have detected an unidentified contact moving through sector [Tile]. Tactical classification is pending."_
     - **World Map:** The incursion displays as a **pulsing amber blip**.
     - **Information Masking:** Faction crest is replaced by a question mark/neutral icon; threat points display `Unknown`; travel route is hidden.
5. **Stage 2 Trigger (Crossing Ring 10):**
   - As the incursion touches Ring 10:
     - **Letter Stack:** A red high-threat letter arrives:
       > **Hostile Incursion: [Faction Name]**  
       > _"Sensors have confirmed contact [Contact-X] as a hostile warband from [Faction Name] marching toward [ColonyName]! Estimated force strength: [ThreatPoints]."_
     - **World Map:** The blip changes from amber to **red with faction crest**.
     - **Route Reveal:** The travel line connecting origin, current tile, and destination colony becomes fully visible.

#### 6.2 Friendly Caravan Stage 2 Identification

1. Trigger a friendly trade caravan (`TraderCaravanArrival`) at distant range.
2. At Ring 24: Stage 1 amber alert arrives (`Unidentified Contact`).
3. At Ring 10:
   - **Verify Positive Letter:** A blue/cyan letter arrives: `Approaching Caravan: [Faction Name]`.
   - **Verify Blip:** Changes to cyan with friendly faction crest.

#### 6.3 Direct High-Spec Entry Bypass

1. In Dev Mode, spawn an incursion directly inside the colony's identification horizon (e.g. Ring 7):
   - **Verify:** The system **skips Stage 1 completely**.
   - **Verify:** Exactly one Stage 2 letter (`Hostile Incursion` / `Approaching Caravan`) arrives immediately with full classification.
   - **Verify:** Stage 1 alert is not replayed or queued.

#### 6.4 Classification Memory Preservation

1. After an incursion is classified at Stage 2, order it (or allow it) to step into a `Mountainous` hex inside the radar perimeter:
   - **Verify:** The contact **does not regress** to an unidentified amber contact.
   - Once classified, intelligence is preserved in memory.

---

### Test 27: Dual-Layer Orbital Rendering & Faction Colour Tinting (`TC-27`) — 🟢 In-Game Pass

_Objective: Validate seamless cross-layer satellite and station rendering between planetary surface and Odyssey orbit views, visibility mask adherence, dynamic faction livery tinting, selection persistence across zoom, logarithmic altitude scaling, and deterministic asteroid/station stratification._

#### 27.1 Dual-Layer Visibility (Surface & Orbit Layers) (🟢 In-Game Pass)

1. With active satellites and stations in orbit, switch to the World Map (`Tab`).
2. Toggle between the planetary surface view and Odyssey orbit shell (using the `View orbit` / `View planet` toggle button or zooming between orbital and planetary scales):
   - **Verify Surface Layer:** Satellites and orbital stations tagged with `<fullyExpandedInSpace>true</fullyExpandedInSpace>` render clearly as expanding icons with their 3D quads visible over the globe.
   - **Verify Orbit & Asteroid Layer:** Switching to `View orbit` or inspecting asteroid/orbital stations renders satellites and stations smoothly along their 3D orbital trajectories around the planetary sphere. `VisibleInBackground` and `TransitionPct_Patch` ensure 2D expanding icons remain drawn and selectable regardless of zoom level or layer selection.
   - **Verify Sensor Masking Adherence:** Satellites and orbital objects traversing unmonitored sectors in Fog of War remain masked outside active radar/lookout detection ranges, preventing cross-layer intelligence leaks.

#### 27.2 Dynamic Faction Livery Colour Tinting (🟢 In-Game Pass)

1. Locate satellites and stations belonging to distinct factions on the World Map:
   - **Voidborn Emporium (or Modded Trade Faction):**
     - **Verify 2D Icon:** The expanding icon tint matches the faction colour (`Faction.Color`, e.g. vibrant magenta / pink).
     - **Verify 3D Mesh Quad:** The 3D world quad texture inherits the faction livery tint.
   - **Pirate / Hostile Orbital Outposts:**
     - **Verify:** Displays faction crimson alert tinting.
   - **Player Colony Satellites:**
     - **Verify:** Inherits player colony faction colour (emerald green).
   - **Derelict / Abandoned Debris:**
     - **Verify:** Satellites or stations with null faction display clean neutral baseline monochrome tinting.
2. Select an orbital object:
   - **Verify Inspect Panel:** Faction name, crest, and telemetry update consistently with the visual livery.

#### 27.3 Orbital Selection Persistence Across Zoom Layers (🟢 In-Game Pass)

1. In high-orbit or low-orbit view, left-click an active satellite or orbital station to select it.
2. With the entity selected, zoom the camera in to transition to the planetary Surface view (or zoom out to the Orbit layer).
3. **Verify Selection Persistence:**
   - The selected satellite or station **remains selected** (`WorldSelector_SelectedLayer_Patch`).
   - The inspector panel in the lower-left remains open, displaying the orbital asset's telemetry, altitude, velocity, and role without resetting or deselecting.

#### 27.4 Logarithmic Orbital Altitude & Station Proximity (🟢 In-Game Pass)

1. Inspect the purple Geostationary Orbital Station (`altitudeKm = 35,786 km`):
2. **Verify Visual Altitude:**
   - Instead of rendering deep out in space at ~5x planet radius ($r \approx 472$), the station sits at visual radius $\approx 133$–$135$ units (framed comfortably within the viewport alongside the planet).
   - LEO satellites (400–800 km) render distinctly lower at visual radius $\approx 116$–$120$ units.

#### 27.5 Deterministic Orbital Shell Stratification for Odyssey Objects (🟢 In-Game Pass)

1. Switch to the Odyssey Orbit / space layer to view standard space stations and asteroid fields.
2. **Verify Natural Multi-Layer Stratification:**
   - Instead of all asteroids and stations hovering on the exact same uniform planar ring ($r = 115$), each asteroid and station receives a deterministic radial offset ($-5$ to $+7$ units, visual radii $110$–$122$) (`WorldObject_DrawPos_Patch`).
   - The asteroids form a realistic, three-dimensional orbital debris belt around the planet.
   - Selecting any stratified asteroid or station works accurately by clicking its visual position.

#### 27.6 Player Entity Orbital Visibility & Curvature Occlusion (🟢 In-Game Pass)

1. With the World Map open, switch to the Odyssey Orbit / space layer (`View orbit` button or zooming out).
2. **Verify Player Settlement & Caravan Visibility:**
   - Player colonies (`MapParent`), listening posts (`WorldObject_ListeningPost`), and caravans (`Caravan`) on the planetary surface remain visible through the orbital shell (`WorldObject_Visibility_Patch.HiddenByRules_Patch`).
   - 2D icons and badges render at full opacity (`TransitionPct` clamped to 1.0) rather than fading away.
3. **Verify Globe Horizon Curvature Occlusion:**
   - Rotate the globe: settlements and caravans situated on the near hemisphere facing the camera remain visible.
   - Rotate the globe until settlements and caravans pass behind the horizon: verify they smoothly occlude behind the globe curvature (`WorldRendererUtility.HiddenBehindTerrainNow`).
4. **Verify Surface Interactivity from Orbit:**
   - Left-click a player settlement or caravan visible from orbit: verify selection and inspect pane open accurately.

> [!NOTE]
> **In-Game Verification (Pass):** Confirmed in-game that player settlements and caravans remain clearly visible, highlighted, and directly interactive from the Odyssey Orbit / space layer (e.g. instructing a caravan back to a settlement from high orbit). Curvature occlusion accurately culls assets behind the planet.

---

### Test 29: Overlapping Entity Disambiguation Menu & Settlement Staging (`TC-29`) — 🟢 In-Game Pass

**Purpose:** Verify that when multiple selectable entities (such as caravans, settlements, and world outposts) occupy identical or overlapping planetary hexes or screen positions, selecting the stack presents a contact disambiguation float menu instead of silently cycling through candidates, and permits staging outside settlements without dissolving into the colony map.

#### 29.1 Multi-Entity Disambiguation Float Menu (🟢 In-Game Pass)

1. Deploy or move a player caravan onto a player settlement, or spawn an incursion/visitor caravan directly on top of an existing settlement/outpost.
2. Left-click the overlapping stack on the World Map.
3. **Verify Float Menu Display:**
   - Instead of immediately selecting one entity and requiring blind clicks to cycle, a float menu opens listing each selectable entity present at the location.
   - Each option displays the entity's label, faction crest/livery colour, status description, and pawn/member count (e.g. `Colony (5 pawns)`, `Caravan (3 pawns)`).
   - The menu includes a dedicated ground sector option: `"Select Tile [TileID]"` to target the underlying terrain sector.

#### 29.2 Modifier Shift-Click Toggle Selection

1. With an overlapping stack on the globe, hold `Shift` and left-click the stack.
2. Select an entity from the disambiguation menu.
3. **Verify Selection State:**
   - The selected entity is added to the active selection (or deselected if already selected) without clearing other selected world objects.

#### 29.3 Sensory Masking & Unidentified Contacts

1. Under Fog of War or Classic Haze, locate an unmonitored or outer-detection contact (e.g. dynamic camp or bandit staging ground outside identification range).
2. Click the contact stack when overlapping.
3. **Verify Masking:** The entry displays `"Unidentified Contact"` with a generic mystery badge, preventing information leaks before sensor identification.

#### 29.4 Mod Settings Toggle

1. Navigate to `Options → Mod Options → Project Vigilance: Geoscape → General / Overlapping Contacts`.
2. Toggle off `Enable Overlapping Entity Selection Menu`.
3. Left-click an overlapping entity stack.
4. **Verify Vanilla Fallback:** Clicking immediately selects and cycles candidates using vanilla RimWorld cycling mechanics. Re-enabling the option restores the disambiguation menu.

#### 29.5 Caravan Settlement Staging & Hold Position (Civilization-Style Camping) (🟢 In-Game Pass)

1. Select a player caravan on the World Map.
2. Right-click your player settlement hex.
3. **Verify Staging Option & Suppression:**
   - Float menu displays: `"Move to hex (Hold position outside [ColonyName])"` alongside vanilla `"Enter Colony"`.
   - Redundant outpost construction options (`Travel and deploy Automated Listening Post`, `Travel and construct Signal Beacon`) are suppressed on settlement tiles.
4. Select `"Move to hex (Hold position outside [ColonyName])"`.
5. **Verify World Staging:**
   - Caravan paths to the settlement hex and halts outside on the world map (`CaravanArrivalAction_HoldPosition`), without dissolving into the colony map or dropping pawns.
   - If the caravan is already traversing across the settlement hex, the menu offers `"Halt & hold position outside [ColonyName]"` (`pather.StopDead()`).

---

### Test 30: Smart Source-Agnostic Contextual Dispatch (`TC-30`) — 🟢 In-Game Pass

**Purpose:** Verify that right-clicking any explored world hex on the Geoscape presents intelligent contextual dispatch options without requiring a source settlement or caravan to be pre-selected.

#### 30.1 Source-Agnostic Movement & Dispatch Options

1. Deselect all world objects (`Esc` or click on empty space).
2. Right-click an explored, passable hex within reasonable distance of your colony and/or caravans.
3. **Verify Context Menu Presentation:**
   - A contextual float menu opens offering movement and dispatch actions.
   - For active caravans: `"Move caravan [CaravanName] to [Target] ([N] tiles away)"`.
   - For player colonies: `"Send caravan from [ColonyName] to [Target] ([N] tiles away)"`.
   - Options are dynamically sorted by overland tile proximity (closest units/colonies listed first).

#### 30.2 Proximity & Expedition Execution

1. Select `"Send caravan from [ColonyName] to [Target]"`.
2. **Verify Caravan Dialog:** Opens `Dialog_FormCaravan` targeting the clicked hex with automatic path confirmation.
3. Select `"Move caravan [CaravanName] to [Target]"`.
4. **Verify Route Assignment:** The caravan immediately plots or appends its travel route towards the destination.

#### 30.3 Colony Self-Targeting Actions

1. Right-click directly on a player colony without having it selected beforehand.
2. **Verify Colony Actions:**
   - Offers `"Form caravan at [ColonyName]"`.
   - If distant player caravans exist, offers `"Recall caravan [CaravanName] to [ColonyName]"`.

#### 30.4 Outpost Construction & Dismantle Actions

1. Right-click an empty passable hex:
   - If a nearby player caravan carries necessary construction supplies (Steel, Components, Wood, or Stone), displays `"Establish Automated Listening Post"` or `"Construct Signal Beacon"`.
2. Right-click an active player listening post:
   - Displays `"Dismantle Outpost (Recover 75% Materials)"` offering to dispatch nearby caravans to salvage the station.

#### 30.5 Fog-of-War Exploration Routing

1. Under `Classic Haze` / `Fog of War`, right-click an unrevealed black hex.
2. **Verify Anti-Leak Guard:** Impassability status of unrevealed hexes is not leaked into the menu; options offer exploration directives rather than blocked error notifications.

#### 30.6 Mod Settings Toggle & Clamping Limits

1. Navigate to `Options → Mod Options → Project Vigilance: Geoscape → Sensory & Radar`.
2. Toggle off `Intelligent Geoscape Right-Click Dispatch`:
   - Right-click on the world map with no entity selected.
   - **Verify Fallback:** Vanilla right-click behaviour is maintained (no menu is spawned when no source is selected).
3. Re-enable `Intelligent Geoscape Right-Click Dispatch`:
   - Verify sliders for `Max Contextual Caravans Displayed` (1–8) and `Max Contextual Colonies Displayed` (1–5) appear.
   - Adjust values and verify the top-level right-click menu clamps directly displayed options accordingly.

#### 30.7 Menu Scaling, Proximity Clamping & Drilldown Sub-Menus

1. With multiple active colonies ($> 2$) and caravans ($> 3$):
   - Right-click any explored world hex:
     - **Verify Top-Level Clamping:** The menu directly displays only the nearest 3 caravans and nearest 2 colonies sorted by proximity.
     - **Verify Caravan Drilldown Option:** `"Other caravans ([N] more)... >"` is present at the end of the caravan section.
     - **Verify Colony Drilldown Option:** `"Other colonies ([N] more)... >"` is present at the end of the colony section.
2. Select `"Other caravans ([N] more)... >"`:
   - **Verify Sub-Menu:** Opens a dedicated `FloatMenu` displaying all remaining distant caravans.
   - Select `"< Back"`: Verify smooth return to the main tile context menu.
3. Select `"Other colonies ([N] more)... >"`:
   - **Verify Sub-Menu:** Opens a dedicated `FloatMenu` displaying all remaining distant colonies with a `"< Back"` navigation button.
4. If a colony has multiple stone block or wood materials in storage:
   - **Verify Material Collapse:** Multiple beacon options collapse into `"Dispatch Construction Expedition from [Colony]: Signal Beacon... > ([N] material types available)"`.
   - Selecting it opens a clean material picker sub-menu with `"< Back"`.

---

### Test 31: Savegame Integrity Replay Reconciliation & Tamper Telemetry (`TC-31`) — 🟢 In-Game Pass

**Purpose:** Verify that campaign rules provenance is cryptographically/deterministically auditable across save files via transaction log replay from the pinned Day-0 baseline, and that unrecorded direct XML mutations or forged ledger edits trigger visual warning telemetry without breaking the save.

#### 31.1 Clean Save Integrity Verification (🟢 In-Game Pass)

1. Load any campaign save created under Milestone 1.3 rules (e.g. `TC-31 New Save.rws`).
2. Open `Options → Mod Options → Project Vigilance: Geoscape → About & Diagnostics`.
3. **Verify Governance Status:**
   - Displays `• Campaign Integrity: Governed & Verified` (or `Review Period Active` if within Day-1 grace period).
   - Sensory & Radar tab displays standard contextual governance banner.

#### 31.2 Direct XML Modification Tamper Detection (🟢 In-Game Pass)

1. Save the game and exit RimWorld.
2. Open the `.rws` save file in a text editor.
3. Locate the `<rules>` element and manually modify an immutable or escalatable rule without creating a matching `<ruleChangeLedger>` transaction (e.g. change `<colonyScoutRadius>5</colonyScoutRadius>` to `<colonyScoutRadius>15</colonyScoutRadius>`).
4. Launch RimWorld and load the modified save file.
5. **Verify Load-Time Reconciliation:**
   - Game loads smoothly without errors or crashes.
   - The engine automatically replays ledger history, identifies that active rules diverge from expected ledger state, and appends a `RuleChangeKind.ExternalModification` transaction to `<ruleChangeLedger>` detailing the discrepancy.
6. Open `Options → Mod Options → Project Vigilance: Geoscape`:
   - **Verify Red Warning Banner:** Context banner displays: `Active Save: EXTERNAL SAVE MODIFICATION DETECTED (Active rules diverge from audit ledger)`.
   - **Verify Diagnostics Tab:** Displays `• Campaign Integrity: External Modification Detected` alongside an explicit warning box detailing the exact expected vs active values and tick recorded.

#### 31.3 Cryptographic Hash Chain & Ledger Anti-Forgery (🟢 In-Game Pass)

1. Open the `.rws` save file in a text editor.
2. Manually alter a rule in `<rules>` (e.g. `<colonyScoutRadius>15</colonyScoutRadius>`) AND simultaneously alter the `<note>` in the baseline `<ruleChangeLedger>` or forge a new transaction to match the altered rule.
3. Launch RimWorld and load the save.
4. **Verify Cryptographic Detection:**
   - The engine validates the HMAC-SHA256 signature chain (`RuleLedgerSigner`) against the campaign GUID and internal assembly secret.
   - Identifies the forged record note or invalid signature.
   - Appends `RuleChangeKind.ExternalModification` with: `Audit ledger signature invalid: record #0 (CampaignCreated) tampering detected`.
   - Mod options diagnostics tab displays `• Campaign Integrity: External Modification Detected` with the red audit warning box.

---

### Test 32: Anomalous Landmark Exploration & Cyan Discovery Marker (`TC-32`) — 🟢 In-Game Pass

**Purpose:** Verify that man-made, anomalous, and otherworldly structures (such as Fallen Monoliths, Ancient Complexes, Ruins, and Odyssey structure landmarks) are masked with a distinctive tactical cyan exclamation mark (`!`) in the outer detection layer ($R_{detect}$), distinguishing points of interest from hostile tactical threats (amber `?`).

#### 32.1 Landmark Discovery Blip Telemetry

1. Start a game with Classic Haze of War or Fog of War active.
2. In Dev Mode or natural world generation, observe an anomalous structure (e.g. `FallenMonolith`, `AncientComplex`, `AncientRuins`, `AncientLaunchSite`, or `AbandonedColonyTribal`) located within outer detection range ($R_{detect}$) but outside inner identification range ($R_{id}$).
3. **Verify Masked Representation:**
   - Rendered with a **Tactical Cyan exclamation mark (`!`)** blip on the Geoscape globe and 2D expanding icon.
   - World map label reads `"Unidentified landmark"` (or `"Unknown landmark"` in Neolithic/Medieval tech).
   - Inspect pane displays: `Status: Anomalous Landmark` with guidance to approach or expand radar coverage.
   - Faction alignment and tactical interaction gizmos are suppressed.

#### 32.2 Identification Envelope & Expedition Reveal

1. Move a player caravan onto the anomalous landmark hex, or construct a radar dome that encompasses the tile within its inner identification ring ($R_{id}$).
2. **Verify Full Reveal:**
   - The cyan exclamation mark blip cleanly dissolves.
   - True world object sprite, full name, inspect telemetry, and interaction options are revealed.

#### 32.3 Mundane Geography Exemption

1. Locate a natural or mundane geographical landmark (e.g. `Lake`, `Pond`, `Oasis`, `Cliffs`, `Valley`, or `Dunes`) in the outer detection layer.
2. **Verify Immediate Visibility:**
   - Mundane natural geography is revealed immediately upon tile exploration without cyan exclamation masking.

---

### Priority 2: Core Colony Playtest — 🟡 Focus 2

### Test 2: Model B+ Vantage & Mountain Masking (`TC-02`) — 🟢 In-Game Pass

_Objective: Validate sensor origin vantage bonuses, target mountain masking, and SIGINT console decryption penetration._

#### 2.1 Origin Vantage Bonuses

1. Settle or place a secondary test outpost/colony on a `Mountainous` or `LargeHills` tile via Dev Mode (`Set player faction`).
2. Construct a **Neolithic/Medieval Lookout Tower** (`Geoscape_LookoutTower`) on:
   - A flat tile: Verify radius is baseline 8 rings.
   - A `LargeHills` tile: Verify radius increases by $+2$ rings to 10 rings.
   - A `Mountainous` tile: Verify radius increases by $+3$ rings to 11 rings.
3. Construct a **Spacer Phased Array** on a `Mountainous` tile:
   - **Verify:** Detection radius remains capped at 24 rings.
   - **Verify:** Identification radius gains $+2$ rings vantage bonus (e.g. Dish 1 baseline 10 rings $\rightarrow$ 12 rings).
   - **Verify Inspect Panel:** Displays: `Terrain Vantage: +2 rings (Mountainous origin)`.

#### 2.2 Target Terrain Masking

1. Identify a `Mountainous` hex located inside your colony's identification horizon ($< 17$ rings away).
2. Spawn a hostile incursion on or adjacent to that mountain hex marching toward your colony.
3. As the incursion enters the `Mountainous` hex:
   - **Verify:** Even though the incursion is well within identification range, it is **held at Contact Tier** (pulsing amber blip).
   - **Verify Info Card / Tooltip:** Hovering over the blip displays `Unidentified Contact [Contact-X]`. Faction crest and threat makeup remain hidden.
   - **Verify Telemetry Note:** Displays: `Signal degraded by mountainous terrain masking`.
4. Allow the incursion to step out of the `Mountainous` hex onto an adjacent flat plains or rolling hills hex:
   - **Verify:** On the very next radar sweep, the contact immediately escalates to **Classified Tier**:
     - Blip transitions from pulsing amber to solid red with faction crest.
     - Letter arrives: `Hostile Incursion: [Faction Name]`.
     - Target destination path becomes visible.

#### 2.3 SIGINT Console Decryption & Masking Penetration

1. On the colony map with the Spacer Phased Array, navigate to `Architect → Security / Vigilance`.
2. Construct an **Ultra SIGINT Decryption Console** (`Geoscape_SigintConsole`) directly adjacent to the Phased Array (within `CompFacility` link distance).
3. Connect power to the SIGINT console (700W standby / 1,400W active).
4. Inspect the Phased Array:
   - **Verify:** Facilities list shows `Linked: Ultra SIGINT Console`.
   - **Verify:** Radar features note: `SIGINT Decryption Active: Penetrates Terrain Masking`.
5. Have another hostile incursion traverse a `Mountainous` hex inside the identification radius:
   - **Verify:** Terrain masking is **completely bypassed**.
   - **Verify:** The incursion escalates to **Tier 4 Analysed**:
     - Blip displays full telemetry: exact pawn count, weapon types, target colony, and real-time travel ETA.

---

### Test 3: Oceanic Hydrophone Sonar Station (`TC-03`) — 🟡 Focus 2: Playtest

_Objective: Validate oceanic acoustic tracking restriction to water-covered hexes._

1. Settle a coastal colony map with ocean tiles adjacent.
2. In God Mode, construct a **Coastal Hydrophone Array** (`Geoscape_SonarArray`).
3. Connect power (500W standby / 1,000W active).
4. Switch to the World Map (`Tab`):
   - **Verify Sonar Overlay:** The sonar coverage highlights strictly maritime/oceanic hexes (`tile.WaterCovered == true`).
   - **Verify Land Gating:** Inland overland hexes within the 18-ring radius show zero sonar coverage.
5. Spawn a hostile overland caravan marching along a coastal road within 10 rings of the sonar array:
   - **Verify:** The overland caravan is **not detected** by the sonar array.
6. Spawn a sea-based or coastal water incursion:
   - **Verify:** The aquatic contact is cleanly detected and tracked across water hexes.

---

### Test 7: Haze Reconnaissance, Dynamic Camps & Traveller Rumours (`TC-07`) — 🟡 Focus 2: Playtest

_Objective: Validate dynamic site concealment, quest timeout suspension in the Haze, and breadcrumb rumours._

#### 7.1 Dynamic Camp Concealment in Haze

1. In `Mod Settings`, ensure visibility mode is set to `Classic Haze of War`.
2. Via Dev Mode, spawn a dynamic quest site (e.g. Bandit Camp, Logging Site, or Mining Work Site) in an unmonitored Haze sector:
   - `Debug Actions Menu → Quests → Generate Quest...` (or generate world site).
3. **Verify Letter Suppression:**
   - No quest announcement banner or popup letter appears on screen.
4. **Verify Globe Concealment:**
   - Switch to the World Map: the target hex remains shrouded in translucent slate haze; the site icon is hidden.
5. **Verify Timeout Clock Suspension:**
   - In Dev Console, inspect the spawned site's `TimeoutComp`:
   - Fast-forward time for 1 game day.
   - **Verify:** The timeout countdown timer has **not decremented** while the site remains concealed in the Haze.

#### 7.2 Uncovering Site via Reconnaissance

1. Dispatch a scout caravan or build a listening post whose sensor radius covers the concealed site hex.
2. As soon as active vision touches the hex:
   - **Verify Discovery Letter:** The deferred quest announcement letter immediately fires.
   - **Verify Icon Reveal:** The site marker becomes visible on the Geoscape.
   - **Verify Timeout Clock Resumption:** The `TimeoutComp` countdown timer resumes ticking down normally.

#### 7.3 Breadcrumb Rumours & Traveller Intelligence

1. Ensure at least one dynamic site remains concealed in the Haze.
2. Trigger an arriving friendly visitor or trade caravan group (`TraderCaravanArrival` or `VisitorGroup`).
3. When the travellers arrive at your colony (or when hailed on the road):
   - **Verify Rumour Letter:** A letter arrives:
     > **Traveller Rumour: Sighting Reported**  
     > _"Travellers from [Faction Name] report sighting an active [Site Type] approximately [X] hexes to the [Direction] (Compass: [e.g. North-West])."_
   - **Verify Target Reveal:** The referenced world hex on the Geoscape is uncovered, revealing the previously concealed site.

#### 7.4 Expired / Despawned Site Auto-Purge Lifecycle (`WorldObject.PostRemove`)

1. Spawn a dynamic quest site in an unmonitored Haze sector (confirming it is registered in `concealedSites` with its announcement letter suppressed).
2. Without revealing the site on the world map, destroy or despawn it via Dev Mode:
   - Select the site via Dev console or debug action: `Debug Actions Menu → Destroy object` (or cancel/fail the quest via Quest tab).
3. **Verify Immediate Purge via `WorldObject_PostRemove_Patch`:**
   - The Harmony patch intercepting `WorldObject.PostRemove` executes immediately, calling `HazeReconnaissanceManager.Notify_WorldObjectRemoved`.
   - The site ID is cleanly purged from `concealedSites`.
4. Fast-forward time past the next 250-tick `TickRare` sweep:
   - In `CheckUncoveredSites()`, verify no null reference exceptions or warnings are thrown.
5. Save and reload the game:
   - Verify zero orphaned `ConcealedSiteRecord` entries, dangling `suppressedLetter` references, or missing object cross-reference warnings appear in the console.

#### 7.5 Detection-Layer Masking for World Objects ("Unidentified Site" Contacts)

1. Ensure `Classic Haze of War` visibility mode is active.
2. Locate or spawn a foreign faction settlement, outpost, or camp that is initially inside the black shroud (unexplored).
   - **Verify Black Shroud Concealment:** The site is completely invisible and cannot be selected or inspected.
3. Bring the site within the outer **Detection Ring** ($R_{detect}$), but keep it outside the inner **Identification Ring** ($R_{id}$) (e.g. using a single dish radar or distant scout caravan):
   - **Verify Site Appears:** The site emerges from the black shroud onto the globe.
   - **Verify Contact Masking:**
     - **Label:** Displays `unidentified site` (or `unknown site` in low-tech campaigns).
     - **Icon:** Renders a distinct gold/yellow unknown question mark / site icon (`UnknownSiteIconColor`).
     - **Inspect String:** Displays `Status: Unidentified Site` and instructs the player to bring sensors closer or send a caravan.
     - **Interaction Suppression:** Trade, raid, and quest gizmos are hidden; quest information is concealed.
     - **Float Menu:** Right-clicking the site with a caravan provides `Investigate unidentified site (Tile X)`.
4. Move a caravan onto the tile, or expand the inner **Identification Ring** ($R_{id}$) over the tile:
   - **Verify Full Identification:** The site instantly updates to show its true name (e.g. `Covenant Stronghold` / `Pirate Artillery Outpost`), real faction crest/tint, full description, and standard interaction gizmos.

---

### Test 8: Inter-Faction Collisions, Background Skirmishes & Persistent Aftermath (`TC-08`) — 🟡 Focus 2: Playtest

_Objective: Validate $O(M)$ tile-bucketed collisions, algebraic background skirmishes, persistent aftermath sites, and trade cargo salvage._

#### 8.1 Colliding Opposing Incursions

1. Switch to the World Map (`Tab`).
2. Identify a neutral road or wilderness hex between your colony and other settlements.
3. Using Dev Mode, spawn two opposing incursions set to traverse through the same hex:
   - Incursion A: Hostile Pirate Warband (e.g. 500 combat points).
   - Incursion B: Friendly Outlander Trade Caravan carrying goods (e.g. 400 combat points).
4. Fast-forward time at `Speed 3`.
5. Watch the two incursions approach the shared tile.

#### 8.2 Algebraic Background Skirmish Execution

1. As both incursions step onto the exact same tile:
   - **Verify Collision Timing:** Collision evaluates on `TickRare` (250 ticks).
   - **Verify Zero Tactical Map:** No tactical battle map is generated; the game stays on the World Map without stutter or loading pause.
   - **Verify Notification Letter:** A letter arrives:
     > **Inter-Faction Clash: [Faction A] vs [Faction B]**  
     > _"A violent skirmish has broken out at sector [Tile] between [Faction A] and [Faction B]! Both forces have engaged in an overland clash."_
2. Allow time to tick forward:
   - The battle resolves algebraically based on combat points, tech levels, and tactical RNG.
   - The defeated incursion takes casualties; survivors retreat toward home.
   - **15,000-Tick Cooldown:** Verify the surviving and retreating forces do not re-trigger collisions on every single subsequent tick.

#### 8.3 Encounter Aftermath Site & Scavenge Action

1. Inspect the clash hex after the skirmish concludes:
   - **Verify Aftermath Spawn:** A new world entity **Encounter Aftermath** (`WorldObject_EncounterAftermath`) appears on the hex.
   - **Verify Loot Preservation:** Inspect the aftermath site in Dev Mode: verify it holds surviving trade inventory from the trade caravan and wounded survivor pawns.
2. Form a player caravan from your colony and march to the Encounter Aftermath hex.
3. Select the caravan and right-click the aftermath site (or click the arrival action):
   - **Verify Action:** Select `Scavenge Aftermath`.
   - **Verify Salvage Window:** A scavenge interface opens displaying all salvageable weapons, apparel, trade cargo, and surviving wounded pawns.
   - Transfer desired items and rescue/capture pawns.
4. Depart the hex:
   - **Verify Aftermath Destruction:** The aftermath site is cleanly deallocated from memory (`RemoveAndDiscardPawnViaGC`).
   - Check debug log: verify zero memory leaks or dangling pawn errors.

---

### Test 9: Grounded Storyteller Caravan Encounters (`TC-09`) — 🟡 Focus 2: Playtest

_Objective: Validate grounded storyteller caravan encounters, shroud staging, contiguous route provenance, non-interrupting sighting alerts, and demand extortion item safety._

#### 9.1 Grounded In-Transit Caravan Sighting & Shroud Staging

1. Form a small player caravan and march it onto the world map under Fog of War or Classic Haze.
2. Trigger the storyteller caravan meeting incident via Dev Mode:
   - `Debug Actions Menu → Incidents → Execute Incident... → CaravanMeeting`.
3. **Verify Zero Modal Disruption:**
   - Instead of an instantaneous modal dialog forcing the game to pause, an early warning sighting message appears:
     - `"[CaravanName] spotted an approaching [GroupLabel] from [FactionName] [N] hexes away en route to [Destination]."`
4. **Verify Shroud Staging & Road Provenance:**
   - Locate the approaching trade caravan on the Geoscape:
     - **Staged in Darkness:** The caravan was staged in the unmonitored shroud 4–7 hexes away along its route, never materialising on an actively monitored tile.
     - **True Provenance:** Inspect the caravan: verify it has an origin settlement, destination settlement, and contiguous route.
5. **Verify Intercept & Roadside Barter:**
   - Right-click the approaching caravan and select `"Intercept caravan"`, or continue moving until both caravans occupy the same tile:
     - **Verify:** The roadside trading dialog opens naturally upon physical collision or interception.
6. **Verify Post-Encounter Continuation:**
   - Trade with the caravan or click `Part ways`:
     - **Verify:** The NPC caravan resumes marching along its route towards its destination rather than vanishing into thin air.

#### 9.2 Hostile Warband Pursuit & Extortion Demand (`CaravanDemand`)

1. With your player caravan on the world map carrying valuable goods, trigger:
   - `Debug Actions Menu → Incidents → Execute Incident... → CaravanDemand`.
2. **Verify Staged Pursuit & Warning Alert:**
   - Hostile warband is staged in the shroud advancing along the road, dispatching a warning alert (`ThreatSmall`).
   - If intercepted or overtaken on the same tile, the extortion demand dialog opens.
3. **Verify Non-Deduction During Dialog:**
   - Before clicking any button, inspect your caravan's inventory via Dev tools: verify demanded goods are **still in the caravan inventory** and have not been prematurely deducted.
4. Click `Refuse`:
   - Combat initiates; goods remain in your caravan.
5. In a separate test, click `Accept`:
   - Demanded goods are deducted cleanly upon button click with no duplication or missing items.

#### 9.3 Encounter Faction & Dialogue Consistency (Peaceful Meeting vs Hostile Warband)

1. Move a player caravan to an explored wilderness hex.
2. Trigger a peaceful roadside meeting via Dev Mode (`IncidentWorker_CaravanMeeting`):
   - `Debug Actions Menu → Incidents → Execute Incident... → CaravanMeeting`.
3. **Verify Zero Console Errors:**
   - Open the debug log (`~`): verify zero instances of `Tried to generate pawn kinds with null faction`.
   - Verify all generated pawns have valid assigned faction rosters and trader kinds.
4. **Verify Dialogue Coherence:**
   - Verify the dialog title and header display appropriate non-hostile labels: e.g. `Encounter: Merchant Caravan` or `Encounter: Peaceful Travellers` (NOT `Encounter: hostile warband`).
   - Verify body greeting matches faction relations: `[CaravanName] has crossed paths with the [CaravanType] from [FactionName]... They hail your caravan peacefully with friendly greetings and road information.`
5. **Verify Contextual Action Buttons:**
   - Verify peaceful interactions (`Trade`, `Move on / Part ways`, `Examine sector on Geoscape`) are available.
   - Verify hostile aggression is clearly identified as a war action: `Attack caravan (Declare War)`.
6. Trigger a hostile incursion encounter on the same hex:
   - Verify dialog header correctly displays `Encounter: Hostile Warband`.
   - Verify enemy forces are correctly marked hostile to the player with no contradictory friendly greetings.

---

### Test 12: Optimistic Fog-of-War Travel Routing & Unknown Hex Assumption (`TC-12`) — 🟡 Focus 2: Playtest

_Objective: Validate authentic 4X Fog-of-War optimistic pathfinding across unmapped sectors, dynamic obstacle detection upon visual contact, and graceful fallback when dead ends are discovered._

#### 12.1 Optimistic Travel Directives Across Uncharted Sectors

1. Start or load a game operating under Fog of War (`FogOfWar`).
2. Form a player caravan from your colony.
3. Switch to the World Map (`Tab`) and select the caravan.
4. Locate an unexplored sector deep within the black shroud that contains impassable mountains or ocean bodies (verify coordinates in Dev Mode / God Mode if necessary).
5. Right-click the unexplored sector with the caravan selected:
   - **Verify:** Travel directive options appear (`Travel to unexplored sector [Tile]`).
   - **Verify:** The caravan pathfinder does **not** leak impassable terrain ahead of time; the plotted route draws optimistically through the shroud assuming baseline 1.0x traversal difficulty.
6. Order the caravan to proceed toward the uncharted sector.

#### 12.2 Dynamic Obstacle Detection & Route Recalculation

1. Follow the caravan at fast forward speed (`Speed 3`) as it marches into the shroud.
2. As the caravan comes within lookout observation distance of an impassable mountain tile:
   - **Verify:** The mountain tile transitions from black shroud into explored terrain.
   - **Verify:** `NotifyCaravanVisionUpdated` detects the newly uncovered impassable tile.
   - **Verify:** The caravan dynamically recalculates its route locally around the discovered obstacle.
   - **Verify:** A notification message displays:
     > _"Caravan [Name] encountered impassable mountains in sector [Tile] and adjusted its route."_
   - **Verify:** The caravan continues optimistically through remaining uncharted sectors along its new route.

#### 12.3 Dead-End Discovery & Caravan Halt

1. Order a caravan toward an unexplored destination tile that is entirely landlocked by impassable mountains.
2. Allow the caravan to approach until vision reveals the destination tile itself is impassable:
   - **Verify:** If an adjacent passable sector exists, the caravan reroutes to that sector.
   - **Verify:** If no passable adjacent sector exists, the caravan halts dead at the perimeter.
   - **Verify:** A caution letter arrives:
     > _"Caravan [Name] reached the edge of sector [Tile]; further passage is blocked by impassable terrain."_

---

### Test 13: Drifting Orbital Mechanics & Non-Repeating Ground Tracks (`TC-13`) — 🟡 Focus 2: Playtest

_Objective: Validate Keplerian drifting orbital satellites with nodal precession ($\dot{\Omega}$) and incommensurate orbital periods ($T_{\text{orbit}} \nmid 60{,}000$)._

#### 13.1 Ground Track Westward Precession

1. With active orbital stations or drifting satellites spawned in orbit, open the World Map (`Tab`).
2. Select a satellite and open its telemetry inspect card:
   - **Verify:** Telemetry displays Keplerian parameters: inclination ($i$), orbital period ($T_{\text{orbit}}$), altitude, and current ground track sub-satellite coordinates.
3. Observe the sub-satellite point across successive orbits:
   - **Verify:** Due to planetary rotation beneath the inclined orbit, the ground track precesses westward on each pass.

#### 13.2 Day-1 Diurnal Non-Repeating Verification

1. Note the exact world hex and solar hour when the satellite passes directly overhead your colony.
2. Advance time forward by exactly 1 in-game day (60,000 ticks) via Dev Mode: `Debug Actions Menu → Tools - General → Fast-forward 1 day`.
3. Check the satellite position at the exact same solar hour on Day 2:
   - **Verify:** The satellite is **not** at the same location overhead, confirming $T_{\text{orbit}} \nmid 60{,}000$ and that passes do not recur on an artificial static diurnal clock.

#### 13.3 Storyteller-Calibrated Orbital Periods & Legacy Period Migration

1. Select satellites of varying roles across the globe and inspect their inspect cards:
   - **Trade Satellite:** Verify `Period: ~25.0 hrs (62,500 ticks)` (delivering a ~3.8 hr comms window over colony).
   - **Reconnaissance Satellite:** Verify `Period: ~18.0 hrs (45,000 ticks)`.
   - **Military / Early-Warning Satellite:** Verify `Period: ~30.0 hrs (75,000 ticks)`.
   - **Navigation Satellite:** Verify `Period: ~48.0 hrs (120,000 ticks)`.
   - **Communications Satellite:** Verify `Period: ~72.0 hrs (180,000 ticks)`.
2. Inspect orbital telemetry:
   - Verify trade satellite displays realistic altitude (~600 km) and orbital velocity (~7.56 km/s).
3. Legacy Save Period Migration:
   - Load a save created under an earlier milestone build where satellites had rapid sub-30,000 tick periods (< 12 in-game hours).
   - Inspect the satellite telemetry on load: verify `orbitalPeriodTicks` is automatically migrated and clamped to a minimum of 30,000 ticks (upgraded to the calibrated role baseline).

---

### Test 14: Persistent Orbital Stations & Constellation Service Zones (`TC-14`) — 🟡 Focus 2: Playtest

_Objective: Validate persistent high-orbit stations (`WorldObject_OrbitalStation`) and satellite constellation replenishment._

#### 14.1 Service Corridor Synchronization

1. Locate an orbital station hub on the World Map in high orbit.
2. Track a satellite entering the station's $35^\circ$ service cone:
   - **Verify:** Status changes to `In Service Area`.
   - **Verify:** Satellite telemetry and trade inventory synchronize with the station hub.

#### 14.2 Constellation Loss & Fabrication Pipeline

1. In Dev Mode, select an orbital satellite and execute `Destroy object`.
2. Inspect the parent orbital station:
   - **Verify:** The station detects the constellation deficiency.
   - **Verify:** An automated fabrication countdown queues:
     > _"Constellation Replenishment: Fabricating replacement satellite (ETA: X hours)"_
3. Advance time until the timer elapses:
   - **Verify:** A new satellite launches into the assigned orbital plane with fresh telemetry.

---

### Test 15: Satellite Mission Roles (Trade & Navigation Boost) (`TC-15`) — 🟡 Focus 2: Playtest

_Objective: Validate orbital trade communications, storyteller-calibrated overpass cadence, and tactical navigation acceleration._

#### 15.1 Orbital Trade Satellite Comms Interaction & Cadence

1. Construct a powered `Building_CommsConsole` on your colony map.
2. Track an orbital trade satellite traversing overhead:
   - **Verify Overpass Window:** The satellite remains within communication range of the colony console for approximately 3.8 in-game hours (~160 seconds in real time at 1x speed, ~27 seconds at 3x speed), providing ample time for colonist drafting and trade transactions without frantic rushing.
   - **Verify Storyteller Cadence Alignment:** Over multiple in-game quadrums, verify trade satellite overpasses occur on average every 8–12 days across a global fleet of 2–3 active trade vessels, naturally matching Cassandra Classic and Phoebe Chillax orbital trade frequencies without virtual dice-roll spawning.
3. Direct a colonist to operate the Comms Console while the satellite is within the colony's orbital overhead cone:
   - **Verify:** The trade satellite appears in the comms dialogue list (e.g. `Comms: Orbital Trade Satellite [Name]`).
   - **Verify:** Opening trade opens standard trade interface; purchased cargo arrives via drop pods.

#### 15.2 Navigation Satellite Caravan Boost

1. Form a player caravan on the World Map.
2. Move the caravan under the 24-tile footprint of an active navigation satellite:
   - **Verify Inspect Panel:** Telemetry inspect string displays `Orbital GPS Uplink: +15% travel speed (0.85x ticks per move)`.
   - **Verify Movement Rate:** Caravan travel ticks decrease proportionally while under footprint.
3. Move the caravan outside the footprint:
   - **Verify:** Overland velocity reverts cleanly to baseline rate.

---

### Test 20: Monotonic Visibility Escalation & Side-Effects Engine (`TC-20`) — 🟡 Focus 2: Playtest

_Objective: Validate monotonic visibility elevation and operational side-effects execution._

#### 20.1 Escalation Confirmation & Warning Modal

1. Load a game operating under Fog of War (`FogOfWar`) with review window locked.
2. Spawn a dynamic quest site in the black shroud so it becomes concealed (`HazeReconnaissanceManager.RegisterConcealedSite`).
3. Form a moving player caravan travelling through the shroud.
4. Open `Options → Mod Settings → Sensory & Radar`.
5. Click `Planetary Surveillance`:
   - **Verify Modal:** A confirmation dialog appears:
     > **Confirm Planetary Visibility Escalation**  
     > _"Are you sure you want to escalate visibility from Fog of War to Planetary Surveillance? This change is PERMANENT for this campaign and cannot be undone."_
6. Click `Cancel`: Verify visibility remains `FogOfWar`.

#### 20.2 Side-Effects Execution Verification

1. Click `Planetary Surveillance` again and click `Confirm Escalation`:
   - **Verify 1: Concealed Site Release:** The concealed dynamic site is uncovered; its deferred quest/announcement letter arrives immediately and its timeout clock resumes.
   - **Verify 2: Caravan Route Recalculation:** The moving player caravan immediately repaths using newly revealed world geography.
   - **Verify 3: Vision Cache Sweep:** Sensors rescan immediately.
   - **Verify 4: World Mesh Rebuild:** 3D fog of war layers and feature labels rebuild without visual artifacts or requiring game restart.
2. Open `About & Diagnostics` tab:
   - **Verify Ledger Logging:** An `Escalation` entry is recorded:
     > `Escalation | visibilityMode: Fog of War -> Planetary Surveillance`

---

### Test 22: Developer-Mode Session Unsafe Override & Audit Trail (`TC-22`) — 🟡 Focus 2: Playtest

_Objective: Validate DevMode-gated session override for emergency debugging and forensic audit logging._

#### 22.1 Unsafe Override Activation

1. Enable Developer Mode in RimWorld options (`Options → Development mode: On`).
2. Open `Options → Mod Settings → About & Diagnostics`:
   - **Verify Checkbox Appearance:** Under `Active Campaign Rules & Governance`, a checkbox appears:
     > `Allow Unsafe Rule Transitions (Session Only)`
3. Check `Allow Unsafe Rule Transitions (Session Only)`:
   - **Verify Context Banner:** Mod Settings header displays a red warning banner:
     > `Active Save: DEV OVERRIDE ACTIVE (All rule mutability restrictions bypassed)`
   - **Verify Ledger Logging:** Ledger records `DevOverrideEnabled` with note `Toggled on`.

#### 22.2 Destructive Downgrade Execution

1. Switch to `Sensory & Radar` tab:
   - **Verify Badges:** Previously locked options now display `(Unsafe)` or `[Unsafe Override]`.
2. Click `Fog of War`:
   - **Verify Modal:** A red modal appears:
     > **Unsafe Rule Downgrade Warning**  
     > _"Warning: Downgrading planetary visibility from Planetary Surveillance to Fog of War is an unsafe operation. Active sensors will re-scan, but previously mapped sectors may be re-shrouded. This action will be permanently recorded in the save game audit ledger."_
3. Click `Confirm`:
   - **Verify:** Visibility mode downgrades to `FogOfWar`.
   - **Verify Fog Re-Flooding:** Planetary fog re-floods the globe, with visibility re-established around active colony radars and lookouts.
   - **Verify Ledger Logging:** An `UnsafeChange` entry is recorded:
     > `UnsafeChange | visibilityMode: Planetary Surveillance -> Fog of War [Developer override]`

---

### Priority 3: Edge Case & Lifecycle Scenarios — 🔵 Focus 3

### Test 19: Legacy Save Migration & Day-1 Review Grace Period (`TC-19`) — 🔵 Focus 3: Scenario

_Objective: Validate migration of unversioned pre-1.3 saves, review window grace period, and rule lock confirmation._

#### 19.1 Legacy Save Ingestion & Review Window Setup

1. Load a legacy save created under Milestone 1.2 or early 1.3 (where `schemaVersion == 0` or missing).
2. Upon map load:
   - **Verify Letter Arrival:** A neutral event letter arrives:
     > **Project Vigilance: Campaign Rules Bound to Save**  
     > _"Project Vigilance now stores gameplay-affecting campaign rules directly within your save file to prevent cross-campaign contamination. A 1-day review window is currently open, allowing you to freely adjust and lock in your desired rules under Mod Settings."_
   - If the save contained explored tiles, verify the letter includes the notice:
     > _"Notice: This save contains Fog of War exploration data and may have been created under Fog of War."_
3. Open `Options → Mod Settings → Project Vigilance: Geoscape`:
   - **Verify Context Banner:** A green review banner displays:
     > `Active Save: 1-Day Review Window Open (Free rule adjustments permitted)`
   - **Verify Rule Mutability:** All rules (including Immutable rules like `colonyScoutRadius`) are freely editable without lock restrictions during the review window.

#### 19.2 Rule Confirmation & Expiration

1. Navigate to the `About & Diagnostics` tab:
   - **Verify Ledger Entry:** Pinned `LegacyMigration` record appears with note `Adopted ModSettings defaults | ...`.
   - **Verify Confirm Button:** A button `Confirm & Lock In Campaign Rules Now` is visible.
2. Click `Confirm & Lock In Campaign Rules Now`:
   - **Verify:** A confirmation message appears: `Campaign rules locked. Mutability restrictions now active.`
   - **Verify Banner Transition:** Banner transitions to standard active save banner:
     > `Active Save: Campaign Rules Bound to Save (Immutable rules locked; Escalation allowed)`
   - **Verify Ledger Logging:** A new record `ReviewWindowLocked` is logged with note `Confirmed by player`.
   - **Verify Rule Locking:** Immutable rules immediately become locked and greyed out.

---

### Test 25: Mutable Notification Preferences & ModSettings Decoupling (`TC-25`) — 🔵 Focus 3: Scenario

_Objective: Validate Mutable rule operation and total decoupling of factory defaults from active saves._

#### 25.1 Mutable Rule In-Game Toggling

1. In an active game with review window locked and DevMode off, open `Options → Mod Settings → Early Warning & Intel`.
2. Toggle `Dispatch Early Warning notifications on contact detection` off:
   - **Verify:** Checkbox toggles immediately with no confirmation modal.
   - **Verify:** Switch to `About & Diagnostics`: 0 new records are added to the ledger (mutable preferences do not pollute the audit trail).
3. Spawn an incursion:
   - **Verify:** Incursion detection does not dispatch early warning letters.

#### 25.2 Factory Defaults Reset Isolation

1. While in an active game, open `About & Diagnostics` tab.
2. Inspect the reset button label:
   - **Verify Label:** Displays `Reset New Campaign Defaults to Factory Settings`.
3. Click `Reset New Campaign Defaults to Factory Settings`:
   - **Verify Notification:** Displays `Project Vigilance new campaign defaults reset. Active save rules remain unaffected.`
   - **Verify Save State Isolation:** Check `Sensory & Radar` and `Incursions & AI` tabs:
     - Active campaign rules, visibility mode, and audit ledger are **completely unchanged**.
4. Quit to the Main Menu and open Mod Settings:
   - **Verify:** Factory defaults have been reset to factory values for newly created future games.

---

### Test 26: First-Time Project Vigilance Adoption (`TC-26`) — 🔵 Focus 3: Scenario

_Objective: Validate that an established colony that has never used Project Vigilance receives explicit agency via the adoption modal, defaults safely to Planetary Surveillance, and records a pinned provenance audit entry._

#### 26.1 Standard Mod Adoption (Planetary Surveillance)

1. Load an established vanilla RimWorld save (e.g. Year 5+ colony) that has never run Project Vigilance.
2. Observe game initialisation:
   - **Verify Game State:** Game loads in paused state.
   - **Verify Modal Presentation:** `Dialog_GeoscapeModAdoption` is presented automatically on screen.
   - **Verify Options:** All four options are visible:
     - `Planetary Surveillance (Rec.)`
     - `Fog of War`
     - `Hardcore Vigilance`
     - `Customise Individual Rules...`
3. Click `Planetary Surveillance (Rec.)`:
   - **Verify World Map:** Terrain, oceans, rivers, and static faction settlements remain charted and fully visible.
   - **Verify Tactical Cloaking:** Active hostile incursions, mechanoid warbands, and dynamic camps outside lookout/radar envelopes remain hidden.
   - **Verify Review Banner:** Open `Options -> Mod Settings`: green 1-day review banner is displayed with remaining ticks and lock confirmation button.
   - **Verify Pinned Provenance Record:** Enable DevMode and inspect the ledger:
     - Record #0 has kind `ModAdoption`.
     - Is marked pinned (`IsPinned == true`).
     - Note confirms `Profile: Planetary Surveillance (User Selection)`.

#### 26.2 "The Great Blackout" Challenge Scenario (Fog of War)

1. Load the vanilla save again.
2. In `Dialog_GeoscapeModAdoption`, inspect the `Fog of War` card:
   - **Verify Warning Note:** Displays prominent amber warning:
     ```text
     Warning:
     Existing geographic knowledge will be lost.
     Recommended only for players intentionally starting a re-discovery challenge.
     ```
3. Click `Fog of War`:
   - **Verify Planetary Shroud:** Unmonitored sectors outside colony lookouts/radar are plunged into black shroud.
   - **Verify Caravan Safety:** Any caravans in transit repath optimistically without pathing errors or crashes.
   - **Verify Ledger:** Pinned record notes `Profile: Fog of War (Recommended) (User Selection)`.

#### 26.3 Dialog Dismissal Fallback & 1-Day Review Window

1. Load the vanilla save again.
2. When `Dialog_GeoscapeModAdoption` appears, press `Esc` or click the top-right `X` close button without making an explicit selection.
3. Observe adoption fallback:
   - **Verify Notification:** Notification confirms adoption with `Planetary Surveillance` baseline.
   - **Verify World Map:** Geography remains charted (non-destructive baseline).
   - **Verify Review Window:** Open Mod Settings: 1-day review window remains open, allowing the player to change their rules freely.
   - **Verify Ledger:** Record notes `Profile: Planetary Surveillance (Dismissal Fallback)`.

---

### Priority 4: Verified Passes (In-Game & Automated) — 🟢 Passed

### Test 10: Waylith DeepProfiler & Zero-Allocation Verification (`TC-10`) — 🟢 Automated Pass

_Objective: Validate that sensory sweeps and collision passes execute within the $\le 1.0\text{ ms}$ budget, use single-pass traversal distance resolution, and generate zero GC allocations during steady state._

1. Settle a world with at least 3 player colonies/outposts equipped with active radars.
2. Spawn 10–15 concurrent travelling incursions across the globe.
3. Open RimWorld's native DeepProfiler:
   - `Debug Actions Menu → Deep profiler` (toggle active).
4. Run the game for 5,000 ticks at `Speed 3`.
5. Pause the game and inspect the DeepProfiler output log:
   - **Verify Vigilance Profiler Blocks:**
     - `ProjectVigilance.PerformSensorySweep`
     - `ProjectVigilance.SensorySweep.VisionUpdates`
     - `ProjectVigilance.SensorySweep.TargetEvaluations`
     - `ProjectVigilance.InterFactionCollisions`
     - `ProjectVigilance.SkirmishResolution`
   - **Single-Pass Traversal Optimization:** Confirm `TryGetDetectionDistance(target, out int distance)` resolves both detection capability and traversal step distance in a single unified BFS pass without duplicate graph queries per sensor/target pair.
   - **Verify Execution Time:** The total sensory sweep time averages **$\le 0.3\text{ ms}$** and peaks well below the **$1.0\text{ ms}$** budget.
   - **Verify Zero Allocation:** Steady-state ticks (when no new world objects are spawning/despawning) show **0 B allocated** during `TickRare`.

---

### Test 11: Scribe Save / Load & Outpost Removal Resilience (`TC-11`) — 🟢 Automated Pass

_Objective: Validate 100% Scribe serialization integrity, material fidelity, and defensive null handling._

#### 11.1 Comprehensive Save / Load Cycle

1. Set up a complex game state with:
   - 2 colonies with triple-dish Phased Arrays and SIGINT consoles.
   - 2 deployed automated listening posts (1 operational, 1 sabotaged).
   - 2 deployed signal beacons (1 built with Wood, 1 built with Granite blocks).
   - 3 active travelling incursions (1 at Stage 1, 1 at Stage 2, 1 traversing mountains).
   - 1 concealed dynamic camp in the Haze.
   - 1 persistent `WorldObject_EncounterAftermath` site.
2. Save the game (`Esc → Save Game → "Test_Milestone1_3"`).
3. Return to the Main Menu (`Quit to Main Menu`).
4. Load the saved game (`Load Game → "Test_Milestone1_3"`).
5. **Verify Debug Log (`~`):**
   - Check the debug log immediately upon loading:
     - **0 Scribe duplicate ID errors.**
     - **0 null-reference exceptions.**
     - **0 missing comp errors.**
6. **Verify State Restoration & Material Fidelity:**
   - Multi-dish interferometry links re-establish immediately.
   - Sabotaged outpost retains its sabotaged status.
   - The Granite Signal Beacon retains its `stuffDef` (`BlocksGranite`), label (`Granite blocks Signal Beacon`), and inspect string.
   - Stage 2 classified incursions retain their faction crests and tracking IDs.
   - Concealed camp remains concealed in the Haze with frozen timeout clock.
   - Aftermath site retains its cargo and survivor pawns.

#### 11.2 External Outpost Deletion Test

1. Select an operational listening post on the World Map.
2. Using Dev Mode, execute `Destroy object` (simulating an external mod or command deleting the outpost directly).
3. Fast-forward time across multiple `TickRare` intervals:
   - **Verify:** `SensoryManager` handles the missing sensor cleanly on the next sweep.
   - **Verify:** No persistent ghost sensor perimeters remain on the globe.
   - **Verify:** Zero errors logged to console.

---

### Test 16: Cross-Campaign Save Isolation & Pinned Rules (`TC-16`) — 🟢 Automated Pass

_Objective: Validate that gameplay rules are strictly persisted within save files and cannot cross-contaminate between campaigns._

#### 16.1 Multi-Campaign Isolation Verification

1. Start RimWorld and open `Options → Mod Settings → Project Vigilance: Geoscape`.
   - Note the default visibility setting (factory default: `FogOfWar`).
2. Start a new game (**Campaign A**):
   - On the campaign creation wizard (`Page_GeoscapeRules`), select the `Fog of War` card.
   - Generate world and embark. Save game as `Campaign_A_Fog`.
3. Quit to the Main Menu and start a second game (**Campaign B**):
   - On the campaign creation wizard (`Page_GeoscapeRules`), select the `Planetary Surveillance` card.
   - Generate world and embark. Save game as `Campaign_B_Surveillance`.
4. Return to the Main Menu and load `Campaign_A_Fog`:
   - Open `Options → Mod Settings → Project Vigilance: Geoscape`:
     - **Verify:** Visibility mode is strictly `FogOfWar`.
     - **Verify:** Unmonitored world tiles show fog of war and haze shroud.
5. Return to the Main Menu and load `Campaign_B_Surveillance`:
   - Open `Options → Mod Settings → Project Vigilance: Geoscape`:
     - **Verify:** Visibility mode is strictly `PlanetarySurveillance`.
     - **Verify:** World geography and continents are fully revealed.
6. Quit to the Main Menu and open Mod Settings:
   - **Verify:** Factory defaults remain completely unaffected by either campaign.

---

### Test 17: Native Campaign Creation Wizard Page & Profile Selection (`TC-17`) — 🟢 In-Game Pass

_Status: Verified (In-game playtest passed on RimWorld 1.7.5000)_

_Objective: Validate the native campaign creation wizard page (`Page_GeoscapeRules`), stitched between Storyteller selection and World Parameters, 2x2 profile selection cards, dedicated Anomaly-style modal dialog (`Dialog_GeoscapeCampaignRules`), and Day-0 ledger pinning._

#### 17.1 Wizard Page Stitching & Navigation Flow

1. From the Main Menu, initiate a new game: click `New Colony`.
2. On `Page_SelectScenario`, select `Crashlanded` and click `Next`.
3. On `Page_SelectStoryteller`, choose any storyteller and difficulty, then click `Next`:
   - **Verify Page Insertion:** The wizard transitions directly to `Page_GeoscapeRules` ("Project Vigilance: Geoscape Rules & Surveillance Protocol").
   - **Verify Bottom Navigation:** Bottom bar presents standard `Back` and `Next` buttons.
4. Click `Back`:
   - **Verify:** Returns cleanly to `Page_SelectStoryteller`.
5. Click `Next` again to return to `Page_GeoscapeRules`.
6. Click `Next` without making changes:
   - **Verify:** Proceeds forward seamlessly to `Page_CreateWorldParams` (World Generation Parameters).
7. On `Page_CreateWorldParams`, click `Back`:
   - **Verify:** Returns cleanly to `Page_GeoscapeRules` with all previous selections preserved.

#### 17.2 Profile Preset Selection Grid ($2 \times 2$)

1. On `Page_GeoscapeRules`, inspect the main central area:
   - **Verify Grid Layout:** Four distinct profile cards are rendered in a clean $2 \times 2$ grid:
     1. **Fog of War** (Recommended default)
     2. **Planetary Surveillance**
     3. **Hardcore Vigilance**
     4. **Omniscient**
2. Click **Planetary Surveillance**:
   - **Verify Card Selection:** Card highlights with a bright selection border.
   - **Verify Rule State:** Visibility switches to `PlanetarySurveillance`; simulation rules adjust to surveillance defaults.
3. Click **Hardcore Vigilance**:
   - **Verify Card Selection:** Card highlights; visibility switches to `FogOfWar`; colony lookout horizon tightens to 4.0 hexes, caravan lookout tightens to 2.0 hexes.
4. Click **Omniscient**:
   - **Verify Card Selection:** Card highlights; planetary fog of war and incursion sensory shielding are fully bypassed.
5. Click **Fog of War**:
   - **Verify Card Selection:** Card highlights; colony lookout horizon resets to 5.0 hexes, caravan lookout resets to 3.0 hexes.

#### 17.3 Anomaly-Style Modal Settings Dialog (`Dialog_GeoscapeCampaignRules`)

1. Below the $2 \times 2$ profile cards, locate the configuration button:
   - **Verify Button Label:** Displays `Customise Individual Rules...`.
2. Click `Customise Individual Rules...`:
   - **Verify Modal Window:** Opens `Dialog_GeoscapeCampaignRules` as a separate Anomaly-style modal window on top of `Page_GeoscapeRules`.
   - **Verify Presets Bar:** Top bar displays 4 presets (`Fog of War`, `Planetary Surv.`, `Hardcore`, `Omniscient`) with active preset highlighting and active profile indicator.
   - **Verify Rules Scroll View:** Displays all sections (`CampaignRulesPanel`) with sliders, radios, and checkboxes.
   - **Verify Visibility Radio:** Displays `Fog of War (Recommended)` without duplicated `(Recommended)` text.
3. Adjust a slider within the modal dialog (e.g. adjust Colony Scout Horizon to 6.5 hexes) or click a preset, then click `Close`:
   - **Verify Reactive Profile Update:** `Dialog_GeoscapeCampaignRules` closes and `Page_GeoscapeRules` immediately reflects the updated state:
     - Profile cards update selection (or deselect with active profile showing `Active Profile: Custom`).
     - The Overview summary section at the bottom reflects the updated values without card text truncation.
4. Click `Next` to navigate to `Page_CreateWorldParams`:
   - **Verify Legacy Button Absence:** The bottom bar of `Page_CreateWorldParams` no longer renders the old `Geoscape Rules...` button.
   - **Verify Draft Preserved:** Draft rules are intact (not wiped by world params open).
5. On `Page_CreateWorldParams`, click `Generate`:
   - **Verify Draft Consumption:** World generation consumes the draft rules and embarks onto the colony map.
   - **Verify Audit Ledger:** Open `Options → Mod Settings → About & Diagnostics`:
     - Verify a pinned record `CampaignCreated` is logged with note `Campaign creation wizard | visibilityMode=...`.

---

### Test 18: Default Campaign Creation & Day-0 Snapshot (TC-18) — 🟢 In-Game Pass

_Objective: Validate campaign creation when wizard defaults are accepted without modification._

1. From the Main Menu, initiate a new game and proceed through Page_GeoscapeRules without altering any cards or settings.
2. Embark onto the colony map.
3. Open Options → Mod Settings → Project Vigilance: Geoscape:
   - **Verify Visibility:** Operates under FogOfWar (Recommended default).
   - **Verify Lookout Horizons:** Colony horizon is 5.0 hexes; Caravan lookout is 3.0 hexes.
4. Switch to About & Diagnostics tab:
   - **Verify Active Profile:** Displays Fog of War (Recommended).
   - **Verify Ledger Origin:** Day-0 pinned record displays:
     > CampaignCreated | Defaults | visibilityMode=FogOfWar ...

---

### Test 21: Mutability Enforcement & Immutable Rule Locking (`TC-21`) — 🟢 Automated Pass

_Objective: Validate that Immutable rules lock permanently and reverse visibility transitions are rejected outside developer mode._

1. Ensure Developer Mode is **disabled** (`Options → Development mode: Off`).
2. Load an active campaign with review window locked.
3. Open `Options → Mod Settings → Project Vigilance: Geoscape`:
4. **Test Sensory & Radar Tab:**
   - Attempt to click `Fog of War`:
     - **Verify:** Option is greyed out and displays `Fog of War (Locked - Cannot reverse)`. Clicking has no effect.
   - Attempt to adjust `Colony Scout Horizon` slider:
     - **Verify:** Header displays `[Locked]` and slider cannot be dragged.
   - Attempt to toggle `Nighttime visual lookout reduction`:
     - **Verify:** Label displays `(Locked)` and checkbox cannot be toggled.
5. **Test Incursions & AI Tab:**
   - Attempt to toggle `Synchronize multi-vector raid departures`, `Biological incursions rest at night`, `Enable tactical ingress diversification`, or `Reset Storyteller threat cycle`:
     - **Verify:** All 4 checkboxes display `(Locked)` and reject user clicks.
6. Open debug log (`~`):
   - **Verify:** Zero errors or exceptions; active campaign rules remain 100% unaltered.

---

### Test 23: Automatic Override Invalidation on DevMode Toggle (`TC-23`) — 🟢 Automated Pass

_Objective: Validate that switching off Developer Mode immediately revokes unsafe override permissions._

1. With `Allow Unsafe Rule Transitions` enabled and active, return to RimWorld options.
2. Toggle Developer Mode **Off** (`Options → Development mode: Off`).
3. Re-open `Options → Mod Settings → Project Vigilance: Geoscape`:
   - **Verify Banner Reversion:** Red banner disappears; standard active save banner returns.
   - **Verify Immediate Locking:** All immutable rules and reverse visibility options immediately return to `(Locked)` state.
4. Switch to `About & Diagnostics` tab:
   - **Verify Override Checkbox Hidden:** `Allow Unsafe Rule Transitions` checkbox is completely removed from UI.
   - **Verify Ledger Record:** Ledger logs `DevOverrideDisabled` with note `Developer mode switched off`.

---

### Test 24: Override Session Ephemerality Across Loads & Restarts (`TC-24`) — 🟢 Automated Pass

_Objective: Validate that unsafe override flag is never written to save XML and does not survive restarts._

1. With Developer Mode on and unsafe override active, save the game as `Test_Override_Save`.
2. Open the `.rws` save file in a text editor (located in `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves`):
   - **Verify:** Search for `allowUnsafeRuleTransitions`: confirm it does **not** appear anywhere in the save file.
3. Quit RimWorld completely to desktop.
4. Relaunch RimWorld:
   - Load `Test_Override_Save`.
   - Open Mod Settings:
     - **Verify:** Unsafe override is **disabled by default**, preventing unintentional rule alterations.
5. In a separate test, enable override at the Main Menu, then load a save:
   - **Verify Ledger Record:** Loading a save with override already active logs `DevOverrideEnabled` with note `Override active when campaign was loaded`.

---

### Test 28: Uncharted Globe Starting Site Selection & Automated Landfall Paradigm (`TC-28`) — 🟢 In-Game Pass

_Objective: Validate that campaign setup on an uncharted globe under Fog of War seamlessly implements automated landfall—skipping purposeless clicking on a dark sphere while ensuring valid plot designation—preserves manual targeting when opted into or when playing with revealed geography, and leaves Step 3 storyteller selection 100% vanilla for maximum mod compatibility._

#### 28.1 Automated Landfall under Fog of War & Hardcore Vigilance (Default Flow)

1. From the Main Menu, click **New colony** $\rightarrow$ select scenario (e.g. Crashlanded) $\rightarrow$ select Storyteller (e.g. Cassandra Classic).
2. On `Page_GeoscapeRules` (Step 4), ensure **Fog of War** (or **Hardcore Vigilance**) is selected:
   - **Verify Summary:** The overview panel displays `Starting Site Selection: Automated Landfall`.
3. Click `Next` to advance to `Page_CreateWorldParams` (Step 5).
4. Configure globe parameters (e.g. 50% coverage, seed, map size via `Advanced settings: Edit...`).
5. Click **Generate**:
   - **Verify Automated Landfall:** World generation completes and advances **directly to Ideology preset or Colonist selection** (`Page_ChooseIdeoPreset` or `Page_ConfigureStartingPawns`).
   - **Verify Black Globe Skipped:** `Page_SelectStartingSite` is completely bypassed.
   - **Verify Diagnostic Log:** Inspect console/debug log to verify `[Project Vigilance] Automated landfall designated starting tile {candidate}.`
6. Click `Back` on Ideology / Colonist selection:
   - **Verify Bidirectional Navigation:** Navigates back directly to `Page_CreateWorldParams`.
   - **Verify Back to Rules:** Clicking `Back` again navigates back to `Page_GeoscapeRules`.
7. Embark into the colony:
   - **Verify Valid Landfall:** Colony map generates on a valid, habitable, non-ocean land tile.
   - Switch to World Map (`Tab`): The dark, fogged globe renders with active lookout vision centred on the colony.

#### 28.2 Optional Manual Globe Targeting under Fog of War

1. Start a new campaign $\rightarrow$ reach `Page_GeoscapeRules` with **Fog of War** active.
2. Click **Customise Individual Rules...**:
   - In `Landing Site Selection`, toggle **Manual Globe Targeting**.
   - Close dialog (`X` or `Esc`).
   - **Verify Status:** Active profile indicates `Active Profile: Custom`.
3. Click `Next` $\rightarrow$ click `Generate` on `Page_CreateWorldParams`:
   - **Verify Globe Screen Shown:** `Page_SelectStartingSite` is presented with the planetary globe shrouded in darkness.
4. Without selecting a hex, click `Next`:
   - **Verify Fallback Auto-Designation:** Instead of blocking with the vanilla error _"Must select a starting site"_, a message displays:
     > _"Random landing site automatically designated."_
   - The setup smoothly advances to Ideology / Colonist selection.
5. Alternatively, rotate the globe, click on a preferred sector (e.g. polar or equatorial latitude), and click `Next`:
   - Colony establishes in the chosen latitude.

#### 28.3 Planetary Surveillance & Omniscient Preservation

1. Start a new campaign $\rightarrow$ select **Planetary Surveillance** (or **Omniscient**) on `Page_GeoscapeRules`.
2. Click `Next` $\rightarrow$ click `Generate` on `Page_CreateWorldParams`:
   - **Verify Full Cartography:** `Page_SelectStartingSite` opens normally with full orbital cartography, revealed terrain, biomes, and standard manual site selection.

#### 28.4 Automated Landfall on Planetary Surveillance / Omniscient (Custom Rules)

1. Start a new campaign $\rightarrow$ select **Planetary Surveillance** (or **Omniscient**) on `Page_GeoscapeRules`.
2. Click `Customise Individual Rules...` $\rightarrow$ set `Starting Site Selection` to **Automated Landfall**.
3. Click `Close` $\rightarrow$ click `Next` $\rightarrow$ click `Generate` on `Page_CreateWorldParams`:
   - **Verify Black Globe Skipped:** `Page_SelectStartingSite` is completely bypassed.
   - The setup smoothly designates a valid random starting site and advances directly to Ideology / Colonist selection.

#### 28.5 Ambiguous Landing Rejection on Physically Impossible Tiles (Manual Targeting)

1. Under `Fog of War` with `Manual Globe Targeting` enabled, locate an unexplored/fogged ocean or deep water hex in the black shroud.
2. Attempt to settle on the hex:
   - **Verify Rejection Message:** Displays ambiguous rejection message:
     > _"Unviable landing coordinates: Selected sector cannot support a colony drop."_
3. Attempt to settle on an unexplored impassable mountain hex:
   - **Verify Rejection Message:** Displays the exact same ambiguous telemetry message without disclosing mountain terrain or impassability.

#### 28.6 Blind Touchdown on Environmental Hazard Sectors (Manual Targeting)

1. Under `Fog of War` with `Manual Globe Targeting` enabled, select an unexplored sector containing hazards (toxic pollution, extreme desert, ice sheet).
2. Click `Next`:
   - **Verify Hazard Suppression:** No hazard confirmation dialog pops up; settle proceeds blind.
   - **Verify Proximity Goodwill Suppression:** Proximity diplomatic warnings are suppressed so hidden faction bases in the shroud are not revealed.
3. Switch to `Planetary Surveillance`:
   - **Verify Informed Confirmation:** Selecting a polluted or hazardous hex prompts the standard vanilla confirmation dialog before embarking.

#### 28.7 Step 3 Storyteller Screen Mod Compatibility

1. On `Page_SelectStoryteller` (Step 3), verify the page is 100% vanilla without modded text overlays or button injections.
2. All Project Vigilance simulation rules, mutability constraints, and profile descriptions remain contained exclusively within `Page_GeoscapeRules` and `Dialog_GeoscapeCampaignRules`.

---

## In-Game Verification Sign-Off Checklist

### Priority 1: Active In-Game Focus (Immediate)

- [x] 🟢 **TC-01:** Multi-dish radar interferometry progression ($10 \rightarrow 17 \rightarrow 22$ rings), compound-wide SIGINT array sharing (`CompSigintConsole`), scaled decryption bandwidth, and unpowered fail-safe redundancy verified (In-Game Pass).
- [x] 🟢 **TC-04:** 3D holographic honeycomb mesh, boundary rings, five-tab Mod Settings UI architecture, unclipped intensity sliders, dynamic tab heights, compact colour chips + hex inputs, and label clean-up verified (In-Game Pass).
- [x] 🟢 **TC-05:** 4X Colony right-click expedition dispatch, automated supply pre-allocation, zero-TPS listening post deployment, mid-build departure protection & material refund/pause lifecycle, stuffDef material tracking, circadian & Night Owl assembly, and active operations verified (In-Game Pass).
- [x] 🟢 **TC-06:** Two-stage early warning non-peeking escalation (Stage 1 unidentified contact alert & yellow '?' blip $\rightarrow$ Stage 2 hostile threat identification & red blip) and direct entry bypass verified with save game TC-06 (In-Game Pass).
- [x] 🟢 **TC-27:** Dual-layer orbital rendering across Surface and Orbit layers (`<fullyExpandedInSpace>`), dynamic faction livery colour tinting, orbital selection persistence across camera zoom layers (`WorldSelector_SelectedLayer_Patch`), logarithmic GEO altitude scaling, deterministic asteroid/station stratification (`WorldObject_DrawPos_Patch`), and surface player entity visibility and interactivity from the orbital layer (`WorldObject_Visibility_Patch`) verified (In-Game Pass).
- [x] 🟢 **TC-29:** Overlapping entity disambiguation menu, faction livery rendering, status details, shift-click toggle, sensory masking, and civilization-style settlement staging outside colonies (`CaravanArrivalAction_HoldPosition`) verified (In-Game Pass).
- [x] 🟢 **TC-30:** Smart source-agnostic contextual dispatch, proximity ordering, move/send caravan formatting, and fog-of-war exploration routing verified (In-Game Pass).
- [x] 🟢 **TC-31:** Deterministic savegame ledger replay reconciliation on load, unrecorded XML tamper detection, cryptographic HMAC-SHA256 signature chain anti-forgery, diff recording in ledger, and Mod Settings integrity telemetry badges verified (In-Game Pass).
- [x] 🟢 **TC-32:** Anomalous and man-made landmark exploration with tactical cyan exclamation mark (`!`) blips, 3D ground mesh concealment until $R_{id}$, non-peeking narrative strings, and mundane geography exemption verified (In-Game Pass).

### Priority 2: Core Colony Playtest

- [x] 🟢 **TC-02:** Model B+ origin vantage bonuses, target mountain masking, and SIGINT penetration verified (In-Game Pass).
- [ ] 🟡 **TC-03:** Coastal hydrophone sonar array aquatic hex restriction verified.
- [ ] 🟡 **TC-07:** Concealed dynamic camps in Fog of War, quest timeout freeze, breadcrumb rumours, and PostRemove despawn auto-purge verified.
- [ ] 🟡 **TC-08:** $O(M)$ tile-bucketed collisions, algebraic background skirmishes, and aftermath scavenging verified.
- [ ] 🟡 **TC-09:** Grounded storyteller caravan encounters, faction/dialogue consistency (no null faction errors or friendly hostile warbands), and extortion demand item safety verified.
- [ ] 🟡 **TC-12:** Optimistic fog-of-war travel routing, unmapped obstacle dynamic route recalculation, and dead-end halting verified.
- [ ] 🟡 **TC-13:** Drifting orbital satellite Keplerian mechanics, calibrated orbital periods (62,500-tick trade, 45,000-tick recon), westward ground track precession, and legacy migration verified.
- [ ] 🟡 **TC-14:** Persistent high-orbit station hubs, $35^\circ$ service cone synchronisation, and automated replenishment fabrication verified.
- [ ] 🟡 **TC-15:** Trade satellite comms console interaction, ~3.8 hr overpass window (~160s at 1x), 8–12 day revisit cadence, and navigation satellite overland velocity boosts (+15%) verified.
- [ ] 🟡 **TC-20:** Monotonic visibility escalation from Fog of War, confirmation modal, site release, sensor rescan, and caravan repath side-effects verified.
- [ ] 🟡 **TC-22:** Developer Mode session unsafe override, red banner, warning modal, fog re-flooding, and `UnsafeChange` ledger audit verified.

### Priority 3: Edge Case & Lifecycle Scenarios

- [ ] 🔵 **TC-19:** Legacy save migration, Day-1 (60,000 tick) review window grace period, Fog of War notice, and lock confirmation verified.
- [ ] 🔵 **TC-25:** Mutable notification rule toggling and factory defaults reset isolation verified.
- [ ] 🔵 **TC-26:** First-time mod adoption lifecycle (`Dialog_GeoscapeModAdoption`), planetary surveillance default, blackout warning, Fog of War challenge, and pinned `ModAdoption` provenance record verified.

### Priority 4: In-Game & Automated Verified Passes

- [ ] 🟢 **TC-10:** Native Waylith DeepProfiler execution budget ($\le 1.0\text{ ms}$), single-pass traversal resolution, and zero-allocation hot path verified.
- [ ] 🟢 **TC-11:** Scribe save/load persistence, stuffDef material serialisation, and defensive outpost removal resilience verified.
- [ ] 🟢 **TC-16:** Cross-campaign save isolation and independent per-save gameplay rule persistence (`FogOfWar` vs `Omniscient`) verified.
- [x] 🟢 **TC-17:** Native campaign creation wizard page (`Page_GeoscapeRules`), $2 \times 2$ profile grid, Anomaly-style modal dialog (`Dialog_GeoscapeCampaignRules`), and Day-0 draft consumption verified (In-Game Pass).
- [x] 🟢 **TC-18:** Default campaign creation wizard defaults (`FogOfWar`) and pinned `CampaignCreated` Day-0 audit ledger snapshot verified (In-Game Pass).
- [ ] 🟢 **TC-21:** Mutability constraints, Immutable rule locking, and reverse visibility transition blocking (Fog of War) verified.
- [ ] 🟢 **TC-23:** Immediate unsafe override revocation and `DevOverrideDisabled` ledger logging upon turning off DevMode verified.
- [ ] 🟢 **TC-24:** Session-only ephemerality (zero XML persistence) and restart safety verified.
- [x] 🟢 **TC-28:** Uncharted globe starting site selection automated landfall paradigm, black globe bypass under Fog of War / Hardcore Vigilance, optional manual globe targeting fallback, and Step 3 vanilla compatibility verified (In-Game Pass).
- [x] 🟢 **TC-29:** Overlapping entity disambiguation menu, faction livery rendering, status details, shift-click toggle, sensory masking, and civilization-style settlement staging outside colonies (`CaravanArrivalAction_HoldPosition`) verified (In-Game Pass).
