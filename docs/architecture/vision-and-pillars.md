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
