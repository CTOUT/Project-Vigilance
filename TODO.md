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
- [ ] **Field Interception & Forward Ambush:** Allow player caravans (and rapid-response transports) to intercept approaching incursions on the global hex grid, generating an encounter map where raiders can be engaged, ambushed, or routed before reaching the colony.
- [ ] **Retreat, Pursuit & Kidnap Recovery:** When raiders flee, rout, or kidnap colonists, simulate their physical retreat across the world map back to their home settlement. Enables hot-pursuit rescue operations to recover captured colonists before they reach enemy strongholds.
- [ ] **Abandoned Camp Fallback & Overland Track Pursuit:** When raiders or incursions arrive at an abandoned colony or vacated camp, replace instant disbanding with intelligent tactical behaviour:
  - **Ground Track Pursuit:** Evaluate recently evacuated player caravans from the tile. If the evacuation departed overland on foot or pack animals (excluding airborne shuttles or 1.6 Odyssey gravships), roll a tactical tracking chance. After a delay searching the empty camp, reroute the incursion to actively pursue the caravan to its next destination or ambush it en route.
  - **Tactical Fallback & Retargeting:** If no tracks are discovered or the trail goes cold, raiders choose between marching back to their home base/nearest faction outpost, establishing an occupation camp, or opportunistically redirecting their assault toward an adjacent player settlement or ally.
- [ ] **Dynamic Inter-Faction Encounters:** Enable roaming NPC incursions and trade caravans to interact when their routes cross—simulating background skirmishes between warring factions, ambush aftermath sites, and trade rendezvous.
- [ ] **Intelligent Multi-Base Strike Coordination:** Replace disconnected per-colony storyteller triggers with theatre-aware campaign logic capable of orchestrating diversionary raids, staggered multi-base assaults, or synchronised offensive campaigns across player settlements.
- [ ] **Early Warning Dispatcher:** Generate advance warning alerts and reveal telemetry based on radar tier.
- [ ] **Community Request: RimWorld 1.6 Legacy Backport Assessment:** Investigate compilation of `ProjectVigilance.Geoscape` for RimWorld 1.6 targeting `.NET Framework 4.7.2` / `.NET Standard 2.1` to support players on the previous release.
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
- [ ] Implement Alien Strategic AI (mission generation: Scout, Harvest, Infiltration, Terror, and coordinated multi-base planetary offensives).
- [ ] Create global Invasion Doom Clock and Dark Events counter-operations.
- [ ] Implement faction defection / alien pact mechanics.

---

## Phase 6: Module 5 — Alien Arsenal & Factions (Thematic Content Pack)

- [ ] Implement alien pawn kinds (Sectoids, Mutons, Chryssalids, Aquatoids, Deep Ones).
- [ ] Create weapon sets: Laser, Plasma, Sonic, and Harpoon/Gauss.
- [ ] Create armour sets: Kevlar, Carapace, Titan, and Diving/Aquatic suits.
- [ ] Integrate sound effects and visual FX faithful to the franchise.
