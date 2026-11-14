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
5. **TC-12 Fix (Optimistic Fog-of-War Routing, Autonomous Obstacle Circumvention, Pre-Entry Safeguards & Unexplored Road Discount Masking):** Ensure land caravans assume all unexplored terrain is traversable at baseline `1.0f` movement difficulty without leaking unexplored road speed discounts (`WorldGrid_GetRoadMovementDifficultyMultiplier_Patch`), autonomously reroute around newly discovered obstacles with an advisory soft notification, guard against stepping onto impassable tiles before entry (`Caravan_PathFollower_TryEnterNextPathTile_Patch` and `Caravan_PathFollower_SetupMoveIntoNextTile_Patch` using `MarkSurroundingTilesExplored(..., 0f)` with `maxSteps = 0` and `StopDead()`), and only halt with a high-priority player prompt when no viable route can break even or make meaningful progress toward the destination.
6. **Unified Outpost Kind Labels:** Standardise structure names (`"Automated Listening Post"`, `"Automated Sonar Station"`, and `"Signal Beacon"`) via `CaravanExpeditionUtility.GetListeningPostKindLabel` across `WorldObject_ListeningPost`, `Dialog_FormCaravan_Patches`, and `OutpostAssemblySpeedUtility`.
7. **Grounded Storyteller Caravan Ambushes & Post-Retreat Hostile Pursuit:** Ground storyteller enemy faction ambushes (`IncidentWorker_Ambush_EnemyFaction`) into persistent `WorldObject_Incursion` entities via `IncidentWorker_Ambush_Patch` and `GroundedCaravanMeetingHelper.TryInterceptAmbush`, and initiate active Geoscape pursuit via `IncursionTacticalRetreatUtility.ResumeIncursionAfterCaravanRetreat` in `Pawn_ExitMap_Patch` when at least 50% of surviving hostiles remain fit.

---

## Proposed Changes

### 1. Test Plan Sign-Off (`TC-02`)

#### [MODIFY] [milestone_1_3_test_plan.md](milestone_1_3_test_plan.md)

- Update **TC-02** status badge from `🟡 Focus 2: Playtest` to `🟢 In-Game Pass` and check off its checklist entry (`[x] 🟢 **TC-02**`).

---

### 2. Coastal Sonar Traversal, Automated Sonar Station & Settlement Validation (`TC-03`)

Sonar is modelled as a coastal capability rather than a land-radius capability. Both standalone Automated Sonar Stations and settlement Sonar Facilities require at least one adjacent `WaterCovered` Geoscape tile, originate acoustic propagation from their adjacent `WaterCovered` tiles, project outward in V-shaped sectors per adjacent water tile, and traverse exclusively through connected `WaterCovered` tiles so land acts as an acoustic barrier.

#### [NEW] [SonarTraversalUtility.cs](../Geoscape/Source/Geoscape/Sensory/SonarTraversalUtility.cs)

- Provide test overrides:
  - `public static Func<PlanetTile, bool>? IsTileWaterCoveredOverride { get; set; }`
  - `public static Action<PlanetTile, List<PlanetTile>>? GetTileNeighborsOverride { get; set; }`
  - `public static Func<PlanetTile, Vector3>? GetTileCenterOverride { get; set; }`
- `public static bool IsTileWaterCovered(PlanetTile tile)`:
  - Evaluates `IsTileWaterCoveredOverride(tile)` if set, otherwise `tile.Valid && tile.Tile?.WaterCovered == true`.
- `public static void GetAdjacentWaterTiles(PlanetTile stationTile, List<PlanetTile> outWaterNeighbors)` and `public static bool HasCoastalAccess(PlanetTile stationTile)`:
  - Queries immediate Geoscape neighbours of `stationTile` and collects those where `IsTileWaterCovered(neighbour)` is true.
  - Returns true when at least one adjacent Geoscape tile is `WaterCovered` (independent of whether `stationTile` itself contains water).
- `public static void PopulateSonarCoverage(PlanetTile stationTile, int maxDetectionRadius, int maxIdentificationRadius, HashSet<PlanetTile> outDetectionTiles, HashSet<PlanetTile>? outIdentificationTiles = null, Dictionary<PlanetTile, int>? outMinSteps = null)`:
  - Collects each adjacent `WaterCovered` tile $W_i$ of `stationTile`.
  - For each $W_i$ independently:
    - Computes the outward direction vector $\vec{d}_i$ from `stationTile` through $W_i$ (tangent-projected when 3D tile centres are available via `GetTileCenterOverride` or `Find.WorldGrid`).
    - Runs a water-only BFS starting at $(W_i, \text{step} = 1)$ up to `maxDetectionRadius`:
      - Enqueues neighbours $N$ only if `IsTileWaterCovered(N)` is true (land blocks acoustic propagation and prevents crossing into disconnected water bodies).
      - Constrains propagation to the directional V-shaped sector of $W_i$ ($\pm 60^\circ$ half-angle around $\vec{d}_i$, $\text{dot} \ge 0.45\text{f}$ when 3D tile centres are available). Because adjacent hex neighbours around `stationTile` are separated by $60^\circ$, neighbouring water tiles' V-sectors overlap seamlessly from shoreline to shoreline, and an island surrounded by 6 water tiles projects a full $360^\circ$ pattern with land obstacles blocking their respective sectors.
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

### 5. Optimistic Fog-of-War Navigation, Autonomous Re-routing, Pre-Entry Safeguards & Unexplored Road Masking (`TC-12`)

Caravans assume all unexplored terrain is potentially traversable at baseline `1.0f` traversal cost until proven otherwise, preventing terrain or road-network leaks when selecting destinations in the shroud. When an obstacle is discovered along the route, the caravan autonomously searches for an alternative route (emitting an advisory soft notification) and only halts with a high-priority player prompt when no viable route can be found or opportunistic pathfinding can no longer break even against the direct route objective. Pre-entry impassability safeguards verify upcoming path nodes before movement so even expanded detour searches cannot step onto impassable tiles.

#### [MODIFY] [WorldPathing_FindPath_Patch.cs](../Geoscape/Source/Geoscape/Patches/WorldPathing_FindPath_Patch.cs), [SensoryManager.cs](../Geoscape/Source/Geoscape/Sensory/SensoryManager.cs), and [Dialog_FormCaravan_Patches.cs](../Geoscape/Source/Geoscape/Patches/Dialog_FormCaravan_Patches.cs)

- Add `Caravan_PathFollower_StartPath_Optimistic_Patch` (`Prefix` + `Finalizer` on `Caravan_PathFollower.StartPath`): activates `OptimisticPathingUtility.Active = true` for player caravans when `GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar`, so `StartPath`'s internal `WorldReachability.CanReach` and `IsPassable` / `Impassable` checks treat unexplored tiles as reachable and passable before `FindPath` runs.
- Add `Dialog_FormCaravan_CheckForErrors_Optimistic_Patch` and `Dialog_FormCaravan_TrySend_Optimistic_Patch` (`Prefix` + `Finalizer` scoping `OptimisticPathingUtility.Active`, complementing `DoWindowContents` and `Notify_ChoseRoute` in [CaravanPathingResiliencePatches.cs](../Geoscape/Source/Geoscape/Patches/CaravanPathingResiliencePatches.cs)) so forming a caravan from a colony into unexplored black terrain never fails reachability checks.
- **Pre-Entry Impassable Tile Safeguards:**
  - In `SensoryManager.MarkSurroundingTilesExplored(PlanetTile centerTile, float radiusInTiles, bool isSonar = false)`: treat `radiusInTiles <= 0f` as `maxSteps = 0` so pre-entry safeguards uncover only the single blocked tile without exposing adjacent shrouded tiles.
  - In `Caravan_PathFollower_TryEnterNextPathTile_Patch` (`[HarmonyPatch(typeof(Caravan_PathFollower), "TryEnterNextPathTile")]`): for player caravans in `VisibilityMode.ClassicHazeOfWar`, check `__instance.nextTile`; when unexplored and `OptimisticPathingUtility.IsTileActuallyImpassable(nextTile)` is true, reveal that single tile via `SensoryManager.Instance?.MarkSurroundingTilesExplored(nextTile, 0f)`, invoke `OptimisticPathingUtility.NotifyCaravanVisionUpdated(caravan)`, and return `false` to prevent entering the blocked tile.
  - In `Caravan_PathFollower_SetupMoveIntoNextTile_Patch` (`[HarmonyPatch(typeof(Caravan_PathFollower), "SetupMoveIntoNextTile")]`): because vanilla consumes the next path node inside `SetupMoveIntoNextTile` after entering the current tile, inspect `path.Peek(1)` when `path != null && path.Found && path.NodesLeftCount >= 2`. When `OptimisticPathingUtility.IsTileActuallyImpassable(upcoming)` is true, reveal that single tile via `SensoryManager.Instance?.MarkSurroundingTilesExplored(upcoming, 0f)`, invoke `OptimisticPathingUtility.NotifyCaravanVisionUpdated(caravan)`, and halt via `__instance.StopDead()` if recovery does not replace the stale path while still moving, returning `false`.
- **Unexplored Road Discount Masking:**
  - Add `WorldGrid_GetRoadMovementDifficultyMultiplier_Patch` (`Postfix` on `WorldGrid.GetRoadMovementDifficultyMultiplier(PlanetTile, PlanetTile, StringBuilder)`): when `__result < 1f` during active optimistic routing (`OptimisticPathingUtility.ShouldUseOptimisticPathing()` or `OptimisticPathingUtility.IsPlayerPlanningRoute && GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar`), return `1f` whenever `fromTile` or `toTile` is unexplored (`OptimisticPathingUtility.IsTileUnexplored`), preventing plotted routes from tracing undiscovered roads through the shroud.

#### [MODIFY] [OptimisticPathingUtility.cs](../Geoscape/Source/Geoscape/Sensory/OptimisticPathingUtility.cs)

- **Unit Movement Capability Abstraction (`UnitTraversalCapability`):**
  - Define `public enum UnitTraversalCapability { LandCaravan, NavalUnit, Aircraft }` and evaluate known-tile obstacles via `IsTileObstacleForUnit(PlanetTile tile, UnitTraversalCapability capability = UnitTraversalCapability.LandCaravan)` paired with `GetImpassableReason(PlanetTile tile)`.
  - Provide `IsTileActuallyImpassable(PlanetTile tile)` checking genuine movement difficulty via `WorldPathGrid.CalculatedMovementDifficultyAt(tile, perceivedStatic: true) < 1000f` (with `IsTileImpassableOverride` test hook) without consulting optimistic passability overrides for unexplored tiles.
- **Opportunistic Detour & Break-Even Evaluation:**
  - Track per-caravan expedition state (`int` caravan ID $\rightarrow$ `ExpeditionDetourState` recording `InitialDirectDistance`, `MaxDetourMultiplier` defaulting to `2.0f`, and `NotifiedObstacleTiles`).
  - When `NotifyCaravanVisionUpdated(Caravan caravan)` detects that a newly explored tile on the active route (or the destination tile itself) is an obstacle for the unit:
    1. **Autonomous Reroute Attempt:**
       - Attempt to calculate an updated optimistic path toward `destTile` (or `CaravanUtility.BestGotoDestNear(destTile, caravan)` if `destTile` itself is revealed to be impassable and a nearby passable tile is adjacent to the objective).
       - Evaluate whether the new path is **viable** ("breaks even" / makes meaningful progress): the caravan remains moving with `curPath.Found`, and `remainingNodes <= detourState.InitialDirectDistance * detourState.MaxDetourMultiplier` when `InitialDirectDistance > 0f`.
    2. **Soft Notification (When Viable Route Exists):**
       - Do **not** pause or prompt the player.
       - Continue moving along the updated route and emit the advisory notification:
         `"Caravan has encountered an obstacle and is searching for an alternative route."` (`MessageTypeDefOf.NeutralEvent`).
       - Invoke `OnCaravanReroutedForTesting?.Invoke(caravan, blockedTile, reason)`.
    3. **Hard Stop Condition & High-Priority Prompt (When No Viable Route Exists or Cannot Break Even):**
       - Halt the caravan (`caravan.pather.StopDead()`) and invoke `OnCaravanHaltedForTesting?.Invoke(caravan, blockedTile, reason)` and `OnCaravanHardStopPromptForTesting?.Invoke(caravan, blockedTile, reason)`.
       - Generate a high-priority `Dialog_NodeTree` prompt when `Current.ProgramState == ProgramState.Playing`:
         - **Prompt Text:** `"Caravan cannot determine a viable route to its destination and has halted awaiting new orders."`
         - **Player Actions:**
           1. **Select a new destination:** Jumps to and selects the caravan on the Geoscape (`CameraJumper.TryJumpAndSelect(caravan)`).
           2. **Authorise a wider search radius:** Increases `state.MaxDetourMultiplier` by `1.5f` and resumes pathfinding toward `caravan.pather.Destination` (guarded before entry by `TryEnterNextPathTile` and `SetupMoveIntoNextTile` safeguards).
           3. **Cancel the expedition:** Plots a return path to the nearest player settlement (`CaravanArrivalAction_Enter`).
           4. **Continue manual exploration:** Keeps the caravan halted at its current tile awaiting manual movement orders.

---

### 6. Unified Outpost Kind Labels (`TC-03` / `TC-05`)

Outpost construction, dismantle confirmation, assembly estimate, and world inspect strings previously used inconsistent hard-coded labels (`"Automated Sensor Relay"`, `"Automated Post"`, `"Automated Coastal Sonar Station"`, or missing `AutomatedSonar` handling). All paths now share a single helper.

#### [MODIFY] [CaravanExpeditionUtility.cs](../Geoscape/Source/Geoscape/Sensory/CaravanExpeditionUtility.cs)

- Add `internal static string GetListeningPostKindLabel(ListeningPostKind kind)` returning:
  - `ListeningPostKind.AutomatedRelay` $\rightarrow$ `"Automated Listening Post"`
  - `ListeningPostKind.AutomatedSonar` $\rightarrow$ `"Automated Sonar Station"`
  - `ListeningPostKind.SignalBeacon` $\rightarrow$ `"Signal Beacon"`
  - Fallback $\rightarrow$ `"Unknown Outpost"`

#### [MODIFY] [WorldObject_ListeningPost.cs](../Geoscape/Source/Geoscape/Sensory/WorldObject_ListeningPost.cs), [Dialog_FormCaravan_Patches.cs](../Geoscape/Source/Geoscape/Patches/Dialog_FormCaravan_Patches.cs), and [OutpostAssemblySpeedUtility.cs](../Geoscape/Source/Geoscape/Sensory/OutpostAssemblySpeedUtility.cs)

- Update `WorldObject_ListeningPost.Label` (fallback when `def == null`) and `GetInspectString()` (`Type:` row, retaining `({stuffDef.LabelCap})` when `SignalBeacon` uses a `stuffDef`) to call `CaravanExpeditionUtility.GetListeningPostKindLabel(kind)`.
- Update `Dialog_FormCaravan_PostOpen_Patch` (dismantle route notice) and `Dialog_FormCaravan_TrySend_Patch` (construction and dismantle confirmation dialogues) to resolve structure labels through `CaravanExpeditionUtility.GetListeningPostKindLabel(directive.Kind)`.
- Update `OutpostAssemblySpeedUtility.GetEstimateLabel` to call `CaravanExpeditionUtility.GetListeningPostKindLabel(kind)`.

---

### 7. Grounded Storyteller Caravan Ambushes & Post-Retreat Hostile Pursuit (`TC-09`)

Storyteller enemy faction ambushes against player caravans previously bypassed the Geoscape incursion system, causing surviving ambushers to vanish when the caravan retreated off the tactical map. Enemy faction ambushes are now grounded into persistent `WorldObject_Incursion` entities that can actively pursue retreating caravans.

#### [NEW] [IncidentWorker_Ambush_Patch.cs](../Geoscape/Source/Geoscape/Patches/IncidentWorker_Ambush_Patch.cs)

- Patch `IncidentWorker_Ambush.TryExecuteWorker` (`[HarmonyPatch(typeof(IncidentWorker_Ambush), "TryExecuteWorker")]`):
  - If `__instance is not IncidentWorker_Ambush_EnemyFaction`, return `true` so wildlife ambushes (`IncidentWorker_Ambush_ManhunterPack`) and other non-faction ambush workers remain vanilla.
  - Otherwise delegate to `GroundedCaravanMeetingHelper.TryInterceptAmbush`.

#### [MODIFY] [GroundedCaravanMeetingHelper.cs](../Geoscape/Source/Geoscape/Patches/GroundedCaravanMeetingHelper.cs)

- Add `TryInterceptAmbush`:
  - Validates that the incident target is a spawned player `Caravan` on a valid `PlanetTile`.
  - Calls `CreateGroundedIncursion` (`groupKind: IncursionGroupKind.HostileWarband`, `isHostile: true`, `spawnAtCurrentTile: true`) to instantiate a persistent hostile `WorldObject_Incursion` at the caravan's tile.
  - Invokes `IncursionInterceptionUtility.ExecuteHostileInterception(caravan, incursion, isAmbush: true)` so the ambush map opens with ambushers registered to the grounded incursion.

#### [MODIFY] [IncursionTacticalRetreatUtility.cs](../Geoscape/Source/Geoscape/World/IncursionTacticalRetreatUtility.cs) and [Pawn_ExitMap_Patch.cs](../Geoscape/Source/Geoscape/Patches/Pawn_ExitMap_Patch.cs)

- Define `internal const float PursuitMinimumFitFraction = 0.5f;` and `ShouldPursueRetreatingCaravan(bool isHostileToPlayer, bool isTradeCaravan, int livingCount, int fitCount)`:
  - Returns `false` if `!isHostileToPlayer || isTradeCaravan || livingCount <= 0`.
  - Returns `true` when `fitCount >= Math.Ceiling(livingCount * PursuitMinimumFitFraction)` ($\ge 50\%$ fit survivors).
- Add `public static void ResumeIncursionAfterCaravanRetreat(WorldObject_Incursion incursion, Caravan? caravan, List<Pawn> survivors)`:
  - Counts non-dead, non-downed survivors (`fit`).
  - When `caravan` is spawned and `ShouldPursueRetreatingCaravan` succeeds, builds a pursuit path via `IncursionOriginResolver.TryBuildPath(incursion.Tile, caravan.Tile, out var built)` (or direct neighbour fallback via `Find.WorldGrid.IsNeighborOrSame`), calls `incursion.SetPursuitTarget(caravan, path)` (displaying `[Pursuit]` on the incursion label), and emits `"{incursion.LabelCap} is giving chase to {caravan.LabelCap}!"` (`MessageTypeDefOf.ThreatBig`).
  - Otherwise falls back to `incursion.AdvancePathTile()` so battered or non-hostile forces resume their route or return home.
- In `Pawn_ExitMap_Patch`, replace the unconditional `incursion.AdvancePathTile()` on non-captive caravan retreat with `IncursionTacticalRetreatUtility.ResumeIncursionAfterCaravanRetreat(incursion, caravan, livingHostiles)`.

---

## Verification Plan

### Automated Tests

Update and run the `Geoscape.Tests` suite (406 tests):

- **TC-03 & Outpost Label Tests:**
  - Verify `SonarTraversalUtility.HasCoastalAccess` requires at least one adjacent `WaterCovered` tile.
  - Verify `SonarTraversalUtility.PopulateSonarCoverage` and `TryGetSonarDetectionDistance` originate from adjacent `WaterCovered` tiles, project in directional V-sectors, traverse strictly through connected `WaterCovered` tiles, never cross land barriers to disconnected water bodies, and seamlessly merge adjacent water sectors.
  - Verify `WorldObject_ListeningPost` (`ListeningPostKind.AutomatedSonar`) placement validation, radii (`18` detection / `8` identification), `SensoryManager` `(tileId, isSonar)` cache isolation when overlapping with Radar, and `CaravanExpeditionUtility.GetListeningPostKindLabel` (`"Automated Listening Post"`, `"Automated Sonar Station"`, `"Signal Beacon"`).
- **Route Masking Tests:** Verify `WorldObject_Incursion` visibility/clipping helper hides segments outside `HasActiveVision` and clips boundary crossings at `0.5f`.
- **Trade Loop Tests:** Verify `ConsumeInterceptOrder` clears `CaravanArrivalAction_InterceptIncursion` and `TickInterval` `1a` respects `IsCaravanOnCooldown`.
- **TC-09 Ambush & Pursuit Tests:**
  - Verify `IncidentWorker_Ambush_Patch` targets `IncidentWorker_Ambush.TryExecuteWorker`.
  - Verify `IncursionTacticalRetreatUtility.ShouldPursueRetreatingCaravan` initiates pursuit only for non-trade hostile forces with $\ge 50\%$ fit survivors.
- **TC-12 Tests:**
  - Verify `Caravan_PathFollower.StartPath` optimistic scope and `FindPath_Patch.Transpiler` (`ModifyMovementDifficulty`) allow routing to unexplored impassable tiles without leaking terrain state.
  - Verify `TryEnterNextPathTile_UnexploredImpassableTile_BlocksEntry` and `SetupMoveIntoNextTilePatch_TargetsVanillaMethod_AndIgnoresNonPlayerPathers` guard against stepping onto unexplored impassable tiles.
  - Verify discovering a circumvention-viable obstacle triggers autonomous re-routing and the soft advisory notification without halting the caravan.
  - Verify discovering an uncircumventable barrier (or one exceeding the break-even detour threshold) halts the caravan and triggers the hard-stop prompt with its 4 player actions (`Select a new destination`, `Authorise a wider search radius`, `Cancel the expedition`, `Continue manual exploration`).

### Manual In-Game Verification

1. **TC-03 & Outpost Labels:**
   - Attempt to construct `Geoscape_SonarArray` at an inland settlement (blocked by `PlaceWorker_CoastalSonar`) vs. a coastal settlement (allowed).
   - Deploy an `Automated Sonar Station` with a caravan on a coastal land hex, peninsula, or island; confirm confirmation dialogues display `"Automated Sonar Station"` (and `"Automated Listening Post"` / `"Signal Beacon"` for the other outpost types), and confirm sonar coverage projects outward in V-shaped sectors over connected water tiles only and is blocked by land barriers.
2. **Route Masking:** Trigger or spawn an approaching caravan near radar perimeter; select it and confirm its path line stops cleanly at the sensor perimeter edge without revealing origin/destination settlements in the shroud.
3. **Trade Loop:** Right-click `Hail & Trade` on a friendly trade caravan, complete a trade in `Dialog_Trade`, and close it; confirm the caravan does not re-open the encounter/trade dialog and can freely move away.
4. **TC-09 Ambush & Pursuit:** Trigger `Ambush_EnemyFaction` against a caravan and retreat all colonists while raiders survive; confirm the raiders persist as a `WorldObject_Incursion`, pursue when $\ge 50\%$ of survivors are fit (`[Pursuit]`), and resume/retreat otherwise, while `Ambush_ManhunterPack` remains vanilla.
5. **TC-12:** Under `Classic Haze of War`, order a caravan across an unexplored road and into unexplored territory behind an obstacle; confirm plotted routes do not trace undiscovered roads (`1.0f` cost), the caravan autonomously reroutes around minor obstacles with a soft advisory message, only halts with the high-priority prompt when the destination is unreachable or no viable route can break even, and never enters an impassable tile even after selecting `Authorise a wider search radius`.
