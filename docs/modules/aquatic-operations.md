# Aquatic Operations & Terror from the Deep Integration

## 1. The Oceanic Opportunity in RimWorld

RimWorld world generation produces expansive oceans that represent approximately 30% to 50% of the planet's total surface area. In the base game, these ocean tiles are purely non-traversable barriers.

Integrating **Terror from the Deep (TftD)** elements transforms these barren expanses into an active, tense theatre of strategic and tactical operations.

---

## 2. Core Oceanic Systems

```
                     ┌─────────────────────────────────────────┐
                     │          The Oceanic Theatre            │
                     └────────────────────┬────────────────────┘
                                          │
            ┌─────────────────────────────┼─────────────────────────────┐
            │                             │                             │
┌───────────▼───────────┐     ┌───────────▼───────────┐     ┌───────────▼───────────┐
│     Sensory Layer     │     │      Craft Layer      │     │    Tactical Sites     │
├───────────────────────┤     ├───────────────────────┤     ├───────────────────────┤
│ • Coastal Sonar Arrays│     │ • Alien USOs          │     │ • Crashed Seabed USOs │
│ • Deep Acoustic Buoys │     │ • Submersible Shuttles│     │ • Sunken Cargo Wrecks │
│ • Thermocline Blasts  │     │ • Amphibious Transports│    │ • Coastal Terror Raids│
│ • Sub-surface Stealth │     │ • Speed penalties     │     │ • Deep Ocean Colonies │
└───────────────────────┘     └───────────────────────┘     └───────────────────────┘
```

---

## 3. Sensory Layer: Sonar & Hydrophones

Conventional radar cannot penetrate seawater. When alien craft descend into ocean hexes, standard radar arrays lose track of them, producing a _"Target Lost in Oceanic Sector"_ warning.

### 3.1 Detection Facilities

- **Coastal Sonar Station (`Geoscape_SonarArray`):** Industrial colony installation (`200 Steel`, `5 Components`; `500 W` standby / `1,200 W` active power draw; requires `Geoscape_EarlyWarningRadar`). Uses `PlaceWorker_CoastalSonar` to restrict construction to settlements on coastal Geoscape tiles with at least one adjacent `WaterCovered` tile (`SonarTraversalUtility.HasCoastalAccess`), scanning **18 water rings** ($R_{detect}$) and **8 water rings** ($R_{id}$).
- **Automated Sonar Station (`Vigilance_ListeningPost_Sonar`):** Caravan-deployed unmanned coastal hydrophone outpost powered by standalone solar banks (`120 Steel + 4 Components`; repairs for `30 Steel + 1 Component`; dismantles for `90 Steel + 3 Components`). Requires direct coastal adjacency (`SonarTraversalUtility.HasCoastalAccess`), operates with zero local colony TPS overhead, and provides **18 water rings** ($R_{detect}$) and **8 water rings** ($R_{id}$).
- **Directional V-Sector Connected-Water Propagation (`SonarTraversalUtility`):** Rather than radiating over land, both sonar installations originate from adjacent `WaterCovered` tiles and project outward in directional V-shaped sectors (`DirectionalSectorCosThreshold = 0.45f`) strictly through connected `WaterCovered` tiles (`PopulateSonarCoverage`); land tiles act as acoustic barriers. In `SensoryManager`, `(tileId, isSonar)` cache keys isolate sonar footprints from ground radar so both layers can overlap on coastal water tiles without conflict.
- **Deep-Sea Acoustic Buoys _(Planned)_:** Deployable world objects dropped into deep ocean tiles to provide remote sensory coverage in international waters.

### 3.2 Oceanic Detection Dynamics

- Submarines and USOs can travel at varying depths (Periscope, Shallow, Abyssal).
- Thermal layers (thermoclines) and underwater topography introduce sensory blind spots, allowing USOs to creep close to coastlines before detection.

---

## 4. Craft & Interception in the Wet Domain

### 4.1 Unidentified Submersible Objects (USOs)

- USOs behave similarly to atmospheric UFOs, but operate within ocean paths.
- When transitioning from air to water (or vice versa), craft experience brief vulnerability windows where their speed drops dramatically.

### 4.2 Interception Mechanics

- **Dual-Medium Interceptors:** High-tier interceptors engineered with hybrid aerospace-cavitation propulsion can engage USOs both in the air and under the surface.
- **Torpedo / Depth-Charge Payloads:** Specialist armaments required for engaging submerged targets.
- **Water-Impact Crash Sites:** USOs downed over water do not create typical scorched-earth craters; they create **Sunken Wreckage Sites** that require submersible recovery teams.

---

## 5. Tactical Skirmishes: Aquatic & Coastal Maps

### 5.1 Coastal Terror Raids

- Aliens land amphibious assault pods along shorelines, striking coastal settlements or ports.
- Battles feature mixed terrain: sandy shores, tidal shallows, dockyards, and breaking surf. Water tiles slow movement and provide unique cover properties.

### 5.2 Seabed Crash Recovery & Sunken Artifacts

- Tactical maps generated on ocean tiles represent seabed wreckage or sunken vessels.
- Special mission modifiers:
  - **Aquatic Pressure & Environmental Hazard:** Pawns require **Diving Suits** or **Atmospheric Enclosed Armour** to survive the crushing depths.
  - **Limited Vision & Murky Waters:** Sight ranges are reduced; pawns rely on tactical flashlights and short-range sonar scanners.
  - **Ballistic Drag:** Standard firearms suffer heavy range and velocity penalties underwater; players must deploy specialised **Harpoon Launchers**, **Sonic Weapons**, or **Gauss Weaponry**.

### 5.3 Sunken Alien Colonies

- Late-campaign multi-stage assault missions located in deep abyssal trenches.
- Multi-chambered alien underwater complexes housing cloning vats, aquatic bio-terrors, and geo-thermal power cores.
