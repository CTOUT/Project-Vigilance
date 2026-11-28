# Project Vigilance: RimWorld X-COM Suite

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![RimWorld Version](<https://img.shields.io/badge/RimWorld-1.7%20(Waylith)-brightgreen.svg>)](https://rimworldgame.com/)
[![Runtime](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Engine](https://img.shields.io/badge/Engine-Waylith%20%2F%20BRUTAL-red.svg)](https://github.com/ahwootech/BRUTAL)
[![Steam Workshop](https://img.shields.io/badge/Steam_Workshop-3822378226-1b2838?logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3822378226)
[![Steam Collection](https://img.shields.io/badge/Steam_Collection-Project_Vigilance-1b2838?logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3822385427)

A modular total systems overhaul bringing the strategic depth and tactical tension of the **X-COM** universe into **RimWorld** (targeting **RimWorld 1.7 - Waylith** on the **BRUTAL** framework).

Rather than simply adding alien races and high-tier weapons, this project introduces the core macro systems of the franchise: **planetary radar networks**, **UFO tracking and air interception**, **oceanic operations (Terror from the Deep)**, **live alien containment**, and **global panic management**.

---

## 🚀 Active Playtest: Project Vigilance Modular Suite (v0.5.0)

[![Steam Workshop](https://img.shields.io/badge/Subscribe_on-Steam_Workshop-1b2838?style=for-the-badge&logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3822378226)
[![Steam Collection](https://img.shields.io/badge/View_Official-Collection-1b2838?style=for-the-badge&logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3822385427)
[![GitHub Release](https://img.shields.io/badge/Direct_Download-GitHub_Release-238636?style=for-the-badge&logo=github)](https://github.com/CTOUT/Project-Vigilance/releases/tag/v0.5.0)

The decoupled 5-package suite (**Core**, **Geoscape & Early Warning**, **Orbital**, **Aquatics**, **Thermal**) is live and ready for community playtesting! Subscribe directly on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3822378226), view the full [Steam Collection](https://steamcommunity.com/sharedfiles/filedetails/?id=3822385427), or grab the [v0.5.0 Release](https://github.com/CTOUT/Project-Vigilance/releases/tag/v0.5.0).

### What Does It Do?

In vanilla RimWorld, raids, trade caravans, and roadside encounters instantaneously materialise at the boundary of your map out of thin air. **Overland Incursions** overhauls this completely:

- **Grounded Storyteller Raids, Trade Caravans, Meetings & Ambushes:** Intercepts walk-in raids (`RaidEnemy`), neutral arrivals (`TraderCaravanArrival`, `TravelerGroup`, `VisitorGroup`), caravan meetings (`CaravanMeeting`), extortion demands (`CaravanDemand`), and enemy faction ambushes (`Ambush_EnemyFaction`), spawning persistent `WorldObject_Incursion` entities with realistic origin resolution, shroud staging, non-blocking sighting alerts, and post-retreat hostile pursuit on the Geoscape.
- **Automated Forward Outposts & Radar Grid:** Extends planetary surveillance with multi-dish radar interferometry, SIGINT decryption consoles, and caravan-deployable **Automated Listening Posts** and **Signal Beacons** (with experimental coastal sonar gated to DevMode).
- **Drifting Orbital Constellations:** Simulates satellites along analytical Keplerian orbits with $J_2$ oblateness nodal precession, persistent high-orbit stations, dual-layer Surface/Orbit rendering, and storyteller-calibrated trade passes via Comms Console drop pods (non-standard military and reconnaissance roles gated to DevMode).
- **Campaign Rules Wizard & Fog-of-War Governance:** Provides a native campaign creation wizard (`Page_GeoscapeRules`) with 4 profiles (**Fog of War**, **Planetary Surveillance**, **Hardcore Vigilance**, **Omniscient**), automated landfall on uncharted globes, optimistic fog-of-war caravan routing, two-axis rule mutability, and HMAC-SHA256-verified savegame integrity ledgers.
- **Realistic ETAs & Tactical Timing:** March speeds are computed dynamically using world pathfinding, distance, and terrain biome costs. Raids can take anywhere from 12 hours to several days to arrive.
- **Tactical World Map Rendering:**
  - Distinct **IFF Colours:** Hostile forces are marked in alert crimson red, while friendly and neutral trade caravans are rendered in cool cyan.
  - Directional quad alignment pointing naturally toward planetary North.
  - Caravan marching silhouette underlay combined with an upright, prominent faction crest overlay.
  - Holographic 3D radar perimeter/honeycomb globe overlays and anomalous landmark/site masking.
- **Interactive Inspection:** Click on approaching world incursions to view real-time telemetry: originating faction, remaining distance, estimated time of arrival (ETA), and combat threat points.
- **Zero Tech Prerequisites:** Completely era-agnostic and standalone. Can be dropped into any tribal, medieval, or industrial playthrough with zero alien content.
- **Custom Visual Assets & Faction Livery Tinting:** Dedicated orthographic sprites for colony sensory buildings (`LookoutTower`, `RadarDomeSmall`, 4-way `PhasedArrayLarge` & `SigintConsole`, `SonarStation`) and neutral `128×128 RGBA32` grayscale world markers that cleanly inherit runtime faction colours.
- **Programmatic Thermal Integrity (`ThermalIntegrityPostDefProcessor`):** Eliminates burning steel/gold/silver/plasteel/uranium, sensory radar dishes, all-metal machinery, archotech parts, and Odyssey heavy bridges/substructures at startup while preserving intentional chemical hazards (batteries, wood bridges, Bioferrite gravship hulls).
- **Configurable Settings:** Unified multi-module tabbed Mod Settings UI (`Project Vigilance`) with a live tactical CRT radar scope preview, WCAG 2.2 AA curated & colourblind themes, storyteller threat cooldown synchronisation, and transit speed multipliers.

### How to Install for Testing

1. Ensure **RimWorld 1.7 (Waylith)** and **Harmony** are installed.
2. Download or clone this repository.
3. Build and link all 5 modular packages (`Core`, `Geoscape`, `Orbital`, `Aquatics`, `Thermal`) into your RimWorld `Mods/` directory using the automated deployment tooling:
   - **Windows (PowerShell):** `npm run deploy:geoscape` (or `./scripts/deploy-geoscape.ps1`)
   - **Linux / Zorin / Ubuntu (Bash):** `npm run deploy:geoscape:linux` (or `./scripts/deploy-geoscape.sh`)
4. Activate **Harmony** first, followed by **Project Vigilance - Core Framework**, **Geoscape & Early Warning**, and any optional modules (**Orbital**, **Aquatics**, **Thermal**) in your mod manager.

💬 **Join the Testing Discussion:** Have feedback, balance notes, or ideas? Join our [GitHub Community Discussions](https://github.com/CTOUT/Project-Vigilance/discussions) or report issues via [GitHub Issues](https://github.com/CTOUT/Project-Vigilance/issues).

---

## The Three Core Pillars

1. **1994 UFO Defense / Enemy Unknown:** Planetary Geoscape simulation, radar coverage networks, alien mission intent (Scout, Harvest, Infiltration, Terror, Base Construction), multi-base outposts, and air interception.
2. **1995 Terror from the Deep:** Turning RimWorld's vast, non-traversable oceans (30–50% of the map) into an active operations theatre with Unidentified Submersible Objects (USOs), coastal sonar arrays, seabed recoveries, and diving operations.
3. **Modern Firaxis XCOM:** Strategic pacing via global invasion counters ("Avatar-style" doom clocks), Alien Dark Events, and tactical mission urgency with strict extraction timers.

### Core Design Practice: "You Don't Know What You Don't Know"

> _"To know, you need to find out — investigate, identify, research, explore."_  
> Information is never given for free. Threat detection, enemy force composition, technology trees, and planetary operations are cloaked in fog. You must actively dispatch scouts, capture specimens, and build radar networks to uncover the truth.

For an in-depth breakdown of the design philosophy, see [docs/architecture/vision-and-pillars.md](docs/architecture/vision-and-pillars.md).

---

## Modular Add-On Architecture

To maintain performance, simplicity, and community compatibility, the project is structured as decoupled modules. **Framework modules are strictly standalone**—they have zero dependencies on other Project Vigilance mods and can be used in any vanilla or modded playthrough without alien content.

### Standalone Frameworks (Zero Suite Dependencies)

- **[Module 1: Geoscape & Radar Grid (`Vigilance.Geoscape`)](docs/modules/geoscape-and-radar.md)** `[STANDALONE]` — Planetary sensory coverage, radar domes, listening posts, detection formulas, and sensory blind spots on the `WorldGrid`. Tracks any airborne or orbital raid vectors (pirates, mechanoids, custom factions).
- **[Module 2: Interception & Rapid Response (`Vigilance.Interception`)](docs/modules/interception-and-rapid-response.md)** `[STANDALONE]` — Aircraft range, fuel, air-to-air dogfights, crash-site generation, and tactical dropship evacuations.
- **[Module 3: Alien Containment & Biology (`Vigilance.Containment`)](docs/modules/containment-and-biology.md)** `[STANDALONE]` — Stasis pods, autopsy tables, live prisoner interrogations, and reverse-engineering for any creature or faction.
- **[Aquatic Operations & Sonar (`Vigilance.Aquatics`)](docs/modules/aquatic-operations.md)** `[STANDALONE]` — Deep ocean activation, coastal sonar arrays, seabed shipwreck recoveries, and diving gear for maritime operations.
- **[Tactical Battlescape & Sensory (`Vigilance.Battlescape`)](docs/modules/battlescape-and-tactical-combat.md)** `[STANDALONE]` — 3-mode tactical fog of war (Obsidian Shroud, Slate Memory Haze, Active Vision), directional vision cones, acoustic/scent tracking for pawns, war animals, and mechanoids, and guerrilla pod combat AI.

### Campaign Overhaul (Dependent Modules)

- **[Module 4: Invasion Campaign & Panic (`Vigilance.Campaign`)](docs/modules/campaign-and-panic.md)** `[DEPENDENT]` — Requires Modules 1, 2, and 3. Council of Planetary Factions funding, regional panic meters, UFO invasion schedules, and the Alien Doom Clock.
- **[Module 5: Alien Arsenal & Factions (`Vigilance.Theme.XCOM`)](docs/architecture/modular-system.md)** `[DEPENDENT]` — Requires Module 4. Thematic content pack: Sectoids, Mutons, Chryssalids, Aquatoids, plasma weapons, and armour tiers.

---

## Documentation Index

### Architectural Blueprints & Design Specifications

- **[Vision & Architectural Pillars](docs/architecture/vision-and-pillars.md)**
- **[Modular System Architecture](docs/architecture/modular-system.md)**
- **[DLC Integration Architecture](docs/architecture/dlc-integrations.md)**
- **[Design Blueprint: Campaign Rules, Mutability Framework & Campaign Identity](docs/archive/Campaign_Rules_And_Mutability_Blueprint.md)**
- **[Design Blueprint: Orbital Logistics & Drifting Satellite Infrastructure](docs/archive/Orbital_Logistics_And_Drifting_Satellites_Blueprint.md)**
- **[Design Blueprint: Classic X-COM Geoscape Experience](docs/archive/Classic_XCom_Geoscape_Blueprint.md)**
- **[Interactive Mockup: Geoscape Command Deck Prototype](docs/archive/Mockups/geoscape_command_deck_mockup.html)**

### Module Specifications

- **[Module 1 Specification: Geoscape & Radar Grid](docs/modules/geoscape-and-radar.md)**
- **[Module 2 Specification: Interception & Rapid Response](docs/modules/interception-and-rapid-response.md)**
- **[Module 3 Specification: Alien Containment & Biology](docs/modules/containment-and-biology.md)**
- **[Tactical Battlescape, Sensory & Fog of War Specification](docs/modules/battlescape-and-tactical-combat.md)**
- **[Module 4 Specification: The Invasion Campaign & Panic](docs/modules/campaign-and-panic.md)**
- **[Aquatic Operations & Terror from the Deep Integration](docs/modules/aquatic-operations.md)**

### Testing, Roadmap & Operations

- **[Milestone 1.5 Quality Assurance & Playtest Plan (TC-01 – TC-10)](docs/milestone_1_5_test_plan.md)**
- **[Comprehensive Security, Accessibility & Architecture Audit (2026-11-26)](docs/audits/2026-11-26-comprehensive-audit.md)**
- **[Authoritative Release & Publishing Process](docs/release-process.md)**
- **[Frequently Asked Questions (FAQ)](docs/faq.md)**
- **[Implementation Roadmap & TODO](TODO.md)**
- **[Changelog](CHANGELOG.md)**

---

## Repository Standards & Tooling

This repository provides cross-platform compilation, automated deployment, formatting, and spellchecking:

```bash
# Compile solution (.NET 10 Release)
npm run build:geoscape
# or: dotnet build Geoscape/Source/Geoscape.slnx -c Release

# Run automated test suite (MSTest, 511 tests)
dotnet test Geoscape/Source/Geoscape.slnx -c Release

# Deploy mod to local RimWorld Mods directory
npm run deploy:geoscape         # Windows (PowerShell / NTFS Junction)
npm run deploy:geoscape:linux   # Linux / Zorin / Ubuntu (Bash / Symlink)

# Format project files (Prettier)
npm run format

# Spellcheck project documentation (en-GB)
npm run spellcheck
```

---

## Licence

Distributed under the [MIT Licence](LICENSE).
