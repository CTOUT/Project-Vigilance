# Implementation Plan: Milestone 1.3 Playtest Fixes & Test Plan Updates

## Goal Description

Address all findings from the Milestone 1.3 in-game playtest:

1. **TC-02 Sign-Off:** Record **TC-02** (elevation vantage, mountain masking, and SIGINT penetration) as passed in [milestone_1_3_test_plan.md](milestone_1_3_test_plan.md).
2. **TC-03 Fix (Sonar Traversal, Automated Sonar Station & Coastal Facility Validation):**
   - Introduce a standalone Geoscape **Automated Sonar Station** (`ListeningPostKind.AutomatedSonar` / `Vigilance_ListeningPost_Sonar`), constructible on any passable tile adjacent to at least one `WaterCovered` tile without requiring a settlement.
   - Restrict Settlement Sonar Facilities (`Geoscape_SonarArray`) to settlements with direct coastal access (at least one adjacent `WaterCovered` Geoscape tile).
   - Originate all sonar calculations from the station's or settlement's adjacent `WaterCovered` tiles, projecting outward in directional V-shaped sectors strictly through connected `WaterCovered` tiles (never crossing land or detecting disconnected water bodies across terrain), and merging overlapping sectors seamlessly from shoreline to shoreline.
   - Allow Radar and Sonar coverage to overlap on the same water tiles without cache collisions or redundancy.
3. **Caravan Route Visibility Masking:** Clip `WorldObject_Incursion` route rendering to tiles inside active detection range so paths terminate cleanly at the sensor perimeter edge instead of leaking origin/destination settlements in the shroud.
4. **Random Caravan Trade Loop Fix:** Eliminate the infinite re-hail / trade loop when interacting with grounded `WorldObject_Incursion` trade caravans by consuming the pending `CaravanArrivalAction_InterceptIncursion` and enforcing encounter cooldowns on `1a` interception checks.
5. **TC-12 Fix (Optimistic Fog-of-War Routing, Autonomous Obstacle Circumvention & Hard-Stop Prompt):** Ensure land caravans assume all unexplored terrain is traversable when plotting routes into the shroud, autonomously reroute around newly discovered obstacles with an advisory soft notification, and only halt with a high-priority player prompt when no viable route can break even or make meaningful progress toward the destination.

---

## Proposed Changes

### 1. Test Plan Sign-Off (`TC-02`)

#### [MODIFY] [milestone_1_3_test_plan.md](milestone_1_3_test_plan.md)

- Update **TC-02** status badge from `🟡 Focus 2: Playtest` to `🟢 In-Game Pass` and check off its checklist entry (`[x] 🟢 **TC-02**`).

---

### 2. Coastal Sonar Traversal, Automated Sonar Station & Settlement Validation (`TC-03`)

Sonar is modeled as a coastal capability rather than a land-radius capability. Both standalone Automated Sonar Stations and settlement Sonar Facilities require at least one adjacent `WaterCovered` Geoscape tile, originate acoustic propagation from their adjacent `WaterCovered` tiles, project outward in V-shaped sectors per adjacent water tile, and traverse exclusively through connected `WaterCovered` tiles so land acts as an acoustic barrier.

#### [NEW] [SonarTraversalUtility.cs](../Geoscape/Source/Geoscape/Sensory/SonarTraversalUtility.cs)

- Provide test overrides:
  - `public static Func<PlanetTile, bool>? IsTileWaterCoveredOverride { get; set; }`
  - `public static Action<PlanetTile, List<PlanetTile>>? GetTileNeighborsOverride { get; set; }`
  - `public static Func<PlanetTile, Vector3>? GetTileCenterOverride { get; set; }`
- `public static bool IsTileWaterCovered(PlanetTile tile)`:
  - Evaluates `IsTileWaterCoveredOverride(tile)` if set, otherwise `tile.Valid && tile.Tile?.WaterCovered == true`.
- `public static void GetAdjacentWaterTiles(PlanetTile stationTile, List<PlanetTile> outWaterNeighbors)` and `public static bool HasCoastalAccess(PlanetTile stationTile)`:
  - Queries immediate Geoscape neighbors of `stationTile` and collects those where `IsTileWaterCovered(neighbor)` is true.
  - Returns true when at least one adjacent Geoscape tile is `WaterCovered` (independent of whether `stationTile` itself contains water).
- `public static void PopulateSonarCoverage(PlanetTile stationTile, int maxDetectionRadius, int maxIdentificationRadius, HashSet<PlanetTile> outDetectionTiles, HashSet<PlanetTile>? outIdentificationTiles = null, Dictionary<PlanetTile, int>? outMinSteps = null)`:
  - Collects each adjacent `WaterCovered` tile $W_i$ of `stationTile`.
  - For each $W_i$ independently:
    - Computes the outward direction vector $\vec{d}_i$ from `stationTile` through $W_i$ (tangent-projected when 3D tile centers are available via `GetTileCenterOverride` or `Find.WorldGrid`).
    - Runs a water-only BFS starting at $(W_i, \text{step} = 1)$ up to `maxDetectionRadius`:
      - Enqueues neighbors $N$ only if `IsTileWaterCovered(N)` is true (land blocks acoustic propagation and prevents crossing into disconnected water bodies).
      - Constrains propagation to the directional V-shaped sector of $W_i$ ($\pm 60^\circ$ half-angle around $\vec{d}_i$, $\text{dot} \ge 0.45\text{f}$ when 3D tile centers are available). Because adjacent hex neighbors around `stationTile` are separated by $60^\circ$, neighboring water tiles' V-sectors overlap seamlessly from shoreline to shoreline, and an island surrounded by 6 water tiles projects a full $360^\circ$ pattern with land obstacles blocking their respective sectors.
    - Merges reached water tiles into `outDetectionTiles` and `outIdentificationTiles` (`step <= maxIdentificationRadius`).
- `public static bool TryGetSonarDetectionDistance(PlanetTile stationTile, PlanetTile targetTile, int maxRadius, out int distance)`:
  - Fast rejection if `!IsTileWaterCovered(targetTile)` or `!HasCoastalAccess(stationTile)`.
  - Runs the per-adjacent-water-tile V-sector water-only BFS up to `maxRadius` and returns the minimum connected water traversal step distance to `targetTile`.

#### [NEW] [PlaceWorker_CoastalSonar.cs](../Geoscape/Source/Geoscape/Sensory/PlaceWorker_CoastalSonar.cs)

- Implement `PlaceWorker_CoastalSonar : PlaceWorker`:
  - Override `AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)`:
    - Require `map != null && map.Tile.Valid && SonarTraversalUtility.HasCoastalAccess(map.Tile)`.
    - Return `new AcceptanceReport("Requires direct coastal access (at least one adjacent Geoscape tile must be water-covered).")` when the settlement lacks an adjacent `WaterCovered` tile.

#### [MODIFY] [Buildings_Radar.xml](../Geoscape/1.7/Defs/ThingDefs_Buildings/Buildings_Radar.xml)

- Add `<placeWorkers><li>ProjectVigilance.Geoscape.Sensory.PlaceWorker_CoastalSonar</li></placeWorkers>` to `Geoscape_SonarArray`.

#### [MODIFY] [CompRadar.cs](../Geoscape/Source/Geoscape/Sensory/CompRadar.cs)

- In `IsSensorActive` and `GetOfflineReason()`: if `Props.isOceanicSonar` and `!SonarTraversalUtility.HasCoastalAccess(SensorTile)` (when `Find.World?.grid != null` or `SonarTraversalUtility.GetTileNeighborsOverride != null`), treat the sonar facility as offline (`"No Coastal Access"`).

#### [MODIFY] [WorldObjects_ListeningPost.xml](../Geoscape/1.7/Defs/WorldObjectDefs/WorldObjects_ListeningPost.xml)

- Add `WorldObjectDef` `Vigilance_ListeningPost_Sonar` (`label`: `"automated sonar station"`, `worldObjectClass`: `ProjectVigilance.Geoscape.Sensory.WorldObject_ListeningPost`, `texture`: `World/WorldObjects/Sites/Outpost`, `expandingIconTexture`: `World/WorldObjects/Expanding/Sites/Outpost`).

#### [MODIFY] [WorldObject_ListeningPost.cs](../Geoscape/Source/Geoscape/Sensory/WorldObject_ListeningPost.cs)

- Add `AutomatedSonar` to `ListeningPostKind`.
- Update `IsOceanicSonar => kind == ListeningPostKind.AutomatedSonar` and `IsTechnological => kind == ListeningPostKind.AutomatedRelay || kind == ListeningPostKind.AutomatedSonar`.
- Update `CalculateRadii`: for `ListeningPostKind.AutomatedSonar`, return `(18f, 8)` (matching coastal sonar range; no elevation vantage bonus).
- Update `Label`, `GetInspectString()`, `FinishConstruction()`, `CancelConstruction()`, `Dismantle()`, `HasRepairMaterials()`, and `ConsumeRepairMaterials()` to handle `ListeningPostKind.AutomatedSonar`.

#### [MODIFY] [OutpostMaterialUtility.cs](../Geoscape/Source/Geoscape/Sensory/OutpostMaterialUtility.cs)

- Add material cost constants and helpers for `AutomatedSonar` (120 Steel, 4 Components; repair: 30 Steel, 1 Component; salvage: 90 Steel, 3 Components): `CanDeployAutomatedSonar`, `ConsumeAutomatedSonarDeployment`.

#### [MODIFY] [CaravanArrivalAction_ConstructOutpost.cs](../Geoscape/Source/Geoscape/Sensory/CaravanArrivalAction_ConstructOutpost.cs), [CaravanExpeditionUtility.cs](../Geoscape/Source/Geoscape/Sensory/CaravanExpeditionUtility.cs), [OutpostAssemblySpeedUtility.cs](../Geoscape/Source/Geoscape/Sensory/OutpostAssemblySpeedUtility.cs), [FloatMenuMakerWorld_ChoicesAtFor_Patch.cs](../Geoscape/Source/Geoscape/Patches/FloatMenuMakerWorld_ChoicesAtFor_Patch.cs), and [WorldSelector_HandleWorldClicks_Patch.cs](../Geoscape/Source/Geoscape/Patches/WorldSelector_HandleWorldClicks_Patch.cs)

- Support `ListeningPostKind.AutomatedSonar` in caravan and settlement construction expeditions, material pre-allocation, and arrival actions.
- Enforce placement validation (`SonarTraversalUtility.HasCoastalAccess(targetTile)`): only allow deploying or dispatching an Automated Sonar Station on Geoscape tiles adjacent to at least one `WaterCovered` tile.

#### [MODIFY] [SensorDetectionUtility.cs](../Geoscape/Source/Geoscape/Sensory/SensorDetectionUtility.cs) and [SensoryManager.cs](../Geoscape/Source/Geoscape/Sensory/SensoryManager.cs)

- In `SensorDetectionUtility.TryGetDetectionDistance`: when `sensor.IsOceanicSonar` is true, delegate to `SonarTraversalUtility.TryGetSonarDetectionDistance(sensor.SensorTile, target.CurrentTile, maxSteps, out distance)`.
- In `SensoryManager`:
  - Change `stationaryExploredRadius` and `stationaryIdentifiedRadius` keys from `int` (`tileId`) to `(int tileId, bool isSonar)` so Radar and Sonar on the same coastal tile never collide in the stationary cache.
  - When `sensor.IsOceanicSonar` is true in `PerformSensorySweep`, `MarkSurroundingTilesExplored`, `MarkSurroundingTilesIdentified`, `PopulateActiveVisionTiles`, `HasActiveVision`, and `HasActiveIdentification`, use `SonarTraversalUtility` so sonar only explores, identifies, and highlights water-connected V-sector tiles originating from adjacent water tiles, while allowing Radar and Sonar coverage to overlap on water tiles without conflict.

---

### 3. Caravan Route Visibility Masking in Fog of War

Currently, [WorldObject_Incursion.cs](../Geoscape/Source/Geoscape/World/WorldObject_Incursion.cs) draws the full `pathTiles` array in `DrawPathOverlay()` once `intelligenceTier >= IntelligenceTier.Classified`, revealing origin settlements and destinations across unexplored shroud/haze. Similarly, [GroundedCaravanMeetingHelper.cs](../Geoscape/Source/Geoscape/Patches/GroundedCaravanMeetingHelper.cs) can mention the settlement name of an unexplored destination in the approaching contact toast.

#### [MODIFY] [WorldObject_Incursion.cs](../Geoscape/Source/Geoscape/World/WorldObject_Incursion.cs)

- Add a helper `DrawVisibilityClippedSegment(PlanetTile tileA, PlanetTile tileB, Vector3 posA, Vector3 posB, Material mat, float width, bool enforceVision, SensoryManager? sensory)`:
  - When `!enforceVision` (`VisibilityMode.Omniscient`), draw `posA -> posB` directly.
  - When `enforceVision` (`VisibilityMode.ClassicHazeOfWar`):
    - Both `tileA` and `tileB` have active vision (`sensory.HasActiveVision(...)`): draw full segment `posA -> posB`.
    - `tileA` in active vision, `tileB` outside: draw `posA -> Vector3.Lerp(posA, posB, 0.5f)` so the line terminates cleanly at the hex boundary of the sensor perimeter.
    - `tileA` outside, `tileB` in active vision: draw `Vector3.Lerp(posA, posB, 0.5f) -> posB`.
    - Neither in active vision: draw nothing.
- Apply this helper to all path segments in `DrawPathOverlay()` (including `Contact`-tier trailing segment, `departureDelayTicks > 0` mobilization, and `Classified` past/active segments).

#### [MODIFY] [GroundedCaravanMeetingHelper.cs](../Geoscape/Source/Geoscape/Patches/GroundedCaravanMeetingHelper.cs)

- In `GetTileDestinationLabel(PlanetTile destTile)`: if `GeoscapeRules.VisibilityMode != VisibilityMode.Omniscient` and `SensoryManager.Instance != null && !SensoryManager.Instance.IsExplored(destTile)`, return `"an uncharted destination"` instead of exposing an unexplored settlement's name.

---

### 4. Random Caravan Trade Loop Fix

Currently, when a player caravan intercepts a trade `WorldObject_Incursion` via `CaravanArrivalAction_InterceptIncursion`:

- Section `1a` in [WorldObject_Incursion.cs](../Geoscape/Source/Geoscape/World/WorldObject_Incursion.cs) triggers `ExecuteInterception` on same-tile or head-on contact **without clearing `c.pather.ArrivalAction` / calling `StopDead()`** and **without checking `IsCaravanOnCooldown(c.ID)`**.
- In [IncursionInterceptionUtility.cs](../Geoscape/Source/Geoscape/World/IncursionInterceptionUtility.cs), clicking `"Trade with caravan"` sets `caravan.pather.Paused = false` while leaving `CaravanArrivalAction_InterceptIncursion` intact, causing an infinite re-hail loop as soon as `Dialog_Trade` closes.

#### [MODIFY] [WorldObject_Incursion.cs](../Geoscape/Source/Geoscape/World/WorldObject_Incursion.cs)

- Add `public void ResetCaravanCooldown(int caravanId) => caravanCooldowns?.Remove(caravanId);`.
- In `GetFloatMenuOptions`: when the player explicitly selects `"Hail & Trade with ..."` (or `"Investigate ..."`), clear any stale cooldown (`ResetCaravanCooldown(caravan.ID)` and `ResetCaravanMet(caravan.ID)`) so manual re-hailing always works.
- In `TickInterval` section `1a`:
  - Guard with `!IsCaravanOnCooldown(c.ID)` and `!IsTradeOrEncounterDialogOpen()`.
  - Before calling `ExecuteInterception`, clear the arrival action / stop the pather (`c.pather.StopDead()`) so the intercept directive is consumed once triggered.

#### [MODIFY] [IncursionInterceptionUtility.cs](../Geoscape/Source/Geoscape/World/IncursionInterceptionUtility.cs)

- Add helper `ConsumeInterceptOrder(Caravan caravan, WorldObject_Incursion incursion)` which calls `caravan.pather.StopDead()` if `caravan.pather?.ArrivalAction is CaravanArrivalAction_InterceptIncursion intercept && intercept.TargetIncursion == incursion`.
- Call `ConsumeInterceptOrder(caravan, incursion)` inside `ExecuteFriendlyInterception` and `ExecuteHostileInterception`.
- In `ExecuteFriendlyInterception`'s `"Trade with caravan"` option: call `ConsumeInterceptOrder` / `StopDead()` instead of `caravan.pather.Paused = false`, and set cooldown (`Find.TickManager.TicksGame + 15000`) immediately so closing `Dialog_Trade` never re-triggers the encounter dialog.

#### [MODIFY] [CaravanArrivalAction_InterceptIncursion.cs](../Geoscape/Source/Geoscape/World/CaravanArrivalAction_InterceptIncursion.cs)

- In `Arrived(Caravan caravan)`: if `targetIncursion.IsCaravanOnCooldown(caravan.ID)` (e.g., already intercepted via `WorldObject_Incursion` tick), return cleanly without re-opening the dialog; otherwise ensure `caravan.pather?.StopDead()` is called when handing off to `ExecuteInterception`.

---

### 5. Optimistic Fog-of-War Navigation, Autonomous Rerouting & Hard-Stop Prompt (`TC-12`)

Caravans assume all unexplored terrain is potentially traversable until proven otherwise, preventing any information leakage when selecting destinations in the shroud. When an obstacle is discovered along the route, the caravan autonomously searches for an alternative route (emitting an advisory soft notification) and only halts with a high-priority player prompt when no viable route can be found or opportunistic pathfinding can no longer break even against the direct route objective.

#### [MODIFY] [WorldPathing_FindPath_Patch.cs](../Geoscape/Source/Geoscape/Patches/WorldPathing_FindPath_Patch.cs) and [Dialog_FormCaravan_Patches.cs](../Geoscape/Source/Geoscape/Patches/Dialog_FormCaravan_Patches.cs)

- Add `Caravan_PathFollower_StartPath_OptimisticPatch` (`Prefix` + `Finalizer` on `Caravan_PathFollower.StartPath`): activates `OptimisticPathingUtility.Active = true` for player caravans when `GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar`, so `StartPath`'s internal `WorldReachability.CanReach` and `IsPassable` / `Impassable` checks treat unexplored tiles as reachable and passable before `FindPath` runs.
- Add `OptimisticPathingUtility.Active` scoping (`Prefix` + `Finalizer`) on `Dialog_FormCaravan.CheckForErrors` and `Dialog_FormCaravan.TrySend` (in addition to `DoWindowContents` and `Notify_ChoseRoute` in [CaravanPathingResiliencePatches.cs](../Geoscape/Source/Geoscape/Patches/CaravanPathingResiliencePatches.cs)) so forming a caravan from a colony into unexplored black terrain never fails reachability checks.

#### [MODIFY] [OptimisticPathingUtility.cs](../Geoscape/Source/Geoscape/Sensory/OptimisticPathingUtility.cs)

- **Unit Movement Capability Abstraction (`UnitTraversalCapability`)**:
  - Define `public enum UnitTraversalCapability { LandCaravan, NavalUnit, Aircraft }` and `GetUnitCapability(Caravan? caravan)` (currently returns `UnitTraversalCapability.LandCaravan`).
  - Evaluate known-tile obstacles according to the unit's traversal capability (`IsTileObstacleForUnit(PlanetTile tile, UnitTraversalCapability capability, out string reason)`): for `LandCaravan`, oceans (`WaterCovered`), impassable mountains (`Hilliness.Impassable`), impassable biomes, and hazardous/impassable terrain (`Find.World.Impassable(tile)`) act as barriers.
- **Opportunistic Detour & Break-Even Evaluation**:
  - Track per-caravan expedition state (`(int caravanId, int destTileId)` -> `DetourBudgetState` recording initial direct distance, current `maxDetourMultiplier` defaulting to `2.0f`, and last notified obstacle tile).
  - When `NotifyCaravanVisionUpdated(Caravan caravan)` detects that a newly explored tile on the active route (or the destination tile itself) is an obstacle for the unit:
    1. **Autonomous Reroute Attempt**:
       - Attempt to calculate an updated optimistic path toward `destTile` (or `CaravanUtility.BestGotoDestNear(destTile, caravan)` if `destTile` itself is revealed to be impassable and a nearby passable tile is adjacent to the objective).
       - Evaluate whether the new path is **viable** ("breaks even" / makes meaningful progress): the path must exist (`curPath.Found`), and its remaining path length must not exceed the allowed detour threshold (`remainingNodes <= Mathf.Max(directDist * maxDetourMultiplier, directDist + 12)`).
    2. **Soft Notification (When Viable Route Exists)**:
       - Do **not** pause or prompt the player.
       - Continue moving along the updated route and emit the advisory notification:
         `"Caravan has encountered an obstacle and is searching for an alternative route."` (`MessageTypeDefOf.NeutralEvent`).
       - Invoke `OnCaravanReroutedForTesting?.Invoke(caravan, blockedTile, reason)`.
    3. **Hard Stop Condition & High-Priority Prompt (When No Viable Route Exists or Cannot Break Even)**:
       - Halt the caravan (`caravan.pather.StopDead()`) and invoke `OnCaravanHaltedForTesting?.Invoke(caravan, blockedTile, reason)`.
       - Generate a high-priority event / modal prompt (`Dialog_NodeTree` / `Dialog_MessageBox` when `Current.ProgramState == ProgramState.Playing`, and `OnHardStopPromptForTesting` hook):
         - **Prompt Text:** `"Caravan cannot determine a viable route to its destination and has halted awaiting new orders."`
         - **Player Actions:**
           1. **Select a new destination:** Selects the caravan on the Geoscape (`Find.WorldSelector.Select(caravan)`) and opens the World Route Planner / targeting flow so the player can choose a new destination.
           2. **Authorise a wider search radius:** Expands `maxDetourMultiplier` (e.g., to `4.0f` / unconstrained detour) for this destination and resumes pathfinding along the wider circumvention route if any finite path exists.
           3. **Cancel the expedition:** Clears any pending outpost directive for the destination and, if a player settlement is reachable on the landmass, plots a return path to the nearest player colony (or holds position if none).
           4. **Continue manual exploration:** Keeps the caravan halted at its current tile awaiting manual movement orders.

---

## Verification Plan

### Automated Tests

Update and run the `Geoscape.Tests` suite:

- **TC-03 Tests:**
  - Verify `SonarTraversalUtility.HasCoastalAccess` requires at least one adjacent `WaterCovered` tile.
  - Verify `SonarTraversalUtility.PopulateSonarCoverage` and `TryGetSonarDetectionDistance` originate from adjacent `WaterCovered` tiles, project in directional V-sectors, traverse strictly through connected `WaterCovered` tiles, never cross land barriers to disconnected water bodies, and seamlessly merge adjacent water sectors.
  - Verify `WorldObject_ListeningPost` (`ListeningPostKind.AutomatedSonar`) placement validation, radii (`18` detection / `8` identification), and `SensoryManager` `(tileId, isSonar)` cache isolation when overlapping with Radar.
- **Route Masking Tests:** Verify `WorldObject_Incursion` visibility/clipping helper hides segments outside `HasActiveVision` and clips boundary crossings at `0.5f`.
- **Trade Loop Tests:** Verify `ConsumeInterceptOrder` clears `CaravanArrivalAction_InterceptIncursion` and `TickInterval` `1a` respects `IsCaravanOnCooldown`.
- **TC-12 Tests:**
  - Verify `Caravan_PathFollower.StartPath` optimistic scope allows routing to unexplored impassable tiles without leaking terrain state.
  - Verify discovering a circumvention-viable obstacle triggers autonomous rerouting and the soft advisory notification without halting the caravan.
  - Verify discovering an uncircumventable barrier (or one exceeding the break-even detour threshold) halts the caravan and triggers the hard-stop prompt with its 4 player actions (`Select a new destination`, `Authorise a wider search radius`, `Cancel the expedition`, `Continue manual exploration`).

### Manual In-Game Verification

1. **TC-03:**
   - Attempt to construct `Geoscape_SonarArray` at an inland settlement (blocked by `PlaceWorker_CoastalSonar`) vs. a coastal settlement (allowed).
   - Deploy an `Automated Sonar Station` with a caravan on a coastal land hex, peninsula, or island; confirm sonar coverage projects outward in V-shaped sectors over connected water tiles only and is blocked by land barriers.
2. **Route Masking:** Trigger or spawn an approaching caravan near radar perimeter; select it and confirm its path line stops cleanly at the sensor perimeter edge without revealing origin/destination settlements in the shroud.
3. **Trade Loop:** Right-click `Hail & Trade` on a friendly trade caravan, complete a trade in `Dialog_Trade`, and close it; confirm the caravan does not re-open the encounter/trade dialog and can freely move away.
4. **TC-12:** Under `Classic Haze of War`, order a caravan into unexplored territory behind an obstacle; confirm it autonomously reroutes around minor obstacles with a soft advisory message, and only halts with the high-priority prompt when the destination is unreachable or no viable route can break even.
