# DLC Integration Architecture

## 1. Overview

While the core frameworks of **Project Vigilance** are engineered to be 100% playable on base **RimWorld 1.7 (Waylith)** with zero DLCs, RimWorld's official expansions provide extraordinarily rich mechanics that match classic and modern X-COM systems.

Each DLC integration is designed as an architectural **soft-hook**:

- If the expansion is detected at startup, Vigilance dynamically injects expansion-specific Defs, components, and mechanics.
- If the expansion is absent, the suite automatically falls back to its standalone C# implementations.

---

## 2. DLC Matrix & Feature Mapping

```
                               ┌────────────────────────────────────────────────────────┐
                               │                 Project Vigilance                      │
                               └──────────────────────────┬─────────────────────────────┘
                                                          │
         ┌───────────────────┬────────────────────┼───────────────────┬───────────────────┐
         │                   │                    │                   │                   │
┌────────▼────────┐ ┌────────▼─────────┐ ┌────────▼─────────┐ ┌───────▼─────────┐ ┌───────▼─────────┐
│     ROYALTY     │ │     IDEOLOGY     │ │     BIOTECH      │ │     ANOMALY     │ │     ODYSSEY     │
│   Psionics &    │ │   EXALT Cults &  │ │ Genetics & MECs  │ │  Containment &  │ │  Gravships &    │
│  Imperial Ranks │ │ Belief Rituals   │ │ Child Cadets     │ │ Chryssalid Bio  │ │ Orbital Biomes  │
└─────────────────┘ └──────────────────┘ └──────────────────┘ └─────────────────┘ └─────────────────┘
```

| Expansion          | Core RimWorld Theme         | X-COM Systems Unlocked                                                                                          | Target Module                                                                  |
| :----------------- | :-------------------------- | :-------------------------------------------------------------------------------------------------------------- | :----------------------------------------------------------------------------- |
| **Royalty** (1.2)  | Combat, Psionics & Nobility | Psi-Lab training, Sectoid/Ethereal psionics, Mind Shields, Imperial Council patronage & orbital fire support.   | **Module 3** (Biology), **Battlescape** & **Module 5** (Theme)                 |
| **Ideology** (1.3) | Culture & Colony Beliefs    | EXALT infiltrator cults, Alien Sympathisers, "Human Purity" vs "Transhuman" precepts, victory rituals.          | **Module 4** (Campaign) & **Module 1** (Geoscape)                              |
| **Biotech** (1.4)  | Genetics & Progression      | Alien Xenotypes, Gene-modded supersoldiers, MEC Trooper frames, child cadet growth traits, mechanoid vanguards. | **Module 3** (Biology), **Battlescape** & **Module 5** (Theme)                 |
| **Anomaly** (1.5)  | Horror & Containment        | Live holding platforms, Chryssalid zombification, mind-controlled colonists, invisible stalkers, flesh beasts.  | **Module 3** (Containment), **Battlescape** & **Module 5** (Theme)             |
| **Odyssey** (1.6)  | Gravships & Exploration     | Skyranger/Avenger gravships, orbital radar layer, Glowforest/Scarlands crash sites, orbital debris scavenging.  | **Module 2** (Interception), **Module 1** (Geoscape) & **Battlescape** (Sites) |

---

## 3. Deep Expansion Integrations

### 3.1 Royalty — Psionic Warfare, Psi-Labs & Imperial Noble Systems

In original 1994 X-COM, the late game turns heavily on psionic warfare: soldiers require high Psionic Strength to resist alien panic and mind control, and train in dedicated base **Psi-Labs**.

- **X-COM Psycasts:** Studying live Sectoid Commanders and Ethereals unlocks an exclusive X-COM Psycast tree:
  - _Mind Panic:_ Induces an immediate mental break or fleeing panic state on hostile targets.
  - _Mind Control:_ Temporarily takes direct command of an enemy pawn for several combat rounds.
  - _Psionic Shield:_ Deploys a mental barrier absorbing psionic attacks and mind-control probes.
- **Psi-Lab Training Furniture:** Reintroduces the original Psi-Lab facility. Colonists assigned to Psi-Training duty progressively unlock and elevate their psylink levels without needing Imperial titles or anima tree meditation.
- **Imperial Council Backing & Noble Ranks:** If Royalty is active, the Shattered Empire serves as a premier Council funding backer:
  - X-COM commanders can earn Imperial recognition and royal honours by securing downed alien craft and sharing retrieved technology.
  - Grants access to Imperial permits: orbital bombardment strikes, emergency shuttle extractions, and rapid deployment of elite Imperial cataphract assault squads during dire base defence crises.
  - High-stakes VIP rescue quests: evacuating stranded Imperial nobles from crash sites surrounded by alien hunting packs.
- **Tactical Battlescape Psionics & Combat Permits:**
  - Psionic abilities integrate directly into Battlescape line-of-sight and fog of war: alien mind-control probes test against colonist psychic sensitivity through the haze, and Psionic Shields intercept high-velocity plasma fire.
  - Imperial permits allow commanders to summon cataphract shock-teams directly into hot landing zones or request emergency dropship extractions from designated tactical perimeter beacons.

### 3.2 Ideology — EXALT Infiltration, Cultural Beliefs & Rituals

Invasion does not occur in a cultural vacuum. As global panic rises, human societies fracture over how to respond.

- **Fifth-Column Alien Cults (EXALT / Advent Sympathisers):**
  - Planetary factions suffering high regional panic have a chance to convert to pro-alien ideologies (e.g., _"Harbinger Cult"_, _"Ascension Believers"_).
  - Cultist factions stage clandestine sabotage raids: planting explosives in base power conduits, poisoning hospital stores, or liberating contained alien prisoners.
- **Ideological Precepts:**
  - _Alien Xenobiology:_ `Abhorrent` (refuse to autopsy aliens or research alien tech) vs `Acceptable` vs `Exalted` (demands continuous alien study and genetic integration).
  - _Human Purity:_ `Strict Purism` (forbids alien genetic splicing or MEC prosthetics) vs `Transhuman Adaptation` (believes humanity must assimilate alien genes to survive).
  - _Alien Captive Treatment:_ `Execution Only` vs `Containment & Vivisection` vs `Sympathetic Reintegration`.
- **Ideological Rituals:**
  - _Victory Rallies:_ Celebratory assemblies held after repelling UFO incursions or clearing terror sites, providing colony-wide mood boosts and reducing regional panic.
  - _Relic Veneration:_ Alien artifacts (e.g., intact Nav-Computers, Elerium-115 cores) can be enshrined as sacred cultural relics.
  - _Interrogation Rituals:_ Solemn scientific or ideological proceedings to break alien mental barriers.

### 3.3 Biotech — Alien Xenotypes, Soldier Gene-Modding, Child Cadets & Mechanoid Armies

Biotech provides the exact engine frameworks needed to reproduce **XCOM: Enemy Within's Gene Mods**, generational resistance, and authentic alien biology:

- **Alien Xenotypes:**
  - Rather than creating rigid pawn kinds, aliens are implemented as true **Biotech Xenotypes**:
    - _Sectoid:_ Fragile skeleton, enlarged cranium, extreme psychic sensitivity, telepathic connection.
    - _Muton:_ Hyper-dense muscle fibres, berserk rage gene, natural armour plating, robust constitution.
    - _Thin Man:_ Hyper-mobility, acid blood, toxic spit gland.
- **Soldier Gene-Modding (Gene Lab):**
  - Extracting genepacks from dissected aliens allows players to graft alien traits into their human soldiers:
    - _Mimetic Skin:_ Camouflage gene providing cloaking when stationary behind full cover.
    - _Hyper-Reactive Muscle:_ Increases movement speed and dodge chance.
    - _Secondary Heart:_ Prevents instantaneous death from fatal torso strikes, granting a triage window.
- **Generational Defence & Child Cadets:**
  - In prolonged campaigns spanning multiple in-game years, colonists raise child cadets within base facilities.
  - Growth moments offer unique military and psionic learning opportunities (e.g., _Iron Will_, _Psi-Attuned_, _Heavy Weapons Specialist_), ensuring the next generation of defenders can counter escalated late-game alien invasions.
- **Mechanoid Vanguard & Heavy MEC Troopers:**
  - Mechanitor frameworks are adapted to create **MEC Heavy Exoskeletons**—requiring cybernetically augmented pilots to operate massive chassis armed with kinetic strike modules and flamethrowers.
  - Players can deploy autonomous mechanoid breach squads (Scythers and Centipedes) as robotic vanguard teams into hazardous UFO wreckage to preserve human lives.
- **Tactical Battlescape Synergy (Gene-Mods & Mechanoid Sensory Profiles):**
  - Soldier gene-mods actively interface with Battlescape sensory cones: _Mimetic Skin_ grants active cloaking (full concealment from enemy vision cones) when occupying high cover without moving; _Hyper-Reactive Muscle_ elevates tactical dodge chance in close-quarters skirmishes.
  - Mechanoids project distinct sensory capabilities on tactical maps: Scythers utilize 180° forward close-quarters tracking, Lancers project extreme-range narrow sniper optics (50-cell range with tunnel-vision blind flanks), and Centipedes project 360° phased-array radar penetrating foliage and smoke.

### 3.4 Anomaly — Psychological Terror, Entity Containment & Cosmic Horrors

The Anomaly expansion introduced native entity containment, psychological terror, and mutagenic abominations—the perfect parallel to X-COM's most terrifying unit: the **Chryssalid**.

- **Live Entity Containment Platforms:**
  - When Anomaly is active, alien holding cells automatically inherit Anomaly's containment platforms, bio-suppression protocols, and study knowledge points.
  - Live aliens generate continuous research data when studied by scientists on containment shifts.
- **Chryssalid Zombification & Parasitism:**
  - When a Chryssalid kills a pawn or animal in melee combat, it injects an egg sac.
  - The victim immediately collapses into a shambling **Chryssalid Zombie**.
  - Upon taking critical damage or after a set turn timer, the zombie's chest bursts, spawning an infant or fully grown Chryssalid directly onto the battlefield.
- **Battlescape Parasitism & Environmental Terror:**
  - Chryssalid melee kills trigger real-time zombification during tactical combat, spawning hostile hatchlings directly into the crossfire.
  - Downed UFO power core breaches emit mutagenic fog and psychic wails, collapsing colonist optical vision cones into pitch darkness and triggering psychological hallucinations.
- **Psychological Paranoia & Sleeper Infiltration:**
  - Creepjoiner and refugee events can conceal telepathically subverted human sleeper agents who activate during crucial defensive operations.
  - Downed UFO cores emit terrifying psychic wails, inducing mass paranoia, hallucinations, and blood rain over nearby territories.
  - Alien pods can release mutagenic biomatter that mutates local wildlife into grotesque, aggressive abominations.

### 3.5 Odyssey — Gravships, Planetary Exploration & Orbital Operations

The 1.6 Odyssey expansion introduced player-piloted gravships, new biomes, and an orbital layer (`PlanetLayerDefs`):

- **Skyranger & Avenger as Native Gravships:**
  - Transports are built as true Odyssey gravships. They can take off vertically from the base launch bay, ascend into low orbit, and touch down anywhere on the globe without traversing mountain or ocean terrain bottlenecks.
- **Orbital Geoscape Tracking & Space Adventures:**
  - The Geoscape radar grid extends into Odyssey's orbital space. Players can track alien scout ships orbiting above asteroids or descending through the thermosphere.
  - Gravships can be launched on deep-space salvage expeditions to explore derelict alien probes and orbital debris fields to recover pristine Elerium-115 and hyper-dense alien composites.
- **Exotic Biomes & Hazardous Recovery Sites:**
  - UFO crash and terror missions spawn across Odyssey's exotic biomes:
    - _Glowforest:_ Dense bioluminescent flora creates dynamic lighting shadows, disorienting night-vision optics and thermal sensors.
    - _Scarlands:_ Hazardous planetary scars with extreme radiation and toxic storms, requiring sealed environmental armour or diving suits.
  - Over 40 exotic Odyssey animals are integrated into alien abduction missions, terror harvests, and biological research projects.
- **Exotic Battlescape Biomes & Environmental Warfare:**
  - Tactical crash recoveries in Odyssey biomes alter line-of-sight and environmental warfare:
    - In the _Glowforest_, bioluminescent flora casts dynamic lighting shadows that distort night-vision optics and create shifting visual cover.
    - In the _Scarlands_, severe radiation and toxic dust storms heavily degrade optical sight cones, requiring sealed environmental carapace armour or diving suits.
- **Final Assault in Deep Space:**
  - The climactic endgame raid on the Alien Mothership is staged as a full deep-space assault on an orbital dreadnought, requiring gravship transit and space-capable pressure gear.

---

## 4. Fallback Architecture (Base Game 1.7)

If any or all DLCs are not present, Project Vigilance maintains 100% feature completeness through its standalone C# fallbacks:

| Feature                   | Expansion Active                 | Fallback (Base RimWorld 1.7)                                  |
| :------------------------ | :------------------------------- | :------------------------------------------------------------ |
| **Psionics**              | Royalty Psycasts & Psylinks      | Standalone `CompPsionicPower` with stamina and cooldown bars. |
| **Ideology / Precepts**   | Ideology Memes & Precepts        | Faction trait modifiers and custom colony policy settings.    |
| **Genetics / Xenotypes**  | Biotech Gene Assembler & Genes   | Custom Hediffs, surgical implants, and unique pawn races.     |
| **Containment Platforms** | Anomaly Entity Holding Platforms | Standalone `ThingComp_Containment` and stasis pod furniture.  |
| **Dropships & Gravships** | Odyssey Gravship & Shuttle comps | Standalone sub-orbital flight and transport pod mechanics.    |

---

## 5. RimWorld 1.7 (Waylith Engine) — Performance & BRUTAL Runtime

RimWorld 1.7 deliberately focuses on architectural performance and engine modernisation rather than introducing a new DLC. By migrating from legacy Unity systems onto RocketWerkz's **BRUTAL** framework:

- **Target Runtime:** Vigilance compiles directly against `.NET 10` (`net10.0`), taking full advantage of modern CoreCLR optimisations, C# 13 language capabilities, and high-performance primitive collections (`CommunityToolkit.HighPerformance`).
- **Zero-Allocation Spatial Math:** Planetary radar scans and distance evaluations use `Span<T>`, `Memory<T>`, and SIMD hardware acceleration to process thousands of planetary hexes every 250 ticks without triggering garbage collection pauses.
- **Native Vulkan Rendering:** Radar pulse rings, oceanic thermoclines, and orbital trajectory paths render directly through Vulkan pipelines, eliminating the frame-rate drops traditionally associated with large-scale RimWorld world-layer mods.
- **Stability for Massive Campaigns:** Because 1.7 stabilises the core platform without introducing competing gameplay systems, Project Vigilance can safely execute deep soft-hooks into all five expansions (Royalty, Ideology, Biotech, Anomaly, Odyssey) while guaranteeing a rock-solid 60+ FPS experience.
