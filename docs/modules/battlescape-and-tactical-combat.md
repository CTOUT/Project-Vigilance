# Module Specification: Battlescape Tactics, Sensory & Fog of War

## 1. System Overview & Design Philosophy

While the **Geoscape & Radar Grid** ([Module 1](geoscape-and-radar.md)) governs planetary threat detection and incursion tracking, the **Battlescape** represents the ultimate payoff: boots on the ground in high-stakes tactical combat.

In vanilla RimWorld, combat encounters frequently devolve into a predictable, passive meta: approaching hostile raids enter the map edge with full knowledge of player defences and sprint headlong into fortified killboxes. This creates an experience characterized by passive defense rather than tactical manoeuvring.

The **Battlescape Tactical Suite** translates authentic X-COM combat principles into RimWorld's Real-Time with Pause (RTwP) engine:

- **Asymmetric Information:** Neither the player nor the enemy possesses omniscient knowledge of unit positions. Information must be actively gathered through scouting, illumination, and electronic or acoustic sensors.
- **Mutual Reconnaissance & Guerrilla Caution:** Enemies do not mindlessly charge fortified firing lines; they patrol in fireteams, set up lethal fatal-funnel ambushes, fall back when outflanked, and deploy suppressive fire to peel away from disastrous firefights.
- **Sensory Diversity:** Every pawn, creature, and mechanoid possesses distinct sensory capabilities spanning optical vision cones, acoustic hearing, scent tracking, and multi-spectral radar arrays.
- **"You Don't Know What You Don't Know":** Operating at the local tactical scale, rooms remain black voids until doors are breached, night battles require tactical flares or night-vision equipment, and long-range fire requires direct visual or forward-spotter telemetry.

---

## 2. Battlescape Visibility: 3-Mode Parity with Geoscape

In direct alignment with the Geoscape visibility settings ([VisibilityMode.cs](../../Geoscape/Source/Geoscape/Sensory/VisibilityMode.cs)), the Battlescape introduces a unified 3-tier visibility system configurable via mod options:

```
                  ┌─────────────────────────────────────────────────────────┐
                  │              Battlescape Visibility Modes               │
                  └────────────────────────────┬────────────────────────────┘
                                               │
          ┌────────────────────────────────────┼────────────────────────────────────┐
          │                                    │                                    │
┌─────────▼──────────┐               ┌─────────▼─────────┐                ┌─────────▼──────────┐
│  Classic Haze      │               │ Tactical Site     │                │    Omniscient      │
│  (True 3-Tier FoW) │               │   Surveillance    │                │ (Vanilla Fallback) │
├────────────────────┤               ├───────────────────┤                ├────────────────────┤
│ • Unexplored:      │               │ • Static layout:  │                │ • Full map reveal  │
│   Obsidian Shroud  │               │   Pre-charted     │                │ • Instant target   │
│ • Explored:        │               │ • Live threats:   │   telemetry         │
│   Memory Haze      │               │   Hidden outside  │                │ • Zero tactical    │
│ • Active Vision:   │               │   direct sensory  │   FoW tension       │
│   Clear + Live LOS │               │   envelopes       │                │                    │
└────────────────────┘               └───────────────────┘                └────────────────────┘
```

### 2.1 Mode 1: Classic Haze of War (True 3-Tier Fog of War)

The definitive tactical X-COM experience, dividing map cells into three discrete visual states:

1. **Tier 0 — Obsidian Shroud (Unexplored):**
   - **Visual State:** Completely black, opaque overlay obscuring terrain, structures, and entities.
   - **Information Suppression:** Click-selection, marquee boxes, hover tooltips, and the Left-Alt Cell Inspector are strictly suppressed.
   - **Pathfinding & Firing Protection:** Pawns cannot target cells inside the shroud without direct line of fire, preventing the exploit of clicking hidden terrain to check for obstruction.
   - **Interior Cloaking:** Enclosed buildings, crashed UFO hulls, ancient shrines, and subterranean caverns remain shrouded until a roof collapse occurs or a pawn physically opens an exterior door and peers inside.

2. **Tier 1 — Slate Memory Haze (Explored, Unmonitored):**
   - **Visual State:** Desaturated, translucent slate overlay rendering the last-known state of static terrain, natural rocks, and constructed walls/furniture.
   - **Entity Concealment:** Dynamic pawns, mechanoids, animals, moving vehicles, and newly ignited fires are completely invisible.
   - **Tactical Realism:** Prevents players from knowing if an alien has moved behind a wall, set up an ambush in a previously cleared room, or repositioned a turret.

3. **Tier 2 — Active Sensory Perimeter (Live Line-of-Sight):**
   - **Visual State:** Full color, crisp illumination rendering real-time pawn positions, ongoing actions, animations, and projectile paths.
   - **Dynamic Sources:** Cast in real time by conscious player colonists, tamed animals, controlled mechanoids, automated turrets, and deployed lighting assets.

### 2.2 Mode 2: Tactical Site Surveillance (Base & Satellite Intel)

Designed for encounters where the physical topography is known, but enemy positions remain clandestine:

- **Pre-Charted Topography:** Static terrain, exterior walls, and known facility structures are visible from mission inception (simulating satellite reconnaissance or home base familiarity).
- **Masked Hostile Entities:** Hostile and neutral entities remain completely hidden until entered into an active friendly sensory envelope.
- **Use Cases:** Home colony defense raids, pre-scouted crash sites (where Geoscape telemetry reached Tier 4 Analysed), and urban settlements.

### 2.3 Mode 3: Omniscient (Vanilla Spectator)

- Full map visibility and instant entity rendering matching vanilla RimWorld.
- Provided as a zero-friction toggle for accessibility, debugging, and players who prefer strategic management over tactical fog of war.

---

## 3. Multi-Tier Sensory Architecture

Different biological and synthetic entities perceive the battlefield through distinct sensory modalities. Sight is not a uniform circle; it is governed by environmental factors, physiological health, species traits, and synthetic optics.

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Tactical Sensory Matrix                         │
├───────────────────┬──────────────┬──────────────┬──────────────────────┤
│ Entity Category   │ Daytime Sight│ Night Sight  │ Sensory Channels     │
├───────────────────┼──────────────┼──────────────┼──────────────────────┤
│ Human Colonists   │ ~30 Cells    │ ~14 Cells    │ Optical + Acoustic   │
│ Pets / Canines    │ ~20 Cells    │ ~18 Cells    │ Scent + Acoustic     │
│ Light Mechs       │ ~28 Cells    │ ~28 Cells    │ Multi-Spectral       │
│ Sniper Mechs      │ ~48 Cells    │ ~38 Cells    │ High-Mag Optical     │
│ Heavy Mechs       │ ~34 Cells    │ ~34 Cells    │ Phased Radar Array   │
└───────────────────┴──────────────┴──────────────┴──────────────────────┘
```

### 3.1 Colonists & Biological Humanoids

- **Sensory Modality:** Dual-channel (Primary Optical Cone + Secondary Acoustic Perimeter).
- **Primary Optical Vision Cone:**
  - A forward 120° visual arc extending up to ~30 cells in broad daylight (`GenCelestial.CurCelestialSunGlow >= 0.7f`).
  - Degrades smoothly during twilight (~20 cells) and drops to ~14 cells in deep night (`CurCelestialSunGlow < 0.35f`).
  - Weather penalties reduce optical range by 25% in heavy rain, 40% in dense fog, and 50% in blizzards.
- **Secondary Acoustic Horizon (Hearing Bubble):**
  - A 360° omnidirectional awareness radius (~6–8 cells) representing hearing footsteps, reloading sounds, and rustling brush.
  - Moving enemies within this radius register as discrete audio disturbance markers (`?`) in the haze, revealing general proximity without providing target locks.
- **Physiological Capacities & Bionics:**
  - `PawnCapacityDefOf.Sight` directly scales optical distance and cone width. Eye injuries, cataracts, or blindness severely constrict the vision cone.
  - **Bionic Eye:** Eliminates night-vision degradation; grants immunity to darkness penalties.
  - **Archotech Eye:** Adds thermal multispectral imaging, enabling detection of living pawns through single-layer wooden/thin walls and smoke screens.

### 3.2 Pets, Canines & War Beasts

- **Sensory Modality:** Olfactory (Scent) + Acute Acoustic.
- **Scent Detection Horizon:**
  - Canines, wargs, and predatory beasts project a 360° scent aura (~18–22 cells) that operates independently of light levels.
  - Scent penetration allows war animals to detect concealed enemies behind solid walls, closed doors, and within dense forest canopies.
  - Hostiles detected via scent appear as pulsing amber scent indicators on the tactical map, serving as natural point-scouts and anti-ambush sentries.
- **Environmental Vulnerability:** Scent tracking range is diminished by 50% during torrential downpours and high-velocity windstorms.

### 3.3 Mechanoids & Synthetic Drones

Mechanoid chassis utilize purpose-built electro-optical and radar systems with zero biological night degradation:

1. **Light Skirmishers (Scyther, Militor):**
   - High-reflex multispectral optics with a 180° forward arc and rapid 60 Hz target tracking.
   - High resilience to smoke clouds and blinding flashbangs.
2. **Precision Marksmen (Lancer, Pikeman):**
   - High-magnification optical sensors projecting an extreme narrow cone (60° arc) up to ~48–50 cells in clear line of sight.
   - **Tunnel Vision Penalty:** Minimal peripheral vision; highly vulnerable to flanking manoeuvres outside their narrow forward arc.
3. **Heavy Armoured Chassis (Centipede, Diabolus):**
   - 360° synthetic phased-radar scanner (~34 cells radius).
   - Immune to darkness, weather, and standard smoke cover. Detects heavy metallic and biological movement through vegetation and light structures.

### 3.4 Base Installations & Tactical Equipment

- **Floodlights & Spotlights:** Structural buildings that project permanent, wide-angle illumination beams across defensive perimeters, locking lit zones into Tier 2 Active Vision.
- **Handheld Motion Scanner:** An equippable off-hand utility item. When activated, it emits a directional acoustic pulse every 250 ticks, pinging all moving targets within a 25-cell radius through walls and terrain as green blips.
- **Tactical Flares & Illumination Mortars:** Throwable or mortar-launched flares that illuminate a 12-cell radius for 60 seconds, piercing darkness and exposing lurking hostiles.
- **Smoke & Chaff Grenades:** Creates dense particulate clouds that block optical line of sight for biological pawns while allowing thermal/radar sensors to retain partial targeting.

---

## 4. Guerrilla Tactics & Combat AI (Anti-Killbox Doctrines)

To eliminate the predictable killbox meta, hostile assault forces employ coordinated tactical doctrines rather than uniform suicide charges.

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Battlescape AI Doctrines                        │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
       ┌────────────────────────────┼────────────────────────────┐
       │                            │                            │
┌──────▼─────────────┐    ┌─────────▼──────────┐      ┌──────────▼──────────┐
│ Ambush & Hold      │    │ Stalking & Flank   │      │ Peeling Fallback    │
├────────────────────┤    ├────────────────────┤      ├─────────────────────┤
│ • Concealed cover  │    │ • Breach side walls│      │ • Suppressive fire  │
│ • Fatal funnels    │    │ • Disperse columns │      │ • Leapfrog retreat  │
│ • Hold fire until  │    │ • Probe perimeter  │      │ • Regroup with pods │
│   close range      │    │   weak spots       │      │   deeper in site    │
└────────────────────┘    └────────────────────┘      └─────────────────────┘
```

### 4.1 Pod Activation & Cohesion

- **Fireteam Modular Structure:** Hostile forces spawn or deploy organized into autonomous fireteams (pods of 2–4 pawns), led by a designated fireteam leader.
- **Dormancy vs. Alert States:**
  - While unalerted, pods conduct localized patrols around mission objectives, guard power cores, or loiter near fortified cover.
  - Engaging or spotting one pod alerts that specific unit immediately. Surrounding pods are only alerted if gunfire, explosions, or verbal distress calls occur within their acoustic hearing range.
  - This eliminates map-wide swarm reactions and restores the classic room-by-room, quadrant-by-quadrant clearing tension of X-COM.

### 4.2 Ambush & Fatal-Funnel Traps

- **Cover Exploitation:** Defenders (e.g. alien crews guarding downed UFOs or pirates defending outposts) entrench themselves behind high cover (rocks, doorframes, reinforced bulkheads).
- **Trigger Disciplines:** Ambushers hold fire until approaching player scouts enter optimal weapon effective range or step into crossfire corridors, triggering devastating initial volleys.

### 4.3 Peeling Fallback & Suppressive Manoeuvres

- **Dynamic Morale & Tactical Peel:** When an enemy pod loses its squad leader or suffers greater than 50% casualties without inflicting damage, it does not mindlessly flee to map edges. Instead, survivors execute a **tactical peeling retreat**:
  - One pawn provides suppressive covering fire or deploys smoke cover.
  - Remaining pawns leapfrog backward to fortified secondary fallback positions to link up with adjacent dormant pods.

### 4.4 Perimeter Breaching & Multi-Vector Assaults

- In settlement raids, intelligent hostile factions assess defensive turret concentrations and avoid direct entrance funnels. Sapper elements use explosives or heavy melee weapons to breach perimeter walls at unexpected angles, dividing player defenders across multiple fronts.

---

## 5. Strategic Geoscape-to-Battlescape Integration

The tactical map does not exist in a vacuum; it directly reflects the strategic campaign state established on the Geoscape.

### 5.1 Telemetry Dictates Battlescape Ingress

The intelligence tier achieved by Geoscape radar networks prior to ground arrival directly modulates tactical battlefield conditions:

| Geoscape Radar Tier                 | Ingress Intelligence Level  | Battlescape Fog & Tactical Setup                                                                                                                                                       |
| :---------------------------------- | :-------------------------- | :------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Tier 0–2 (Undetected / Contact)** | **Blind Insertion**         | Full Obsidian Shroud across the entire map. Exact enemy force composition, armament, and compound layouts are completely unknown. Dropship lands at an unverified boundary hex.        |
| **Tier 3 (Classified)**             | **Reconnaissance Briefing** | Major terrain contours and UFO wreckage footprint pre-charted. Enemy faction confirmed with broad squad strength estimates.                                                            |
| **Tier 4 (Analysed)**               | **Orbital Telemetry Scan**  | Complete compound architecture and exterior terrain mapped in Tactical Site Surveillance mode. A designated clear Landing Zone (LZ) is established free of immediate enemy sightlines. |

### 5.2 Tactical Urgency & The Mission Doom Clock

To replicate the intense extraction urgency of modern X-COM:

- **Inbound Hostile Interceptions:** If tracking on the Geoscape detects secondary alien craft or reinforcement warbands converging on the encounter tile, a live countdown timer appears on the tactical HUD:
  > _"Hostile Alien Corvette inbound: ETA 4.2 in-game hours (10,500 ticks) — Secure objectives and extract!"_
- **Volatile Reactor Destabilization:** Downed craft with severe hull damage feature unstable antimatter/power cores that tick down toward catastrophic detonation, forcing rapid breach-and-clear tactics over slow turtling.
- **Tactical Extraction Zones:** To complete mission objectives and evacuate wounded personnel or captured artifacts, pawns must physically retreat to the dropship staging perimeter, enabling tactical aborts when missions go disastrously wrong.

---

## 6. Technical Implementation: Zero-Allocation Map Bitmasks

To ensure silky-smooth 60 FPS and 900+ TPS performance on local maps containing up to 100,000 cells (325x325 maps), the Battlescape sensory engine is designed strictly against RimWorld 1.7's Waylith / BRUTAL engine and `.NET 10`.

### 6.1 Spatial Cell Bitsets

- Cell visibility and exploration states are stored in contiguous 64-bit unsigned integer arrays (`ulong[]`):
  ```csharp
  // 300x300 map = 90,000 cells = 1,406 ulongs = ~11.2 KB total memory per map
  private ulong[] exploredBitset;
  private ulong[] visibleBitset;
  ```
- Fast bitwise operations (`BitwiseAnd`, `BitwiseOr`, `PopCount`) evaluate line-of-sight overlaps with near-zero CPU overhead and zero heap allocations during the game loop.

### 6.2 Raycasting & Symmetrical Shadowcasting

- Field-of-view (FOV) calculations leverage recursive symmetrical shadowcasting algorithms (standard in high-performance roguelike engines), optimized using C# `ReadOnlySpan<int>`.
- **Tick Gating:**
  - Full sensory sweeps execute conditionally when pawns change cell position, change facing orientation, or open doors.
  - Stationary pawns skip per-tick raycasting entirely, updating on `TickRare` (250 ticks) for ambient lighting changes.

### 6.3 Minimally Invasive Harmony Hooks

Render suppression adheres to RimWorld's standard render pipeline:

- `Pawn.DrawAt` / `Thing.DrawAt`: Prefix checks whether cell coordinate is marked active in `visibleBitset`. If unrevealed, draw call is bypassed immediately.
- `Selector.Select` / `WorldInspectPane`: Guards prevent selection boxes or info cards from inspecting shrouded entities.
- Fog rendering is handled via a single custom `MapDrawer` layer utilizing a GPU vertex-color mesh overlay, matching the high performance of [WorldDrawLayer_HazeOfWar.cs](../../Geoscape/Source/Geoscape/Sensory/WorldDrawLayer_HazeOfWar.cs).

---

## 7. Modular System Placement & Roadmap

- **Modular Independence:** The Battlescape Sensory & Tactical suite is designed to be fully decoupled:
  - Can function as an independent standalone tactical enhancement mod (`ProjectVigilance.Battlescape`), providing fog of war, vision cones, and pod AI for standard RimWorld gameplay.
  - Dynamically binds to `Vigilance.Geoscape` to inherit pre-mission telemetry and strategic reinforcement timers when both modules are active.
- **Roadmap Alignment:** Authoring this specification establishes the technical blueprint so that upcoming Geoscape milestones (e.g. Milestone 1.3 Radar Infrastructure and site concealment) serialize and transmit telemetry contracts that are 100% compatible with future tactical ground combat.
