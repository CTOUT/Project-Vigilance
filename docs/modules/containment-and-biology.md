# Module 3 Specification: Alien Containment & Biology

## 1. System Overview

The **Alien Containment & Biology** module covers the research and development pipeline of the X-COM initiative. Players secure alien specimens, extract intelligence through interrogation, conduct autopsies, and reverse-engineer recovered technology.

---

## 2. Containment Facilities

Capturing aliens alive is the primary method to unlock high-tier research:

- **Alien Holding Cell / Stasis Pod:** Specialised containment furniture requiring continuous electrical power. Prevents alien prisoners from using psychic abilities, teleporting, or breaking out.
- **Containment Breach Risk:** Power outages or structural damage to holding pods can trigger containment breaches, unleashing panicked or hostile alien entities into the colony.
- **DLC Synergy:**
  - If the **Anomaly DLC** is detected, holding pods automatically inherit and extend native entity containment, study mechanics, and suppression intervals.
  - If running vanilla or without Anomaly, the module falls back to its standalone `ThingComp_Containment` framework.

---

## 3. Autopsies & Interrogations

### 3.1 Medical Autopsies

- Performed at an **Autopsy Table** using butcher/surgical bills.
- Yields:
  - **Corpse Research Knowledge:** Unlocks biological vulnerabilities (e.g., +"15% damage against Sectoids").
  - **Biological Components:** Hyper-fibres, alien nervous tissue, psionic glands.
  - **Organ Harvesting:** High-value bio-samples required for hybrid medical stims and gene therapies.

### 3.2 Live Interrogation

- Conducted by colonists with high Social and Intellectual skills.
- Progression stages:
  - _Resistance:_ Alien attempts mental resistance or psychopathic frenzy.
  - _Compliance:_ Intelligence extracted regarding alien fleet intentions, UFO design schematics, or hidden alien base coordinates.
  - _Resolution:_ The alien expires or remains subdued in deep stasis.

### 3.3 Genetic & Psionic Augmentation (DLC Synergies)

- **Biotech — Soldier Gene-Modding (_Enemy Within_):**
  - Dissecting alien corpses at the autopsy table produces unique alien genepacks.
  - Using a Gene Assembler, players can graft alien traits into their colonists:
    - _Mimetic Skin:_ Camouflage cloaking when stationary behind low cover.
    - _Hyper-Reactive Muscle:_ Drastically boosts movement speed and tactical dodge chance.
    - _Secondary Heart:_ Prevents bleeding out from fatal torso injuries, granting a triage stabilization window.
- **Royalty — Psi-Lab Training & Psionic Warfare:**
  - Studying live Sectoid Commanders and Ethereals unlocks an exclusive X-COM Psycast tree (_Mind Panic, Mind Control, Psionic Shield_).
  - Introduces the **Psi-Lab facility**: allows colonists to undergo rigorous mental conditioning, unlocking and elevating psylink levels without Imperial titles or anima tree meditation.

---

## 4. Reverse-Engineering & Manufacturing

Recovered alien artifacts cannot be built immediately; they must be unlocked through progressive reverse-engineering:

```
Recovered Alien Alloys ────────► Heavy Carapace Armour ────────► Power Armour Chassis
                                        │
Recovered Elerium-115 ─────────► Alien Power Generator ────────► Firestorm Propulsion
                                        │
Weapon Fragments ──────────────► Alien Laser Weapons ──────────► Heavy Plasma Cannon
```

- **Alien Alloys:** Extremely durable, lightweight structural material for walls, blast doors, and high-tier armour.
- **Elerium-115 (Fuel / Power):** Rare consumable power source recovered from intact UFO flight cores, required to fuel advanced craft and plasma weapons.

---

## 5. Salvage-Driven Research & "Fog of Research"

In the original 1994 X-COM, research is not a static tech tree known from day one. You cannot research a Plasma Rifle, Alien Alloys, or Sectoid Biology until your soldiers physically recover specimens and artifacts from the field.

### 5.1 Dynamic Discovery Triggers

Project Vigilance implements an item-triggered research discovery system via `ResearchDiscoveryRuleDef`:

```
[ Field Salvage / Autopsy / Interrogation ]
                    │
                    ▼  (Trigger Met)
   ┌───────────────────────────────────┐
   │  "Tech Discovered: Heavy Plasma"  │
   │   Project revealed on Tech Tree   │
   └────────────────┬──────────────────┘
                    │
                    ▼  (Colonist Research)
[ Unlocked: Plasma Weapon Manufacturing ]
```

1. **Physical Artifact Acquisition:**
   - Possessing a physical item in colony stockpiles (e.g., `Weapon_HeavyPlasma`, `Component_UFO_FlightComputer`, `Resource_AlienAlloy`).
   - The research project remains completely hidden (`HiddenUntilDiscovered = true`) until an item is secured.
2. **Autopsy Completion:**
   - Performing a medical autopsy on a corpse unlocks species-specific projects (e.g., Sectoid Autopsy -> +15% damage bonus and unlocks Psionic Barrier study).
3. **Live Interrogation Extraction:**
   - Interrogating alien leaders or navigators reveals advanced craft propulsion schematics or coordinates to hidden planetary bases.
4. **Standalone Utility for Vanilla & Modded Games:**
   - This discovery system is fully standalone. Modders or vanilla purists can attach `ResearchDiscoveryRuleDef` to vanilla tech (e.g., requiring a salvaged Charge Rifle before "Charged Shot" can be researched, or a Mechanoid Sub-core before "Standard Mechtech").
