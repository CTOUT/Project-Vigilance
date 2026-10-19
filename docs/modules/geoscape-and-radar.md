# Module 1 Specification: Geoscape & Radar Grid

## 1. System Objective

The **Geoscape & Radar Grid** module introduces a planetary early-warning and detection layer to RimWorld.

In vanilla RimWorld, all quest sites, faction bases, and world incidents appear with full visibility across the entire planetary sphere. This module introduces **planetary fog of war**: alien activities, moving craft, and clandestine sites remain hidden unless detected by an active radar or sensor installation.

---

## 2. Core Concepts

### 2.1 Sensory Installations

Radar and sensor facilities can be constructed at player colonies or deployed as unmanned forward listening posts on the world map:

| Facility Type                      | Detection Radius (Tiles) | Power Draw | Operational Data Provided                                           |
| :--------------------------------- | :----------------------: | :--------: | :------------------------------------------------------------------ |
| **Small Radar Dome**               |            12            |   800 W    | Blip presence, coarse location                                      |
| **Large Radar Array**              |            25            |  2,500 W   | Blip, craft size class, speed, heading                              |
| **Hyper-wave Decoder**             |            35            |  5,000 W   | Full telemetry: craft type, crew race, mission profile, destination |
| **Oceanic Sonar Array**            |     18 (Ocean/Coast)     |  1,200 W   | Sub-surface USOs, seabed anomaly detection                          |
| **Listening Post (World Outpost)** |            10            | Standalone | Small radar coverage at remote global coordinates                   |

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

Information on the Geoscape is treated as a strategic resource. Detected entities progress through five intelligence tiers based on the sensing facility's technological tier:

| Intelligence Tier      | Visual Representation         | Telemetry Provided                                                                                    | Detection Source                                                                        |
| :--------------------- | :---------------------------- | :---------------------------------------------------------------------------------------------------- | :-------------------------------------------------------------------------------------- |
| **Tier 0: Undetected** | _Invisible_                   | None. The entity moves across the planetary hex grid silently.                                        | Outside all sensory cones and scout horizons.                                           |
| **Tier 1: Suspected**  | _Narrative Cue / Ripple_      | Rumoured presence or acoustic anomaly. Exact location remains unconfirmed.                            | Scout rumours, forward outpost radio chatter, or regional acoustic cues.                |
| **Tier 2: Contact**    | Generic pulsing marker / blip | Presence, rough heading, coarse speed. Faction emblem and headcount are hidden. Updates discretely.   | Colony Scout Horizon (~5 tiles), roaming caravans (~3 tiles), or small radar perimeter. |
| **Tier 3: Classified** | Faction emblem + category     | Attacking faction identified, broad classification (e.g. _Pirate Warband_, _Mechanoid Strike Fleet_). | Mid-range Radar array, forward listening post, or line-of-sight within 2 hexes.         |
| **Tier 4: Analysed**   | Full tactical telemetry       | Exact pawn headcount, siege weaponry breakdown, target colony destination, and live arrival ETA.      | Hyper-wave decoder, high-tech intelligence centre, or direct optical reconnaissance.    |

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
│   ├── Assemblies/           # Compiled net10.0 C# binaries
│   ├── Defs/                 # XML Defs (Buildings, Comps, Research)
│   └── Textures/             # UI blips, radar overlays, building art
├── About/
│   ├── About.xml             # RimWorld 1.7 mod metadata
│   └── Preview.png           # Workshop preview image
└── Source/                   # C# .NET 10 solution (targeting Waylith)
    ├── Geoscape.sln
    └── Geoscape/
        ├── Geoscape.csproj   # TargetFramework: net10.0
        ├── Comps/
        │   ├── CompProperties_Radar.cs
        │   ├── CompRadar.cs
        │   └── WorldObjectComp_RadarCoverage.cs
        ├── World/
        │   ├── WorldObject_MovingCraft.cs
        │   ├── WorldObject_UFO.cs
        │   ├── WorldObject_USO.cs
        │   └── WorldManager_SensoryGrid.cs
        ├── Detection/
        │   ├── DetectionMatrix.cs
        │   └── RadarPulseHandler.cs
        ├── Rendering/
        │   ├── WorldOverlay_RadarCoverage.cs
        │   └── WorldObjectRenderer_CraftBlip.cs
        └── Defs/
            ├── RadarFacilityDef.cs
            └── CraftSizeDef.cs
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

### 6.5 Abandoned Site Incursion Behaviour & Overland Track Pursuit

In vanilla RimWorld, when a settlement or temporary camp is abandoned while a hostile force is en route, the incident abruptly terminates upon arrival and the raiding party simply disappears. In Project Vigilance, incursions maintain persistent physical agency upon arriving at an abandoned site:

1. **Physical Track Detection:**
   - When an incursion reaches an abandoned tile, raiders actively search the vacant camp.
   - The engine checks recent caravan departure records from that tile. If the evacuation departed **overland** on foot or with pack animals, raiders roll a tactical tracking chance based on time elapsed since departure and biome weather.
   - **Odyssey / Flight Exclusion:** Evacuations conducted via high-tech shuttles, drop-pods, or Odyssey gravships leave no ground trail, preventing pursuers from acquiring tracks.
2. **Search Delay & Hot Pursuit:**
   - If the tracking check succeeds, the incursion enters a search state at the abandoned camp for a short delay (e.g. 2–6 in-game hours) before locking onto the fleeing caravan's trajectory.
   - Raiders reroute into an active pursuit on the `WorldGrid`, tracking the caravan toward its destination or ambushing it on the road.
3. **Fallback March & Opportunistic Retargeting:**
   - If no tracks are found or the trail goes cold:
     - **Return March:** Raiders reverse course toward their home settlement or march to the nearest friendly outpost.
     - **Opportunistic Retargeting:** Incursions can dynamically redirect their assault toward an adjacent player outpost, listening post, or allied settlement within range.
     - **Occupation Camp:** In remote wilderness, raiders may convert the abandoned player camp into an enemy outpost or bandit staging base.
