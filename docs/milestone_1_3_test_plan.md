# Project Vigilance — Milestone 1.3 In-Game Verification & Test Plan

This test plan provides comprehensive, step-by-step procedures to validate all systems implemented across **Milestone 1.3: Radar Infrastructure, Advanced Early Warning & Inter-Faction Encounters**.

It covers multi-dish radar interferometry, dynamic power scaling, Model B+ terrain vantage and mountain masking, SIGINT decryption penetration, coastal sonar arrays, holographic 3D radar coverage globe overlays, lightweight unmanned Geoscape listening posts, right-click caravan construction and salvage, two-stage early warning non-peeking escalation, concealed dynamic camps and breadcrumb rumours in the Haze of War, $O(M)$ tile-bucketed inter-faction collisions, algebraic background skirmishes, persistent encounter aftermath sites, grounded storyteller trade caravans, Waylith native `DeepProfiler` telemetry, and Scribe save/load state resilience.

---

## Test Matrix Overview

| Test Case | Feature Under Test                                  | Expected Behaviour                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     | Status             | Primary Screen / Tool                     |
| :-------- | :-------------------------------------------------- | :--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | :----------------- | :---------------------------------------- |
| **TC-01** | Multi-Dish Radar & Interferometry Progression       | • 1 Dish: $R_{detect} = 24$ rings, $R_{id} = 10$ rings, locked 250-tick sweep cadence.<br>• 2 Dishes: $R_{detect} = 24$ rings, $R_{id} = 17$ rings (locked 250-tick cadence).<br>• 3 Dishes: $R_{detect} = 24$ rings, $R_{id} = 22$ rings (locked 250-tick cadence).<br>• Dynamic power scaling: 800W standby $\rightarrow$ 1,800W active when tracking targets.<br>• Redundancy: cutting power to any dish gracefully falls back to the lower tier.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   | **Ready for Test** | Colony Map / Power Grid / World Map       |
| **TC-02** | Model B+ Vantage & Mountain Masking                 | • Origin Vantage: Lookouts gain $+1$ to $+3$ rings on hills/mountains; high-tech radars gain $+1$ to $+2$ rings to $R_{id}$.<br>• Target Terrain Masking: Incursions in `Mountainous` hexes remain held at Contact tier (amber blip) even within $R_{id}$.<br>• Terrain Exit: Moving into open plains immediately elevates to Classified/Analysed.<br>• SIGINT Console: Linked facility penetrates terrain masking and unlocks Tier 4 Analysed telemetry.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              | **Ready for Test** | World Map / Inspector / Dev Console       |
| **TC-03** | Oceanic Hydrophone Sonar Station                    | • Coastal sonar array (`Geoscape_SonarArray`) restricts acoustic tracking strictly to water-covered hexes (`tile.WaterCovered`).<br>• Adjacent overland hexes within the 18-ring envelope remain completely unmonitored.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               | **Ready for Test** | Colony Map / World Map / Coastal Tile     |
| **TC-04** | Tactical Radar Overlay & Globe UX                   | • Native 3D mesh overlay renders glowing perimeter boundary rings and hexagonal holographic honeycomb grid on the globe.<br>• Configurable display modes: `Boundary & Grid`, `Boundary Only`, `None`.<br>• 5 color palettes: Tactical Cyan, Surveillance Amber, Emerald Phosphor, Crimson Alert, Ghost White.<br>• Inward distance fade and dynamic sensor movement updates.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           | **Ready for Test** | World Map / Mod Settings                  |
| **TC-05** | Lightweight Geoscape Outposts & 4X Context Actions  | • **4X Colony Dispatch:** Right-clicking explored passable hex with Colony selected opens contextual dispatch menu (`Form caravan`, `Dispatch Construction Expedition: Automated Relay / Signal Beacon`).<br>• **Automated Supply Pre-Allocation:** Pre-selects exact build materials in `Dialog_FormCaravan` item list and locks destination route.<br>• **Distant Caravan Orders:** Right-clicking distant hex with caravan selected offers move and queued travel + construct.<br>• **Material Tracking:** Tracks consumed material (`stuffDef`), updating world label (e.g. `Granite blocks Signal Beacon`) and inspect string.<br>• **Zero-TPS execution:** 100% unmanned, no `WorldPawns`, no map generated.<br>• **Incursion Sabotage & Salvage:** Hostile traversal sabotages sensor; caravan repair (25% cost) and dismantle (75% salvage partitioned into valid stacks respecting `stackLimit`, e.g. 75 + 37 items, preventing infinite wood transmutation).<br>• **Upright Icon Consistency:** Outpost and sensor icons remain upright and camera-aligned regardless of zoom level or latitude. | **Ready for Test** | World Map / Colony & Caravan Float Menu   |
| **TC-06** | Two-Stage Early Warning & Non-Peeking Escalation    | • Stage 1 ($R_{detect}$ boundary crossing): Amber alert letter (`ThreatSmall`), pulsing amber blip, masked faction, concealed threat points, hidden path.<br>• Stage 2 ($R_{id}$ boundary crossing): Red alert letter (`ThreatBig` hostile / `PositiveEvent` friendly), faction crest, known destination path.<br>• Direct high-spec entry: Incursions spawning within $R_{id}$ trigger Stage 2 directly without replaying Stage 1.<br>• Classification preservation: Re-entering mountainous masking does not regress known intelligence.                                                                                                                                                                                                                                                                                                                                                                                                                             | **Ready for Test** | World Map / Letter Stack / Inspector      |
| **TC-07** | Haze Reconnaissance, Dynamic Camps & Rumours        | • Dynamic quest sites and bandit camps in unmonitored Haze sectors have announcement letters suppressed.<br>• Quest timeout clocks (`TimeoutComp`) are frozen while concealed so quests do not expire invisibly.<br>• **Detection-Layer Site Masking:** Non-player settlements, outposts, camps, and sites emerging from the black shroud into the outer Detection ring ($R_{detect}$) are masked as "Unidentified site" contacts with gold mystery icons, concealed factions, and suppressed interaction gizmos until brought within the inner Identification ring ($R_{id}$) or visited.<br>• Uncovering site via radar or scout caravan triggers deferred discovery letter and resumes timeout clock.<br>• Friendly caravan arrival dispatches Breadcrumb Rumour letter with 8-cardinal compass heading, uncovering target hex.<br>• Expired / Despawned Site Auto-Purge: Despawned or expired dynamic sites are automatically intercepted by Harmony patch on `WorldObject.PostRemove` and purged on the next sweep pass, preventing unbounded save game bloat. | **Ready for Test** | World Map / Letter Stack / Quest Tab      |
| **TC-08** | Inter-Faction Collisions & Background Skirmishes    | • $O(M)$ tile-bucketed collision detection on `TickRare` (250 ticks).<br>• Hostile vs hostile/neutral collisions resolve purely algebraically without loading a tactical map.<br>• Clashes dispatch notification letter and spawn persistent `WorldObject_EncounterAftermath` at clash hex.<br>• Caravan arrival action `Scavenge Aftermath` cleanly transfers trade cargo and survivor pawns.<br>• 15,000-tick skirmish cooldown prevents repeat collision churn.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     | **Ready for Test** | World Map / Letter Stack / Caravan Action |
| **TC-09** | Grounded Storyteller Caravan Encounters             | • Storyteller caravan meetings and extortion incidents are grounded into physical `WorldObject_Incursion` entities marching on contiguous multi-leg routes (`origin -> current -> destination`).<br>• **Faction & Dialogue Consistency:** Robust non-null faction resolution prevents `Tried to generate pawn kinds with null faction` errors; explicit group kind synchronization ensures friendly merchants/travellers never generate contradictory "hostile warband" titles.<br>• Extortion demand dialog delays item deduction until player explicitly confirms acceptance.<br>• Caravans continue overland march across the globe post-encounter.                                                                                                                                                                                                                                                                                                                 | **Ready for Test** | World Map / Dialog / Dev Incident         |
| **TC-10** | Waylith DeepProfiler & Zero-Allocation Verification | • Dev Mode DeepProfiler reveals native `ProjectVigilance.PerformSensorySweep`, `ProjectVigilance.SensorySweep.VisionUpdates`, `ProjectVigilance.InterFactionCollisions`, and `ProjectVigilance.SkirmishResolution` markers.<br>• Single-Pass Traversal: Unified `TryGetDetectionDistance(target, out int distance)` eliminates redundant graph traversals per sensor/target pair on the sweep hot path.<br>• Verification of $\le 1.0\text{ ms}$ execution budget and 0-byte GC allocation during steady-state `TickRare`.                                                                                                                                                                                                                                                                                                                                                                                                                                             | **Ready for Test** | Dev Actions Menu / Deep Profiler          |
| **TC-11** | Scribe Save/Load & Outpost Removal Resilience       | • Saving and loading with active radars, outposts, incursions, aftermath sites, and concealed haze sites produces 0 Scribe errors and 0 duplicate IDs.<br>• Deleting an outpost via dev tools or external mod leaves no dangling references; next sweep clears entry cleanly.<br>• Outpost `stuffDef` is serialized via `Scribe_Defs.Look`, maintaining construction material fidelity across saves.<br>• Expired or despawned dynamic sites leave zero dangling `ConcealedSiteRecord` entries upon save/load.                                                                                                                                                                                                                                                                                                                                                                                                                                                         | **Ready for Test** | Save/Load Menu / Debug Log                |

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

## In-Game Verification Sign-Off Checklist

- [ ] **TC-01:** Multi-dish radar interferometry progression ($10 \rightarrow 17 \rightarrow 22$ rings), locked 250-tick cadence, and dynamic power scaling verified.
- [ ] **TC-02:** Model B+ origin vantage bonuses, target mountain masking, and SIGINT penetration verified.
- [ ] **TC-03:** Coastal hydrophone sonar array aquatic hex restriction verified.
- [ ] **TC-04:** 3D holographic honeycomb mesh and boundary rings verified across display styles and color palettes.
- [ ] **TC-05:** 4X Colony right-click expedition dispatch, automated supply pre-allocation, zero-TPS listening post deployment, stuffDef material tracking, stack-partitioned dismantle salvage (75 + 37 items), hostile sabotage, and repair verified.
- [ ] **TC-06:** Two-stage early warning non-peeking escalation and direct entry bypass verified.
- [ ] **TC-07:** Concealed dynamic camps in Haze, quest timeout freeze, breadcrumb rumours, and PostRemove despawn auto-purge verified.
- [ ] **TC-08:** $O(M)$ tile-bucketed collisions, algebraic background skirmishes, and aftermath scavenging verified.
- [ ] **TC-09:** Grounded storyteller caravan encounters, faction/dialogue consistency (no null faction errors or friendly hostile warbands), and extortion demand item safety verified.
- [ ] **TC-10:** Native Waylith DeepProfiler execution budget ($\le 1.0\text{ ms}$), single-pass traversal resolution, and zero-allocation hot path verified.
- [ ] **TC-11:** Scribe save/load persistence, stuffDef material serialization, and defensive outpost removal resilience verified.
