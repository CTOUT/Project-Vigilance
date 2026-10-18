# Implementation Roadmap & TODO

## Phase 1: Architecture & Foundation (Current)

- [x] Initialise git repository and enforce repository standards.
- [x] Define three-pillar architectural vision (1994 UFO Defense, Terror from the Deep, Modern Firaxis).
- [x] Establish decoupled modular system design and dependency matrix.
- [x] Author technical specifications for Module 1 (Geoscape & Radar).
- [x] Author technical specifications for Module 2 (Interception & Deployment).
- [x] Author technical specifications for Module 3 (Containment & Biology).
- [x] Author technical specifications for Module 4 (Campaign & Panic).
- [x] Document aquatic operations and Terror from the Deep integration.

---

## Phase 2: Module 1 — Geoscape & Radar Grid (Standalone Framework)

- [x] Create `Geoscape/` C# project (`Geoscape.csproj`) targeting RimWorld 1.7 (Waylith) and `.NET 10` (`net10.0`).
- [ ] Implement `CompProperties_Radar` and `CompRadar` building components.
- [ ] Implement `WorldObjectComp_Radar` for settlements and forward listening posts.
- [ ] Build `RadarPulseHandler` utilizing `TickRare` (250 ticks) for efficient spatial distance checks.
- [ ] Create `WorldObject_FlyingCraft` base class for moving planetary entities.
- [ ] Implement sensory blind spots and planetary fog of war masking on the `WorldGrid`.
- [ ] Build Oceanic Sonar detection layer for water hexes.
- [ ] Implement semi-transparent radar coverage overlay rendering on the world map.
- [x] **Physical Incursion Engine (Milestone 1A):** Intercept walk-in raids (`RaidEnemy`), resolve origin bases/wilderness staging, and simulate real-time overland transit on the `WorldGrid`.
- [x] **Physical Incursion Engine (Milestone 1B):** Expand incursion routing to friendly caravans (`TraderCaravanArrival`) and roaming visitor/traveller groups.
- [ ] **Early Warning Dispatcher:** Generate advance warning alerts and reveal telemetry based on radar tier.
- [ ] **Standalone Test:** Validate radar detection, listening posts, and fog of war in vanilla RimWorld 1.7 with zero other Vigilance mods loaded.

---

## Phase 3: Module 2 — Interception & Rapid Response (Standalone Framework)

- [ ] Define craft stats (speed, fuel, range, armament slots).
- [ ] Build tactical air interception dialogue UI.
- [ ] Implement crash site generation (`SiteDef` with wreckage, debris, and survivor spawns).
- [ ] Create tactical deployment and extraction drop-zone mechanics.
- [ ] Build amphibious transport capabilities (Triton analogue for aquatic sites).
- [ ] **Odyssey DLC Synergy:** Hook into 1.6 gravship components and orbital layer transit (`PlanetLayerDefs`).
- [ ] **Standalone Test:** Validate interception combat and dropship deployment against standard pirate shuttles without Module 1.

---

## Phase 4: Module 3 — Alien Containment & Biology (Standalone Base System)

- [ ] Implement stasis pods and containment cell comps.
- [ ] Hook into Anomaly DLC study mechanics (with standalone fallback for non-DLC).
- [ ] Create autopsy bills and surgical extraction recipes.
- [ ] Implement interrogation mechanics with mental resistance tracking.
- [ ] **Item-Triggered Research Discovery:** Build "Fog of Research" system unlocking tech upon physical salvage, autopsy, or interrogation.
- [ ] Design reverse-engineering knowledge tree (Alloys, Elerium-115, Weapon Fragments).
- [ ] **Standalone Test:** Validate containment pods and autopsy bills on vanilla pawns/creatures in isolation.

---

## Phase 5: Module 4 — Invasion Campaign & Panic (Dependent Campaign Engine)

- [ ] Build Council of Planetary Factions funding and monthly evaluation ledger.
- [ ] Implement regional panic tracking per territory.
- [ ] Implement Alien Strategic AI (mission generation: Scout, Harvest, Infiltration, Terror).
- [ ] Create global Invasion Doom Clock and Dark Events counter-operations.
- [ ] Implement faction defection / alien pact mechanics.

---

## Phase 6: Module 5 — Alien Arsenal & Factions (Thematic Content Pack)

- [ ] Implement alien pawn kinds (Sectoids, Mutons, Chryssalids, Aquatoids, Deep Ones).
- [ ] Create weapon sets: Laser, Plasma, Sonic, and Harpoon/Gauss.
- [ ] Create armour sets: Kevlar, Carapace, Titan, and Diving/Aquatic suits.
- [ ] Integrate sound effects and visual FX faithful to the franchise.
