# Module 2 Specification: Interception & Rapid Response

## 1. System Overview

The **Interception & Rapid Response** module bridges the planetary Geoscape with local tactical maps. It provides the vehicles, combat resolutions, and deployment protocols required to engage UFOs in flight and deploy strike teams to recovery sites.

---

## 2. Craft Architecture

Craft exist in two primary categories: **Interceptors** (air superiority) and **Transports** (tactical troop carriers).

### 2.1 Craft Specifications

| Craft Model             | Class                 |  Crew / Capacity  | Range (Hexes) | Max Speed |    Hardpoints    | Primary Purpose                     |
| :---------------------- | :-------------------- | :---------------: | :-----------: | :-------: | :--------------: | :---------------------------------- |
| **Interceptor (F-301)** | Light Interceptor     |      1 Pilot      |      45       | Mach 3.0  |     2 Weapon     | Early-game air defence              |
| **Skyranger**           | Transport Dropship    | 10 Pawns + 1000kg |      60       | Mach 1.5  |       None       | Long-range troop deployment         |
| **Triton (Amphibious)** | Dual-Medium Transport |  8 Pawns + 800kg  |      55       | Mach 1.2  |       None       | Aquatic & coastal operations (TftD) |
| **Firestorm**           | Advanced Interceptor  |      1 Pilot      |      75       | Mach 5.2  |  2 Alien Heavy   | Mid-game hybrid alien alloy craft   |
| **Avenger**             | Assault Dreadnought   | 14 Pawns + 2000kg |    Global     | Mach 4.5  | 2 Heavy + 1 Bomb | Late-game multi-role planetary flag |

---

## 3. Aerial Combat Resolution

Rather than forcing full 3D flight simulation into RimWorld's engine, air combat is resolved through a dedicated **Dogfight Tactical Dialogue** inspired by the 1994 original and 2012 Firaxis air minigame:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        AIR INTERCEPTION IN PROGRESS                    │
├────────────────────────────────────────────────────────────────────────┤
│ Target: UFO Medium Scout (UF-08)       Craft: Interceptor Alpha        │
│ Distance: 1,800m                       Altitude: Medium (4,500m)       │
│ UFO Hull: [██████████░░░░░░] 62%        Interceptor: [██████████████] 100%│
│                                                                        │
│ Weapons: Avalanche Missiles (Ready)     Stinger Pods (Reloading - 2s)  │
│                                                                        │
│ Combat Stance:                                                         │
│ [ ( ) Stand-off (Long Range) ]   [ (•) Standard ]   [ ( ) Aggressive ] │
│                                                                        │
│ Actions:                                                               │
│ [ Disengage & Retreat ]                   [ Fire Avalanche (Salvo) ]   │
└────────────────────────────────────────────────────────────────────────┘
```

### 3.1 Combat Mechanics

- **Engagement Ranges:** Maximum range determined by equipped weapons (e.g., Stingers at 800m, Avalanche Missiles at 2,500m, Plasma Beams at 3,500m).
- **Damage & Destructive Outcomes:**
  - **Intact Crash Site:** UFO brought down with low overkill damage; high salvage potential, live alien survivors, mostly intact alien tech.
  - **Severe Crash Site:** Heavy damage; partial alien survival, destroyed power cores, widespread fires.
  - **Vaporised:** Target destroyed in mid-air; no mission site spawned, debris burns up in atmosphere (yields only small strategic points/panic reduction).
  - **Escape:** UFO disengages, out-accelerates, or downs the interceptor, completing its planetary objective.

---

## 4. Rapid Tactical Deployment & Evacuation

When deploying to a crash, terror, or landing site:

1. **Drop-zone Deployment:** The dropship lands in a designated border region of the map, serving as the pawn staging area, mobile medical bay, and equipment locker.
2. **Extraction Beacons:** To leave the map, pawns must return to the dropship boundaries. This allows tactical retreats: if a mission goes catastrophic, soldiers can carry wounded squadmates back to the ship and abort.
3. **Site Urgency & Self-Destruct:**
   - Alien reactors may destabilise, triggering a countdown timer before explosion.
   - Alien reinforcement waves or orbital bombardment timers create tactical tension, preventing passive killbox turtling.
