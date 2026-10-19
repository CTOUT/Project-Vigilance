# Project Vigilance: Overland Incursions (Geoscape Framework)

This directory hosts the C# source code, XML Defs, and compiled mod package for **Project Vigilance: Overland Incursions** (Package ID: `ProjectVigilance.Geoscape`).

[![Steam Workshop](https://img.shields.io/badge/Steam_Workshop-3822378226-1b2838?logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3822378226)
[![Steam Collection](https://img.shields.io/badge/Steam_Collection-Project_Vigilance-1b2838?logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3822385427)
[![GitHub Release](https://img.shields.io/badge/Release-v0.1.0-238636?logo=github)](https://github.com/CTOUT/Project-Vigilance/releases/tag/v0.1.0)

This is a **standalone, zero-dependency framework mod** designed for RimWorld 1.7 (Waylith) and `.NET 10`. It replaces instantaneous map-edge teleportation with physical overland transit across the global planetary grid.

---

## Current Features (v0.1.0 Playtest)

- **Physical Incursion Engine:** Intercepts walk-in raids (`IncidentWorker_RaidEnemy`) and trade caravans (`IncidentWorker_TraderCaravanArrival`), spawning physical world entities that march across the planet.
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

```
Geoscape/
├── 1.7/
│   ├── Assemblies/           # Compiled .NET 10 (net10.0) C# binaries (Geoscape.dll)
│   ├── Defs/                 # XML WorldObjectDefs and ThingDefs
│   └── Textures/             # Tactical UI markers and icons
├── About/
│   ├── About.xml             # RimWorld 1.7 mod metadata
│   └── Preview.png           # Workshop & mod-manager preview graphic
└── Source/                   # C# .NET 10 solution (targeting Waylith)
    ├── Geoscape.slnx         # Modern Visual Studio solution
    └── Geoscape/
        ├── Geoscape.csproj   # TargetFramework: net10.0
        ├── Patches/          # Harmony patches (RaidEnemy, TraderCaravan)
        ├── Settings/         # In-game ModSettings & UI dialogue
        ├── UI/               # Telemetry and inspect pane string builders
        └── World/            # WorldObject_Incursion, pathing, and arrival management
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

```powershell
npm run deploy:geoscape
```

For answers to common compatibility and design questions, see the [Frequently Asked Questions (FAQ)](../docs/faq.md).
For the long-term technical specification of the sensory grid and radar domes, see [docs/modules/geoscape-and-radar.md](../docs/modules/geoscape-and-radar.md).
