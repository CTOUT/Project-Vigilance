# Project Vigilance: RimWorld X-COM Suite

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![RimWorld Version](<https://img.shields.io/badge/RimWorld-1.7%20(Waylith)-brightgreen.svg>)](https://rimworldgame.com/)
[![Runtime](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Engine](https://img.shields.io/badge/Engine-Waylith%20%2F%20BRUTAL-red.svg)](https://github.com/ahwootech/BRUTAL)

A modular total systems overhaul bringing the strategic depth and tactical tension of the **X-COM** universe into **RimWorld** (targeting **RimWorld 1.7 - Waylith** on the **BRUTAL** framework).

Rather than simply adding alien races and high-tier weapons, this project introduces the core macro systems of the franchise: **planetary radar networks**, **UFO tracking and air interception**, **oceanic operations (Terror from the Deep)**, **live alien containment**, and **global panic management**.

---

## 🚀 Active Playtest: Project Vigilance - Overland Incursions (v0.1.0)

The first standalone module of the suite—**Overland Incursions**—is implemented and ready for community playtesting in the [`Geoscape/`](Geoscape/) directory!

### What Does It Do?

In vanilla RimWorld, raids and trade caravans instantaneously materialise at the boundary of your map out of thin air. **Overland Incursions** overhauls this completely:

- **Physical WorldGrid Transit:** When a storyteller incident fires (e.g. `RaidEnemy` or `TraderCaravanArrival`), the party spawns at a physical origin point on the planet (a nearby enemy base or wilderness staging tile) and physically marches across the world map to your colony.
- **Realistic ETAs & Tactical Timing:** March speeds are computed dynamically using world pathfinding, distance, and terrain biome costs. Raids can take anywhere from 12 hours to several days to arrive.
- **Tactical World Map Rendering:**
  - Distinct **IFF Colours:** Hostile forces are marked in alert crimson red, while friendly and neutral trade caravans are rendered in cool cyan.
  - Directional quad alignment pointing naturally toward planetary North.
  - Caravan marching silhouette underlay combined with an upright, prominent faction crest overlay.
- **Interactive Inspection:** Click on approaching world incursions to view real-time telemetry: originating faction, remaining distance, estimated time of arrival (ETA), and combat threat points.
- **Zero Tech Prerequisites:** Completely era-agnostic and standalone. Can be dropped into any tribal, medieval, or industrial playthrough with zero alien content.
- **Configurable Settings:** In-game options to synchronise storyteller threat timers upon arrival and adjust transit speeds.

### How to Install for Testing

1. Ensure **RimWorld 1.7 (Waylith)** and **Harmony** are installed.
2. Download or clone this repository.
3. Copy the [`Geoscape/`](Geoscape/) folder into your RimWorld `Mods/` directory (e.g. `RimWorld/Mods/ProjectVigilance-OverlandIncursions`).
4. Activate **Harmony** first, followed by **Project Vigilance - Overland Incursions** in your mod manager.

💬 **Join the Testing Discussion:** Have feedback, balance notes, or ideas? Join our [GitHub Community Discussions](https://github.com/CTOUT/Project-Vigilance/discussions) or report issues via [GitHub Issues](https://github.com/CTOUT/Project-Vigilance/issues).

---

## The Three Core Pillars

1. **1994 UFO Defense / Enemy Unknown:** Planetary Geoscape simulation, radar coverage networks, alien mission intent (Scout, Harvest, Infiltration, Terror, Base Construction), multi-base outposts, and air interception.
2. **1995 Terror from the Deep:** Turning RimWorld's vast, non-traversable oceans (30–50% of the map) into an active operations theatre with Unidentified Submersible Objects (USOs), coastal sonar arrays, seabed recoveries, and diving operations.
3. **Modern Firaxis XCOM:** Strategic pacing via global invasion counters ("Avatar-style" doom clocks), Alien Dark Events, and tactical mission urgency with strict extraction timers.

For an in-depth breakdown of the design philosophy, see [docs/architecture/vision-and-pillars.md](docs/architecture/vision-and-pillars.md).

---

## Modular Add-On Architecture

To maintain performance, simplicity, and community compatibility, the project is structured as decoupled modules. **Framework modules are strictly standalone**—they have zero dependencies on other Project Vigilance mods and can be used in any vanilla or modded playthrough without alien content.

### Standalone Frameworks (Zero Suite Dependencies)

- **[Module 1: Geoscape & Radar Grid (`Vigilance.Geoscape`)](docs/modules/geoscape-and-radar.md)** `[STANDALONE]` — Planetary sensory coverage, radar domes, listening posts, detection formulas, and sensory blind spots on the `WorldGrid`. Tracks any airborne or orbital raid vectors (pirates, mechanoids, custom factions).
- **[Module 2: Interception & Rapid Response (`Vigilance.Interception`)](docs/modules/interception-and-rapid-response.md)** `[STANDALONE]` — Aircraft range, fuel, air-to-air dogfights, crash-site generation, and tactical dropship evacuations.
- **[Module 3: Alien Containment & Biology (`Vigilance.Containment`)](docs/modules/containment-and-biology.md)** `[STANDALONE]` — Stasis pods, autopsy tables, live prisoner interrogations, and reverse-engineering for any creature or faction.
- **[Aquatic Operations & Sonar (`Vigilance.Aquatics`)](docs/modules/aquatic-operations.md)** `[STANDALONE]` — Deep ocean activation, coastal sonar arrays, seabed shipwreck recoveries, and diving gear for maritime operations.

### Campaign Overhaul (Dependent Modules)

- **[Module 4: Invasion Campaign & Panic (`Vigilance.Campaign`)](docs/modules/campaign-and-panic.md)** `[DEPENDENT]` — Requires Modules 1, 2, and 3. Council of Planetary Factions funding, regional panic meters, UFO invasion schedules, and the Alien Doom Clock.
- **[Module 5: Alien Arsenal & Factions (`Vigilance.Theme.XCOM`)](docs/architecture/modular-system.md)** `[DEPENDENT]` — Requires Module 4. Thematic content pack: Sectoids, Mutons, Chryssalids, Aquatoids, plasma weapons, and armour tiers.

---

## Documentation Index

- **[Vision & Architectural Pillars](docs/architecture/vision-and-pillars.md)**
- **[Modular System Architecture](docs/architecture/modular-system.md)**
- **[DLC Integration Architecture](docs/architecture/dlc-integrations.md)**
- **[Module 1 Specification: Geoscape & Radar Grid](docs/modules/geoscape-and-radar.md)**
- **[Module 2 Specification: Interception & Rapid Response](docs/modules/interception-and-rapid-response.md)**
- **[Module 3 Specification: Alien Containment & Biology](docs/modules/containment-and-biology.md)**
- **[Module 4 Specification: The Invasion Campaign & Panic](docs/modules/campaign-and-panic.md)**
- **[Aquatic Operations & Terror from the Deep Integration](docs/modules/aquatic-operations.md)**
- **[Implementation Roadmap & TODO](TODO.md)**
- **[Changelog](CHANGELOG.md)**

---

## Repository Standards & Tooling

This repository enforces formatting and spellchecking standards:

```bash
# Format Markdown, JSON, and YAML files
npm run format

# Spellcheck project documentation (en-GB)
npm run spellcheck
```

---

## Licence

Distributed under the [MIT Licence](LICENSE).
