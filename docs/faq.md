# Frequently Asked Questions (FAQ)

Answers to common community inquiries regarding compatibility, design philosophy, and version support for **Project Vigilance: Overland Incursions**.

---

### 1. Compatibility with Modded Factions & Custom Raids

**Q: Does this work with modded factions like Vanilla Factions Expanded, Medieval Overhaul, or custom races?**

> **Yes, 100% compatible.**
> Any modded faction that utilizes the standard RimWorld walk-in raid incident (`IncidentWorker_RaidEnemy`) or inherits from it is automatically intercepted. The engine locates an appropriate origin settlement or staging camp for that faction and routes them overland across the planetary `WorldGrid`.

**Q: How does it handle non-walk-in raids (Drop-pods, Sieges, Orbital Drops)?**

> Drop-pod assaults, orbital drops, and mechanoid cluster spawns intentionally bypass overland marching and continue to arrive at your colony with their normal suddenness.

**Q: Does it work if my colony is on an isolated island or enclosed by mountains?**

> Yes. The pathing engine features an automated failsafe: if standard ground pathfinding cannot find a valid overland route to your colony, the transit delay is bypassed and the incident executes immediately, ensuring your game loop never hangs.

---

### 2. Combat Overhauls & Tactical Map AI

**Q: Is it compatible with Combat Extended (CE) or CAI 5000?**

> **Yes.** Project Vigilance: Overland Incursions operates strictly on the planetary `WorldGrid` layer before the tactical map incident occurs. It does not alter pawn generation, weapon loadouts, ballistics, or in-map combat AI. Once the incursion arrives at the map tile, the vanilla or modded incident executes normally.

---

### 3. Comparison with Rim War & World Domination

**Q: How does this mod compare to Rim War or World Domination?**

> While both mods involve physical movement on the world map, their architecture and design goals are fundamentally different:
>
> | Feature                     | Rim War / World Domination                                      | Project Vigilance: Overland Incursions                                       |
> | :-------------------------- | :-------------------------------------------------------------- | :--------------------------------------------------------------------------- |
> | **Storyteller Integration** | Replaces the Storyteller with an autonomous AI faction economy. | **Storyteller-Native:** Retains Cassandra, Randy, or any modded storyteller. |
> | **Performance & TPS**       | High overhead from simulating global settlement economies.      | **Zero TPS Impact:** Only simulates active incidents in transit.             |
> | **Scope**                   | Complete 4X empire strategy overhaul.                           | Focused military logistics, early warning, and tactical timing.              |
> | **Suite Synergy**           | Standalone monolithic 4X mod.                                   | Foundational movement framework for the wider Project Vigilance X-COM suite. |

---

### 4. Save Game Safety

**Q: Can I add this mod to an ongoing save game?**

> **Yes.** You can safely add Overland Incursions to any existing save at any time.

**Q: Can I safely remove this mod from an existing save?**

> **Yes.** If removed mid-game, any in-flight planetary incursions will safely despawn upon save load without corrupting your world or colony.

---

### 5. Version Support & Backports

**Q: Why does this mod target RimWorld 1.7 (Waylith)? Is there a 1.6 version?**

> The primary target for Project Vigilance is **RimWorld 1.7 (Waylith Engine)** running on **.NET 10**, leveraging the modern runtime for zero-allocation pathfinding and high TPS performance.
>
> A **1.6 backport** targeting the legacy .NET Framework runtime is currently under evaluation based on community interest. Progress on backport feasibility can be tracked in [TODO.md](../TODO.md).

---

### 6. Radar Infrastructure & Multi-Dish Interferometry

**Q: Why doesn't building more radars in my settlement increase my maximum detection range?**

> **Detection radius is constrained primarily by transmission power and planetary line-of-sight limitations; additional dishes improve resolution rather than raw detection distance.**
>
> On a spherical planet, ground radar cannot see through planetary curvature without orbital relays or forward outposts. Instead, clustering secondary dishes functions as an **interferometer** (synthetic aperture array), which expands your angular resolution:
>
> - **Identification Ring Expansion:** Your inner identification horizon ($R_{id}$) surges outward from 10 rings (~40%) to 17 rings (~70%) and 22 rings (~92%) of your 24-ring envelope, revealing attacking faction emblems and threat ratings much earlier.
> - **Faster Pulse Cadence:** Sweep intervals accelerate from `250 ticks` (~4.1s) down to `150 ticks` and `75 ticks`.
> - **Fail-Safe Redundancy:** If a mortar strike or electrical fault destroys your primary dish, secondary dishes immediately maintain uninterrupted coverage.
