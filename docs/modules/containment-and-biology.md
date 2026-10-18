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
