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
