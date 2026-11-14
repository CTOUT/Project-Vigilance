# Module 1 Specification: Geoscape & Radar Grid

## 1. System Objective

The **Geoscape & Radar Grid** module introduces a planetary early-warning and detection layer to RimWorld.

In vanilla RimWorld, all quest sites, faction bases, and world incidents appear with full visibility across the entire planetary sphere. This module introduces **planetary fog of war**: alien activities, moving craft, and clandestine sites remain hidden unless detected by an active radar or sensor installation.

---

## 2. Core Concepts

### 2.1 Sensory Installations

Radar and sensor facilities can be constructed at player colonies or deployed as unmanned forward listening posts on the world map:

| Facility Type                       |      Tech Level      |               Type               | Detection Radius ($R_{detect}$) | Identification Radius ($R_{id}$) |   Power Draw (Standby / Active)   |          Intelligence Tier          | Operational Data Provided                                                                                                                                             |
| :---------------------------------- | :------------------: | :------------------------------: | :-----------------------------: | :------------------------------: | :-------------------------------: | :---------------------------------: | :-------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Lookout Watchtower**              | Neolithic / Medieval |             Building             |    **8 rings** _(+Vantage)_     |             4 rings              | 0 W (Passive Optical Observation) |         Tier 2 (_Contact_)          | Visual horizon extension; detects overland movement blips. Gains up to +3 rings on hills/mountains.                                                                   |
| **Early-Warning Radar Dome**        |      Industrial      |             Building             |          **14 rings**           |             6 rings              |           350 W / 750 W           |         Tier 2 (_Contact_)          | Weatherproof radome microwave dish; detects overland blips, speed, and heading.                                                                                       |
| **Phased Radar Array**              |        Spacer        |             Building             |          **24 rings**           |      **10 / 17 / 22 rings**      |          800 W / 1,800 W          |        Tier 3 (_Classified_)        | Steerable electronically scanned array; multi-dish interferometry expands $R_{id}$. Identifies faction crest and broad force size.                                    |
| **SIGINT Decryption Console**       |        Ultra         | Facility Linker (`CompFacility`) |       _Attached to Array_       |         _Matches Array_          |          700 W / 1,400 W          | **Upgrades to Tier 4 (_Analysed_)** | Signal Intelligence terminal linked to Phased Array; decrypts tactical radio comms to reveal exact pawn counts, carried weapons, and ETA. Penetrates terrain masking. |
| **Coastal Sonar Station**           |      Industrial      |             Building             |      **18 rings (Water)**       |             8 rings              |          500 W / 1,200 W          |           Coastal / Ocean           | Shoreline hydrophone array monitoring maritime transit and aquatic incursions on water tiles (`tile.WaterCovered`).                                                   |
| **Listening Post: Automated Relay** | Industrial / Spacer  |           WorldObject            |    **12 rings** _(+Vantage)_    |             5 rings              |         Standalone Solar          |         Tier 2 (_Contact_)          | Lightweight zero-TPS global listening post deployed on remote hexes by caravans. 100% unmanned. Capped vantage (+1 to +2 rings).                                      |
| **Listening Post: Automated Sonar** |      Industrial      |           WorldObject            |      **18 rings (Water)**       |             8 rings              |   Standalone Solar / Hydrophone   |    Coastal / Tier 2 (_Contact_)     | Unmanned coastal hydrophone outpost deployed by caravans on coastal hexes adjacent to `WaterCovered` tiles (`120 Steel` + `4 Components`).                            |
| **Listening Post: Signal Beacon**   | Neolithic / Medieval |           WorldObject            |    **6 rings** _(+Vantage)_     |             0 rings              |          0 W (Unpowered)          |         Tier 2 (_Contact_)          | Unmanned timber or stone signal beacon deployed by caravans. Full optical vantage (+1 to +3 rings on high ground); zero identification capability.                    |

> [!NOTE]
> **Coastal Sonar Access & Directional Water Propagation:** Both the **Coastal Sonar Station** (`Geoscape_SonarArray`) and **Listening Post: Automated Sonar** (`Vigilance_ListeningPost_Sonar`) require direct coastal access (at least one adjacent `WaterCovered` tile validated via `PlaceWorker_CoastalSonar` / `SonarTraversalUtility.HasCoastalAccess`). Rather than radiating across land, sonar originates from each adjacent water tile and projects outward in directional V-shaped sectors (`SonarTraversalUtility.PopulateSonarCoverage`, `DirectionalSectorCosThreshold = 0.45f`) strictly through connected `WaterCovered` tiles. Separate `(tileId, isSonar)` cache keys in `SensoryManager` allow ground radar and oceanic sonar coverage to overlap on coastal water tiles without conflict.

#### 2.1.1 Multi-Dish Settlement Stacking (Interferometry & Identification Expansion)

When multiple radar installations operate on the same colony map:

1. **Concentric Ring Distance on the WorldGrid:** In RimWorld, planetary distance is evaluated using `Find.WorldGrid.TraversalDistanceBetween(...)`, which measures radial step distance in discrete concentric hexagonal rings outwards from the origin:
   - **Ring 0:** 1 tile (the origin settlement, outpost, or camp itself).
   - **Ring 1:** adds 6 tiles (the immediate hexagonal ring surrounding the origin).
   - **Ring 2:** adds 12 tiles.
   - **Ring 3:** adds 18 tiles.
   - **Ring 4:** adds 24 tiles.
   - In general, each successive **Ring $n$ adds exactly $6 \times n$ new tiles**. _(As a geometric consequence, the cumulative total of enclosed tiles within radius $n$ follows the centred hexagonal formula $\text{Total} = 1 + 3n(n+1)$, though gameplay logic and balance operate strictly on discrete traversal ring steps)._
2. **The Physical Role of Synthetic Aperture Interferometry:** In real-world radar physics, clustering antennas into an interferometer does not push radio waves arbitrarily further across the horizon; it drastically sharpens **angular resolution** ($\theta \approx \lambda / B$).
3. **Identification Ring ($R\_{id}$) Expansion (Dual-Ring Model):**
   - The maximum detection radius $R_{detect}$ remains stable at **24 rings**, providing an outer boundary where incoming incursions register as **amber blips** (Contact Tier — speed and heading known, faction hidden).
   - Adding secondary dishes expands the inner **Identification / Classification Horizon ($R\_{id}$)** outward toward the perimeter:
     - **1 Dish (Baseline):** $R_{detect} = 24$ rings; $R_{id} =$ **10 rings** (~40% radius). Long-range warning blip; faction identified only when forces draw close.
     - **2 Dishes (+Interferometry):** $R_{detect} = 24$ rings; $R_{id} =$ **17 rings** (~70% radius). High-resolution synthetic aperture resolves faction insignia and force size much earlier.
     - **3 Dishes (+Full Aperture):** $R_{detect} = 24$ rings; $R_{id} =$ **22 rings** (~92% radius). Near-instant classification across almost the entire radar envelope.
4. **Temporal Resolution (Sweep Cadence):**
   - Radar sweeps operate on a locked `250-tick` cadence (~4.1s) across all installations. Adding secondary dishes expands the inner identification horizon (10 -> 17 -> 22 rings) without accelerating the sweep cadence, preserving deterministic performance and zero-TPS stability.
5. **Fail-Safe Redundancy:** If mortar shells, electrical faults (`Zzzt!`), or solar flares destroy or de-power the primary array, secondary dishes instantly maintain uninterrupted coverage.
6. **Visual Representation on the Globe:** `WorldDrawLayer_RadarCoverage` renders both the outer glowing boundary ring ($R_{detect}$) and a subtle dashed/pulsing inner ring ($R_{id}$). Players visually see the inner identification ring expand outward as secondary dishes are built.

> [!NOTE]
> **Why doesn't building more radars increase my maximum detection range?**
> Detection radius is constrained primarily by transmission power and planetary line-of-sight limitations; additional dishes improve resolution rather than raw detection distance. By expanding the interferometric baseline, multiple dishes allow the colony to resolve fine telemetry and identify hostile forces much earlier within its existing envelope, rather than pushing microwave beams through planetary curvature.

#### 2.1.2 SIGINT Console vs. Alien Hyper-Wave Decoder

To strictly preserve **Rule 1 (Zero Thematic Hardcoding in Frameworks)**:

- **Module 1 (Generic Framework):** Uses the **Signals Intelligence (SIGINT) Decryption Console**, an indoor electronics console placed adjacent to a Phased Radar Array via RimWorld's native `CompFacility` linking system. It intercepts and decrypts human and mechanoid radio transmissions.
- **Module 4/5 (Alien Invasion Campaign):** The iconic **Hyper-wave Decoder** is introduced as an alien-tech research project reverse-engineered from downed UFO navigation computers and captive alien leaders. The alien Hyper-wave Decoder tracks cloaked UFO craft, orbital motherships, and subterranean alien bases on a specialized orbital layer.

#### 2.1.3 Sensor Terrain Model (Locked Model B+: "Vantage & Masking")

To make world tile elevation (`tile.elevation`) and topography (`tile.hilliness`) strategically meaningful without the severe CPU overhead and player confusion of world-scale raycasting:

1. **Vantage Bonus (Origin Tile):**
   - Elevated ground expands the operational envelope of optical and lightweight sensors:
     - **Lookout Watchtower & Caravan Lookouts:** `Flat` = baseline ($8\text{ rings}$); `SmallHills` = $+1\text{ ring}$ ($9\text{ rings}$); `LargeHills` = $+2\text{ rings}$ ($10\text{ rings}$); `Mountainous` = $+3\text{ rings}$ ($11\text{ rings}$).
     - **Unmanned Listening Posts:** $+1$ to $+2\text{ rings}$ if deployed on hills or mountains.
     - **High-Tech Radars (Radomes, Phased Arrays):** Raw detection range ($R_{detect} = 24$) remains fixed by transmission power. Elevated ground grants $+1$ to $+2$ rings to the **Identification Horizon ($R\_{id}$)**, reflecting high ground clearing low-altitude ground clutter.
2. **Tactical Terrain Masking (Target Tile):**
   - **Detection is Preserved:** Contacts moving through rugged terrain always register within $R_{detect}$ as an **amber Contact blip** with known speed and heading. Terrain never induces a sudden, unfair zero-warning ambush.
   - **Identification is Gated:** If an incursion's current tile is `Hilliness.Mountainous`, rugged rock walls and deep gorges mask heraldry and scatter radar returns. The contact **cannot advance beyond `IntelligenceTier.Contact`** while inside a Mountainous tile, even if inside the $R_{id}$ ring.
   - **Emergence Classification:** The moment the incursion exits the mountain chain into Foothills or Plains, immediate classification occurs if within $R_{id}$.
   - **Spacer Penetration Override:** The **Spacer Phased Radar Array with linked SIGINT Decryption Console** ignores terrain masking, utilizing advanced synthetic aperture processing and tactical radio interception to resolve forces regardless of terrain.
3. **Technical Boundaries & Future Note:**
   - **Zero Raycasting:** Line-of-sight raycasting, radar shadows, and mesh deformation across the `WorldGrid` are permanently rejected to preserve 60 FPS globe rendering, zero garbage collection, and intuitive player UX.
   - **Oceanic Sonar:** Sonar strictly scans water tiles (`tile.WaterCovered == true`). Land tiles completely block sonar propagation. Future milestones may investigate ocean depth, bathymetry, and negative elevation values.
   - **Elevation-Aware Visual Observation (Future Investigation):** Evaluating whether `tile.elevation` should grant an additional vantage bonus for optical observation systems (Lookout Watchtowers, Signal Beacons, Caravan Lookouts) on exceptionally high-altitude mountain tiles (> X metres) beyond standard `Hilliness` modifiers (tracked for Milestone 1.4). Does not apply to radar systems unless gameplay testing demonstrates a clear benefit.

### 2.2 Planetary Radar Overlays

When viewing the world map, players can toggle the **Radar Coverage Overlay**:

- Renders semi-transparent sensory domes projected across the `WorldGrid`.
- Overlapping radar coverage improves detection probability for stealthy or high-altitude craft.
- Areas outside radar coverage represent **Sensory Blind Spots** where enemy craft can operate undetected.

### 2.3 Configurable Planetary Visibility Modes

Guided by the foundational principles—_**"You don't know what you don't know"**_ and _**"Information must be earned, not guessed"**_—Project Vigilance treats the planetary globe not as an omniscient atlas, but as an uncharted celestial frontier. The engine provides three selectable visibility models balancing discovery, operational surveillance, and sandbox play:

1. **Classic Haze of War (3-Tier RTS Style — Recommended Default):**
   - **Terra Incognita (Unexplored):** The procedural planet begins completely uncharted and shrouded in darkness. Continents, mountain ranges, biomes, and foreign settlements must be actively discovered from ground level.
   - **Haze of War (Explored but Unmonitored):** Rendered with a desaturated translucent slate veil. Known terrain, charted settlements, and permanent landmarks remain visible, but active enemy incursions, caravans, and dynamic hostile camps are completely concealed.
   - **Active Vision:** Full daylight clarity revealing live moving entities, accompanied by subtle radar sweep rings and lookout horizons.
   - **Core Philosophy:** Purest expression of the Vigilance design thesis. Every lookout watchtower, signal beacon, caravan expedition, and reconnaissance satellite expands your charted frontier.

2. **Planetary Surveillance (Classic X-COM):**
   - **Orbital Survey Mapping Available:** Planetary geography (continents, oceans, mountain chains, biomes, and visible faction settlements) is charted and known from initial orbital cartography, mirroring the 1994 X-COM experience on Earth.
   - **Dynamic Movement Concealed:** Dynamic hostile incursions, alien warbands, and clandestine forward camps remain concealed unless they fall within active Radar or Scout coverage.
   - **Ideal For:** Industrial, spacer, and high-tech playthroughs focusing strictly on the tactical detection and interception of incoming threats rather than cartographic exploration.

3. **Omniscient Mode (Sandbox / Observer):**
   - Zero fog of war or haze. All worldwide incursion movements, faction emblems, dynamic camps, and arrival ETAs are immediately visible across the globe without technology or sensor requirements.
   - **Ideal For:** Mod debugging, performance profiling, storyteller testing, and casual role-players who enjoy observing the living world simulation without managing sensor networks.

### 2.4 5-Tier Contact Classification & Intelligence Progression

Guided by our core design practice—_"You don't know what you don't know: to know, you need to find out (investigate, identify, research, explore)"_—information on the Geoscape is treated as an active strategic resource. Detected entities progress through five intelligence tiers based on the sensing facility's technological tier and reconnaissance proximity:

| Intelligence Tier      | Visual Representation         | Telemetry Provided                                                                                    | Detection Source                                                                                                     |
| :--------------------- | :---------------------------- | :---------------------------------------------------------------------------------------------------- | :------------------------------------------------------------------------------------------------------------------- |
| **Tier 0: Undetected** | _Invisible_                   | None. The entity moves across the planetary hex grid silently.                                        | Outside all sensory cones and scout horizons.                                                                        |
| **Tier 1: Suspected**  | _Narrative Cue / Ripple_      | Rumoured presence or acoustic anomaly. Exact location remains unconfirmed.                            | Scout rumours, forward outpost radio chatter, or regional acoustic cues.                                             |
| **Tier 2: Contact**    | Generic pulsing marker / blip | Presence, rough heading, coarse speed. Faction emblem and headcount are hidden. Updates discretely.   | Colony Scout Horizon (~5 tiles), roaming caravans (~3 tiles), or small radar perimeter.                              |
| **Tier 3: Classified** | Faction emblem + category     | Attacking faction identified, broad classification (e.g. _Pirate Warband_, _Mechanoid Strike Fleet_). | Phased Radar Array within $R_{id}$ horizon (10 / 17 / 22 rings), or line-of-sight within 2 hexes.                    |
| **Tier 4: Analysed**   | Full tactical telemetry       | Exact pawn headcount, siege weaponry breakdown, target colony destination, and live arrival ETA.      | SIGINT decryption console (linked to Phased Array), high-tech intelligence centre, or direct optical reconnaissance. |

#### Two-Stage Early Warning Escalation (Fog-of-War Parity)

To preserve authentic tactical uncertainty, the engine strictly forbids "faction peeking" when issuing early warnings:

1. **Stage 1: Contact Tier Alert (Outer Horizon):**
   - When any force crosses the outer detection threshold $R_{detect}$ (e.g. 24 rings for Phased Arrays, 14 rings for Small Radars, 12 rings for Automated Outposts, or 5 hexes for natural colony lookouts), an **"Early Warning: Unidentified Contact"** letter is dispatched (`LetterDefOf.ThreatSmall`).
   - The force displays as an **amber pulsing blip** on the world map without a faction emblem or projected path line.
   - The player does not know whether the approaching force is a hostile pirate raid, mechanoid swarm, or peaceful trade caravan.
2. **Stage 2: Classified Tier Confirmation (Close Visual / Reconnaissance):**
   - When the entity reaches the inner identification threshold $R_{id}$ (e.g. 10 / 17 / 22 rings for Phased Arrays, 6 rings for Small Radars, or ~2.2 hexes for natural lookouts) or is intercepted in the field by player scout caravans, visual identification succeeds (provided the target is not masked by mountainous terrain):
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

In vanilla RimWorld, right-clicking empty world tiles is inactive unless a player caravan is already actively selected. The module introduces a grand-strategy strategic context menu (`WorldSelector_HandleWorldClicks_Patch` / `FloatMenuMakerWorld_ChoicesAtFor_Patch`):

```text
[ Right-Click World Hex #14820 ]
 ├── Smart Source-Agnostic Dispatch (Nearest N Caravans & M Colonies)
 │    ├── "Move caravan [Name] to..." / "Travel and deploy..."
 │    ├── "Dispatch Expedition from [Colony]..." ──> Dialog_FormCaravan (destination locked)
 │    └── "Other caravans (...)" / "Other colonies (...)" ──> Drilldown sub-menus
 │
 ├── "Establish Outpost / Relay Here..."
 │    ├── (Caravan on tile): "Deploy Automated Listening Post" (100 Steel, 4 Components)
 │    ├── (Coastal hex): "Deploy Automated Sonar Station" (120 Steel, 4 Components)
 │    ├── (Caravan on tile): "Construct Signal Beacon" (150 Wood/Stone)
 │    └── (Remote tile): "Dispatch Construction Caravan from [Colony]..."
 │         └── Opens Dialog_FormCaravan with destination locked & Outpost Kit pre-selected
 │
 └── Settlement & Exploration Actions
      ├── "Move to hex (Hold position outside [Colony])"
      └── "Travel into unexplored sector (Tile #...)" (Classic Haze of War shroud)
```

1. **Smart Source-Agnostic Contextual Dispatch (`WorldSelector_HandleWorldClicks_Patch`):**
   - Right-clicking any world hex without pre-selecting a source entity opens an intelligent dispatch menu listing actions from the nearest _N_ player caravans (`maxContextualCaravansShown`, default `3`) and nearest _M_ player colonies (`maxContextualColoniesShown`, default `2`), ordered by traversal proximity.
   - Additional caravans and colonies beyond the configured display caps remain accessible through `"Other caravans (...)"` and `"Other colonies (...)"` drilldown sub-menus so the right-click menu stays uncluttered on sprawling multi-colony campaigns.
   - Automatically opens `Dialog_FormCaravan` with the destination tile **pre-populated and locked** to the clicked hex when dispatching from a colony, or issues direct route/deployment orders to caravans. Under _Classic Haze of War_, right-clicking unexplored shroud tiles offers exploration movement without leaking hidden impassable terrain or outpost construction options.
2. **"Establish Outpost / Relay Here..." & Outpost Logistics:**
   - **Local & Remote Deployment:** If a player caravan is present on the tile and holds the required cargo, construction begins on-site; for remote tiles, colony dispatch pre-populates required materials and expedition rations in `Dialog_FormCaravan`. Unified outpost titles are resolved via `CaravanExpeditionUtility.GetListeningPostKindLabel` (`"Automated Listening Post"`, `"Automated Sonar Station"`, `"Signal Beacon"`).
   - **Automated Listening Post (`AutomatedRelay`):** **100 Steel + 4 Components** to deploy; dismantles for **75 Steel + 3 Components** (75% recovery); repairs for **25 Steel + 1 Component**.
   - **Automated Sonar Station (`AutomatedSonar`):** **120 Steel + 4 Components** to deploy (offered exclusively on coastal tiles adjacent to `WaterCovered` terrain via `SonarTraversalUtility.HasCoastalAccess`); dismantles for **90 Steel + 3 Components** (75% recovery); repairs for **30 Steel + 1 Component**.
   - **Signal Beacon (`SignalBeacon`):** **150 Wood or Stone blocks** (`stuffDef` permanently serialised via `Scribe_Defs.Look`, updating the outpost label and inspect pane dynamically); dismantles for **112 items** of the matching `stuffDef` partitioned into valid `stackLimit` stacks; repairs for **30 Wood or Stone blocks**.
3. **Builder-Scaled Outpost Assembly (`OutpostAssemblySpeedUtility`) & Mid-Build Departure Protection:**
   - **Team Assembly Speed:** Expedition assembly duration scales dynamically from composite colonist construction capability (`100%` of the lead builder's `ConstructionSpeed` plus `50%` of each assistant, clamped between `2,500` and `40,000` ticks from base durations of `5,000` ticks for Signal Beacons and `15,000` ticks for Automated Relays / Automated Sonar Stations; immediate on-site caravan deployments start at `2,500` ticks). Progress pauses automatically whenever all capable builders rest according to their circadian/schedule window.
   - **Pre-Departure Validation:** `Dialog_FormCaravan_CheckForErrors_Patch` requires at least one colonist capable of Construction for build expeditions and verifies spare carry capacity for dismantle salvage, while `Dialog_FormCaravan_TrySend_Patch` presents an assembly time and provision summary before launch.
   - **Mid-Build Departure Protection (`Caravan_PathFollower_StartPath_Patch`):** Ordering a stationary caravan to depart while construction or dismantling is incomplete intercepts the move and offers three options: pause and leave part-built/part-dismantled, cancel (refunding 100% of construction materials or restoring operational telemetry), or stay on-site to finish.
4. **Civilization-Style Settlement Staging (`CaravanArrivalAction_HoldPosition`):**
   - Right-clicking any explored settlement tile with a player caravan offers `"Move to hex (Hold position outside [Colony])"` (or `"Halt & hold position outside [Colony]"` when already passing through), allowing caravans to camp or stage outside a settlement on the world map without entering its map or triggering automatic colony absorption.

### 2.7 Orbital Infrastructure: Persistent Stations & Drifting Satellite Constellations

Orbital-only factions (such as `TradersGuild` / _Merchant's Bazaar_) and advanced space defence programs deploy orbital infrastructure analogous to surface settlements and mobile caravans:

1. **Persistent Orbital Stations (`WorldObject_OrbitalStation`):**
   - High-altitude, stable operational centres serving as deployment, maintenance, and logistics hubs.
   - Maintains a $35^\circ$ angular service corridor. Satellites passing through the corridor enter `In Service Area` mode to sync telemetry, receive maintenance, and restock trade inventories.
   - Houses automated fabrication queues to schedule and launch replacement satellites if assets are destroyed.

2. **Drifting Keplerian Flight Mechanics (`OrbitalFlightModel` & `OrbitalParameters`):**
   - Replaces fixed repeating loops with closed-form, analytical Keplerian state vectors ($O(1)$ evaluation, zero heap allocations).
   - Planetary oblateness ($J_2$) nodal precession ($\dot{\Omega}$) and incommensurate orbital periods ($T_{\text{orbit}} \nmid 60{,}000$) ensure ground tracks continually drift across longitudes.
   - A surface colony never experiences satellite overpasses on a rigid or predictable repeating clock.

3. **Specialized Satellite Payload Roles:**
   - **Trade Satellite (`Satellite_Trade`):** Implements `ICommunicable`, `ITrader`, and `IThingHolder`. When passing overhead a colony with a powered `Building_CommsConsole`, it opens an orbital trade communication window with drop pod delivery. Restocks when rendezvousing with its parent station.
   - **Reconnaissance Satellite:** Implements `ISensorProvider` (`IsTechnological = true`, `PenetratesTerrainMasking = true`, `MaxIntelligenceTier = Analysed`). Operates on high-inclination polar sweeps ($88^\circ$ Sun-synchronous), clearing Haze/Fog of War and spotting hidden sites.
   - **Military Satellite:** High-inclination tactical surveillance platforms providing fire control and instant incursion analysis in contested sectors.
   - **Navigation Satellite:** Medium-orbit positioning constellations ($12{,}000\text{ km}$). Surface caravans passing under the satellite's footprint receive a 15% overland movement speed acceleration (`CaravanTicksPerMoveUtility_GetTicksPerMove_Patch`).
   - **Communication Satellite:** High-apogee relays ($35{,}786\text{ km}$) bridging remote, unmanned listening posts across mountain chains and oceans to the player's colony sensor network.

4. **Future Vessel Extensibility (`IOrbitalEntity`):**
   - Universal interface allowing future orbital actors (player gravships, shuttles, interceptors, UFOs) to enter station service areas or navigate drifting orbits without modifying station or satellite logic.

### 2.8 Campaign Rules, Persistence & Mutability Framework

Guided by our twin design principles—_**"You don't know what you don't know"**_ and _**"Information must be earned, not guessed"**_—gameplay-affecting settings in Project Vigilance are not floating global UI options; they are **authoritative campaign state** bound directly to save files:

$$\mathbf{Campaign\ Identity} = \mathbf{Scenario} + \mathbf{World\ Seed} + \mathbf{Storyteller\ \&\ Difficulty} + \mathbf{Campaign\ Rules}$$

#### 1. The Two-Axis Rule Model

The framework separates the _lifecycle constraint_ of a rule from its _operational transition side-effects_:

- **Axis 1: Mutability Lifecycle (`RuleMutability`):**
  - **`Mutable`:** Presentation and notification filters that can be freely altered at any time without compromising simulation integrity (e.g. `enableEarlyWarningLetters`, radar outline styles, custom colours).
  - **`Escalatable`:** Epistemic knowledge rules that follow a monotonic forward-progression path (e.g. `visibilityMode`: Fog of War $\to$ Planetary Surveillance $\to$ Omniscient). Information can be safely revealed; it cannot be cleanly unrevealed without loss of historical state.
  - **`Immutable`:** Physical, biological, and storyteller simulation laws locked at world generation (e.g. `colonyScoutRadius`, `caravanScoutRadius`, `enableNighttimeSensoryReduction`, `incursionsRestAtNight`, `synchronizeMultiVectorRaids`, `enableTacticalIngressFlanking`, `resetThreatCooldownOnArrival`, `startingSiteSelection`).
- **Axis 2: Transition Effects (`RuleTransitionEffects`):**
  - Bitmask declaring automated engine routines triggered when permitted rule changes occur (`ReleaseConcealedSites`, `RescanSensors`, `RepathPlayerCaravans`, `RebuildWorldLayers`).

#### 2. Authoritative Architecture & Zero-Allocation Facade

```text
[ Global ModSettings ]
      │ (Player's new-campaign defaults)
      ▼
[ World Generation Page ] ──> Dialog_GeoscapeCampaignRules (Draft Set + Preset Profiles)
      │ (Snapshot taken upon world creation)
      ▼
[ GeoscapeCampaignRules ] (WorldComponent: saved in .rws, authoritative)
      │
      ▼
[ GeoscapeRules Facade ] ───> Gameplay hot path (Zero-alloc thread-safe reads)
```

- **`GeoscapeCampaignRules` (`WorldComponent`):** Serialises the authoritative `GeoscapeRuleSet` into the `.rws` save file under `<world><components>` alongside `schemaVersion`, `campaignId`, and `ruleChangeLedger`.
- **`GeoscapeRules` Facade:** High-performance, zero-allocation accessor routing all production queries to the active campaign's rules, safely falling back to defaults at the main menu or in headless unit tests.
- **Rule Change Ledger (`RuleChangeRecord`):** Capped at 100 entries, recording every escalation, unsafe dev override, and legacy migration. Pinned origin records (`CampaignCreated`, `LegacyMigration`, `ModAdoption`) are permanently preserved.
- **Day-0 Creation Snapshot:** The initial `CampaignCreated` ledger entry records an exhaustive semicolon-delimited string snapshot of all 10 campaign rules selected at Day 0, providing permanent forensic integrity for bug reporting and challenge runs.
- **Cryptographic HMAC-SHA256 Signing & Replay Reconciliation (`RuleLedgerSigner`):** Every `RuleChangeRecord` carries a chained HMAC-SHA256 signature (`RuleLedgerSigner.ComputeRecordHash`) linking the predecessor hash (starting from `GENESIS-00000000`), tick, change kind, rule key, transition values, note, and `campaignId`. On save load (`CampaignEntryPath.ActiveSave`), `GeoscapeCampaignRules.ReconcileLedgerOnLoad()` verifies every signature in the chain, guards against schema downgrades (`CurrentSchemaVersion = 2`), and replays recorded rule transitions from the Day-0 baseline snapshot against active rule values. Unrecorded external XML edits or forged signatures non-destructively log a warning and record an `ExternalModification` audit entry (`CampaignIntegrityStatus.ExternallyModified` / `"External Modification Detected"`) without blocking or corrupting the savegame.

#### 3. Campaign Profiles (Presets)

To streamline world setup, the campaign creation wizard page (`Page_GeoscapeRules`) and mod settings provide one-click profile presets:

- **Fog of War (Recommended):** Full planetary fog of war, automated landfall on uncharted globes (`StartingSiteSelectionMode.AutomatedLandfall`), biological nocturnal rest, multi-vector synchronisation, and tactical flanking.
- **Planetary Surveillance (Classic X-COM):** Known planetary geography with dynamic incursion tracking outside radar envelopes.
- **Hardcore Vigilance:** Uncharted planet, tight visual horizons, active night blindness, automated landfall, and aggressive tactical flanking.
- **Omniscient (Vanilla):** Vanilla RimWorld global visibility. No fog of war.
- **Custom:** Full manual control over individual rule parameters via the modal dialog (`Dialog_GeoscapeCampaignRules`).

When **Automated Landfall** (`StartingSiteSelectionMode.AutomatedLandfall`) is active on uncharted globes, `CampaignWizardPageUtility.SyncPageLinkage` bypasses RimWorld's `Page_SelectStartingSite` screen during world-generation page stitching and designates a viable starting tile automatically via `TileFinder.RandomStartingTile()`, preventing pre-landing terrain probing. If a player selects _Manual Targeting_ under Fog of War, `StartingSiteSelectionFeedback_Patch` replaces explicit invalid-tile rejection messages (`AmbiguousLandingRejectionMessage`) and suppresses settlement proximity hazard and goodwill warnings on unexplored tiles.

#### 4. Strategic Runway: The Planetary Expedition Phase

To eliminate the "Blind Guessing Paradox" of starting on an uncharted planet under _Classic Haze of War_, the architecture incorporates an extensible runway for the **Civilization Settler Paradigm** (Milestone 1.4):

- Scenarios can declare an overland **Expedition Phase** (`ExpeditionStartMode`), starting the player with a mobile reconnaissance caravan to survey local rivers, choke points, and roads before committing to a base location.
- Heavy starting resources (steel, components, food crates) are preserved as **Orbital Landing Cargo / Mission Assets**, deployed via drop pods only when the player activates their Colony Beacon on the chosen hex.
- Orbital and space starts (Odyssey platforms, GravShips, Save Our Ship 2) automatically bypass the overland expedition, preserving 100% scenario compatibility.

---

## 3. Detection Engine & Performance Design

### 3.1 Performance on the WorldGrid & Empirical Profiling Requirements

RimWorld's world map can feature up to 100,000+ hex tiles depending on coverage settings (30% to 100%). Running unconstrained distance calculations or pairwise scans for moving objects will cripple ticks per second (TPS).

**Optimisation Strategy & Performance Budget:**

1. **Measurable Execution Budget:** The complete `SensoryManager.DoRadarSweeps` pass must execute in **$\le 1.0\text{ ms}$** total CPU time on `TickRare` (250 ticks / ~4.1 seconds) under a stress scenario of 20 radar installations and 100 active world incursions at 100% world coverage.
2. **Zero-Allocation Policy:** Exactly **0 bytes** allocated per sweep. All bitsets, candidate lists, and query buffers must be pre-allocated and pooled.
3. **Waylith & `DeepProfiler` Instrumentation (.NET 10):** All sensory and collision passes are instrumented with RimWorld's native `DeepProfiler` blocks for in-engine diagnostic inspection via the Debug Menu:
   ```csharp
   DeepProfiler.Start("ProjectVigilance.RadarSweep");
   try { /* sweep logic */ }
   finally { DeepProfiler.End(); }
   ```
   For headless stress benchmarks, .NET 10 runtime diagnostic tools (`dotnet-trace`, `System.Diagnostics.Stopwatch`) measure microsecond execution latency and GC allocations.
4. **Tile-Bucketed Incursion Collisions ($O(M)$):** Inter-faction collision detection completely eliminates $O(M^2)$ pairwise distance scans. On `TickRare`, incursions register their `CurrentTile` into a dictionary bucket (`Dictionary<int, List<WorldObject_Incursion>>`). Collisions trigger strictly when multiple non-allied incursions occupy identical tiles (`incA.CurrentTile == incB.CurrentTile`).
5. **Spatial Fast-Reject & Modern .NET 10:** Active radar installations register their tile locations into a spatial hash table. On RimWorld 1.7 (Waylith), distance queries take advantage of `.NET 10` zero-allocation primitives (`ReadOnlySpan<int>`, SIMD distance vectors, and value-type buffers) to evaluate tiles without GC churn.
6. **Spherical Fast-Reject Filter:** Before running graph traversal BFS via `TraversalDistanceBetween`, an instant vector angle check ($\vec{v}_{\text{sensor}} \cdot \vec{v}_{\text{target}} \ge \cos(\theta_{\text{max}})$) rejects targets in opposite hemispheres in single-digit CPU cycles.
7. **Single-Pass Traversal Distance Resolution (`TryGetDetectionDistance`):** Rather than evaluating `CanDetect` and then running a second graph traversal BFS to determine traversal distance for tier scoring, `ISensorProvider.TryGetDetectionDistance(target, out int distance)` evaluates planetary reach and outputs the exact traversal step distance in a single pass, eliminating redundant graph calculations on the 250-tick sweep hot path.

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

> [!NOTE]
> **Milestone 1.3 Scope Annotation:** Overland incursion detection on the planetary grid uses deterministic traversal ring thresholds ($R_{detect}$ and $R_{id}$). The stochastic multi-variable probability formula above is reserved for future high-altitude aerial craft and UFO/USO strategic intercept profiles (Module 2).

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
3. **Locked Model B+ ("Vantage & Masking") Integration:**
   - Planetary elevation and hilliness integrate cleanly through the locked Model B+ architecture: origin high ground grants vantage bonuses to radius, while mountainous target tiles mask faction heraldry and gate intelligence at Contact tier.
   - World-scale raycasting, Bresenham sightline sweeps, and sensory shadows are permanently rejected to preserve 60 FPS globe rendering, zero garbage collection, and intuitive player UX.

---

## 4. UFO & USO Strategic Profiles

Moving craft on the world map are represented as custom subclasses of `WorldObject` (e.g., `WorldObject_FlyingCraft`).

### 4.1 Mission Profiles

1. **Scout / Reconnaissance:** Patrols randomly across a region, pauses over points of interest, returns to orbit or sub-surface base.
2. **Harvest / Abduction:** Lands at an isolated hex tile for 12–36 game hours. If not intercepted or assaulted, departs having completed its quota.
3. **Infiltration:** Navigates toward allied or neutral faction settlements to conduct covert subversion.
4. **Terror Strike:** Deploys a large dreadnought craft directly to a major settlement or colony.
5. **Sub-surface Cruise (TftD):** Travels through ocean tiles to conduct seabed mapping or launch coastal raids.

### 4.2 Telemetry Reveal Stages (5-Tier Alignment)

- **Tier 2 (Contact):** Amber blip on the world map. Shows coarse position, speed class, and heading.
- **Tier 3 (Classified):** Shows craft chassis class, propulsion type, and broad mission profile.
- **Tier 4 (Analysed / Decoded):** Tactical decryption displays exact craft identity (e.g. _"Small Scout - Crew Composition - Mission Objective"_).

---

## 5. C# Class Architecture

```
Project-Vigilance/Geoscape/
├── 1.7/
│   ├── Assemblies/           # Compiled net10.0 C# binaries (Geoscape.dll)
│   └── Defs/                 # XML Defs (WorldObjects, Buildings, Research, Letters)
├── About/
│   ├── About.xml             # RimWorld 1.7 mod metadata & dependencies
│   └── Preview.png           # Workshop preview artwork
└── Source/                   # C# .NET 10 solution (targeting Waylith / BRUTAL)
    ├── Geoscape.slnx         # Modern .NET solution
    └── Geoscape/
        ├── Geoscape.csproj   # TargetFramework: net10.0
        ├── GeoscapeMod.cs    # Mod entrypoint & Harmony initialisation
        ├── GeoscapeRules.cs  # Zero-allocation static facade for active campaign rules
        ├── GeoscapeSettings.cs # Visibility modes, 5-tab UI, colours, and incursion tuning
        ├── Debug/            # Tactical debug actions & telemetry gizmos
        ├── Orbital/          # IOrbitalEntity, OrbitalFlightModel, OrbitalManager,
        │                     # WorldObject_OrbitalStation, WorldObject_Satellite, Satellite_Trade
        ├── Patches/          # Minimally invasive Harmony hooks (World, Pawns, Incidents, UI)
        ├── Rules/            # GeoscapeCampaignRules, GeoscapeRuleSet, CampaignProfile,
        │                     # RuleLedgerSigner, Page_GeoscapeRules, Dialog_GeoscapeModAdoption
        ├── Sensory/          # ISensorProvider, SensoryManager, SonarTraversalUtility,
        │                     # OptimisticPathingUtility, LandmarkSensoryUtility, WorldDrawLayers
        ├── UI/               # RadarScopeGeometry, RadarScopePreview (180x180 live CRT scope)
        └── World/            # WorldObject_Incursion, ContactRegistry, IncursionArrivalManager,
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
2. **Telemetry Quality by Radar Tier (Intelligence Progression):**
   - _Small Radar Dome (Tier 2 Contact):_ Detects an unclassified hostile vector, speed class, and estimated ETA.
   - _Phased Radar Array (Tier 3 Classified):_ Identifies faction crest, broad force category, and travel destination; multi-dish interferometry expands the identification horizon.
   - _SIGINT Decryption Console (Tier 4 Analysed):_ Fully reveals faction identity, exact pawn count, equipped armaments, and penetrates mountainous terrain masking.
3. **Mid-Route Interception (Module 2 Synergy):**
   - Because raiders physically exist as world entities during transit, the player can scramble **Interceptors** or deploy a **Skyranger Strike Team** to ambush and neutralise the raiding force on the world map before they ever reach the home base.
4. **Standalone Framework Value:**
   - This storyteller overhaul is entirely generic and modular. Players who want realistic military logistics, scouting, and early warning in vanilla RimWorld can use this module without any X-COM alien themes.

### 6.3 Dynamic Travel Encounters, Grounded Caravan Events & Sight Horizons

In vanilla RimWorld, travelling encounters (meeting traders, extortion demands, or faction ambushes on the road) occur as sudden, non-spatial dice rolls. The Geoscape framework overhauls this through a **Hybrid Horizon Model** and grounded encounter interception:

1. **Inter-Settlement Roaming Groups & Grounded Storyteller Encounters (`GroundedCaravanMeetingHelper`):**
   - `IncidentWorker_CaravanMeeting_Patch` (`IncidentWorker_CaravanMeeting`) and `IncidentWorker_CaravanDemand_Patch` (`IncidentWorker_CaravanDemand`) intercept vanilla storyteller caravan encounters and ground them via `GroundedCaravanMeetingHelper`: if a compatible `WorldObject_Incursion` is already present or adjacent, interception executes immediately; otherwise, a persistent `WorldObject_Incursion` with full carried `TradeStock`, inventory, and a continuous two-leg route is spawned in unmonitored shroud (or at its origin settlement when under active sensor coverage). Staged encounters dispatch an approaching notice rather than interrupting immediately, masking unexplored destination settlements as `"an uncharted destination"`.
   - `IncidentWorker_Ambush_Patch` intercepts `IncidentWorker_Ambush_EnemyFaction` (leaving manhunter packs to vanilla handling) and invokes `GroundedCaravanMeetingHelper.TryInterceptAmbush`, grounding enemy ambushes into persistent hostile `WorldObject_Incursion` entities on the caravan's tile (`ExecuteHostileInterception(..., isAmbush: true)`) so ambush survivors remain physical Geoscape forces after the tactical map closes.
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
5. **Post-Retreat Hostile Pursuit (`IncursionTacticalRetreatUtility.ResumeIncursionAfterCaravanRetreat`):**
   - When all conscious player colonists flee an encounter map without captives (`Pawn_ExitMap_Patch`), surviving hostiles are returned to `WorldPawns` (`KeepForever`) and evaluated by `IncursionTacticalRetreatUtility.ResumeIncursionAfterCaravanRetreat`.
   - Hostile non-trade forces whose surviving roster includes at least **50% fit, non-downed combatants** (`PursuitMinimumFitFraction = 0.5f`) lock onto the retreating player caravan via `SetPursuitTarget` and actively give chase across the Geoscape (`"[Incursion] is giving chase to [Caravan]!"`). Battered or non-hostile forces instead resume their prior route (`AdvancePathTile()`).

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

### 6.10 Dynamic Site Reconnaissance, Haze Concealment & Expired Site Lifecycle Management

1. **Epistemic Quest & Incident Concealment (`HazeReconnaissanceManager`):**
   - Dynamic quest and incident sites (logging camps, bandit work sites, hunting lodges, prisoner compounds) spawned in unmonitored sectors of the Haze of War have their announcement letters suppressed and their world markers hidden until active vision (radar dish, colony lookout, mobile caravan, or forward listening post) charts the tile.
2. **Quest Timeout Clock Suspension (`WorldObjectComp_Timeout_CompTick_Patch`):**
   - Vanilla quest expiration timers (`TimeoutComp`) are frozen while a site remains concealed in the Haze. Quests will not expire invisibly before the player has had an opportunity to discover or hear rumours about them. The timer resumes ticking down normally once the site is uncovered.
3. **Breadcrumb Rumours (`BreadcrumbRumourUtility`):**
   - Arriving trade caravans and visiting parties share situational rumours pointing toward concealed sites, uncovering the target tile and providing an 8-cardinal compass heading.
4. **Robust Despawn & Removal Lifecycle Hooking (`WorldObject_PostRemove_Patch`):**
   - To eliminate save-game bloat and missing cross-reference errors, a Harmony postfix on `WorldObject.PostRemove` intercepts dynamic sites that are cancelled, expired, or destroyed while concealed in the Haze, calling `Notify_WorldObjectRemoved`.
   - On every 250-tick sweep, `HazeReconnaissanceManager.CheckUncoveredSites()` also validates that cached entries are still spawned and not destroyed, preventing stale `ConcealedSiteRecord` references and suppressed letters with complex `LookTargets` from leaking into save files.
5. **Detection-Layer Site Masking (Gold `?` Mystery Icons):**
   - Under `ClassicHazeOfWar`, static world objects (`Settlement`, `Site`, `Camp`, `PeaceTalks`, `EscapeShip`, `WorldObject_ListeningPost`) that fall inside the outer detection perimeter ($R_{detect}$ / explored bitset) but outside the inner identification horizon ($R_{id}$ / identified bitset) are masked by `SensoryManager.ShouldMaskWorldObjectAsUnknown` and `WorldObject_Visibility_Patch` as gold `?` mystery icons (`UnknownSiteIconColor`, `"Unidentified site"` / `"Unknown site"`).
   - Faction names, quest links, gizmos, and detailed inspect tabs remain concealed until the site enters an active identification ring or is investigated directly by a caravan.
6. **Anomalous Landmark Exploration (`LandmarkSensoryUtility` Cyan `!` Discovery Markers):**
   - Anomalous, man-made, and otherworldly structures (`LandmarkSensoryUtility.IsAnomalousStructure` and `IsManMadeLandmarkDef` — including monoliths, ancient complexes and ruins, archotech shrines, mechhives, insectoid megahives, pit gates, and Odyssey landmarks) detected within $R_{detect}$ prior to identification are demarcated separately with procedural 64×64 cyan `!` discovery markers (`TacticalCyan`) and `"Unidentified landmark"` / `"Anomalous Landmark"` telemetry via `Landmarks_DrawStandard_Patch`, `ExpandableLandmarksUtility_ExpandableLandmarksOnGUI_Patch`, and `TerrainTelemetryUtility.HasUnidentifiedManMadeLandmark`, distinguishing high-interest exploration targets from tactical `?` contacts.

### 6.11 Caravan Route Visibility Masking & Optimistic Fog-of-War Navigation

1. **Caravan Route Visibility Masking (`WorldObject_Incursion.DrawVisibilityClippedSegment`):**
   - Under `ClassicHazeOfWar` (and all non-`Omniscient` visibility modes), `WorldObject_Incursion.DrawPathOverlay` renders path lines through `DrawVisibilityClippedSegment`.
   - Path segments with both endpoints inside active vision draw normally; segments crossing an active sensor perimeter boundary clip at the midpoint (`Vector3.Lerp(pA, pB, 0.5f)`); and segments wholly outside active vision remain hidden. (`Contact`-tier entities omit projected future routes entirely until classified.)
2. **Optimistic Fog-of-War Pathing & Unexplored Road Discount Masking (`OptimisticPathingUtility` & `WorldPathing_FindPath_Patch`):**
   - Under `ClassicHazeOfWar`, `WorldPathing_FindPath_Patch` (`FindPath`, `WorldReachability.CanReach`, `WorldPathGrid.Passable` / `PassableFast` / `PerceivedMovementDifficultyAt`, `World.Impassable`, `Caravan_PathFollower.IsPassable`, and `WorldRoutePlanner`) treats unexplored shroud tiles as traversable at baseline movement cost (`1.0f`), preventing route planning from leaking hidden mountains or coastlines.
   - `WorldGrid_GetRoadMovementDifficultyMultiplier_Patch` overrides `WorldGrid.GetRoadMovementDifficultyMultiplier` to return `1.0f` whenever either endpoint tile is unexplored during optimistic routing, ensuring pathfinding cannot exploit undiscovered roads inside the shroud.
3. **Autonomous Obstacle Circumvention & 4-Option Hard-Stop Prompt:**
   - As moving caravans uncover tiles (`OptimisticPathingUtility.NotifyCaravanVisionUpdated`), upcoming path nodes (up to 30 ahead) are evaluated against `UnitTraversalCapability` (`LandCaravan`, `NavalUnit`, `Aircraft`).
   - When a newly revealed obstacle stays within `ExpeditionDetourState.MaxDetourMultiplier` (`2.0×` initial direct distance), the caravan automatically replots around it and posts a soft advisory (`"Caravan has encountered an obstacle and is searching for an alternative route."`).
   - When no route breaks even or none exists, `TriggerHardStopPrompt` halts the caravan (`StopDead()`) and opens a 4-option high-priority dialog: _Select a new destination_, _Authorise a wider search radius_ (`+1.5f` multiplier), _Cancel the expedition_ (return to home settlement), or _Continue manual exploration_.
4. **Pre-Entry Impassable Safeguards (`SetupMoveIntoNextTile` & `TryEnterNextPathTile`):**
   - `Caravan_PathFollower_TryEnterNextPathTile_Patch` and `Caravan_PathFollower_SetupMoveIntoNextTile_Patch` (which inspects `path.Peek(1)` before vanilla consumes the next node) validate `OptimisticPathingUtility.IsTileActuallyImpassable`.
   - If an upcoming tile is unwalkable, only that single blocked tile is uncovered via `MarkSurroundingTilesExplored(tile, 0f)` (`maxSteps = 0`), `NotifyCaravanVisionUpdated(caravan)` triggers recovery, and if the stale path is not replaced, `StopDead()` halts the caravan before stepping onto the impassable tile.
