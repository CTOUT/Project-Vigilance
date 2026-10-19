# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Comprehensive Community FAQ ([`docs/faq.md`](docs/faq.md)) addressing modded faction compatibility (Vanilla Factions Expanded, Medieval Overhaul), tactical combat overhauls (Combat Extended, CAI 5000), Rim War architectural comparisons, and 1.6 backport evaluations.
- Steam Workshop Collection badges and cross-links across project documentation.
- Architectural specifications for 3-tier visibility modes (Planetary Surveillance, Classic Haze of War, Omniscient) and 5-tier contact intelligence progression in [`docs/modules/geoscape-and-radar.md`](docs/modules/geoscape-and-radar.md).
- Decoupled internal namespace design rules (`World`, `Sensory`, `Tactical`) in [`docs/architecture/modular-system.md`](docs/architecture/modular-system.md) to guarantee clean future standalone extraction.
- Dynamic sensory tracking fidelity distinction: discrete 250-tick sweep jumps for radar/sonar blips vs smooth 60 FPS spherical interpolation (`Vector3.Slerp`) for direct optical vision in [`docs/modules/geoscape-and-radar.md`](docs/modules/geoscape-and-radar.md).
- Planetary simulation lifecycle optimisations: complete off-world render skipping (`WorldRendererUtility.WorldRenderedNow == false`), peacetime sweep early-exit when zero incursions are active, stationary sensor caching, and Omniscient bypass.
- Baseline Scout Horizon lookout system providing line-of-sight early warning for colonies (~5 hexes) and caravans (~3 hexes).
- Early Warning Letter dispatching (`LetterDefOf.ThreatSmall` / `ThreatBig`) upon hostile sensor contact.

### Changed

- Scoped CI spellcheck workflow (`.github/workflows/spellcheck.yml`) and `cspell.json` to target documentation and prevent false positives on C# engine symbols and scripts.

## [0.1.0] - 2026-10-18

### Added

- **Physical Incursion Engine:** Replaced instantaneous map-edge pawn materialisation with real-time planetary transit across the `WorldGrid` for walk-in enemy raids (`IncidentWorker_RaidEnemy`) and friendly trade caravans (`IncidentWorker_TraderCaravanArrival`).
- **Dynamic Origin Resolution:** Added `IncursionOriginResolver` to locate nearest faction settlements or establish procedural wilderness staging sites based on terrain accessibility.
- **Realistic Transit Simulation:** Implemented `IncursionMovementHandler` computing tile-by-tile pathing, biome travel costs, and live ETAs (hours to days depending on terrain).
- **Tactical Map Rendering:** Added composite dual-layer world rendering in `WorldObject_Incursion`:
  - Quad alignment fixed to point naturally toward planetary North at all camera zoom levels.
  - Distinct IFF colouring: Alert Crimson Red for hostile raids and Cool Cyan for friendly/neutral caravans.
  - Caravan marching silhouette underlay combined with an upright, prominent faction crest overlay.
- **Interactive Incursion Telemetry:** Added custom world inspect strings displaying originating faction, remaining distance, live ETA, and combat threat rating.
- **Storyteller & Cooldown Integration:** Added `IncursionArrivalManager` to trigger incidents upon physical hex arrival, with configurable options to reset `lastThreatBigTick` to prevent storyteller threat stacking.
- **Save/Load State Resilience:** Built full Scribe serialization (`Scribe_Values`, `Scribe_References`, `Scribe_Deep`) so ongoing incursions persist across game save cycles.
- **In-Game Settings Dialogue:** Added `GeoscapeMod` and `GeoscapeSettings` allowing players to configure threat cooldown resets and transit speed multipliers.
- **Architectural Specifications & Master Vision:** Comprehensive technical documentation for Geoscape & Radar, Interception, Containment & Biology, Invasion Campaign, Aquatic Operations (TftD), and DLC integrations.
