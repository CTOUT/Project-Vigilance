# Module 1 Specification: Geoscape & Radar Grid

## 1. System Objective

The **Geoscape & Radar Grid** module introduces a planetary early-warning and detection layer to RimWorld.

In vanilla RimWorld, all quest sites, faction bases, and world incidents appear with full visibility across the entire planetary sphere. This module introduces **planetary fog of war**: alien activities, moving craft, and clandestine sites remain hidden unless detected by an active radar or sensor installation.

---

## 2. Core Concepts

### 2.1 Sensory Installations

Radar and sensor facilities can be constructed at player colonies or deployed as unmanned forward listening posts on the world map:

| Facility Type | Tech Level | Type | Detection Radius | Power Draw (Standby / Active) | Intelligence Tier | Operational Data Provided |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| **Lookout Watchtower** | Neolithic / Medieval | Building | **8 hexes** | 0 W (Unpowered, Manned) | Tier 2 (*Contact*) | Visual horizon extension; detects overland movement blips. |
| **Early-Warning Radar Dome** | Industrial | Building | **14 hexes** | 350 W / 750 W | Tier 2 (*Contact*) | Weatherproof radome microwave dish; detects overland blips, speed, and heading. |
| **Phased Radar Array** | Spacer | Building | **26 hexes** | 800 W / 1,800 W | Tier 3 (*Classified*) | Steerable electronically scanned array; identifies attacking faction insignia and broad force size. |
| **SIGINT Decryption Console** | Ultra | Facility Linker (`CompFacility`) | *Attached to Array* | 700 W / 1,400 W | **Upgrades to Tier 4 (*Analysed*)** | Signal Intelligence terminal linked to Phased Array; decrypts tactical radio comms to reveal exact pawn counts, carried weapons, and ETA. |
| **Oceanic Sonar Array** | Industrial | Building | **18 hexes (Water)** | 500 W / 1,200 W | Coastal / Ocean | Shoreline hydrophone array monitoring maritime transit and aquatic incursions. |
| **Listening Post (World Outpost)** | Industrial / Spacer | WorldObject | **10 hexes** | Standalone Solar | Tier 2 (*Contact*) | Lightweight zero-TPS global listening post deployed on remote hexes by caravans. |

#### 2.1.1 Multi-Dish Settlement Stacking (Interferometry & Redundancy)

When multiple radar installations operate on the same colony map:
1. **Primary Sensor Designation:** The highest-tier radar structure acts as the **Primary Array**, projecting 100% of its base detection radius.
2. **Interferometric Baseline Expansion:** Additional active, powered radar dishes on the same map function as a synthetic aperture array, providing diminishing returns:
   - **2nd Dish:** +20% radius boost (`Mathf.RoundToInt(baseRadius * 0.20f)`: **+5 hexes** on a 26-hex Phased Array $\rightarrow$ **31 hexes**).
   - **3rd Dish:** +10% radius boost (`Mathf.RoundToInt(baseRadius * 0.10f)`: **+3 hexes** on a 26-hex Phased Array $\rightarrow$ **34 hexes** total).
   - **Subsequent Dishes:** +0% additional range (hard cap at +30% maximum boost).
3. **Discrete Integer Hex Policy:** In RimWorld's discrete hexagonal `WorldGrid`, fractional radii (`0.x`) are avoided. All radii and bonuses are rounded to whole integer hexes using `Mathf.RoundToInt`, ensuring that each tile is deterministically 100% inside or outside coverage.
4. **Power & Resource Trade-Off:** Each secondary dish draws its full standby and active power requirements (e.g. three Phased Arrays draw 2,400 W standby / 5,400 W active!), demanding dedicated power grids.
5. **Fail-Safe Redundancy:** If a mortar strike, electrical fault (`Zzzt!`), or solar flare disables the primary dish, secondary dishes instantly maintain uninterrupted sensory coverage without the colony going blind.
6. **Configurable Balancing:** Players can choose between *Diminishing Interferometry (+30% Max)* [Default] and *Strict One-Per-Settlement Limit* via Mod Settings.

#### 2.1.2 SIGINT Console vs. Alien Hyper-Wave Decoder

To strictly preserve **Rule 1 (Zero Thematic Hardcoding in Frameworks)**:
- **Module 1 (Generic Framework):** Uses the **Signals Intelligence (SIGINT) Decryption Console**, an indoor electronics console placed adjacent to a Phased Radar Array via RimWorld's native `CompFacility` linking system. It intercepts and decrypts human and mechanoid radio transmissions.
- **Module 4/5 (Alien Invasion Campaign):** The iconic **Hyper-wave Decoder** is introduced as an alien-tech research project reverse-engineered from downed UFO navigation computers and captive alien leaders. The alien Hyper-wave Decoder tracks cloaked UFO craft, orbital motherships, and subterranean alien bases on a specialized orbital layer.

### 2.2 Planetary Radar Overlays

When viewing the world map, players can toggle the **Radar Coverage Overlay**:

- Renders semi-transparent sensory domes projected across the `WorldGrid`.
- Overlapping radar coverage improves detection probability for stealthy or high-altitude craft.
- Areas outside radar coverage represent **Sensory Blind Spots** where enemy craft can operate undetected.

### 2.3 Configurable Planetary Visibility Modes

Recognising that player preferences range from authentic military surveillance to immersive exploration and casual storytelling, the engine exposes three selectable visibility models:

1. **Planetary Surveillance (X-COM Style — Default):**
   - Planetary geography (landmasses, oceans, biomes, and visible faction settlements) is charted and known from orbital mapping.
   - Dynamic tactical movement is hidden: hostile incursions, alien craft, and clandestine sites are concealed unless they fall within active Radar or Scout coverage.
   - Ideal for industrial, spacer, and authentic X-COM playthroughs tracking the _difference_ in global activity.

2. **Classic Haze of War (3-Tier RTS Style):**
   - **Terra Incognita (Unexplored):** Shrouded in darkness until visited or scanned.
   - **Haze of War (Explored but Unmonitored):** Rendered with a desaturated translucent slate veil. Known terrain and static bases remain visible, but active incursions and moving hostile units are completely concealed.
   - **Active Vision:** Full daylight clarity revealing live moving entities, accompanied by subtle radar sweep rings.
   - Ideal for low-tech, tribal, medieval, or roleplaying campaigns emphasizing planetary discovery.

3. **Omniscient Mode (Living Map / Spectator):**
   - Zero fog of war or haze. All worldwide incursion movements, faction emblems, and arrival ETAs are immediately visible across the globe without technology requirements.
   - Ideal for players who enjoy observing the planetary simulation without managing radar grids.

### 2.4 5-Tier Contact Classification & Intelligence Progression

Guided by our core design practice—_"You don't know what you don't know: to know, you need to find out (investigate, identify, research, explore)"_—information on the Geoscape is treated as an active strategic resource. Detected entities progress through five intelligence tiers based on the sensing facility's technological tier and reconnaissance proximity:

| Intelligence Tier      | Visual Representation         | Telemetry Provided                                                                                    | Detection Source                                                                        |
| :--------------------- | :---------------------------- | :---------------------------------------------------------------------------------------------------- | :-------------------------------------------------------------------------------------- |
| **Tier 0: Undetected** | _Invisible_                   | None. The entity moves across the planetary hex grid silently.                                        | Outside all sensory cones and scout horizons.                                           |
| **Tier 1: Suspected**  | _Narrative Cue / Ripple_      | Rumoured presence or acoustic anomaly. Exact location remains unconfirmed.                            | Scout rumours, forward outpost radio chatter, or regional acoustic cues.                |
| **Tier 2: Contact**    | Generic pulsing marker / blip | Presence, rough heading, coarse speed. Faction emblem and headcount are hidden. Updates discretely.   | Colony Scout Horizon (~5 tiles), roaming caravans (~3 tiles), or small radar perimeter. |
| **Tier 3: Classified** | Faction emblem + category     | Attacking faction identified, broad classification (e.g. _Pirate Warband_, _Mechanoid Strike Fleet_). | Mid-range Radar array, forward listening post, or line-of-sight within 2 hexes.         |
| **Tier 4: Analysed**   | Full tactical telemetry       | Exact pawn headcount, siege weaponry breakdown, target colony destination, and live arrival ETA.      | SIGINT decryption console (linked to Phased Array), high-tech intelligence centre, or direct optical reconnaissance. |

#### Two-Stage Early Warning Escalation (Fog-of-War Parity)

To preserve authentic tactical uncertainty, the engine strictly forbids "faction peeking" when issuing early warnings:

1. **Stage 1: Contact Tier Alert (Outer Horizon):**
   - When any force crosses the outer detection threshold (e.g. 5 hexes for colony lookouts), an **"Early Warning: Unidentified Contact"** letter is dispatched (`LetterDefOf.ThreatSmall`).
   - The force displays as an **amber pulsing blip** on the world map without a faction emblem or projected path line.
   - The player does not know whether the approaching force is a hostile pirate raid, mechanoid swarm, or peaceful trade caravan.
2. **Stage 2: Classified Tier Confirmation (Close Visual / Reconnaissance):**
   - When the entity reaches close visual range (within 45% of lookout radius, ~2.2 hexes) or is intercepted in the field by player scout caravans, visual identification succeeds:
     - **Hostile Force:** Escalates to an urgent **"Hostile Incursion: [Faction]"** letter (`LetterDefOf.ThreatBig`), shifting the map blip to red with full faction insignia and attack path.
     - **Friendly Force:** Dispatches an **"Approaching Caravan: [Faction]"** letter (`LetterDefOf.PositiveEvent`), shifting the map blip to cyan with full trade route projection.

### 2.5 Zero-Allocation Spatial Bitmask Architecture & Soft Integration

1. **High-Performance Spatial Bitmasks (.NET 10):**
   - With planetary grids containing up to 100,000+ tiles, storing per-tile object wrappers incurs severe garbage collection stutter.
   - The engine utilizes compact 64-bit word arrays (`ulong[]` bitsets) allocating less than ~15 KB of memory per state:
     - `isExploredBitset`: Tracks tiles permanently uncovered by player exploration.
     - `isActiveVisionBitset`: Recomputed during `TickRare()` (250 ticks) combining colony scout horizons, caravan line-of-sight, and active powered radar domes.
2. **Soft Compatibility with Community Fog Mods:**
   - Detects external world fog overhauls (such as D4egon's _World Fog of War_ `#3765828104`) via soft reflection.
   - When detected, `SensoryManager` integrates with their visibility state without requiring a hard dependency on `Vanilla Expanded Framework` (VEF), allowing radar installations to act as natural revealers.

### 2.6 Geoscape Strategic Context Actions (Right-Click World Hexes)

In vanilla RimWorld, right-clicking empty world tiles is inactive unless a player caravan is already actively selected. The module introduces a grand-strategy strategic context menu (`FloatMenuMakerWorld` / `WorldInterface`):

```text
[ Right-Click World Hex #14820 ]
 ├── "Dispatch Expedition Here..."
 │    ├── "From: Home Colony"  ──────────> Opens Dialog_FormCaravan with destination locked to #14820
 │    ├── "From: Mining Outpost"
 │    └── "From: 1st Recon Caravan (3 hexes away)" ──> Direct route waypoint to hex #14820
 │
 ├── "Establish Outpost / Relay Here..."
 │    ├── (If caravan is on tile): "Deploy Automated Listening Post" (Consumes carried 100 Steel, 4 Comp)
 │    ├── (If caravan is on tile): "Construct Hilltop Watchtower" (Consumes carried 150 Wood/Stone)
 │    └── (If remote tile): "Dispatch Construction Caravan from [Colony]..."
 │         └── Opens Dialog_FormCaravan with destination locked & Outpost Kit pre-selected
 │
 └── "Survey Sector / Set Waypoint"
      └── Places a tactical map pin / scout waypoint for visual planning
```

1. **"Dispatch Expedition Here...":**
   - Presents a list of player settlements or nearby active caravans within range.
   - Automatically opens vanilla's `Dialog_FormCaravan` with the destination tile **pre-populated and locked** to the clicked hex.
2. **"Establish Outpost / Relay Here...":**
   - **Local Deployment:** If a player caravan is present on the tile and holds required cargo (100 Steel, 4 Components for Automated Relay; 150 Wood/Stone for Watchtower), an immediate 1–2 hour setup task elapses, and the lightweight `WorldObject_ListeningPost` spawns on the hex.
   - **Remote Dispatch:** Prompts to dispatch a construction caravan from the nearest colony, automatically pre-selecting the necessary outpost construction ingredients in the checklist as a preset!
3. **Existing Outpost Context Options:**
   - *"Reclaim Garrison"* (absorbs garrisoned lookouts back into visiting caravan).
   - *"Dismantle Outpost"* (salvages 75% of construction materials back into caravan).
   - *"Repair Sabotaged Station"* (repairs stations disabled or vandalized by passing enemy incursions).

---

## 3. Detection Engine & Performance Design

### 3.1 Performance on the WorldGrid

RimWorld's world map can feature up to 100,000+ hex tiles depending on coverage settings (30% to 100%). Running distance calculations for moving objects on every tick will cripple ticks per second (TPS).

**Optimisation Strategy:**

1. **Tick Intervals:** Detection checks do not run on `Tick()`. They run on `TickRare()` (every 250 ticks / ~4.1 seconds) or a custom 500-tick pulse cycle.
2. **Spatial Indexing & Modern .NET 10:** Active radar installations register their tile locations into a spatial hash table. On RimWorld 1.7 (Waylith), distance queries take advantage of `.NET 10` zero-allocation primitives (`ReadOnlySpan<int>`, SIMD distance vectors, and value-type buffers) to evaluate tiles without GC churn.
3. **Spherical Distance Queries:** Use RimWorld's native `WorldGrid.ApproxDistanceInTiles(tileA, tileB)` or precomputed spherical distance lookups rather than recalculating 3D vector distances from scratch.

### 3.2 Detection Probability Formula

Detection is resolved on each radar pulse cycle:

$$P_{\text{detect}} = 1 - \prod_{i=1}^{N} \left(1 - \text{BaseChance}_i \times \text{SizeMultiplier} \times \text{AltitudeModifier} \times \text{WeatherModifier}\right)$$

Where:

- $N$ is the number of overlapping radar networks with the target in range.
- $\text{SizeMultiplier}$: Very Small (0.4), Small (0.7), Medium (1.0), Large (1.4), Very Large (1.8).
- $\text{AltitudeModifier}$:
  - _Very Low (Nap-of-the-earth):_ 0.5 (harder for ground radar, vulnerable to visual lookouts).
  - _Low / Medium:_ 1.0 (standard tracking profile).
  - _High / Stratospheric:_ 1.3 (easily tracked at extreme line-of-sight).
  - _Sub-surface (USO):_ 0.0 for conventional radar; detected only by Oceanic Sonar Arrays.

### 3.3 Sampled Radar Tracking vs. Continuous Optical Smoothing

To capture authentic military sensor behaviour, entity movement on the Geoscape dynamically adapts its interpolation fidelity based on the detection source:

1. **Direct Optical Vision (Lookouts, Binoculars, Analysed Tier):**
   - When human lookouts or scout caravans maintain direct continuous line-of-sight, entities glide across the terrain in a silky-smooth 60 FPS spherical interpolation (`Vector3.Slerp`).
2. **Discrete Radar & Sonar Sweeps (Contact Tier / Long-Range Blips):**
   - Real-world military radar (PPI displays) and naval sonar do not provide continuous analog motion; screens update in discrete steps each time the physical antenna beam passes over the target (every 2–5 seconds).
   - In Contact Tier, moving entities update their position strictly on the 250-tick pulse sweep (`TickRare`), holding stationary between sweeps and "blinking" to their newly swept hex. This produces a tense, authentic military tracking room aesthetic.

### 3.4 Off-World Render Gating & Peacetime Performance Optimisations

To ensure that planetary simulation never impacts colony map ticks-per-second (TPS), the sensory engine enforces strict lifecycle boundaries:

1. **Complete Off-World Render Halting:**
   - RimWorld's engine automatically skips the entire world rendering pipeline when the player is managing their local colony (`WorldRendererUtility.WorldRenderedNow == false`).
   - `WorldObject.Draw()`, `DrawPathOverlay()`, `WorldDrawLayer.Render()`, and mesh shader passes consume zero CPU/GPU cycles while the colony map is active.
2. **Background Pulse Purpose (Early Warning):**
   - The lightweight 250-tick sensory sweep runs in the background solely to evaluate whether approaching threats have crossed into lookout or radar horizons, triggering **Early Warning Letters** to the player on their colony screen.
3. **Peacetime Early Exit:**
   - If there are zero active detectable targets on the planet (the default state during 85–90% of peacetime gameplay), the sensory sweep immediately exits on tick 1 without executing distance lookups.
4. **Stationary Sensor Caching:**
   - In _Classic Haze of War_, stationary installations (colonies, radar dishes, listening posts) only calculate their revealed tile radius once upon construction or game load. Only mobile units (caravans) update their explored footprint as they march across the grid.
5. **Omniscient Bypass:**
   - When set to _Omniscient_, all bitmask tracking, tile flood-filling, and mesh generation are completely disabled.

### 3.5 Central Contact Registry & Eager Force Materialization

To ensure 100% deterministic persistence of all planetary forces across both Geoscape transit and Battlescape tactical encounters, Project Vigilance implements a centralized registry and eager force materialization architecture:

1. **Globally Unique Contact Identification (`TrackingId`):**
   - Every planetary incursion receives a permanent, sequential monotonic ID (`Contact-1`, `Contact-2`, etc.) generated by `ContactRegistry.Instance.AllocateTrackingId()`.
   - The ID persists across all lifecycle states: mobilization, overland marching, stealth ambushes, battlescape transits, tactical retreats, and final colony assaults.

2. **Eager Force Materialization at Departure:**
   - Rather than lazily rolling pawns only when combat begins, `WorldObject_Incursion.SetupIncursion` eagerly pre-materializes the entire force makeup at the exact tick the incursion mobilizes from its origin stronghold.
   - **Pawns (Humans & Aliens):** Full RimWorld `Pawn` instances registered into `Find.WorldPawns` with `KeepForever` and `ForcefullyKeptPawns` (granting immunity to vanilla GC discard).
   - **Pets & Pack Animals:** Dromedary camels, muffalo, horses, and attack beasts are created with pack saddles and luggage.
   - **Mechanoids:** Full mechanoid warframes (centipedes, scythers, militors) with specific chassis stats.
   - **Weapons, Armour & Cargo:** Primary weapons, apparel, carried provisions (medicine, food, silver), and merchant bulk stock (`TradeStock`).
   - **Continuous Physiological Simulation:** Overland physiology (`SimulatePawnsPhysiology`) ticks health, wound bleeding, medic tending, night rest, and food consumption while en route.

3. **Central Query API (`ContactRegistry`):**
   - `WorldComponent` singleton indexing all active incursions via hash table (`Dictionary<int, WorldObject_Incursion>`).
   - Query methods: `TryGetContact(trackingId)`, `GetRosterBreakdown(trackingId)`, `HostileContacts`, `RetreatingContacts`, `EngagedContacts`.
   - **Historical Operations Archive (`ContactArchiveRecord`):** When an incursion finishes its lifecycle (neutralized, departed peacefully, arrived at colony, or returned to base), a deep-serialized archive record preserves its initial and final headcount, casualties, captives taken, member names, and resolution timestamp.

### 3.6 Tactical Intelligence Dossier & Roster Inspection View (Design Specification)

Guided by the principle _"You don't know what you don't know: to know, you need to find out"_, detailed tactical telemetry is locked behind reconnaissance and intelligence capability.

#### Intelligence Gating & Access Requirements

- **Tier 0–1 (Undetected / Suspected):** No inspect window or contact selection.
- **Tier 2 (Contact):** Basic inspect pane in the bottom-left corner displaying coarse telemetry: designation (`Contact-N`), speed class, rough heading, and status (`Camped`, `Marching`, `Stationary`).
- **Tier 3 (Classified):** Confirms faction identity, faction insignia, broad category (_Trade Caravan_, _Pirate Warband_, _Mechanoid Hive_), and approximate force size (_Small Squad [~3–5]_, _Company [~10–15]_, _Army [~25+]_) with projected destination vector.
- **Tier 4 (Analysed / Reconnoitred) & Omniscient Mode:** Unlocks the **Tactical Intelligence Dossier** dialog and Geoscape Inspect Tabs. Achieved by:
  1. Operating a high-tech planetary scanning array (SIGINT Decryption Console linked to Phased Radar Array / Strategic Intelligence Facility).
  2. Field reconnaissance: Maintaining a player scout caravan within direct visual range (~2 hexes).
  3. Field interception: Engaging the contact on a tactical encounter map.

#### Dossier UI Layout & Inspect Tabs

Selecting an Analysed contact on the Geoscape provides an interactive inspection button (**"Open Intelligence Dossier"**) and dynamic tabs:

1. **Header Banner:**
   - Unique Tracking ID badge (`Contact-12 [Pirate Warband]`).
   - Faction emblem, current hex location, speed, heading, and live ETA countdown to target.
   - Threat Assessment rating (e.g. _Extreme - Mechanoid Armour Detected_).

2. **Tab 1: Force Roster (Combatants, Mechs, & Animals):**
   - **Category Sections:**
     - _Combatants & Leaders:_ Headcount, names, combat roles (Sniper, Brawler, Heavy Gunner), and conscious/downed status.
     - _Mechanoids & War Engines:_ Identified chassis types (e.g. `2x Centipede (Heavy Gunner)`, `3x Scyther`).
     - _Pack Animals & War Beasts:_ Identified species (e.g. `4x Dromedary Camel (Pack)`, `2x Warg`).
   - **Health Telemetry:** Live health conditions, untreated bleed rates, infection warnings, and mobility ratings derived from continuous physiological simulation.

3. **Tab 2: Equipment, Inventory & Cargo:**
   - **Primary Armament:** List of equipped ranged and melee weapons (with quality and tech tier icons).
   - **Armour Rating:** Average blunt/sharp armour protection across the squad.
   - **Carried Provisions:** Total medicine units, food rations/pemmican, and combat drugs.
   - **Trade Stock (Merchant Caravans):** Catalog of carried trade goods, techprints, silver, and exotic items.
   - **Captives:** Known abducted colonists held in captivity, showing their health state and rescue urgency.

4. **Tab 3: Route & Operational Telemetry:**
   - Origin stronghold and departure timestamp.
   - Waypoints and terrain traversal speed penalties (mountains, swamps, roads).
   - Estimated rest windows (night camping schedule for biological mortal factions).
   - Interception vector calculation helper showing nearest player strike team and intercept ETA.

### 3.7 Fog-of-War Performance Architecture: In-Place Dynamic Vertex Colors & Selective Invalidation

When navigating moving caravans across the planetary sphere in _Classic Haze of War_ mode, revealing new tiles previously induced noticeable lag hitches (100–250ms freeze per hex step). Performance profiling revealed two primary architectural bottlenecks in vanilla RimWorld rendering and naive layer dirtying:

1. **Full Submesh Re-Triangulation Churn:**
   - Naive fog layers regenerate the entire planetary mesh by calling `ClearSubMeshes()` and re-allocating 3D vertices, normals, and triangle index buffers across all 100,000+ planet tiles in a single thread-blocking frame.
2. **Cascading Global Layer Invalidation:**
   - Calling `SetDirty()` on `WorldObjects_Expandable`, `WorldObjects_NonExpandable`, and `WorldDrawLayer_Landmarks` on every tile step forced RimWorld to rebuild 2D icons, names, and mountain text globally across the planet, even when no new landmarks or sites were uncovered.

#### High-Performance Solution & Findings

To eliminate all movement hitches and achieve silky-smooth 60 FPS caravan travel, the Geoscape sensory engine employs a multi-tier optimization pipeline:

1. **Static 3D Hex Geometry with Direct $O(1)$ Vertex Indexing:**
   - The spherical hex mesh is constructed once upon world generation or save load, allocating static submeshes with altitude offset (`0.042f`) and vertex-color transparency.
   - Each tile's vertex slice is permanently mapped into a spatial lookup array:
     $$\text{TileMeshLocation}[tileId] = (\text{subMeshIndex}, \text{startVertIndex}, \text{vertCount})$$
   - When a tile changes state between **Terra Incognita (Shroud)**, **Explored (Haze)**, and **Active Vision (Clear)**, the engine performs direct in-place mutation of the 4–6 vertex colors within `subMesh.colors`.
2. **Zero-Allocation GPU Color Finalization (`FinalizeMesh(MeshParts.Colors)`):**
   - Rather than rebuilding mesh structures, dirty submeshes are updated via `subMesh.FinalizeMesh(MeshParts.Colors)`.
   - This bypasses vertex coordinate, normal, and UV recalculation completely, streaming only vertex color buffers directly to the GPU.
   - **Benchmarked Impact:** Frame latency per fog reveal dropped from **~200 ms to < 0.1 ms** (a ~2,000× speedup), completely eliminating caravan movement stutter.
3. **Selective World Layer Invalidation:**
   - `MarkSurroundingTilesExplored` no longer indiscriminately dirties vanilla object and label layers.
   - It performs an instant bitmask and spatial check (`Find.WorldObjects.AnyWorldObjectAt` and landmark center tile lookup) strictly across newly uncovered tiles:
     - Vanilla expandable/non-expandable world object layers are dirtied **only** if a previously shrouded world object or settlement was physically uncovered.
     - Landmark label layers are dirtied **only** if a geographical feature center was uncovered.
4. **Time-Sliced Geometry Budgeting (Initialization Coroutine):**
   - Initial world load or resolution rebuild runs `Regenerate()` as an incremental coroutine yielding execution every 3ms (`Stopwatch.ElapsedMilliseconds >= 3`), preventing game initialization hitching.
5. **High-Frequency 5-Tick Caravan Movement Sampling:**
   - `SensoryManager.CheckPlayerCaravanMovement()` checks active caravan coordinates every 5 ticks (`Find.TickManager.TicksGame % 5 == 0`).
   - Fog boundaries reveal instantly the exact moment a caravan crosses a hex border, eliminating the visible 250-tick pulse latency without taxing game performance.

### 3.8 Topological Hex-Step Metric & Future Line-of-Sight Backlog

To provide players with a clean, intuitive sensory boundary akin to _Civilization_—where every hex hop is uniform and predictable—active vision, fog-of-war exploration, and sensor ranges use discrete **Topological Hex-Step Distance** rather than continuous spherical geodesic distance:

1. **Elimination of Icosahedral Projection Distortion:**
   - On RimWorld's subdivided icosahedron globe (Goldberg polyhedron), continuous spherical distance (`ApproxDistanceInTiles`) projects a continuous circular cap onto non-uniform hexagons, clipping corners where tiles are stretched and creating protruding teeth where tiles are compressed.
   - Project Vigilance's zero-allocation `FloodFillTopological` explores strictly up to `maxSteps` graph hops, guaranteeing a mathematically symmetric, clean hexagonal perimeter with all corners intact.
2. **Visual & Logical Parity:**
   - The outer active vision perimeter (`maxSteps = 5`), the inner optical identification horizon (`maxSteps = 2`), and sensor detection checks (`TraversalDistanceBetween <= maxSteps`) now share the exact same topological metric.
   - Moving contacts transition from Unidentified to Classified the exact instant their blip crosses the visible inner orange perimeter.
3. **Future Milestone Backlog — Mountainous Line-of-Sight Occlusion:**
   - _Design Goal:_ In a future update, integrate raycast or Bresenham sightline checks on the globe where impassable mountains, high elevation peaks, or severe weather cells cast **sensory shadows** behind them, blocking ground lookouts and low-tech spotters while high-altitude radar arrays and orbital reconnaissance bypass ground occlusion.

---

## 4. UFO & USO Strategic Profiles

Moving craft on the world map are represented as custom subclasses of `WorldObject` (e.g., `WorldObject_FlyingCraft`).

### 4.1 Mission Profiles

1. **Scout / Reconnaissance:** Patrols randomly across a region, pauses over points of interest, returns to orbit or sub-surface base.
2. **Harvest / Abduction:** Lands at an isolated hex tile for 12–36 game hours. If not intercepted or assaulted, departs having completed its quota.
3. **Infiltration:** Navigates toward allied or neutral faction settlements to conduct covert subversion.
4. **Terror Strike:** Deploys a large dreadnought craft directly to a major settlement or colony.
5. **Sub-surface Cruise (TftD):** Travels through ocean tiles to conduct seabed mapping or launch coastal raids.

### 4.2 Telemetry Reveal Stages

- **Stage 1 (Unidentified):** Red/amber blip on the world map. Shows only position.
- **Stage 2 (Tracked):** Shows speed vector, altitude class, estimated size.
- **Stage 3 (Decoded):** Hyper-wave decoding displays exact craft identity (e.g., _"Small Scout - Sectoid Crew - Researching Mountainous Hex #4128"_).

---

## 5. C# Class Architecture

```
Project-Vigilance/Geoscape/
├── 1.7/
│   ├── Assemblies/           # Compiled net10.0 C# binaries (Geoscape.dll)
│   └── Defs/                 # XML Defs (WorldObjects, Settings, ModExtensions)
├── About/
│   ├── About.xml             # RimWorld 1.7 mod metadata & dependencies
│   └── Preview.png           # Workshop preview artwork
└── Source/                   # C# .NET 10 solution (targeting Waylith / BRUTAL)
    ├── Geoscape.slnx         # Modern .NET solution
    └── Geoscape/
        ├── Geoscape.csproj   # TargetFramework: net10.0
        ├── GeoscapeMod.cs    # Mod entrypoint & Harmony initialization
        ├── GeoscapeSettings.cs # Visibility modes, colors, and incursion tuning
        ├── Debug/            # Tactical debug overlays & telemetry gizmos
        ├── Patches/          # Minimally invasive Harmony hooks (World, Pawns, UI)
        ├── Sensory/          # ISensorProvider, ScoutHorizonSensor, SensoryManager,
        │                     # VisibilityMode, WorldDrawLayer_HazeOfWar, WorldDrawLayer_RadarCoverage
        └── World/            # WorldObject_Incursion, IncursionArrivalManager,
                              # IncursionInterceptionUtility, IncursionTacticalRetreatUtility,
                              # MapComponent_OverlandAmbush, LordJob_IncursionTransit
```

---

## 6. Physical Incursion Tracking & Storyteller Overhaul

In vanilla RimWorld, Storyteller incidents (such as `IncidentDefOf.RaidEnemy`, `VisitorGroup`, and `TravelerGroup`) instantiate pawns at the map boundary instantaneously out of thin air.

The **Physical Incursion System** overhauls this behaviour completely: **every incident is given a physical origin point on the WorldGrid and travels across the planet before reaching the colony.**

```
[ Storyteller Fires Raid Event ]
                │
                ▼
  [ Resolve Origin Hex Tile ]
  (Nearest Hostile Settlement / Pirate Camp / Orbital Vector)
                │
                ▼
  [ Spawn Travelling Incursion Entity ]
  (WorldObject_Incursion marches/flies across WorldGrid)
                │
                ├─────────────────────────────────────────┐
                │                                         │
        (Enters Radar Bubble)                   (Travels in Blind Spot)
                │                                         │
                ▼                                         ▼
   ┌─────────────────────────┐              ┌───────────────────────────┐
   │ EARLY WARNING TRIGGERED │              │     NO EARLY WARNING      │
   │ ETA: 12 Hours           │              │ Raid arrives at map edge  │
   │ Interception Possible!  │              │ with vanilla suddenness   │
   └─────────────────────────┘              └───────────────────────────┘
```

### 6.1 Origin Resolution Logic

When an incident triggers, the system intercepts the event and calculates an appropriate source point:

| Incursion Type                  | Resolved Source Origin                                    | Transit Method & Speed                          |
| :------------------------------ | :-------------------------------------------------------- | :---------------------------------------------- |
| **Faction Raid (Human/Tribal)** | Nearest hostile settlement or hidden pirate staging camp  | Overland march (terrain-dependent: 12–48 hours) |
| **Drop-Pod / Shuttle Raid**     | Hostile base or orbital trajectory                        | Sub-orbital arc (high speed: 1–3 hours)         |
| **Mechanoid Incursion**         | Mechanoid orbital carrier or dormant world hive           | Orbital drop or heavy mechanical crawl          |
| **Friendly Caravan / Visitors** | Nearest allied faction settlement or trade road           | Overland caravan transit                        |
| **Creepjoiners & Wanderers**    | Abandoned world ruins, crashed escape pods, or wilderness | Solitary overland journey                       |

### 6.2 Early Warning & Tactical Interception en Route

1. **Strategic Advance Warning:**
   - Instead of sudden panic, colonies with radar installations receive actionable advance notice:
     - _"Sensory Alert: Hostile group detected 15 hexes east. Vector heading: Colony. Estimated arrival: 18 hours."_
   - Players can order miners and hunters back inside, seal perimeter blast doors, deploy field traps, and position heavy artillery.
2. **Telemetry Quality by Radar Tier:**
   - _Small Radar:_ Detects only an unclassified hostile vector and estimated ETA.
   - _Large Radar:_ Identifies estimated force size and speed class.
   - _Hyper-wave Decoder:_ Fully reveals faction identity, pawn count, and equipped armaments (e.g., _"18 Pirates from Broken Skull Clan armed with doomsday launchers and assault rifles"_).
3. **Mid-Route Interception (Module 2 Synergy):**
   - Because raiders physically exist as world entities during transit, the player can scramble **Interceptors** or deploy a **Skyranger Strike Team** to ambush and neutralise the raiding force on the world map before they ever reach the home base.
4. **Standalone Framework Value:**
   - This storyteller overhaul is entirely generic and modular. Players who want realistic military logistics, scouting, and early warning in vanilla RimWorld can use this module without any X-COM alien themes.

### 6.3 Dynamic Travel Encounters & Sight Horizons

In vanilla RimWorld, travelling encounters (meeting traders or being ambushed on the road) occur as sudden, non-spatial dice rolls. The Geoscape framework overhauls this through a **Hybrid Horizon Model**:

1. **Inter-Settlement Roaming Groups:**
   - Major trade caravans, pilgrim bands, and patrol groups physically spawn and journey along planetary roads between NPC settlements.
2. **Caravan Sight Horizon (2–3 Hex Scouting Radius):**
   - A player caravan possesses a natural visual horizon revealing moving entities within a 2–3 hex radius.
   - When another group enters line of sight, they are rendered on the globe:
     - _"Friendly Bulk Goods Trader (Blue Moon Cove) spotted 2 hexes north-east."_
3. **Tactical Agency & Risk-Reward:**
   - **Opportunistic Barter:** Players can divert their route to intercept traders and purchase emergency medicine or high-tech components.
   - **Hazard Avoidance:** Spotting a large hostile warband ahead allows caravans to divert into mountain terrain or take cover.
   - **Highway Banditry:** Raider or pirate ideoligions can actively hunt down and ambush wealthy merchant caravans.
   - **Dangerous Encounters:** Not all roaming groups are benevolent. Travellers may turn out to be disguised bandits, disease carriers, or desperate refugees demanding food and silver under threat of violence.

### 6.4 Inaccessible Routing Strategy (Islands & Impassable Mountains)

If the player's colony or destination is located on an isolated island or enclosed by impassable mountain tiles, standard ground pathfinding (`WorldPathFinder`) will report an unreachable destination. The engine applies a tiered resolution strategy:

1. **Sub-Orbital / Airborne Incursion (Industrial+ Factions):**
   - Factions with advanced technology convert the raid into an aerial contact (`WorldObject_AirIncursion`), flying directly over oceans and mountain peaks.
2. **Amphibious / Coastal Embarkation (Island Settlements):**
   - Forces march to the nearest coastal hex, board maritime craft, and traverse ocean tiles.
3. **Local Wilderness Staging (Enclosed Valleys & Tribals):**
   - Neolithic or landlocked factions stage from a concealed mountain camp or wilderness tile in the same enclosed valley.
4. **Zero-Failure Safe-Guard (Idempotent Fallback):**
   - If path resolution fails completely, the system safely bypasses incursion transit and executes the vanilla incident instantly, ensuring the Storyteller loop never deadlocks.

### 6.5 Empty & Abandoned Site Incursion Behaviour: Probabilistic Overland Pursuit & Opportunistic Retargeting

In vanilla RimWorld, when a settlement or temporary camp is abandoned or evacuated while an incursion is en route, the incident abruptly terminates upon arrival and the raiding party simply disappears into thin air. In Project Vigilance, both hostile and peaceful incursions maintain persistent physical agency upon arriving at an evacuated or abandoned site:

1. **Peaceful Incursions (Visitors & Traders):**
   - Arriving visitors and merchant caravans that find no conscious colonists present safely turn back (`incursion.ReverseCourse`), physically marching across the Geoscape back to their home base with status `Status: Returning home` and full ETA telemetry.
   - Upon reaching their home base, they cleanly disband and register outcome `"ReturnedHomeSafely"`.

2. **Hostile Incursions (Raids & Warbands) — 3-Tier Tactical Hierarchy:**
   - When a hostile incursion arrives at an active settlement with zero conscious colonists or an abandoned settlement tile, raiders execute a dynamic tactical decision tree:

   - **Tier 1: Physical Track Detection & Hot Pursuit (`TryFindEvacuatingCaravanTracks`, `SetPursuitTarget`):**
     - Raiders actively scour the deserted settlement for signs of recent departure.
     - Detects player caravans within tracking radius ($\le 8$ hexes) in the same planetary layer.
     - **Probabilistic Tracking Roll:** Raiders roll a tactical tracking chance based on technology level, doctrine, trail age, and weather:
       - **Faction Tech Level Baseline:**
         - `Neolithic` (Tribals): **85%** base tracking capability (wilderness survivalists, scouts, and hunters reading broken vegetation, ash, and footprints).
         - `Medieval`: **65%** base tracking capability.
         - `Industrial` (Outlanders, Pirates): **50%** base tracking capability (standard infantry reconnaissance).
         - `Spacer` / `Ultra` (Empire): **45%** base tracking capability.
         - `Archotech`: **35%** base tracking capability.
         - `Mechanoid`: **30%** base tracking capability (devoid of biological tracking instincts; relies strictly on ground seismic and electromagnetic sensor disturbance).
       - **Tactical Modifiers:**
         - Sapper / Breacher Raids: **-15%** penalty (encumbered with heavy mining/siege apparatus and focused on structures).
         - Hunting / War Beasts: **+15%** bonus if the incursion includes hunting hounds, wolves, or wargs utilizing scent tracking.
         - Distance Decay: **-5% per hex** of separation between the settlement and the caravan (colder trails and dispersed tracks).
         - Weather Factor: **-20%** penalty if active rain has washed away ground tracks; **-15%** in heavy snow.
         - Clamped within **5% to 90%** certainty.
     - **Hot Pursuit Execution:** If the roll succeeds, the incursion locks onto the target caravan, shifts into hot pursuit (`[Pursuit]`), and charts an intercept path toward the caravan.
     - **Dynamic Trajectory Updates:** Every 250 game ticks, the pursuing warband checks the moving caravan's coordinates; if the caravan moves to a new hex, the pursuer automatically re-plots its intercept course.
     - **Field Ambush:** Upon entering the caravan's hex, the pursuers immediately ambush the fleeing colonists (`ExecuteHostileInterception(isAmbush: true)`).

   - **Tier 2: Opportunistic Retargeting (Adjacent Outposts & Settlements):**
     - If raiders fail to find tracks or no caravans are within tracking radius, the warband evaluates nearby targets.
     - Searches for reachable player colonies, camps, or listening outposts within striking distance ($\le 10$ hexes).
     - If found, the incursion dynamically diverts course (`SetRetargetRoute`), dispatching a threat letter to the player (`"Hostile Force Diverting"`) as it marches to assault the secondary outpost.

   - **Tier 3: Fallback Ransack & Return March:**
     - If no caravan tracks are found and no secondary outposts are within striking distance, the warband ransacks the vacant structures, claims whatever plunder was left behind, and reverses course (`incursion.ReverseCourse`).
     - The incursion physically marches across the Geoscape back to its origin base or nearest friendly stronghold, remaining visible on sensors until exiting into home territory (`ReturnedHomeSafely`).

### 6.6 Night Owl & Schedule-Aware Caravan Rest

In vanilla RimWorld, player caravans are governed by a hardcoded planetary rest lock (`CaravanNightRestUtility.WouldBeRestingAt`), forcing every caravan to halt and make camp between **22:00 and 06:00** local time regardless of colonist traits or timetable settings.

Project Vigilance replaces this rigid lock (`Caravan_NightResting_Patch` and `CaravanNightRestUtility_LeftRestTicksAt_Patch`) with dynamic **circadian and Schedule-tab evaluation**:

1. **Nocturnal / Night Owl Squads:**
   - Colonists with the `NightOwl` trait on an untouched default schedule or an all-`Anything` timetable automatically adopt a nocturnal march cycle: they camp during daytime (**11:00–18:00** local time) and march through the night (**18:00–11:00**) without mood penalties.
   - **Night Vision Advantage (`ScoutHorizonSensor`):** Awake Night Owls negate the 50% nighttime darkness penalty (`22:00–06:00`), maintaining **100% full scout horizon radius** throughout the night. During their **11:00–18:00** daytime rest window, an all-Night-Owl party is asleep in camp and its sight horizon collapses to `1.0` hex (`Asleep in Camp`).
   - Caravan inspection strings dynamically compute remaining daytime rest hours (`LeftRestTicksAt`) rather than reporting `0s`.
2. **Mixed Squad Majority Shift & 24/7 Watch Coverage:**
   - When a caravan contains a mix of standard colonists and Night Owls, the caravan follows the **majority shift** of its conscious colonists so the party only camps once per 24-hour cycle (8 hours) rather than halting twice a day for 15 hours.
   - On an exact 50/50 tie without explicit `Work` overrides, the caravan defaults to the standard **22:00–06:00** rest window.
   - **24/7 Full Geoscape Visibility:** Because the opposite shift remains awake on camp watch while the majority sleeps—Daywalkers standing watch during daytime camp (`11:00–18:00`) and Night Owls standing watch with full night vision during nighttime camp (`22:00–06:00`)—a mixed party effectively maintains **100% "day" visibility (`baseRadius`) 24/7**.
3. **Schedule-Tab Override & Force-Marching:**
   - Explicit `Pawn_TimetableTracker` assignments in the **Schedule** tab override default circadian windows:
     - Assigning a majority of colonists to `Sleep` at any hour instructs the caravan to make camp during those hours.
     - Assigning colonists to `Work` (or `Recreation` / `Meditate`) during rest hours overrides automatic camping, allowing the player to **force-march** through the night (or day) at the cost of accumulating pawn exhaustion.

### 6.7 Field Interception, Concealed Ambushes & Threat-Aware Tactical Retreat

When a player caravan intercepts or is engaged by an overland incursion on the `WorldGrid`:

1. **Edge-to-Edge Within-Hex Interpolation & Predictive Lead Interception (`WorldObject_Incursion`):**
   - **Sensor-True Hex Positioning (`TrueDrawPos`):** Rather than interpolating from hex center to hex center (which would visually place an incursion inside the next hex while its logical `Tile` still points to the previous hex for `tileProgress` `0.50 → 0.99`), movement interpolates across the current hex's physical span: `EntryBorder (0.0) → HexCenter (0.5) → ExitBorder (1.0)`. The visual blip is always strictly inside its true `Tile` and crosses the hex boundary at the exact tick `Tile` advances.
   - **X-COM Predictive Intercept Vectors (`GetInterceptTargetTile`):** Ordering a caravan to intercept a moving incursion plots a lead course toward the incursion's next transit hex (`NextPathTile`) when appropriate, and triggers engagement immediately upon head-on shared-border crossings (`tileProgress + caravanProgress >= 0.95`) rather than forcing the intercepting squad to walk into the hex behind the target.
2. **Unaware Transit & Concealed Ambush (`MapComponent_OverlandAmbush`):**
   - Intercepted enemy patrols enter the map in an unalerted marching column (`LordJob_IncursionTransit`) along a trajectory separated from the player's concealed deployment zone, while player colonists start drafted with **Hold Fire** enabled.
   - Enemies remain unaware unless a player colonist enters direct optical line-of-sight ($\le 18$ cells), opens fire within acoustic range ($\le 50$ cells), or damages a member of the column.
   - If the player holds fire and lets the column pass to the exit edge, surviving pawns preserve their exact 1:1 health and equipment back onto the `WorldObject_Incursion`.
3. **Humanlike Combat Break & Opportunistic Looting:**
   - Humanlike hostile factions (Pirates, Outlanders, Tribals) retain full opportunistic stealing (`canSteal: true`) and casualty break transitions (`Trigger_FractionPawnsLost` / `LordToil_StealCover`), allowing battered raiders to grab high-value loot and flee the battlefield rather than fighting to the last pawn like mindless automatons.
4. **Threat-Aware Tactical Retreat (`IncursionTacticalRetreatUtility`):**
   - Ordering a **Retreat** from either a tactical encounter map or a besieged home settlement evaluates all known/visible threats (hostile raiders, mechanoids, insectoids, and active turrets) across all four map borders.
   - Rather than blindly pathing to the geographically closest border cell—which could route fleeing colonists directly through an enemy vanguard—each retreating pawn scores candidate exit sectors using angular corridor threat proximity and verifies actual `PawnPath` exposure, selecting the safest reachable extraction route back to the Geoscape.

### 6.8 Overland Trade Caravans: Cargo Persistence, Trader Lifecycle & Roadside Trading

Project Vigilance implements an overland trade caravan architecture:

1. **Persistent Trader Component & Roadside Commerce (`IncursionTrackingHelper` & `PawnComponentsUtility_AddAndRemoveDynamicComponents_Patch`):**
   - In vanilla RimWorld, when non-player pawns pass into `WorldPawns`, `Pawn_MindState.Reset()` wipes `wantsToTradeWithColony = false`. Consequently, `PawnComponentsUtility.AddAndRemoveDynamicComponents` discards `pawn.trader`, permanently erasing the merchant's `TraderKindDef` upon sector transitions.
   - Project Vigilance intercepts `WorldPawns.PassToWorld` and dynamic component evaluation, guaranteeing that active merchant traders preserve `wantsToTradeWithColony = true` and maintain their trade dialogues across multiple sector transitions and Geoscape encounters.
   - Caravans crossing paths with player squads on the Geoscape offer roadside commerce via `Dialog_Trade`.
2. **Pack Animal Cargo Transportation Lifecycle (`SyncCargoFromTradeStockToPawns` & `SyncCargoFromPawnsToTradeStock`):**
   - **Carrier Packing:** When transitioning from the Geoscape to a tactical map or Battlescape transit, the caravan's trade stock (`incursion.TradeStock`) is loaded directly into the `pawn.inventory.innerContainer` of their carrier pack animals (dromedaries, muffalos, etc., falling back to humanoids if beasts of burden are absent).
   - **Tactical Movement:** Pack animals physically transport their goods on their backs across the battlefield. Goods are never dropped in a heap on the floor upon map generation.
   - **Casualty-Based Salvage:** If an individual pack animal is downed or killed in combat, vanilla RimWorld drops that specific animal's inventory at its death cell for player looting. Surviving pack animals retain their carried cargo.
   - **Return to Geoscape:** When surviving caravan members retreat or exit the sector, remaining goods in surviving carrier inventories are gathered back into `incursion.TradeStock`, persisting accurately for future Geoscape interactions or encounters.
3. **Trader Loss & Expedition Abort Lifecycle (`AbortTradeMission`, `Trigger_TraderLost`):**
   - If a caravan's designated merchant (e.g. Sandra) is killed or incapacitated by hazards (wildlife, mechanoids, or crossfire), the expedition recognizes the commercial mission cannot proceed.
   - On tactical maps: `Trigger_TraderLost` transitions surviving guards to an evasive jog (`LordToil_ExitMap`), escorting their remaining pack animals and cargo to the map border.
   - On the Geoscape: The trade caravan reclassifies to `"returning caravan (aborted mission)"`, cancels the remaining itinerary, and charts an emergency return route back to their origin stronghold or nearest friendly outpost.
4. **Pre-Save Relationship Sanitization (`SanitizeDanglingRelations`):**
   - Procedural pawn generation often rolls non-essential background relationships (e.g. ex-lovers) for NPC pawns pointing to ephemeral world pawns that RimWorld's `WorldPawnGC` immediately discards.
   - Before saving, `WorldObject_Incursion.ExposeData` purges references to discarded or non-serialized world pawns, preventing missing cross-reference warnings (`Thing_Human... curParent=RimWorld.DirectPawnRelation`) upon loading.
5. **Empty Settlement & Evacuation Return March (`IncursionArrivalManager.ExecuteArrival`):**
   - When friendly visitors or trade caravans arrive at an empty settlement (e.g. all colonists away in an overland caravan) or an abandoned site, they recognize the colony is vacant.
   - Rather than instantly despawning, they physically reverse course (`incursion.ReverseCourse`) and march back along their overland route toward their home territory.
   - Their contact remains fully visible and selectable on the Geoscape (`Status: Returning home`), maintaining accurate speed and ETA telemetry.
   - Upon safely returning to their home base, they disband with outcome `"ReturnedHomeSafely"`, ensuring accurate tracking in `ContactRegistry` rather than false `"Neutralized"` battle outcome logs.

### 6.9 Symmetrical Fog-of-War & Zero-Allocation In-Place Vertex Colors

1. **Discrete Topological Hex Vision Perimeters:**
   - Switched fog exploration, active vision perimeters, and sensor ranges from continuous spherical geodesic distance (`ApproxDistanceInTiles`) to discrete **Topological Hex Steps** (`FloodFillTopological` & `TraversalDistanceBetween`).
   - Eliminates corner clipping and asymmetric teeth caused by Goldberg icosahedron distortion, ensuring vision reveals form clean, perfectly regular hexagonal boundaries (_Civilization_ style).
2. **Dynamic In-Place Vertex Colors (`WorldDrawLayer_HazeOfWar`):**
   - Eliminated the 100–250ms movement stutter caused by full-globe 3D mesh re-triangulation when caravans uncover new sectors.
   - The 3D globe mesh geometry (vertices, normals, triangles) is generated once during startup using a time-sliced coroutine yielding every 3ms.
   - Sector exploration and active vision state transitions update GPU vertex colors in-place (`LayerSubMesh.FinalizeMesh(MeshParts.Colors)`) in $\approx 0.05$ ms with zero garbage collection allocations.
   - Explicitly clears `subMesh.finalized = false` prior to color buffer uploads, preventing false-positive log warnings.
