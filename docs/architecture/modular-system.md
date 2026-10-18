# Modular System Architecture

## 1. Overview

To ensure maintainability, rapid iteration, and wide community adoption, the project is structured as a **suite of decoupled add-ons**.

Instead of a single monolithic mod that risks breaking with game updates or conflicting with other popular overhauls, each module has clearly defined boundaries. Several modules function as **standalone utilities** that benefit the wider RimWorld modding ecosystem even outside of an X-COM context.

---

## 2. Standalone vs. Dependent Classification

Every module in the suite is strictly classified as either **Standalone** (zero dependencies on other Vigilance mods) or **Dependent** (part of the overarching campaign layer):

| Module                                      | Identifier               | Classification | Suite Dependencies          | Standalone Use Case                                                                                                                              |
| :------------------------------------------ | :----------------------- | :------------: | :-------------------------- | :----------------------------------------------------------------------------------------------------------------------------------------------- |
| **Module 1: Geoscape & Radar Grid**         | `Vigilance.Geoscape`     | **STANDALONE** | **None**                    | Planetary fog of war, early-warning radar domes, satellite listening posts, tracking any airborne/orbital vectors (pirate shuttles, mechanoids). |
| **Module 2: Interception & Rapid Response** | `Vigilance.Interception` | **STANDALONE** | **None** _(Soft-dep on M1)_ | Air combat scrambles, aerial dogfights against incoming raids, rapid transport drops, and tactical extraction beacons.                           |
| **Module 3: Alien Containment & Biology**   | `Vigilance.Containment`  | **STANDALONE** | **None**                    | Stasis pods, live prisoner interrogation, surgical autopsies, and tech reverse-engineering for _any_ mod or faction.                             |
| **Aquatic Operations (TftD Layer)**         | `Vigilance.Aquatics`     | **STANDALONE** | **None** _(Hooks M1/M2)_    | Activates deep ocean tiles, coastal sonar arrays, seabed shipwreck recoveries, and diving gear for maritime playthroughs.                        |
| **Module 4: Invasion Campaign & Panic**     | `Vigilance.Campaign`     | **DEPENDENT**  | Modules 1, 2, 3             | The geopolitical doom clock, Council funding, regional panic meters, and alien strategic invasion AI.                                            |
| **Module 5: Alien Arsenal & Factions**      | `Vigilance.Theme.XCOM`   | **DEPENDENT**  | Module 4                    | Authentic X-Com alien races, plasma/sonic weapons, carapace/titan armours, and franchise audio/visual FX.                                        |

---

## 3. Core Standalone Engineering Principles

To guarantee that standalone modules remain truly independent, all code must adhere to three non-negotiable architectural rules:

### Rule 1: Zero Thematic Hardcoding in Frameworks

Framework modules (Modules 1, 2, 3, and Aquatics) **must never reference X-COM lore, alien race names, or campaign logic**.

- _Module 1 (Radar)_ tracks generic `WorldObject_MovingCraft` targets via XML `ModExtension` or `DetectionProfileDef`. A pirate gunship, mechanoid dropship, or custom faction transport can be tracked just as easily as a UFO.
- _Module 3 (Containment)_ exposes generic `CompProperties_Containment` and `RecipeDef_Autopsy` bills. Players can use it to contain and study vanilla mechanoids, mutated prisoners, or Anomaly entities without any alien content.

### Rule 2: Contract-Driven APIs & Soft Dependencies

Where modules integrate, they must do so via **loose coupling and dynamic event hooks**:

- Module 2 (Interception) operates out-of-the-box using manual scramble orders and vanilla quest locations. If Module 1 (Radar) is detected in the mod list, it automatically subscribes to radar vector alerts.
- Module 3 (Containment) checks dynamically for the **Anomaly DLC**: if active, it binds to native entity study platforms; if absent, it executes its standalone holding-pod logic.

### Rule 3: Isolated Packaging & Distribution

Each standalone module has its own independent Steam Workshop / GitHub release pipeline:

- Distinct `packageId` and self-contained `About.xml`.
- No shared monolithic assemblies; each module distributes its own clean `.dll`.
- Independent settings menus allowing players to tune features without the wider suite.

---

## 4. Dependency Matrix

```mermaid
graph TD
    subgraph Standalone Frameworks [Standalone Layer - Zero Inter-Dependencies]
        M1[Module 1: Geoscape & Radar Grid<br><b>Vigilance.Geoscape</b>]
        M2[Module 2: Interception & Rapid Response<br><b>Vigilance.Interception</b>]
        M3[Module 3: Alien Containment & Biology<br><b>Vigilance.Containment</b>]
        AQ[Aquatic Operations & Sonar<br><b>Vigilance.Aquatics</b>]
    end

    subgraph Campaign Layer [Dependent Overhaul Layer]
        M4[Module 4: Invasion Campaign & Panic<br><b>Vigilance.Campaign</b>]
        M5[Module 5: Alien Arsenal & Factions<br><b>Vigilance.Theme.XCOM</b>]
    end

    M1 -.->|Optional Target Telemetry| M2
    M1 --> M4
    M2 --> M4
    M3 --> M4
    AQ -.->|Oceanic Sonar Data| M1
    M4 --> M5
```

---

## 5. Module Breakdown

### Module 1: Geoscape & Radar Grid (`Rimworld-Geoscape`)

- **Role:** Planetary sensory framework.
- **Standalone Utility:** High. Any mod requiring radar, early warning systems, planetary fog of war, or hidden world locations can use this without any alien content.
- **Core Systems:**
  - `WorldObjectComp_Radar`: Attached to settlements or standalone world objects (listening posts, satellites).
  - Planetary sensory bubbles: Calculates detection range on the `WorldGrid`.
  - Detection probability algorithms based on craft altitude, size, and stealth signatures.
  - Hidden world objects / dynamic moving targets (`UFO_WorldObject`).
  - Aquatic sonar support for oceanic tiles (Terror from the Deep layer).

### Module 2: Interception & Rapid Response (`Rimworld-Interception`)

- **Role:** Aerial and orbital interception, long-range transport deployment.
- **Dependencies:** Soft dependency on Module 1 (for target vectors).
- **Standalone Utility:** Medium-High. Can be used for general military air-defence and rapid deployment.
- **Core Systems:**
  - Craft range, fuel consumption, speed, and armament loadouts.
  - Interception combat resolution (simulated tactical engagement dialogue or automated resolution).
  - Downed / crash-site generator (spawning temporary `SiteDef` instances with salvageable wreckage).
  - Deployment and extraction drop-zones for tactical response teams.

### Module 3: Alien Containment & Biology (`Rimworld-Containment`)

- **Role:** Live entity capture, alien autopsies, and reverse-engineering.
- **Dependencies:** None (Fully standalone).
- **Standalone Utility:** High. Expands medical research and prisoner interactions for any sci-fi or creature mod.
- **Core Systems:**
  - Stasis holding pods and secure containment cells.
  - Interrogation bills and alien autopsy work tables.
  - Knowledge discovery trees and reverse-engineering data points (synergises with vanilla 1.5 / Anomaly study mechanics if present, but functions independently).

### Module 4: The Invasion Campaign & Panic (`Rimworld-Campaign`)

- **Role:** The overarching strategic game loop, global doom clock, and geopolitics.
- **Dependencies:** Requires Module 1, Module 2, and Module 3.
- **Core Systems:**
  - Planetary faction funding and Council evaluations (monthly performance reviews).
  - Regional panic metrics per faction/territory.
  - Alien strategic AI: Schedules scouting runs, harvest waves, terror raids, and alien base construction.
  - Escalation clock ("Avatar-style" invasion progress counter) with counter-operations.

### Module 5: Alien Arsenal & Factions (`Rimworld-XCOM-Theme`)

- **Role:** Pure thematic content pack.
- **Dependencies:** Requires Module 4.
- **Core Systems:**
  - Faction definitions: Ethereal/Alien Invaders, Advent/Infiltrators, Deep Ones/Aquatic invaders.
  - Xenobiology: Sectoids, Floaters, Chryssalids, Mutons, Aquatoids, Gillmen, Tentaculats.
  - Arsenal: Laser, Plasma, and Sonic weaponry.
  - Armour tiers: Kevlar, Carapace, Titan, Diving/Aqua-suits, Power Armour.
  - Audio and visual assets faithful to the original series.
