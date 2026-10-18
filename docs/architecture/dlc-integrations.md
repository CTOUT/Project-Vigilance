# DLC Integration Architecture

## 1. Overview

While the core frameworks of **Project Vigilance** are engineered to be 100% playable on base **RimWorld 1.7 (Waylith)** with zero DLCs, RimWorld's official expansions provide extraordinarily rich mechanics that match classic and modern X-COM systems.

Each DLC integration is designed as a **soft-hook**:

- If the expansion is detected at startup, Vigilance dynamically injects expansion-specific Defs, comps, and mechanics.
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
│  Imperial Backer│ │   Human Purity   │ │ (Enemy Within)   │ │  Chryssalid Bio │ │  Orbital Layer  │
└─────────────────┘ └──────────────────┘ └──────────────────┘ └─────────────────┘ └─────────────────┘
```

| Expansion          | Core RimWorld Theme       | X-COM Systems Unlocked                                                                                    | Target Module                                         |
| :----------------- | :------------------------ | :-------------------------------------------------------------------------------------------------------- | :---------------------------------------------------- |
| **Royalty** (1.2)  | Combat & Psionics         | Psi-Lab training, Sectoid/Ethereal mind control, Mind Shields, Imperial Council funding.                  | **Module 3** (Biology) & **Module 5** (Theme)         |
| **Ideology** (1.3) | Culture & Colony Beliefs  | EXALT infiltrator cults, Alien Sympathiser fifth-columns, "Human Purity" vs "Transhuman Hybrid" precepts. | **Module 4** (Campaign) & **Module 1** (Geoscape)     |
| **Biotech** (1.4)  | Genetics & Mechanoids     | Alien Xenotypes, Gene-modded supersoldiers (_Enemy Within_), MEC Trooper cybernetic frames.               | **Module 3** (Biology) & **Module 5** (Theme)         |
| **Anomaly** (1.5)  | Horror & Containment      | Live holding platforms, psychic suppression, Chryssalid zombification and embryo-bursting.                | **Module 3** (Containment) & **Module 5** (Theme)     |
| **Odyssey** (1.6)  | Gravships & Orbital Space | Skyranger/Avenger gravships, orbital radar layer, asteroid bases, Cydonia orbital finale.                 | **Module 2** (Interception) & **Module 1** (Geoscape) |

---

## 3. Deep Expansion Integrations

### 3.1 Royalty — Psionic Warfare & Psi-Lab Training

In original 1994 X-COM, the late game turns heavily on psionic warfare: soldiers require high Psionic Strength to resist alien panic and mind control, and train in dedicated base **Psi-Labs**.

- **X-COM Psycasts:** Studying live Sectoid Commanders and Ethereals unlocks an exclusive X-COM Psycast tree:
  - _Mind Panic:_ Induces an immediate mental break / fleeing state on hostile targets.
  - _Mind Control:_ Temporarily takes direct command of an enemy pawn for several combat rounds.
  - _Psionic Shield:_ Deploys a mental barrier absorbing psionic attacks and mind-control probes.
- **Psi-Lab Training Furniture:** Reintroduces the original Psi-Lab facility. Colonists placed on Psi-Training duty progressively unlock and elevate their psylink levels without needing Imperial titles or anima tree meditation.
- **Imperial Council Backing:** If Royalty is active, the Shattered Empire serves as a high-tier Council backer, offering orbital shuttle support and Imperial cataphract strike teams in exchange for alien intelligence.

### 3.2 Ideology — EXALT Infiltrators & Philosophical Precepts

Invasion does not happen in a cultural vacuum. As global panic rises, human societies fracture over how to respond.

- **Fifth-Column Alien Cults (EXALT / Advent Sympathisers):**
  - Planetary factions suffering high regional panic have a chance to convert to pro-alien ideologies (e.g., _"Harbinger Cult"_, _"Ascension Believers"_).
  - Cultist factions stage clandestine sabotage raids: planting bombs in power grids, poisoning water supplies, or freeing contained aliens.
- **Ideological Precepts:**
  - _Alien Xenobiology:_ `Abhorrent` (refuse to autopsy aliens or research alien tech) vs `Acceptable` vs `Exalted` (demands continuous alien study).
  - _Human Purity:_ `Strict Purism` (forbids alien genetic splicing or MEC prosthetics) vs `Genetic Adaptation` (believes humanity must integrate alien genes to survive).
- **Ideological Relics:** Alien artifacts (e.g., intact Nav-Computers, Elerium cores) can be designated as sacred cultural relics for unique rituals.

### 3.3 Biotech — Alien Xenotypes & Gene-Modded Supersoldiers

Biotech's gene-splicing system provides the exact engine framework needed to reproduce **XCOM: Enemy Within's Gene Mods** and authentic alien biology:

- **Alien Xenotypes:**
  - Rather than creating rigid pawn kinds, aliens are implemented as true **Biotech Xenotypes**:
    - _Sectoid:_ Fragile skeleton, enlarged brain, high psychic sensitivity, telepathic connection.
    - _Muton:_ Hyper-dense muscle fibres, berserk gene, natural armour plating, high pain tolerance.
    - _Thin Man:_ Hyper-mobility, acid blood, toxic spit gland.
- **Soldier Gene-Modding (Gene Lab):**
  - Extracting genepacks from dissected aliens allows players to graft alien traits into their human soldiers:
    - _Mimetic Skin:_ Camouflage gene providing cloaking when stationary behind cover.
    - _Hyper-Reactive Muscle:_ Increases movement speed and dodge chance.
    - _Secondary Heart:_ Prevents bleeding out from fatal torso injuries, granting a triage window.
- **MEC Troopers:**
  - Mechtech and mechanitor frameworks are adapted to create **MEC Heavy Exoskeletons**—requiring cybernetically augmented pilots to operate massive chassis armed with kinetic strike modules and flamethrowers.

### 3.4 Anomaly — Chryssalid Horror & Entity Containment

The Anomaly expansion introduced native entity containment, psychological terror, and mutagenic abominations—the perfect parallel to X-COM's most terrifying unit: the **Chryssalid**.

- **Live Entity Containment:**
  - When Anomaly is active, alien holding cells automatically inherit Anomaly's containment platforms, bio-suppression protocols, and study knowledge points.
  - Live aliens generate continuous research data when studied by researchers on containment shifts.
- **Chryssalid Zombification & Parasitism:**
  - When a Chryssalid kills a pawn or animal in melee combat, it injects an egg sac.
  - The victim immediately collapses into a shambling **Chryssalid Zombie**.
  - Upon taking critical damage or after a set turn timer, the zombie's chest bursts, spawning an infant or fully grown Chryssalid on the battlefield.

### 3.5 Odyssey — Gravships & Orbital Operations

The 1.6 Odyssey expansion introduced player-piloted gravships, new biomes, and an orbital layer (`PlanetLayerDefs`):

- **Skyranger & Avenger as Native Gravships:**
  - Transports are built as true Odyssey gravships. They can take off vertically from the base launch bay, ascend into low orbit, and touch down anywhere on the globe without traversing mountain or ocean terrain bottlenecks.
- **Orbital Geoscape Tracking:**
  - The Geoscape radar grid extends into Odyssey's orbital space. Players can track alien scout ships orbiting above asteroids or descending through the thermosphere.
- **Final Assault in Deep Space:**
  - The climactic endgame raid on the Alien Mothership is staged as a full deep-space assault on an orbital dreadnought, requiring gravship transit and space-capable pressure gear.

---

## 4. Fallback Architecture (Base Game 1.7)

If any or all DLCs are not present, Project Vigilance maintains 100% feature completeness through its standalone C# fallbacks:

| Feature                   | Expansion Active                 | Fallback (Base RimWorld 1.7)                                 |
| :------------------------ | :------------------------------- | :----------------------------------------------------------- |
| **Psionics**              | Royalty Psycasts & Psylinks      | Standalone `CompPsionicPower` with stamina/cooldown bars.    |
| **Ideology / Precepts**   | Ideology Memes & Precepts        | Faction trait modifiers and custom colony policy settings.   |
| **Genetics / Xenotypes**  | Biotech Gene Assembler & Genes   | Custom Hediffs, surgical implants, and unique pawn races.    |
| **Containment Platforms** | Anomaly Entity Holding Platforms | Standalone `ThingComp_Containment` and stasis pod furniture. |
| **Dropships & Gravships** | Odyssey Gravship & Shuttle comps | Standalone sub-orbital flight and transport pod mechanics.   |
