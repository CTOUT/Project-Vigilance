# Project Vigilance — Milestone 1.4 In-Game Verification & Test Plan

This test plan provides step-by-step procedures to validate the systems implemented in **Milestone 1.4: Tooling, Visual Assets & Structural Materials** (`v0.4.1`).

It covers automated build provenance telemetry (`BuildInfo` and `Diagnostics` tab), custom orthographic sprites for battlescape sensory buildings, `128x128 RGBA32` grayscale masks and runtime `Faction.Color` livery tinting across Geoscape outposts and orbital assets, populated-coverage station anchoring, stratified Keplerian visual altitude bands (`LEO` / `MEO` / `GEO`), the programmatic `ThermalIntegrityPostDefProcessor` post-def audit engine (metals, sensory arrays, bridges, gravship hulls, machinery, and archotech hardware), and headless CI verification using stripped `lib/ref/` reference assemblies.

## Current Status & Next Tests

- **Passed (In-Game):** `TC-01` (`Diagnostics` build provenance `v0.4.1+931f3cf`, Module 2 aligned to `v{BuildInfo.Version}`), `TC-02` (all 5 battlescape sensory sprites & 4-way rotation; `<shadowData>` tuning noted for visual art polish milestone), `TC-03` (outpost & aftermath grayscale masks + `Faction.Color` tinting; ground `Caravan`s & `WorldObject_ListeningPost` outposts now culled from deep `Orbit` view), `TC-04` (orbital hub & satellite liveries, populated coverage anchoring, stratified bands, and restored Odyssey `MapParent`/`Site` altitude/classification telemetry; added `Orbital: Spawn Orbital Logistics Hub Overhead` / `Clear All Orbital Hubs` debug tools + deep-orbit raycast targeting), `TC-05` (`Steel`, `Silver`, `Gold` stacks, metal structures, and sensory buildings remain completely non-flammable amidst surrounding grass/wood fires), `TC-06` (Odyssey `HeavyBridge`/`Substructure`, thrusters, and non-Bioferrite grav walls stay non-flammable while wooden `Bridge` and `Bioferrite` hull structures burn).
- **Passed (Automated Test Suite):** `TC-07` verified passing via `dotnet test` (`441/441` tests, `0` warnings under `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
- **Pending In-Game Verification:** `0`. Milestone 1.4 verification complete!

---

## Testing Priority & Focus Guide

| Status Badge            | Priority Tier                        | Description & Tester Action                                                                                    |
| :---------------------- | :----------------------------------- | :------------------------------------------------------------------------------------------------------------- |
| `🔴 Focus 1: Immediate` | **Priority 1: Active In-Game Focus** | New build provenance UI and custom visual assets requiring hands-on inspection in RimWorld.                    |
| `🟡 Focus 2: Playtest`  | **Priority 2: Core Colony Playtest** | Programmatic flammability properties verified during active colony construction, fire tests, and battlescape.  |
| `🟢 Automated Pass`     | **Priority 4: Unit-Test Verified**   | Headless CI build, Keplerian solver optimisations, and thermal classification rules verified by `dotnet test`. |

### Recommended Testing Sequence

1. **Step 1: Startup Build Telemetry & Diagnostics Tab** (`🟢 TC-01 Passed`)  
   Launch RimWorld 1.7 with `Project Vigilance: Geoscape`, inspect `Player.log` / dev console for the `v0.4.1` initialisation banner, and open Mod Settings $\rightarrow$ `Project Vigilance: Geoscape` $\rightarrow$ `Diagnostics`.
2. **Step 2: Battlescape Sensory Sprites & 4-Way Rotation** (`🟢 TC-02 Passed`)  
   Enter a colony map in DevMode (`God Mode` or blueprint placement) and inspect `Geoscape_LookoutTower`, `Geoscape_RadarSmall`, `Geoscape_RadarLarge`, `Geoscape_SigintConsole`, and DevMode `Geoscape_SonarArray`.
3. **Step 3: World Outpost & Aftermath Markers + Faction Livery** (`🟢 TC-03 Passed`)  
   Deploy listening post, signal beacon, sonar station, and skirmish aftermath objects via DevMode debug actions on the World Map; inspect zoomed-out 2D icons, zoomed-in 3D quads, and `Faction.Color` tinting.
4. **Step 4: Orbital Station / Satellite Sprites, Coverage Anchoring & Altitude Stratification** (`🟢 TC-04 Passed`)  
   Trigger orbital spawn debug actions, verify `OrbitalHub.png` and specialized satellite chassis masks across factions, confirm stations anchor over populated coverage tiles (`FindPopulatedCoverageAnchorTile`), and verify Odyssey space-layer inspect strings (`SpaceObject_InspectString_Patch`).
5. **Step 5: Thermal Integrity — Metals & Sensory Structures** (`🟢 TC-05 Passed`)  
   Inspect info cards (`Flammability`) and test fire exposure on raw metal stockpiles, steel walls/doors, and colony radar structures.
6. **Step 6: Thermal Integrity — Machinery, Bridges & Gravship Hulls** (`🟢 TC-06 Passed`)  
   Verify fixed-recipe metal machinery, battery exemption, vanilla vs Odyssey bridge flammability, explicit non-combustible spacer items, archotech parts, and Stuff-governed `GravshipHull` flammability.

---

## Test Matrix Overview

| Test Case | Feature Under Test                                     | Expected Behaviour                                                                                                                                               | Status              | Primary Screen / Tool                 |
| :-------- | :----------------------------------------------------- | :--------------------------------------------------------------------------------------------------------------------------------------------------------------- | :------------------ | :------------------------------------ |
| **TC-01** | Build Provenance Stamp & Diagnostics Telemetry         | Log and Mod Settings `Diagnostics` tab display `v0.4.1` build metadata (`FormatBuildDetails()`) cleanly without truncation.                                      | `🟢 In-Game Pass`   | Startup Console / Mod Settings        |
| **TC-02** | Battlescape Sensory Sprites & 4-Way Rotation           | Custom sprites render at intended footprints with clean 4-way orientation (`PhasedArrayLarge_*`, `SigintConsole_*`) and no vanilla placeholder silhouettes.      | `🟢 In-Game Pass`   | Colony Map (God Mode / Build Menu)    |
| **TC-03** | Outpost & Aftermath Markers + Faction Livery Tinting   | `Outpost_Radar`, `Outpost_Beacon`, `Outpost_Sonar`, and `EncounterAftermath` grayscale masks multiply cleanly with `Faction.Color` in 2D and 3D views.           | `🟢 In-Game Pass`   | World Map / Debug Actions Menu        |
| **TC-04** | Orbital Sprites, Anchoring & Stratified Visual Bands   | `OrbitalHub` and 5 satellite chassis render per-faction liveries, anchor over populated tiles, and stratify across `LEO` / `MEO` / `GEO` within `135f` radius.   | `🟢 In-Game Pass`   | World Map / Orbit View / Inspector    |
| **TC-05** | Thermal Integrity — Metals, Stone & Sensory Arrays     | Structural/precious metals (`Steel`, `Plasteel`, `Gold`, `Silver`, `Uranium`) and all 4 sensory buildings report `0%` Flammability (`Flammability = 0`).         | `🟢 In-Game Pass`   | Info Cards / Incendiary Test on Map   |
| **TC-06** | Thermal Integrity — Machinery, Bridges & Gravship Hull | All-metal machines, spacer chips, archotech hardware, and `HeavyBridge`/`Substructure` report `0%`; `Battery` & wood structures burn; `GravshipHull` uses Stuff. | `🟢 In-Game Pass`   | Info Cards / RimWorld DLC Content     |
| **TC-07** | Headless Reference Assemblies, Keplerian Solver & CI   | `lib/ref/` CI builds with `0` warnings under `/p:TreatWarningsAsErrors=true`; `441/441` unit tests pass.                                                         | `🟢 Automated Pass` | GitHub Actions / `dotnet test` (`CI`) |

---

## Detailed Test Cases

### TC-01: Build Provenance Stamp & Diagnostics Telemetry (`v0.4.1`)

- **Priority:** `🔴 Focus 1: Immediate` (`🟢 In-Game Pass`)
- **Automated Status:** `BuildInfoTests` validates non-empty constants and `v0.4.1` string formatting.

**Preconditions:** Build Release (`dotnet build Geoscape/Source/Geoscape.slnx -c Release`) and deploy to RimWorld `Mods/` via `scripts/deploy-geoscape.ps1`.

**Steps:**

1. Launch RimWorld 1.7 and open the DevMode Debug Log (`~`).
2. Locate the initialisation log line emitted by `GeoscapeMod`.
3. Open **Options** $\rightarrow$ **Mod Settings** $\rightarrow$ **Project Vigilance: Geoscape** $\rightarrow$ **Diagnostics** tab.

**Expected Results:**

- Debug log prints `[Project Vigilance] Initialized Module 1: Geoscape & Radar Grid (v0.4.1... | Release | Built ... UTC). Harmony patches applied.`
- The `Diagnostics` tab displays the matching `v0.4.1` version string, build configuration, and UTC build timestamp inside the active modules box without layout clipping.

---

### TC-02: Battlescape Sensory Infrastructure Orthographic Sprites & 4-Way Rotation

- **Priority:** `🔴 Focus 1: Immediate` (`🟢 In-Game Pass` — rectangular `<shadowData>` tuning deferred to high-fidelity visual art overhaul)
- **Automated Status:** Not automated (visual texture inspection required in engine).

**Preconditions:** Active colony map with DevMode `God Mode` enabled.

**Steps:**

1. Open Architect $\rightarrow$ build/place the following buildings on open terrain (test wood and stone stuff where applicable):
   - `Geoscape_LookoutTower` (`2x3`, expects `LookoutTower.png` instead of vanilla `SteleLarge`)
   - `Geoscape_RadarSmall` (`3x3`, expects `RadarDomeSmall.png`)
   - `Geoscape_RadarLarge` (`4x4`, rotate with `Q`/`E` to check `_north`, `_east`, `_south`, `_west`)
   - `Geoscape_SigintConsole` (`3x2`, rotate with `Q`/`E` to check `_north`, `_east`, `_south`, `_west`)
2. Open DevMode $\rightarrow$ `Spawn thing...` and place `Geoscape_SonarArray` (`3x3`, expects `SonarStation.png`) adjacent to ocean/deep water coastal cells.

**Expected Results:**

- Every structure displays its dedicated custom orthographic sprite cleanly aligned to its cell footprint.
- Rotating `Geoscape_RadarLarge` and `Geoscape_SigintConsole` switches seamlessly between `_north`, `_east`, `_south`, and `_west` variants without mirrored seams, stretched aspect ratios, or missing-texture purple squares.
- Material-configurable buildings (`Geoscape_LookoutTower`) tint appropriately with chosen timber/stone materials.

---

### TC-03: Geoscape Outpost & Encounter Aftermath World Markers with Faction Livery Tinting

- **Priority:** `🔴 Focus 1: Immediate` (`🟢 In-Game Pass` — `WorldObject_Visibility_Patch.IsSurfaceAnchorVisibleFromOrbit` updated to cull ground `Caravan`s and `WorldObject_ListeningPost` outposts from the deep `Orbit` view so only strategic `MapParent` colonies remain visible over the globe)
- **Automated Status:** `OrbitalVisibilityTests.IsSurfaceAnchorVisibleFromOrbit_AllowsColoniesAndExcludesCaravansAndOutposts` verifies Orbit-layer anchor scoping; world shader multiplication verified in-game.

**Preconditions:** Active campaign on World Map (`F8`) with DevMode enabled.

**Steps:**

1. Use `DebugActions` $\rightarrow$ `Project Vigilance`:
   - Click `Outpost: Deploy Listening Post` on an explored land tile (`Vigilance_ListeningPost_Automated`, expects `Outpost_Radar.png`).
   - Click `Outpost: Deploy Signal Beacon` on another tile (`Vigilance_ListeningPost_Beacon`, expects `Outpost_Beacon.png`).
   - Click `Outpost: Deploy Sonar Station` on a coastal tile (`Vigilance_ListeningPost_Sonar`, expects `Outpost_Sonar.png`).
   - Click `Incursion: Spawn Skirmish Collision Pair` on a flat tile (`Vigilance_EncounterAftermath`, expects `EncounterAftermath.png` once the background skirmish resolves).
2. Zoom smoothly from planet overview (2D `expandingIcon`) down to surface hex close-up (3D world object mesh).

**Expected Results:**

- All outpost icons multiply their neutral `128x128 RGBA32` grayscale masks against `Faction.OfPlayer.Color` cleanly without muddy saturation or harsh black fringe artifacts.
- `WorldObject_EncounterAftermath` renders the crater wreckage debris graphic (`EncounterAftermath.png`) instead of vanilla `Sites/ItemStash`.
- Both 2D expanding icons and 3D world quads remain upright relative to the globe camera.

---

### TC-04: Orbital Station & Satellite Chassis Sprites, Populated Coverage Anchoring & Altitude Stratification

- **Priority:** `🔴 Focus 1: Immediate` (`🟢 In-Game Pass` — verified Odyssey `MapParent`/`Site` altitude telemetry, faction livery tinting, and satellite coverage rings; added `Orbital: Spawn Orbital Logistics Hub Overhead` & `Orbital: Clear All Orbital Hubs` debug actions)
- **Automated Status:** `OrbitalLogisticsTests` validates visual altitude clamping (`[4, 34]`), `MaxOrbitalRenderRadius = 135f`, and Odyssey `Site`/`MapParent` inspect/stratification hooks.

**Preconditions:** Active campaign with World Coverage `< 100%` (e.g., default `30%`) and multiple factions present (e.g., `TradersGuild` / Outlander / Pirate). Also verify under Odyssey Orbit layer if Odyssey DLC is active.

**Steps:**

1. Open `DebugActions` $\rightarrow$ `Project Vigilance`:
   - Run `Orbital: Spawn Trade Satellite Over Colony` (`Vigilance_Satellite_Trade`, `Satellite_Trade.png`).
   - Run `Orbital: Spawn Recon Satellite Overhead` (`Vigilance_Satellite_Recon`, `Satellite_Recon.png`).
   - Run `Orbital: Spawn Nav Satellite Overhead` (`Vigilance_Satellite_Nav`, `Satellite_Nav.png`).
2. Spawn or locate a `WorldObject_OrbitalStation` (`Vigilance_OrbitalStation`, expects `OrbitalHub.png`) and inspect `Satellite_Comms` / `Satellite_Military` defs if spawned via dev tools.
3. Temporarily set Mod Settings visibility to `Omniscient` (or enable DevMode override) if verifying non-player faction liveries on orbital hubs/satellites.
4. Compare orbital shell radii in 3D globe view across `LEO` (Recon 450 km), `MEO` (Trade/Nav), and `GEO` stations, and select any Odyssey space asteroid/station to inspect `SpaceObject_InspectString_Patch` output.
5. Run `Orbital: Clear All Satellites` afterward to confirm clean despawn.

**Expected Results:**

- `OrbitalHub.png` and all satellite chassis display distinct silhouettes cleanly tinted by their owning faction's `Faction.Color` in both 2D expanding icon and 3D mesh layers.
- Orbital stations spawned via `OrbitalManager.FindPopulatedCoverageAnchorTile` spawn exclusively over populated tiles within generated world coverage (never over unpopulated ocean voids outside the `30%` coverage cap).
- All satellites and stations stay within `MaxOrbitalRenderRadius` (`135f`) across `LEO` (`4–10`), `MEO` (`10–24`), and `GEO` (`24–34`) altitude bands without clipping into the planet (`r < 100f`) or drifting off-screen.
- Odyssey space objects append `Orbital Altitude: ~X km` and `Classification: ...` cleanly in the inspect pane.

---

### TC-05: Thermal Integrity — Structural Metals, Stone & Sensory Weatherproofing

- **Priority:** `🟡 Focus 2: Playtest` (`🟢 In-Game Pass` — verified `0%` Flammability info cards and fire immunity across `Steel`, `Silver`, `Gold` stacks, metal structures, and colony radar/SIGINT buildings even when underlying grass burns)
- **Automated Status:** `Milestone13FixesTests.ThermalIntegrityPostDefProcessor_StructuralAndPreciousMetals` & `SensoryBuildings_Flammability_AlignedWithThermalIntegrity` pass in `dotnet test`; live DefDatabase integration verified in RimWorld.

**Preconditions:** Colony map loaded with active colonists or DevMode `Spawn thing` / `Place direct thing` tools.

**Steps:**

1. Check the info card (`i`) **Flammability** stat on raw resource stacks: `Steel`, `Plasteel`, `Gold`, `Silver`, `Uranium`.
2. Build or spawn `Wall` / `Door` / `Barricade` stuffed with `Steel`, `Plasteel`, and `Gold`; open their info cards.
3. Check the info card **Flammability** stat on `Geoscape_RadarSmall`, `Geoscape_RadarLarge`, `Geoscape_SigintConsole`, and `Geoscape_SonarArray`.
4. Attempt to ignite a `Steel Wall`, stacked `Steel`, and a `Geoscape_RadarSmall` dome using DevMode `Start fire` or an incendiary launcher.

**Expected Results:**

- All listed raw metals and Stuff factors report `Flammability: 0%`.
- `Geoscape_RadarSmall`, `Geoscape_RadarLarge`, `Geoscape_SigintConsole`, and `Geoscape_SonarArray` all report `Flammability: 0%`.
- No metal wall, metal stack, or colony radar building catches fire from adjacent flames or direct incendiary hits.

---

### TC-06: Thermal Integrity — All-Metal Machinery, Bridges & Gravship Hulls

- **Priority:** `🟡 Focus 2: Playtest` (`🟢 In-Game Pass` — verified `HeavyBridge`/`Substructure`, thrusters, and standard gravship walls stay unburnt while wooden `Bridge` and `Bioferrite` hull components catch fire)
- **Automated Status:** `Milestone13FixesTests` unit tests verify `IsAllMetalOrStoneBuilding`, `AuditTerrainBridges`, `GravshipHull`, `Battery` exemption, and `ExplicitNonCombustibleItems`.

**Preconditions:** Colony map with Industrial/Spacer equipment (plus Odyssey DLC active for bridges/gravships and Biotech for boss chips where installed).

**Steps:**

1. Inspect info cards on fixed-recipe metal/stone buildings: `ElectricSmelter`, `ElectricSmithy`, `TableMachining`, `FabricationBench`, `PowerSwitch`, `Turret_MiniTurret` / `AutocannonTurret`, `Hopper`, `SignalJammer`.
2. Inspect info cards on wood/combustible structures: `Battery` and `WatermillGenerator`.
3. Inspect info cards on explicit non-combustibles (`ReinforcedBarrel`, `SignalChip`, `PowerfocusChip`, `NanostructuringChip`, `Gravcore`, `GravlitePanel`, `ShuttleEngine`), any `Archotech*` item/bionic, and pure-metal `Bionic*` prostheses.
4. Check terrain info for wooden `Bridge` vs Odyssey `HeavyBridge`, `AncientHeavyBridge`, and `Substructure`.
5. Build two `GravshipHull` sections: one made from `Steel` and one made from `Bioferrite` (Anomaly), then inspect Flammability.

**Expected Results:**

- Fixed-recipe all-metal/stone machinery, explicit spacer items, archotech hardware, and pure bionics all display `0%` Flammability.
- `Battery` and wood-framed buildings (`WatermillGenerator`) retain non-zero flammability (`> 0%`) as intentional exceptions.
- Vanilla wooden `Bridge` displays `100%` flammability (`1.0`); Odyssey steel/ancient bridges and `Substructure` display `0%`.
- Steel `GravshipHull` displays `0%` flammability; Bioferrite `GravshipHull` scales cleanly via Stuff factor (`75%`).

---

### TC-07: Headless Reference Assembly CI, Keplerian Solver Optimization & Automated Test Suite

- **Priority:** `🟢 Automated Pass`
- **Automated Status:** Verified passing via headless `dotnet test Geoscape/Source/Geoscape.slnx -c Release`.

**Verification Scope:**

1. **Stripped Reference Assemblies (`lib/ref/`):** Solution compiles against stripped public API stubs (`Assembly-CSharp.dll`, `RimWorld.dll`, `UnityStubs.dll`, `Waylith.UnityCompat.dll`, `0Harmony.dll`, `CommunityToolkit.HighPerformance.dll`) on clean environments without local Steam/RimWorld installs.
2. **Zero-Warning Policy (`.github/workflows/dotnet-ci.yml`):** Full Release build passes under `/p:TreatWarningsAsErrors=true`.
3. **Keplerian & Inspection Optimisations:** `OrbitalFlightModel.PredictNextPassTick` hoists invariant tile normal calculations outside loops; `WorldObject_DrawPos_Patch` bails $O(1)$ for non-orbital ground entities; `SpaceObject_InspectString_Patch` uses `[ThreadStatic]` pooled `StringBuilder`.
4. **Unit Test Coverage:** Automated test suite validates `ThermalIntegrityPostDefProcessor`, `OrbitalFlightModel` altitude stratification, `OrbitalManager` coverage anchoring, `SpaceObject_InspectString_Patch`, and `BuildInfo` (`v0.4.1`).

---

## Sign-Off Checklist

- [x] `TC-01` — Build Provenance Stamp & Diagnostics Telemetry (`v0.4.1`)
- [x] `TC-02` — Battlescape Sensory Sprites & 4-Way Rotation
- [x] `TC-03` — Outpost & Aftermath Markers + Faction Livery Tinting
- [x] `TC-04` — Orbital Station / Satellite Sprites, Coverage Anchoring & Stratified Bands
- [x] `TC-05` — Thermal Integrity: Metals, Stone & Sensory Weatherproofing
- [x] `TC-06` — Thermal Integrity: Machinery, Bridges & Gravship Hulls
- [x] `TC-07` — Headless Reference Assembly CI, Keplerian Solver Optimization & Automated Test Suite
