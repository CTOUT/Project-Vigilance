# Rimworld X-COM Suite

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![RimWorld Version](https://img.shields.io/badge/RimWorld-1.5%2B-brightgreen.svg)](https://rimworldgame.com/)

A modular total systems overhaul bringing the strategic depth and tactical tension of the **X-COM** universe into **RimWorld**.

Rather than simply adding alien races and high-tier weapons, this project introduces the core macro systems of the franchise: **planetary radar networks**, **UFO tracking and air interception**, **oceanic operations (Terror from the Deep)**, **live alien containment**, and **global panic management**.

---

## The Three Core Pillars

1. **1994 UFO Defense / Enemy Unknown:** Planetary Geoscape simulation, radar coverage networks, alien mission intent (Scout, Harvest, Infiltration, Terror, Base Construction), multi-base outposts, and air interception.
2. **1995 Terror from the Deep:** Turning RimWorld's vast, non-traversable oceans (30–50% of the map) into an active operations theatre with Unidentified Submersible Objects (USOs), coastal sonar arrays, seabed recoveries, and diving operations.
3. **Modern Firaxis XCOM:** Strategic pacing via global invasion counters ("Avatar-style" doom clocks), Alien Dark Events, and tactical mission urgency with strict extraction timers.

For an in-depth breakdown of the design philosophy, see [docs/architecture/vision-and-pillars.md](docs/architecture/vision-and-pillars.md).

---

## Modular Add-On Architecture

To maintain performance, simplicity, and community compatibility, the project is structured as five decoupled modules:

```
┌────────────────────────────────────────────────────────┐
│      Module 5: Alien Arsenal & Factions (Theme)        │
└───────────────────────────┬────────────────────────────┘
                            │ requires
┌───────────────────────────▼────────────────────────────┐
│      Module 4: Invasion Campaign & Panic (Engine)      │
└───────────────┬────────────────────────┬───────────────┘
                │                        │
┌───────────────▼───────────┐ ┌──────────▼───────────────┐
│ Module 1: Geoscape & Radar│ │ Module 3: Containment    │
│ (Planetary Framework)     │ │ & Biology (Base Systems) │
└───────────────┬───────────┘ └──────────────────────────┘
                │ integrates
┌───────────────▼───────────┐
│ Module 2: Interception    │
│ & Rapid Response (Craft)  │
└───────────────────────────┘
```

- **[Module 1: Geoscape & Radar Grid](docs/modules/geoscape-and-radar.md)** — Planetary sensory coverage, radar domes, detection formulas, and sensory blind spots on the `WorldGrid` _(Standalone Framework)_.
- **[Module 2: Interception & Rapid Response](docs/modules/interception-and-rapid-response.md)** — Aircraft range, fuel, air-to-air dogfights, crash-site generation, and tactical dropship evacuations.
- **[Module 3: Alien Containment & Biology](docs/modules/containment-and-biology.md)** — Stasis pods, autopsy tables, live alien interrogations, and reverse-engineering _(Standalone Base System)_.
- **[Module 4: Invasion Campaign & Panic](docs/modules/campaign-and-panic.md)** — Council of Planetary Factions funding, regional panic meters, UFO invasion schedules, and the Alien Doom Clock.
- **[Module 5: Alien Arsenal & Factions](docs/architecture/modular-system.md)** — Thematic content pack: Sectoids, Mutons, Chryssalids, Aquatoids, plasma weapons, and armour tiers.

Additionally, see **[docs/modules/aquatic-operations.md](docs/modules/aquatic-operations.md)** for details on how oceanic and coastal operations are integrated across all modules.

---

## Documentation Index

- **[Vision & Architectural Pillars](docs/architecture/vision-and-pillars.md)**
- **[Modular System Architecture](docs/architecture/modular-system.md)**
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
