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
> - **Identification Ring Expansion:** Your inner identification horizon ($R_{id}$) surges outward from 10 rings (~40%) to 17 rings (~70%) and 22 rings (~92%) of your 24-ring envelope at a locked 250-tick sweep cadence, revealing attacking faction emblems and threat ratings much earlier.
> - **Fail-Safe Redundancy:** If a mortar strike or electrical fault destroys your primary dish, secondary dishes immediately maintain uninterrupted coverage.

**Q: Do hills and mountains affect my radars, watchtowers, and early warnings?**

> **Yes, through our "Vantage & Masking" terrain model:**
>
> - **Vantage Bonus (Origin Ground):** Building a Lookout Watchtower or forward outpost on high ground organically expands its visual reach: for Lookout Watchtowers, `Flat` gives baseline (8 rings), `Small Hills` gives +1 ring (9 rings), `Large Hills` gives +2 rings (10 rings), and `Mountainous` gives +3 rings (11 rings). Automated Outposts feature a 12-ring baseline (+1 to +2 vantage), and Signal Beacons feature a 6-ring baseline (+1 to +3 vantage).
> - **Terrain Masking (Hostile Ground):** Moving through `Mountainous` tiles delays classification. While an incursion hugs a mountain range, you always receive the **amber Contact blip** (speed and heading known), but attacking faction crests and headcount remain masked until the force exits into open foothills or plains.
> - **Spacer Tech Override:** Constructing the **SIGINT Decryption Console** adjacent to a **Phased Radar Array** bypasses mountain masking entirely through tactical radio decryption and advanced synthetic aperture processing.
> - **Zero Clutter:** Terrain never requires confusing line-of-sight raycasting or jagged radar shadow cutouts; the radar boundary remains a clean, intuitive holographic ring.

---

### 7. Cross-Platform Support & Operating Systems

**Q: Does Project Vigilance work on Linux, CachyOS, Steam Deck (SteamOS), or macOS?**

> **Yes, 100% natively supported.**
> Project Vigilance contains zero native C++ binaries or OS-specific dynamic libraries; it runs entirely on RimWorld 1.7's managed .NET runtime.
>
> For developers and playtesters building or linking locally from source:
> - **Linux / CachyOS / SteamOS:** Execute `./scripts/deploy-geoscape.sh` (or `npm run deploy:geoscape:linux`). It creates a native Unix symbolic link into `~/.local/share/Steam/steamapps/common/RimWorld/Mods/ProjectVigilance-Geoscape` and supports `--skip-build` and `--copy` modes.
> - **Windows:** Execute `./scripts/deploy-geoscape.ps1` (or `npm run deploy:geoscape`), which links via an NTFS Junction to `D:\Games\Steam\steamapps\common\RimWorld\Mods\ProjectVigilance-Geoscape`.

---

### 8. Campaign Creation, Mutability & Mod Adoption

**Q: What happens when I load a pre-existing save from before Milestone 1.3?**

> When loading an ongoing save that predates the Campaign Rules framework, the mod presents a non-destructive **First-Time Mod Adoption dialog** (`Dialog_GeoscapeModAdoption`):
>
> - **Planetary Surveillance (Default):** Retains known world geography while enabling incursion transit and radar envelopes.
> - **1-Day Review Window:** Grants a 60,000-tick grace period with a deferred notification letter allowing you to adjust or lock your campaign rules.
> - **Fog of War Challenge:** Opts into full planetary blackout for an immediate exploration challenge.
>
> All adopted rules are stored permanently in your `.rws` save file under `GeoscapeCampaignRules` (`WorldComponent`), ensuring independent rules per campaign without cross-save contamination.

**Q: Can I change rules during a campaign?**

> Rules follow a formal two-axis mutability model:
> - **Mutable rules** (e.g. notification letters, UI styles) can be modified at any time in Mod Options.
> - **Escalatable rules** (e.g. `visibilityMode`) can only progress forward (Fog of War $\to$ Planetary Surveillance $\to$ Omniscient) to maintain epistemic simulation integrity.
> - **Immutable rules** (e.g. sensory radii, nighttime biological rest, combat synchronisation) are locked at Day 0 to maintain campaign challenge integrity. Developer Mode allows temporary, audited session overrides (`allowUnsafeRuleTransitions`) that clear automatically on exit.
