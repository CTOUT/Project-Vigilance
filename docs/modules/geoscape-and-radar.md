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
