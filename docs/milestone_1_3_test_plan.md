# Project Vigilance — Milestone 1.3 In-Game Verification & Test Plan

This test plan provides comprehensive, step-by-step procedures to validate all systems implemented across **Milestone 1.3: Radar Infrastructure, Advanced Early Warning & Inter-Faction Encounters**.

It covers multi-dish radar interferometry, dynamic power scaling, Model B+ terrain vantage and mountain masking, SIGINT decryption penetration, coastal sonar arrays, holographic 3D radar coverage globe overlays, lightweight unmanned Geoscape listening posts, right-click caravan construction and salvage, two-stage early warning non-peeking escalation, concealed dynamic camps and breadcrumb rumours in the Fog of War, $O(M)$ tile-bucketed inter-faction collisions, algebraic background skirmishes, persistent encounter aftermath sites, grounded storyteller trade caravans, Keplerian orbital mechanics with storyteller-calibrated drift, dual-layer orbital rendering and faction colour tinting, native campaign creation wizard integration (`Page_GeoscapeRules`), Waylith native `DeepProfiler` telemetry, and Scribe save/load state resilience.

---

## Test Matrix Overview

| Test Case | Feature Under Test                                                              | Expected Behaviour                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         | Status             | Primary Screen / Tool                           |
| :-------- | :------------------------------------------------------------------------------ | :--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | :----------------- | :---------------------------------------------- |
| **TC-01** | Multi-Dish Radar & Interferometry Progression                                   | • 1 Dish: $R_{detect} = 24$ rings, $R_{id} = 10$ rings, locked 250-tick sweep cadence.<br>• 2 Dishes: $R_{detect} = 24$ rings, $R_{id} = 17$ rings (locked 250-tick cadence).<br>• 3 Dishes: $R_{detect} = 24$ rings, $R_{id} = 22$ rings (locked 250-tick cadence).<br>• Dynamic power scaling: 800W standby $\rightarrow$ 1,800W active when tracking targets.<br>• Redundancy: cutting power to any dish gracefully falls back to the lower tier.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       | **Ready for Test** | Colony Map / Power Grid / World Map             |
| **TC-02** | Model B+ Vantage & Mountain Masking                                             | • Origin Vantage: Lookouts gain $+1$ to $+3$ rings on hills/mountains; high-tech radars gain $+1$ to $+2$ rings to $R_{id}$.<br>• Target Terrain Masking: Incursions in `Mountainous` hexes remain held at Contact tier (amber blip) even within $R_{id}$.<br>• Terrain Exit: Moving into open plains immediately elevates to Classified/Analysed.<br>• SIGINT Console: Linked facility penetrates terrain masking and unlocks Tier 4 Analysed telemetry.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  | **Ready for Test** | World Map / Inspector / Dev Console             |
| **TC-03** | Oceanic Hydrophone Sonar Station                                                | • Coastal sonar array (`Geoscape_SonarArray`) restricts acoustic tracking strictly to water-covered hexes (`tile.WaterCovered`).<br>• Adjacent overland hexes within the 18-ring envelope remain completely unmonitored.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   | **Ready for Test** | Colony Map / World Map / Coastal Tile           |
| **TC-04** | Tactical Radar Overlay & Globe UX                                               | • Native 3D mesh overlay renders glowing perimeter boundary rings and hexagonal holographic honeycomb grid on the globe.<br>• Configurable display modes: `Boundary & Grid`, `Boundary Only`, `None`.<br>• 5 color palettes: Tactical Cyan, Surveillance Amber, Emerald Phosphor, Crimson Alert, Ghost White.<br>• Inward distance fade and dynamic sensor movement updates.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               | **Ready for Test** | World Map / Mod Settings                        |
| **TC-05** | Lightweight Geoscape Outposts & 4X Context Actions                              | • **4X Colony Dispatch:** Right-clicking explored passable hex with Colony selected opens contextual dispatch menu (`Form caravan`, `Dispatch Construction Expedition: Automated Relay / Signal Beacon`).<br>• **Automated Supply Pre-Allocation:** Pre-selects exact build materials in `Dialog_FormCaravan` item list and locks destination route.<br>• **Distant Caravan Orders:** Right-clicking distant hex with caravan selected offers move and queued travel + construct.<br>• **Material Tracking:** Tracks consumed material (`stuffDef`), updating world label (e.g. `Granite blocks Signal Beacon`) and inspect string.<br>• **Zero-TPS execution:** 100% unmanned, no `WorldPawns`, no map generated.<br>• **Incursion Sabotage & Salvage:** Hostile traversal sabotages sensor; caravan repair (25% cost) and dismantle (75% salvage partitioned into valid stacks respecting `stackLimit`, e.g. 75 + 37 items, preventing infinite wood transmutation).<br>• **Upright Icon Consistency:** Outpost and sensor icons remain upright and camera-aligned regardless of zoom level or latitude. | **Ready for Test** | World Map / Colony & Caravan Float Menu         |
| **TC-06** | Two-Stage Early Warning & Non-Peeking Escalation                                | • Stage 1 ($R_{detect}$ boundary crossing): Amber alert letter (`ThreatSmall`), pulsing amber blip, masked faction, concealed threat points, hidden path.<br>• Stage 2 ($R_{id}$ boundary crossing): Red alert letter (`ThreatBig` hostile / `PositiveEvent` friendly), faction crest, known destination path.<br>• Direct high-spec entry: Incursions spawning within $R_{id}$ trigger Stage 2 directly without replaying Stage 1.<br>• Classification preservation: Re-entering mountainous masking does not regress known intelligence.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | **Ready for Test** | World Map / Letter Stack / Inspector            |
| **TC-07** | Haze Reconnaissance, Dynamic Camps & Rumours                                    | • Dynamic quest sites and bandit camps in unmonitored Haze sectors have announcement letters suppressed.<br>• Quest timeout clocks (`TimeoutComp`) are frozen while concealed so quests do not expire invisibly.<br>• **Detection-Layer Site Masking:** Non-player settlements, outposts, camps, and sites emerging from the black shroud into the outer Detection ring ($R_{detect}$) are masked as "Unidentified site" contacts with gold mystery icons, concealed factions, and suppressed interaction gizmos until brought within the inner Identification ring ($R_{id}$) or visited.<br>• Uncovering site via radar or scout caravan triggers deferred discovery letter and resumes timeout clock.<br>• Friendly caravan arrival dispatches Breadcrumb Rumour letter with 8-cardinal compass heading, uncovering target hex.<br>• Expired / Despawned Site Auto-Purge: Despawned or expired dynamic sites are automatically intercepted by Harmony patch on `WorldObject.PostRemove` and purged on the next sweep pass, preventing unbounded save game bloat.                                        | **Ready for Test** | World Map / Letter Stack / Quest Tab            |
| **TC-08** | Inter-Faction Collisions & Background Skirmishes                                | • $O(M)$ tile-bucketed collision detection on `TickRare` (250 ticks).<br>• Hostile vs hostile/neutral collisions resolve purely algebraically without loading a tactical map.<br>• Clashes dispatch notification letter and spawn persistent `WorldObject_EncounterAftermath` at clash hex.<br>• Caravan arrival action `Scavenge Aftermath` cleanly transfers trade cargo and survivor pawns.<br>• 15,000-tick skirmish cooldown prevents repeat collision churn.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         | **Ready for Test** | World Map / Letter Stack / Caravan Action       |
| **TC-09** | Grounded Storyteller Caravan Encounters                                         | • Storyteller caravan meetings and extortion incidents are grounded into physical `WorldObject_Incursion` entities marching on contiguous multi-leg routes (`origin -> current -> destination`).<br>• **Faction & Dialogue Consistency:** Robust non-null faction resolution prevents `Tried to generate pawn kinds with null faction` errors; explicit group kind synchronization ensures friendly merchants/travellers never generate contradictory "hostile warband" titles.<br>• Extortion demand dialog delays item deduction until player explicitly confirms acceptance.<br>• Caravans continue overland march across the globe post-encounter.                                                                                                                                                                                                                                                                                                                                                                                                                                                     | **Ready for Test** | World Map / Dialog / Dev Incident               |
| **TC-10** | Waylith DeepProfiler & Zero-Allocation Verification                             | • Dev Mode DeepProfiler reveals native `ProjectVigilance.PerformSensorySweep`, `ProjectVigilance.SensorySweep.VisionUpdates`, `ProjectVigilance.InterFactionCollisions`, and `ProjectVigilance.SkirmishResolution` markers.<br>• Single-Pass Traversal: Unified `TryGetDetectionDistance(target, out int distance)` eliminates redundant graph traversals per sensor/target pair on the sweep hot path.<br>• Verification of $\le 1.0\text{ ms}$ execution budget and 0-byte GC allocation during steady-state `TickRare`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | **Ready for Test** | Dev Actions Menu / Deep Profiler                |
| **TC-11** | Scribe Save/Load & Outpost Removal Resilience                                   | • Saving and loading with active radars, outposts, incursions, aftermath sites, and concealed haze sites produces 0 Scribe errors and 0 duplicate IDs.<br>• Deleting an outpost via dev tools or external mod leaves no dangling references; next sweep clears entry cleanly.<br>• Outpost `stuffDef` is serialized via `Scribe_Defs.Look`, maintaining construction material fidelity across saves.<br>• Expired or despawned dynamic sites leave zero dangling `ConcealedSiteRecord` entries upon save/load.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             | **Ready for Test** | Save/Load Menu / Debug Log                      |
| **TC-12** | Optimistic Fog-of-War Travel Routing & Unknown Hex Assumption                   | • Caravan right-click travel directives assume unexplored black hexes are traversable without leaking impassability information ahead of time.<br>• Upon entering a hex adjacent to impassable terrain, the route dynamically recalculates locally around the obstacle without revealing distant black tiles.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              | **Ready for Test** | World Map / Caravan Right-Click Orders          |
| **TC-13** | Drifting Orbital Mechanics & Non-Repeating Ground Tracks                        | • Satellites orbit along Keplerian paths with nodal precession ($\dot{\Omega}$) and storyteller-calibrated periods: Trade satellites (62,500 ticks / ~25.0 in-game hrs), Recon satellites (45,000 ticks / 18.0 in-game hrs), Military satellites (75,000 ticks / 30.0 hrs), Navigation satellites (120,000 ticks), Communication satellites (180,000 ticks).<br>• Ground tracks shift westward across successive orbits.<br>• Incommensurate periods ($T_{\text{orbit}} \nmid 60{,}000$) ensure sub-satellite position after 1 day (60,000 ticks) is distinctly displaced, avoiding artificial diurnal repeating clocks.<br>• Legacy save migration automatically upgrades pre-existing orbital periods below 30,000 ticks to the calibrated minimum threshold.                                                                                                                                                                                                                                                                                                                                            | **Ready for Test** | World Map / Orbit Telemetry / Inspector         |
| **TC-14** | Persistent Orbital Stations & Constellation Service Zones                       | • Persistent orbital station hub (`WorldObject_OrbitalStation`) operates in stable high orbit for orbital factions (`TradersGuild`).<br>• Satellites entering the $35^\circ$ service corridor transition to `In Service Area`, syncing telemetry and restock inventory.<br>• Lost or destroyed satellites queue automated replenishment countdown, spawning replacements after fabrication.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                | **Ready for Test** | World Map / Station Inspector                   |
| **TC-15** | Satellite Mission Roles (Trade & Navigation Boost)                              | • **Trade Satellite:** Storyteller-calibrated orbital passes maintain an overhead communications window of ~3.8 in-game hours (~160s at 1x, ~27s at 3x) per pass; natural revisit cadence of once every 8–12 days across 2–3 active orbital trade vessels matches Cassandra Classic and Phoebe Chillax frequency; opens trade comms dialog with drop pod delivery.<br>• **Navigation Satellite:** Caravans under the satellite's 24-tile footprint receive 15% overland speed boost (0.85x ticks per move) with inspect string telemetry feedback.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         | **Ready for Test** | Colony Comms Console / Caravan Inspector        |
| **TC-16** | Cross-Campaign Save Isolation & Pinned Rules                                    | • Campaign A created under `FogOfWar`. Campaign B created under `Omniscient`.<br>• Reloading Campaign A restores `FogOfWar`; reloading Campaign B restores `Omniscient`.<br>• Global `ModSettings` defaults are untouched by save loads and do not cross-contaminate active saves.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         | **Ready for Test** | World Map / Mod Settings / Save System          |
| **TC-17** | Native Campaign Creation Wizard Page (`Page_GeoscapeRules`) & Profile Selection | • Stitched seamlessly into the campaign creation sequence between Storyteller selection (`Page_SelectStoryteller`) and World Parameters (`Page_CreateWorldParams`) via `PageUtility_StitchedPages_Patch`.<br>• $2 \times 2$ profile card selection grid: **Fog of War** (Recommended default), **Planetary Surveillance**, **Hardcore Vigilance**, and **Omniscient**.<br>• Dedicated Anomaly-style modal window (`Dialog_GeoscapeCampaignRules`) launched via `Customise Individual Rules...` preserves full-height uncompressed profile cards, eliminates text truncation, and enables fine-grained rule tuning with live custom profile detection upon close.<br>• Standard Next/Back navigation links correctly forward to world generation and back to storyteller selection; consumes draft and commits Day-0 `CampaignCreated` ledger snapshot upon world generation.                                                                                                                                                                                                                               | **Ready for Test** | Campaign Creation Wizard (`Page_GeoscapeRules`) |
| **TC-18** | Default Campaign Creation & Day-0 Snapshot                                      | • Generating world without altering `Page_GeoscapeRules` adopts factory defaults (`FogOfWar`).<br>• Pins initial `CampaignCreated` Day-0 snapshot record in ledger with full rule state.<br>• Transient draft remains cleared.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             | **Ready for Test** | World Generation / Audit Ledger                 |
| **TC-19** | Legacy Save Migration & Day-1 Review Grace Period                               | • Loading unversioned pre-1.3 save detects `schemaVersion == 0`.<br>• Adopts current `ModSettings` as baseline, sets 60,000-tick review window, and pins `LegacyMigration` record in ledger.<br>• Dispatches non-blocking explanation letter warning player of review period (including Fog of War exploration data notice if applicable).<br>• Review banner in Mod Settings displays remaining time and `[Confirm & Lock In Campaign Rules Now]` button.<br>• Confirming or window expiration locks immutable rules and records `ReviewWindowLocked`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    | **Ready for Test** | Legacy Save Load / Letter Stack                 |
| **TC-20** | Monotonic Visibility Escalation & Side-Effects Engine                           | • In-game escalation from `FogOfWar` $\rightarrow$ `PlanetarySurveillance` or `Omniscient` prompts permanent escalation confirmation dialog.<br>• Executing transition invokes `RuleTransitionExecutor`: releases all concealed dynamic sites and delivers deferred quest/announcement letters, invalidates sensory caches and sweeps vision, repaths active player caravans around discovered terrain, and rebuilds 3D GPU world meshes and feature labels.<br>• Ledger records `Escalation` transition with from/to labels.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              | **Ready for Test** | Mod Settings / World Map / Caravans             |
| **TC-21** | Mutability Enforcement & Immutable Rule Locking                                 | • Once campaign starts and review window closes, all 7 Immutable rules (`colonyScoutRadius`, `caravanScoutRadius`, `enableNighttimeSensoryReduction`, `incursionsRestAtNight`, `synchronizeMultiVectorRaids`, `enableTacticalIngressFlanking`, `resetThreatCooldownOnArrival`) are greyed out with `(Locked)` suffix.<br>• Reverse visibility transitions (e.g. `PlanetarySurveillance` $\rightarrow$ `FogOfWar`) are greyed out with `(Locked - Cannot reverse)` suffix.<br>• Sliders and checkboxes reject interaction when DevMode is disabled; 0 changes recorded to ledger.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           | **Ready for Test** | Mod Settings / Incursions & Sensory Tabs        |
| **TC-22** | Developer-Mode Session Unsafe Override & Audit Trail                            | • With `Prefs.DevMode` active, About & Diagnostics tab exposes `Allow Unsafe Rule Transitions (Session Only)` checkbox.<br>• Enabling override unlocks immutable rules and reverse visibility transitions, displaying red warning banner and `(Unsafe)` badge.<br>• Executing unsafe transition prompts destructive warning modal; upon confirmation, forces transition and records `UnsafeChange` in ledger.<br>• Downgrading to `FogOfWar` triggers sensor radius sweep to re-flood fog of war around active lookouts.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   | **Ready for Test** | Mod Settings / About Tab / Dev Mode             |
| **TC-23** | Automatic Override Invalidation on DevMode Toggle                               | • Toggling `Prefs.DevMode` off in Options immediately deactivates `UnsafeRuleOverride.Requested` and sets `Effective` to false.<br>• Active campaign ledger records `DevOverrideDisabled` with note `Developer mode switched off`.<br>• Mutability constraints immediately re-engage in Mod Settings.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      | **Ready for Test** | Options / Mod Settings / Audit Ledger           |
| **TC-24** | Override Session Ephemerality Across Loads & Restarts                           | • Enabling unsafe override and saving game does not persist override flag into save XML.<br>• Loading save while DevMode is active logs `DevOverrideEnabled` (`Override active when campaign was loaded`).<br>• Restarting RimWorld executable launches with override disabled by default, preventing accidental unintended rule mutations.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                | **Ready for Test** | Save/Load Menu / Game Restart                   |
| **TC-25** | Mutable Notification Preferences & ModSettings Decoupling                       | • In-game toggling of `EarlyWarningLetters` (`enableEarlyWarningLetters`) takes effect immediately without confirmation modal or ledger entry.<br>• In-game reset button (`Reset New Campaign Defaults to Factory Settings`) resets global preferences and factory defaults for future games, leaving active campaign rules and ledger completely untouched.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               | **Ready for Test** | Mod Settings / Early Warning Tab                |
| **TC-26** | First-Time Project Vigilance Adoption & Save Provenance                         | • Loading established save never exposed to Project Vigilance (`schemaVersion == 0` and foundational components absent from save XML) triggers `DetermineEntryPath` $\rightarrow$ `ModAdoption`.<br>• Game pauses on load and presents `Dialog_GeoscapeModAdoption` modal.<br>• Choosing `Planetary Surveillance` adopts rules preserving charted geography, opens 1-day review window, and pins Day-0 `ModAdoption` origin snapshot in ledger.<br>• Choosing `Fog of War ("The Great Blackout")` displays explicit warning note that existing geographic knowledge will be lost, resets shroud around lookouts, and triggers optimistic caravan route recalculation.<br>• Dismissing modal via `Esc` or `Close (X)` defaults to `Planetary Surveillance` with active 1-day grace period.                                                                                                                                                                                                                                                                                                                  | **Ready for Test** | Mod Adoption Modal / Audit Ledger               |
| **TC-27** | Dual-Layer Orbital Rendering & Faction Colour Tinting                           | • Satellites and orbital stations tagged with `<fullyExpandedInSpace>true</fullyExpandedInSpace>` render in both Surface and Odyssey Orbit layers when detected or revealed.<br>• Unidentified or unmonitored orbital objects remain shrouded outside active sensor horizons.<br>• 2D expanding icons and 3D mesh quads dynamically inherit faction livery colours (`Faction.Color`, e.g. Voidborn pink, Pirate crimson, Player emerald green) for immediate tactical identification.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      | **Ready for Test** | World Map (Surface & Orbit Layers) / Inspector  |
| **TC-28** | Uncharted Globe Starting Site Selection & Blind Touchdown                       | • Ambiguous Rejection: Settle attempts on unexplored ocean or impassable mountains under Fog of War display `"Unviable landing coordinates: Selected sector cannot support a colony drop."` rather than explicit terrain leaks.<br>• Blind Touchdown: Confirmation dialog prompts for toxic pollution (Biotech), extreme climate, and harsh biomes are suppressed under Fog of War, landing blind.<br>• Faction Proximity Masking: Proximity diplomatic warnings are suppressed so hidden bases are not revealed.<br>• Inspect Shroud: Tile inspection displays `"Uncharted Sector"` with geology/biome tabs hidden.<br>• Surveillance/Omniscient profiles retain full informed vanilla confirmations. | **Ready for Test** | Starting Site Selection (`Page_SelectStartingSite`) |

---

## Prerequisites & Test Environment Setup

1. **Deploy Latest Build to RimWorld 1.7:**
   - Execute [`scripts/deploy-geoscape.ps1`](file:///d:/Repos/GitHub/Project-Vigilance/scripts/deploy-geoscape.ps1) to compile `Release` binary and ensure the NTFS junction is linked to `RimWorld/Mods/ProjectVigilance-Geoscape`.
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

### Test 1: Multi-Dish Radar Construction & Interferometry Stacking (`TC-01`)

_Objective: Validate multi-dish interferometry progression, locked 250-tick sweep cadence, dynamic power scaling, and fail-safe redundancy._

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

---

### Test 2: Model B+ Vantage & Mountain Masking (`TC-02`)

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

### Test 3: Oceanic Hydrophone Sonar Station (`TC-03`)

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

### Test 4: Tactical Radar Coverage Overlay & Globe UX (`TC-04`)

_Objective: Validate 3D native mesh rendering, boundary rings, holographic honeycomb grid, and user display settings._

1. Open the World Map (`Tab`) with active radar installations at your colony.
2. Observe the globe visual overlay:
   - **Verify Boundary Ring:** A glowing circular border encircles the maximum detection boundary (24 rings).
   - **Verify Holographic Honeycomb:** Inside the perimeter, a subtle hexagonal grid mesh is visible with an inward distance fade (glowing brightly at edge, fading toward center).
3. Open `Options → Mod Settings → Project Vigilance: Geoscape`:
4. **Test Coverage Display Styles:**
   - Change style to `Boundary Only`: Verify hexagonal honeycomb grid disappears, leaving only the outer glowing perimeter ring.
   - Change style to `None`: Verify all radar overlays are completely hidden.
   - Change style to `Boundary & Grid`: Verify both the glowing boundary and honeycomb mesh reappear.
5. **Test Color Palettes:**
   - Switch palette through each preset:
     - `Tactical Cyan` (Default high-tech sci-fi blue)
     - `Surveillance Amber` (Retro CRT amber)
     - `Emerald Phosphor` (Classic military radar green)
     - `Crimson Alert` (Urgent combat red)
     - `Ghost White` (Clean monochrome)
   - **Verify:** Globe shader immediately updates in real-time without requiring game reload or map restart.
6. **Test Opacity & Background Dimming:**
   - Adjust the opacity slider from 10% to 100%: Verify smooth visual scaling.
   - Toggle `Hybrid Background Dimming`: Verify unmonitored world tiles slightly dim when viewing active radar envelopes.

---

### Test 5: Lightweight Geoscape Outposts & Context Actions (`TC-05`)

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
   - **Verify Automated Pre-allocation:** In the Items tab, exactly 100 Steel and 4 Components are automatically pre-selected.
   - **Verify Notification:** A confirmation message indicates supplies have been pre-allocated.
4. Select 1 colonist and sufficient travel provisions, then click **Accept**:
   - Once the colonists exit the map boundary, inspect the caravan on the world map.
   - **Verify Caravan Status:** Status displays `travelling to establish automated listening post`.
   - Fast-forward time until the caravan reaches the destination hex.
   - **Verify Autonomous Deployment & Timed Construction:** Upon stepping onto the hex, the caravan consumes the 100 Steel and 4 Components, placing the **Automated Listening Post** in an `Under Construction` state.
   - **Verify Construction Progression:** Inspecting the outpost displays `Status: Under Construction (X%, ~X.Xh remaining)`. Time advances as the caravan works on-site (~1 game hour / 2,500 ticks).
   - If the caravan moves away, verify the status switches to `Status: Construction Paused`. When the caravan returns, construction resumes.
   - Upon completion, a positive alert sounds, status switches to `Operational`, and the 12-ring sensory envelope activates on the Geoscape!
   - **Verify Faction Tinting & Upright Orientation:** The outpost symbol renders upright on the world sphere and is tinted with the player faction's color.

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
2. Ensure the caravan carries repair materials (at least 25 Steel and 1 Component for relays, or 38 Wood/Stone for beacons).
3. Right-click the sabotaged outpost:
   - **Verify Context Option:** `Repair Sabotaged Station (25 Steel, 1 Component)` or matching beacon repair cost.
4. Click `Repair Sabotaged Station`:
   - **Verify:** Repair materials are deducted from caravan inventory.
   - **Verify:** Outpost status restores to `Operational`.
   - **Verify:** Sensory envelope reappears on the world map immediately.
5. **Dismantle Dispatch from Colony Settlement:**
   - Select your Colony Settlement on the Geoscape globe.
   - Right-click an existing listening post or signal beacon:
     - **Verify Context Option:** `Dispatch Caravan to Dismantle [Outpost Name] (Recover 75% Materials)`.
   - Select option: `Dialog_FormCaravan` opens with route waypoint locked to the outpost. Upon departure, caravan paths with status `travelling to dismantle [outpost name]`.
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

---

### Test 6: Two-Stage Early Warning & Non-Peeking Escalation (`TC-06`)

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

### Test 7: Haze Reconnaissance, Dynamic Camps & Traveller Rumours (`TC-07`)

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

### Test 8: Inter-Faction Collisions, Background Skirmishes & Persistent Aftermath (`TC-08`)

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

### Test 9: Grounded Storyteller Caravan Encounters (`TC-09`)

_Objective: Validate grounded storyteller caravan meetings, persistent multi-leg routing, and demand extortion item safety._

#### 9.1 Grounded Caravan Meeting Incident

1. Form a small player caravan and march it onto the world map.
2. Trigger the storyteller caravan meeting incident via Dev Mode:
   - `Debug Actions Menu → Incidents → Execute Incident... → CaravanMeeting`.
3. When the meeting dialog opens:
   - **Verify Physical Entity:** Switch to the World Map (or check behind dialog): verify the encountered caravan is a physical, persistent `WorldObject_Incursion` entity present on the hex.
   - **Verify Contiguous Pathing:** Inspect the encountered incursion's path: verify it has a contiguous 3-leg route (`origin settlement → meeting tile → destination settlement`).
4. Interact with the caravan (Trade or Move On):
   - **Verify Continued Transit:** After closing the dialog, the NPC caravan continues marching along its assigned route across the globe rather than despawning into thin air.

#### 9.2 Extortion Demand Safety

1. With your player caravan on the world map carrying valuable goods, trigger:
   - `Debug Actions Menu → Incidents → Execute Incident... → CaravanDemand`.
2. An extortion dialog opens demanding specific goods (e.g. silver or medicine).
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

### Test 10: Waylith DeepProfiler & Zero-Allocation Verification (`TC-10`)

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

### Test 11: Scribe Save / Load & Outpost Removal Resilience (`TC-11`)

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

### Test 12: Optimistic Fog-of-War Travel Routing & Unknown Hex Assumption (`TC-12`)

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

### Test 13: Drifting Orbital Mechanics & Non-Repeating Ground Tracks (`TC-13`)

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

### Test 14: Persistent Orbital Stations & Constellation Service Zones (`TC-14`)

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

### Test 15: Satellite Mission Roles (Trade & Navigation Boost) (`TC-15`)

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

### Test 16: Cross-Campaign Save Isolation & Pinned Rules (`TC-16`)

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

### Test 17: Native Campaign Creation Wizard Page & Profile Selection (`TC-17`)

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

### Test 18: Default Campaign Creation & Day-0 Snapshot (`TC-18`)

_Objective: Validate campaign creation when wizard defaults are accepted without modification._

1. From the Main Menu, initiate a new game and proceed through `Page_GeoscapeRules` without altering any cards or settings.
2. Embark onto the colony map.
3. Open `Options → Mod Settings → Project Vigilance: Geoscape`:
   - **Verify Visibility:** Operates under `FogOfWar` (Recommended default).
   - **Verify Lookout Horizons:** Colony horizon is 5.0 hexes; Caravan lookout is 3.0 hexes.
4. Switch to `About & Diagnostics` tab:
   - **Verify Active Profile:** Displays `Fog of War (Recommended)`.
   - **Verify Ledger Origin:** Day-0 pinned record displays:
     > `CampaignCreated | Defaults | visibilityMode=FogOfWar ...`

---

### Test 19: Legacy Save Migration & Day-1 Review Grace Period (`TC-19`)

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

### Test 20: Monotonic Visibility Escalation & Side-Effects Engine (`TC-20`)

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

### Test 21: Mutability Enforcement & Immutable Rule Locking (`TC-21`)

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

### Test 22: Developer-Mode Session Unsafe Override & Audit Trail (`TC-22`)

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

### Test 23: Automatic Override Invalidation on DevMode Toggle (`TC-23`)

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

### Test 24: Override Session Ephemerality Across Loads & Restarts (`TC-24`)

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

### Test 25: Mutable Notification Preferences & ModSettings Decoupling (`TC-25`)

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

### Test 26: First-Time Project Vigilance Adoption (`TC-26`)

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

### Test 27: Dual-Layer Orbital Rendering & Faction Colour Tinting (`TC-27`)

_Objective: Validate seamless cross-layer satellite and station rendering between planetary surface and Odyssey orbit views, visibility mask adherence, and dynamic faction livery tinting._

#### 27.1 Dual-Layer Visibility (Surface & Orbit Layers)

1. With active satellites and stations in orbit, switch to the World Map (`Tab`).
2. Toggle between the planetary surface view and Odyssey orbit shell (using the `View orbit` / `View planet` toggle button or zooming between orbital and planetary scales):
   - **Verify Surface Layer:** Satellites and orbital stations tagged with `<fullyExpandedInSpace>true</fullyExpandedInSpace>` render clearly as expanding icons with their 3D quads visible over the globe.
   - **Verify Orbit Layer:** Switching to `View orbit` renders satellites and stations smoothly along their 3D orbital trajectories around the planetary sphere.
   - **Verify Sensor Masking Adherence:** Satellites and orbital objects traversing unmonitored sectors in Fog of War remain masked outside active radar/lookout detection ranges, preventing cross-layer intelligence leaks.

#### 27.2 Dynamic Faction Livery Colour Tinting

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

---

### Test 28: Uncharted Globe Starting Site Selection & Blind Touchdown (`TC-28`)

_Objective: Validate that starting site selection on an uncharted globe under Fog of War does not leak unearned intelligence via explicit terrain rejections, hazard dialog prompts, proximity goodwill penalties, or inspect panels._

#### 28.1 Ambiguous Landing Rejection on Physically Impossible Tiles

1. Start a new campaign on `Fog of War` (`VisibilityMode.FogOfWar`).
2. Proceed past Storyteller and Campaign Rules wizard to `Page_SelectStartingSite`.
3. Locate an unexplored/fogged ocean or deep water hex in the black shroud.
4. Attempt to click `Next` or settle on the hex:
   - **Verify Rejection Message:** Instead of vanilla _"Cannot land on ocean"_, verify an ambiguous rejection message is displayed:
     > _"Unviable landing coordinates: Selected sector cannot support a colony drop."_
5. Locate an unexplored impassable mountain hex in the black shroud.
6. Attempt to settle on the hex:
   - **Verify Rejection Message:** Displays the exact same ambiguous telemetry message without disclosing mountain terrain or impassability.
7. Switch profile to `Planetary Surveillance` or `Omniscient`:
   - **Verify Vanilla Behaviour:** Attempting to settle on ocean or impassable mountains displays standard explicit vanilla messages (_"Cannot land on ocean."_ / _"Cannot land on impassable mountains."_).

#### 28.2 Blind Touchdown on Environmental Hazard Sectors

1. Under `Fog of War`, select an unexplored sector that contains environmental hazards (e.g. toxic pollution from Biotech DLC, extreme desert, ice sheet, or high disease frequency).
2. Click `Next`:
   - **Verify Hazard Suppression:** Verify no confirmation dialog (e.g. _"This tile is X% polluted..."_ or extreme temperature warning) pops up. Settle proceeds directly to starting colonist configuration.
   - **Verify Proximity Goodwill Suppression:** If settling adjacent to a hidden faction base in the shroud, verify no diplomatic penalty warning (_"-15 goodwill with..."_) leaks the presence or identity of the faction.
3. Embark and start the game:
   - **Verify Blind Touchdown:** The colony drops onto the map, and colonists only discover the pollution, extreme climate, or nearby neighbours upon touchdown.
4. Repeat under `Planetary Surveillance`:
   - **Verify Informed Confirmation:** Selecting a polluted or hazardous hex prompts the standard vanilla confirmation dialog before embarking.

#### 28.3 Inspect Panel Masking during Starting Site Selection

1. Under `Fog of War`, click various unexplored tiles on `Page_SelectStartingSite`:
   - **Verify Inspect Panel:** The bottom-left inspect panel masks biome name, elevation, rainfall, stone types, and pollution, displaying generic uncharted telemetry (`"Uncharted Sector"`).
   - **Verify Terrain Tabs:** `WITab_Terrain` and `WITab_Planet` hide detailed geological and environmental survey data.

---

## In-Game Verification Sign-Off Checklist

- [ ] **TC-01:** Multi-dish radar interferometry progression ($10 \rightarrow 17 \rightarrow 22$ rings), locked 250-tick cadence, and dynamic power scaling verified.
- [ ] **TC-02:** Model B+ origin vantage bonuses, target mountain masking, and SIGINT penetration verified.
- [ ] **TC-03:** Coastal hydrophone sonar array aquatic hex restriction verified.
- [ ] **TC-04:** 3D holographic honeycomb mesh and boundary rings verified across display styles and color palettes.
- [ ] **TC-05:** 4X Colony right-click expedition dispatch, automated supply pre-allocation, zero-TPS listening post deployment, stuffDef material tracking, stack-partitioned dismantle salvage (75 + 37 items), hostile sabotage, and repair verified.
- [ ] **TC-06:** Two-stage early warning non-peeking escalation and direct entry bypass verified.
- [ ] **TC-07:** Concealed dynamic camps in Fog of War, quest timeout freeze, breadcrumb rumours, and PostRemove despawn auto-purge verified.
- [ ] **TC-08:** $O(M)$ tile-bucketed collisions, algebraic background skirmishes, and aftermath scavenging verified.
- [ ] **TC-09:** Grounded storyteller caravan encounters, faction/dialogue consistency (no null faction errors or friendly hostile warbands), and extortion demand item safety verified.
- [ ] **TC-10:** Native Waylith DeepProfiler execution budget ($\le 1.0\text{ ms}$), single-pass traversal resolution, and zero-allocation hot path verified.
- [ ] **TC-11:** Scribe save/load persistence, stuffDef material serialization, and defensive outpost removal resilience verified.
- [ ] **TC-12:** Optimistic fog-of-war travel routing, unmapped obstacle dynamic route recalculation, and dead-end halting verified.
- [ ] **TC-13:** Drifting orbital satellite Keplerian mechanics, calibrated orbital periods (62,500-tick trade, 45,000-tick recon), westward ground track precession, and legacy migration verified.
- [ ] **TC-14:** Persistent high-orbit station hubs, $35^\circ$ service cone synchronization, and automated replenishment fabrication verified.
- [ ] **TC-15:** Trade satellite comms console interaction, ~3.8 hr overpass window (~160s at 1x), 8–12 day revisit cadence, and navigation satellite overland velocity boosts (+15%) verified.
- [ ] **TC-16:** Cross-campaign save isolation and independent per-save gameplay rule persistence (`FogOfWar` vs `Omniscient`) verified.
- [ ] **TC-17:** Native campaign creation wizard page (`Page_GeoscapeRules`), $2 \times 2$ profile grid, Anomaly-style modal dialog (`Dialog_GeoscapeCampaignRules`), and Day-0 draft consumption verified.
- [ ] **TC-18:** Wizard default campaign creation (`FogOfWar`) and pinned `CampaignCreated` ledger snapshot verified.
- [ ] **TC-19:** Legacy save migration, Day-1 (60,000 tick) review window grace period, Fog of War notice, and lock confirmation verified.
- [ ] **TC-20:** Monotonic visibility escalation from Fog of War, confirmation modal, site release, sensor rescan, and caravan repath side-effects verified.
- [ ] **TC-21:** Mutability constraints, Immutable rule locking, and reverse visibility transition blocking (Fog of War) verified.
- [ ] **TC-22:** Developer Mode session unsafe override, red banner, warning modal, fog re-flooding, and `UnsafeChange` ledger audit verified.
- [ ] **TC-23:** Immediate unsafe override revocation and `DevOverrideDisabled` ledger logging upon turning off DevMode verified.
- [ ] **TC-24:** Session-only ephemerality (zero XML persistence) and restart safety verified.
- [ ] **TC-25:** Mutable notification rule toggling and factory defaults reset isolation verified.
- [ ] **TC-26:** First-time mod adoption lifecycle (`Dialog_GeoscapeModAdoption`), planetary surveillance default, blackout warning, Fog of War challenge, and pinned `ModAdoption` provenance record verified.
- [ ] **TC-27:** Dual-layer orbital rendering across Surface and Orbit layers (`<fullyExpandedInSpace>`), shroud masking adherence, and dynamic faction livery colour tinting verified.
- [ ] **TC-28:** Uncharted globe starting site selection feedback, ambiguous rejection for impossible sectors, hazard confirmation dialog suppression (blind touchdown), and proximity goodwill masking verified under Fog of War.
