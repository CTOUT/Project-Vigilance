# Vision & Architectural Pillars

## 1. Executive Summary

The **Rimworld X-COM Suite** is an ambitious, modular overhaul designed to integrate the strategic and tactical gameplay loops of classic and modern X-COM into RimWorld.

Rather than merely introducing alien races or high-tech weapons—which are common across many RimWorld mods—this project focuses on the **macro systems**: planetary detection networks, global threat management, aerial interception, oceanic operations, live containment, and time-sensitive tactical deployments.

---

## 2. The Three Historical Pillars

The project synthesises the best elements of three eras of the franchise:

```
                  ┌────────────────────────────────────────────────────────┐
                  │                 Rimworld X-COM Suite                   │
                  └──────────────────────────┬─────────────────────────────┘
                                             │
         ┌───────────────────────────────────┼───────────────────────────────────┐
         │                                   │                                   │
┌────────▼──────────┐              ┌─────────▼─────────┐              ┌──────────▼──────────┐
│ 1994 UFO Defense  │              │ 1995 Terror Deep  │              │ Modern Firaxis XCOM │
│ (Julian Gollop)   │              │ (MicroProse)      │              │ (2012 / 2016)       │
├───────────────────┤              ├───────────────────┤              ├─────────────────────┤
│ • Global Geoscape │              │ • Oceanic theatre │              │ • Strategic pacing  │
│ • Radar coverage  │              │ • USOs in water   │              │ • Avatar/Dark Events│
│ • Alien missions  │              │ • Seabed recovery │              │ • Turn/evac urgency │
│ • Multi-outposts  │              │ • Coastal terror  │              │ • Clear telemetry   │
│ • Air dogfights   │              │ • Diving suits    │              │ • Squad synergy     │
└───────────────────┘              └───────────────────┘              └─────────────────────┘
```

### Pillar I: 1994 UFO Defense / Enemy Unknown (The Foundational Soul)

1. **Planetary Geoscape:** The world is not a static map of known points. It is a live sphere of radar bubbles, sensory blind spots, and hidden activities.
2. **UFO Operational Intent:** Aliens do not merely "raid" out of nowhere. UFOs have distinct strategic flight trajectories and mission profiles:
   - _Scout / Recon:_ Charting population centres and defence strengths.
   - _Abduction / Harvest:_ Landing in rural areas, taking biological specimens, taking off if unhindered.
   - _Alien Infiltration:_ Cultivating diplomatic subversion with planetary factions to cut your funding.
   - _Base Construction:_ Establishing fortified hidden alien installations on remote planetary hexes.
   - _Terror Missions:_ Direct punitive strikes against civilian cities and allied faction settlements.
3. **Multi-Outpost Radar Network:** To protect a whole planet, players cannot rely on one base. Establishing remote radar listening posts, forward operating bases (FOBs), and refuelling stations across the globe becomes vital.
4. **Air Interception:** Fast craft must be deployed to intercept, damage, or down UFOs before they complete missions.

### Pillar II: 1995 Terror from the Deep (The Aquatic Dimension)

In vanilla RimWorld, oceans make up 30–50% of the planetary surface, yet they are almost entirely **dead space**—impassable without transport pods or shuttles, and devoid of events or interaction. _Terror from the Deep_ provides the perfect solution:

1. **Unidentified Submersible Objects (USOs):** Alien craft navigating ocean channels, diving underwater to evade conventional ground radar.
2. **Sonar Detection Networks:** Coastal bases and ocean acoustic buoys required to detect sub-surface signatures.
3. **Aquatic & Coastal Operations:**
   - _Coastal Terror Raids:_ Assaults on coastal faction settlements and player maritime outposts.
   - _Sunken Crash / Seabed Salvage:_ Recovering downed craft from shallow water or deep trenches.
   - _Sunken Alien Colonies:_ Late-game underwater strongholds built in abyssal ocean rifts.
4. **Specialised Operational Gear:** Diving suits, rebreathers, sonic weapons, and submersible landing craft (e.g., Triton analogues).

### Pillar III: Modern Firaxis XCOM (Strategic Tension & Pacing)

1. **Strategic Pacing ("Doom Clock" / Alien Dark Events):** RimWorld's open-ended nature can lead to stagnant turtling. Firaxis introduced clear strategic pressure (the Avatar Project / Alien Progress counter) that forces the player onto the offensive.
2. **Tactical Urgency & Extraction Zones:** Rather than static skirmishes where players camp in killboxes:
   - Recoveries have extraction countdowns before enemy dreadnoughts arrive or auto-destruct sequences initiate.
   - Pawns must extract via designated landing craft zones with whatever loot and casualties they can carry.
3. **Visible Progression & Telemetry:** Clear line-of-sight indicators, mission briefings, and meaningful tactical choices.

---

## 3. The RimWorld Adaptation Philosophy

### Real-Time with Pause (RTwP) vs. Turn-Based

- **Decision:** We preserve RimWorld’s native **Real-Time with Pause (RTwP)** engine.
- **Rationale:** RimWorld's pawn AI, job drivers, tick scheduler, and combat mechanics are deeply rooted in real-time ticks. Attempting to force strict turn-based movement onto RimWorld destroys compatibility and creates unnatural latency.
- **How We Capture the X-COM Feel:**
  - High-stakes operational rules (extraction zones, ticking objectives, destructible mission artifacts).
  - Extreme lethality and tactical positioning (cover bonuses, flanking modifiers, suppression).
  - Permadeath tension: losing a veteran sniper or psionic specialist has lasting colony consequences.

### Ant Farm Base Management

- RimWorld already excels at base building, power management, workshops, and hospital wings.
- The suite introduces the **X-COM Facilities Layer**: Radar domes, containment cells, research laboratories, manufacturing workshops for alien alloys, and launch pads.

---

## 4. Engine Architecture: RimWorld 1.7 (Waylith) & BRUTAL

### 4.1 Moving Beyond Legacy Unity

Starting in **RimWorld 1.7**, Ludeon Studios transitioned the game from the Unity engine to their in-house **Waylith Engine**, built upon the open-source **BRUTAL** framework by RocketWerkz.

This fundamental architectural shift moves modding away from legacy `.NET Framework 4.7.2` / Mono and targets modern **`.NET 10` (`net10.0`)**.

### 4.2 Impact on Project Vigilance

1. **Zero-Allocation Planetary Geometry:** Planetary radar detection requires continuous spatial distance and raycasting queries across 100,000+ `WorldGrid` hex tiles. Under legacy Unity, large-scale world objects generated heavy GC allocation overhead. Leveraging modern C# features (`Span<T>`, `ref struct`, SIMD) alongside BRUTAL's low-level memory primitives allows sensory sweeps to run with near-zero GC allocations.
2. **2x to 4x Engine TPS Gains:** Waylith delivers 2x to 4x throughput improvements in tick-heavy operations, giving Project Vigilance ample performance headroom for complex background simulations (multiple active UFO flight vectors, oceanic sonar sweeps, and planetary panic calculations).
3. **Native Rendering & Vulkan Foundations:** Map overlays and radar bubbles are rendered against modern graphics APIs rather than legacy Unity immediate-mode GUI or legacy shader passes.

### 4.3 Expansion Architecture: Odyssey (1.6) & Anomaly (1.5)

Project Vigilance is engineered to dynamically hook into RimWorld's modern expansions while maintaining complete standalone stability for the base game:

- **Odyssey (1.6) — Gravships & Orbital Operations:**
  - Utilises Odyssey's player-controlled gravships and shuttles for late-game Skyranger and Avenger deployments.
  - Extends radar tracking into Odyssey's orbital layer (`PlanetLayerDefs`), detecting UFOs loitering in orbit or entering atmosphere from asteroid fields.
  - Stages the final campaign assault against the alien dreadnought as a true orbital mission.
- **Anomaly (1.5) — Entity Containment & Study:**
  - Maps alien holding pods directly to Anomaly's containment platforms, inheriting psychic suppression and study knowledge loops.
- **Dynamic Fallback:**
  - If expansions are absent, the suite falls back to its standalone C# implementations (`ThingComp_Containment` and sub-orbital drop mechanics).

---

## 5. Engineering Principles: Security, Performance, DRY & KISS

The project is governed by strict engineering standards to ensure high reliability, maintainability, and zero impact on player gameplay performance:

### 5.1 Security by Design (Foundational)

Security and state resilience are core architectural requirements, not afterthoughts:

1. **Principle of Least Privilege:** Enforce the tightest possible visibility scope across all classes, methods, and fields (`private`, `internal`, `readonly`). Never expose mutable internal collections (`IReadOnlyList<T>` over `List<T>`).
2. **Defensive Boundary Validation:** Enforce strict null-safety (`#nullable enable`), validate bounds on all collection lookups, and ensure external inputs or modded incident parameters are sanitized before execution.
3. **Serialization & Save Game Isolation:** Rigorously guard Scribe save/load cycles (`ExposeData()`). Cleanse stale or orphaned cross-references (e.g. destroyed maps, despawned pawns) immediately upon loading to eliminate save corruption and reference leaks.
4. **Minimally Invasive Harmony Patching:** Restrict patches to declarative prefixes/postfixes with clean fallbacks. Avoid complex byte-level transpilers where standard architectural hooks or interfaces suffice.

### 5.2 Performance Efficiencies & Zero-Allocation Hot Paths

With planetary grids featuring 100,000+ tiles, background simulation must never compromise game frame rates (FPS) or ticks per second (TPS):

1. **Zero Heap Allocations on Hot Paths:** The `Tick()` and `Draw()` loops must produce zero garbage collection pressure. Leverage `.NET 10` value types (`PlanetTile`), `ReadOnlySpan<T>`, and spatial bitmasks (`ulong[]`).
2. **Render & Off-World Gating:** Cease all world rendering logic immediately when the player is managing local colonies (`WorldRendererUtility.WorldRendered == false`).
3. **Peacetime Early-Exits & Caching:** Heavy sensory checks run strictly on `TickRare()` (250 ticks / ~4.1 seconds). If zero incursions are active, sweeps exit on tick 1. Stationary sensor radii are flood-filled once and cached until upgraded.

### 5.3 Consistency, KISS & DRY

1. **KISS (Keep It Simple, Stupid):** Choose the simplest, most declarative solution that solves the problem. Avoid over-engineered abstractions, excessive generic wrappers, or unnecessary state machines.
2. **DRY (Don't Repeat Yourself):** Centralise shared spatial math, routing algorithms, and detection logic into unified managers (`IncursionOriginResolver`, `SensoryManager`) rather than duplicating checks across individual incident workers.
3. **Version-Controlled Declarative Scripts:** All build, test, and deployment procedures are automated as declarative, idempotent PowerShell 7+ scripts.

---

## 6. Authoritative Universe & Game Design References

To maintain authentic immersion, mechanics fidelity, and narrative tone consistent with the source material, the project references the following authoritative archives:

- **[UFOpaedia](https://www.ufopaedia.org/):** The definitive encyclopedia and mechanics archive covering both classic MicroProse titles (_UFO: Enemy Unknown / UFO Defense_, _Terror from the Deep_, _Apocalypse_) and modern Firaxis titles (_Enemy Unknown_, _Enemy Within_, _XCOM 2_, _Long War_). Used for cross-referencing:
  - Planetary Geoscape tracking designations (`UFO-1`, `UFO-2`) and continuous craft lifespan persistence.
  - Radar detection equations, blind-spot simulations, and Hyperwave telemetry progression.
  - Incursion operational profiles (Scout, Harvester, Terror, Infiltration, Retaliation).
  - Interception mechanics, craft dogfight engagement math, and forward crash-site recovery.
