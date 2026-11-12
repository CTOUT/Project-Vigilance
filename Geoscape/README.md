# Project Vigilance: Overland Incursions (Geoscape Framework)

This directory hosts the C# source code, XML Defs, and compiled mod package for **Project Vigilance: Overland Incursions** (Package ID: `ProjectVigilance.Geoscape`).

[![Steam Workshop](https://img.shields.io/badge/Steam_Workshop-3822378226-1b2838?logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3822378226)
[![Steam Collection](https://img.shields.io/badge/Steam_Collection-Project_Vigilance-1b2838?logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3822385427)
[![GitHub Release](https://img.shields.io/badge/Release-v0.3.0-238636?logo=github)](https://github.com/CTOUT/Project-Vigilance/releases/tag/v0.3.0)

This is a **standalone, zero-dependency framework mod** designed for RimWorld 1.7 (Waylith) and `.NET 10`. It replaces instantaneous map-edge teleportation with physical overland transit across the global planetary grid.

---

## Current Features (v0.3.0 Live & v0.4.0 Milestone 1.3)

- **Physical Incursion Engine:** Intercepts walk-in raids (`IncidentWorker_RaidEnemy`) and trade caravans (`IncidentWorker_TraderCaravanArrival`), spawning physical world entities that march across the planet.
- **Planetary Sensory & Radar Network:** Early-warning radar domes, lookout watchtowers, coastal sonar arrays, and multi-dish interferometry expanding identification horizons.
- **Lightweight Forward Outposts:** Caravan-deployable automated relays and signal beacons providing zero-TPS forward perimeter surveillance with dynamic material salvage.
- **Dynamic Inter-Faction Encounters:** Tile-bucketed $O(M)$ collision scans, algebraic background skirmishes, and persistent salvageable aftermath sites.
- **Orbital Logistics & Drifting Constellations:** Closed-form analytical Keplerian mechanics engine with $J_2$ oblateness nodal precession, persistent orbital station hubs, and 5 specialised satellite payload roles.
- **Campaign Creation Wizard & Mutability Framework:** First-class campaign wizard page (`Page_GeoscapeRules`), 4 distinct campaign profiles (**Fog of War**, **Planetary Surveillance**, **Hardcore Vigilance**, **Omniscient**), two-axis rule mutability, first-time mod adoption lifecycle, and forensic change ledgers.
- **Automated Landfall & Blind Touchdown:** Prevents global map cheese on uncharted globes by auto-selecting landing sectors or masking terrain telemetry until touchdown.
- **Optimistic Fog-of-War Pathfinding:** Friendly caravans assume unmapped sectors are passable until discovered, dynamically re-routing locally when meeting impassable terrain.
- **Dynamic Origin Resolution:** Automatically resolves genuine origin settlements or wilderness staging sites based on faction territory and terrain accessibility.
- **Real-Time WorldGrid Pathing:** Calculates terrain-based transit speeds and realistic ETAs (hours to days depending on biomes, roads, and mountains).
- **Tactical World Map Rendering:**
  - **IFF Identification:** Hostile raids are rendered in alert crimson red; friendly and neutral traders in cool cyan.
  - **Dual-Layer Marker:** Features a marching caravan silhouette underlay and an upright, high-contrast faction crest overlay.
  - **Dynamic Quad Alignment:** Rotated naturally to align with planetary North at any camera zoom.
- **Interactive Telemetry:** Click approaching incursions to inspect their originating faction, remaining distance, estimated arrival time, and threat points.
- **Save/Load Resilient:** Fully serialised via `IExposable` (`Scribe_Values`, `Scribe_References`, `Scribe_Deep`) so ongoing incursions persist across saves.
- **Configurable Mod Settings:** In-game options menu to toggle storyteller cooldown resets upon incursion arrival and adjust overland speed multipliers.

---

## Directory Layout

```text
Geoscape/
├── 1.7/
│   ├── Assemblies/           # Compiled .NET 10 (net10.0) C# binaries (Geoscape.dll)
│   ├── Defs/                 # XML Defs (Buildings, Research, WorldObjects, Letters)
│   └── Patches/              # XML Patches (PlanetLayerPatch)
├── About/
│   ├── About.xml             # RimWorld 1.7 mod metadata
│   └── Preview.png           # Workshop & mod-manager preview graphic
└── Source/                   # C# .NET 10 solution (targeting Waylith)
    ├── Geoscape.slnx         # Solution file
    ├── Geoscape/             # Mod core project
    │   ├── Geoscape.csproj   # TargetFramework: net10.0
    │   ├── Orbital/          # Drifting Keplerian satellite & station engine
    │   ├── Patches/          # Harmony runtime patches
    │   ├── Rules/            # Campaign rules, mutability framework, wizard & dialogs
    │   ├── Sensory/          # Radar coverage, listening posts, optimistic pathing & Haze
    │   └── World/            # Incursion simulation, collision detection & skirmishes
    └── Geoscape.Tests/       # Automated MSTest test suite (216 tests)
```

---

## Local Build & Deployment

### Prerequisites

- .NET 10 SDK (`dotnet --version`)
- RimWorld 1.7 (Waylith engine)

### Building

```powershell
npm run build:geoscape
```

### Deploying to RimWorld Mods Directory

**Windows (PowerShell):**

```powershell
npm run deploy:geoscape
# or directly:
./scripts/deploy-geoscape.ps1
```

**Linux / Zorin OS / Ubuntu / CachyOS (Bash):**

```bash
npm run deploy:geoscape:linux
# or directly:
./scripts/deploy-geoscape.sh
```

> **Default Linux Mods path:** `~/.local/share/Steam/steamapps/common/RimWorld/Mods/ProjectVigilance-Geoscape`  
> To specify a custom path: `./scripts/deploy-geoscape.sh -p /path/to/RimWorld/Mods`  
> To deploy XML/Defs without compiling C#: `./scripts/deploy-geoscape.sh --skip-build`

For answers to common compatibility and design questions, see the [Frequently Asked Questions (FAQ)](../docs/faq.md).
For the long-term technical specification of the sensory grid and radar domes, see [docs/modules/geoscape-and-radar.md](../docs/modules/geoscape-and-radar.md).
