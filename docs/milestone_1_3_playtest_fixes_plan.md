# Implementation Plan: Milestone 1.3 Playtest Fixes & Test Plan Updates

## Goal Description

Address all findings from the Milestone 1.3 in-game playtest:

1. **TC-02 Sign-Off:** Record **TC-02** (elevation vantage, mountain masking, and SIGINT penetration) as passed in [milestone_1_3_test_plan.md](milestone_1_3_test_plan.md).
2. **TC-03 Fix (Coastal Sonar Array):** Restrict `Geoscape_SonarArray` (`IsOceanicSonar`) coverage, Fog-of-War exploration/identification, and radar overlay rendering strictly to water-covered hexes (`tile.WaterCovered == true`).
3. **Caravan Route Visibility Masking:** Clip `WorldObject_Incursion` route rendering to tiles inside active detection range so paths terminate cleanly at the sensor perimeter edge instead of leaking origin/destination settlements in the shroud.
4. **Random Caravan Trade Loop Fix:** Eliminate the infinite re-hail / trade loop when interacting with grounded `WorldObject_Incursion` trade caravans by consuming the pending `CaravanArrivalAction_InterceptIncursion` and enforcing encounter cooldowns on `1a` interception checks.
5. **TC-12 Fix (Optimistic Fog-of-War Routing & Obstacle Circumvention):** Ensure player caravans can always plot routes into unexplored black tiles without premature `"not accessible"` / `"cannot reach"` errors, and prompt the player when hitting a newly discovered obstacle to choose whether to try circumventing or halt.

---

## Open Questions

> [!IMPORTANT]
> Please confirm your preference on two design choices before execution:
>
> 1. **TC-03 Sonar Traversal vs. Output Filtering:** Should the Coastal Sonar Station (`IsOceanicSonar`) allow BFS distance calculation across land tiles while only exposing/highlighting `WaterCovered` tiles (matching existing [SensorDetectionUtility.cs](../Geoscape/Source/Geoscape/Sensory/SensorDetectionUtility.cs) and working even if a colony is 1 tile inland), **or** should sonar waves propagate *strictly* through connected `WaterCovered` tiles from the shoreline (preventing sonar from crossing a landmass to scan a separate ocean/lake — which would also be updated in `SensorDetectionUtility`)?
> 2. **TC-12 Obstacle Prompt Timing:** When marching into the black, should the `"immediate path appears blocked — try circumventing?"` prompt trigger **right when the caravan reaches the obstacle edge** (`nextTile` is impassable), **or as soon as an impassable tile on the route enters the caravan's 2–3 hex scout horizon**?

---

## Proposed Changes

### 1. Test Plan Sign-Off (`TC-02`)

#### [MODIFY] [milestone_1_3_test_plan.md](milestone_1_3_test_plan.md)

- Update **TC-02** status badge from `🟡 Focus 2: Playtest` to `🟢 In-Game Pass` and check off its checklist entry (`[x] 🟢 **TC-02**`).

---

### 2. Coastal Hydrophone Sonar Water-Only Filtering (`TC-03`)

Currently, [SensorDetectionUtility.cs](../Geoscape/Source/Geoscape/Sensory/SensorDetectionUtility.cs) filters target detections by `targetTileData.WaterCovered`, **but** [SensoryManager.cs](../Geoscape/Source/Geoscape/Sensory/SensoryManager.cs) omits the `IsOceanicSonar` check in `PopulateActiveVisionTiles`, `EvaluateAllContacts`, `MarkSurroundingTilesExplored`, `MarkSurroundingTilesIdentified`, `HasActiveVision`, and `HasActiveIdentification`. Additionally, `stationaryExploredRadius` and `stationaryIdentifiedRadius` are keyed solely by `tileId`, causing cache collisions when a coastal colony hosts both a sonar array and a land sensor.

#### [MODIFY] [SensoryManager.cs](../Geoscape/Source/Geoscape/Sensory/SensoryManager.cs)

- Add `public static Func<PlanetTile, bool>? TileWaterCoveredOverride { get; set; }` and a helper `IsTileWaterCovered(PlanetTile tile)` (returns `TileWaterCoveredOverride(tile)` if set, otherwise `tile.Valid && tile.Tile?.WaterCovered == true`).
- Change `stationaryExploredRadius` and `stationaryIdentifiedRadius` keys from `int` (`tileId`) to `(int tileId, bool isSonar)` so stationary land and sonar sensors on the same tile track radii independently.
- Add `bool waterOnly = false` parameter to `MarkSurroundingTilesExplored` and `MarkSurroundingTilesIdentified`; when `waterOnly` is true, skip marking `exploredBytes` / `identifiedBytes` (and dirtying haze/objects) for tiles where `!IsTileWaterCovered(currentTile)`.
- Pass `waterOnly: sensor.IsOceanicSonar` from `EvaluateAllContacts`.
- In `PopulateActiveVisionTiles`, when `sensor.IsOceanicSonar` is true:
  - Only add `currentTile` to `outTiles` and `outIdentifiedTiles` when `IsTileWaterCovered(currentTile)` is true.
  - (If Water-Connected Propagation is selected in Open Question #1: also gate neighbor enqueueing on `currentStep == 0 || IsTileWaterCovered(neighbor)` and mirror in `SensorDetectionUtility`.)
- In `HasActiveVision` and `HasActiveIdentification` fallback loops, skip `sensor.IsOceanicSonar` sensors if `!IsTileWaterCovered(tile)`.

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

### 5. Optimistic Fog-of-War Travel Routing & Obstacle Circumvention (`TC-12`)

Currently:

1. [WorldPathing_FindPath_Patch.cs](../Geoscape/Source/Geoscape/Patches/WorldPathing_FindPath_Patch.cs) activates `OptimisticPathingUtility.Active` inside `FindPath` and `NeedNewPath`, **not** inside `Caravan_PathFollower.StartPath` or `Dialog_FormCaravan.TrySend` / `CheckForErrors`. Because RimWorld's `StartPath` runs `WorldReachability.CanReach` and `IsPassable` *before* calling `FindPath`, ordering a caravan into an unexplored ocean, impassable mountain, or disconnected continent rejects the order as `"not accessible"` / `"cannot find a route"`.
2. [OptimisticPathingUtility.cs](../Geoscape/Source/Geoscape/Sensory/OptimisticPathingUtility.cs) silently auto-reroutes up to 30 nodes ahead instead of pausing when hitting a newly uncovered obstacle to ask the player whether to try circumventing or halt.

#### [MODIFY] [WorldPathing_FindPath_Patch.cs](../Geoscape/Source/Geoscape/Patches/WorldPathing_FindPath_Patch.cs)

- Add `Caravan_PathFollower_StartPath_OptimisticPatch` (`Prefix` + `Finalizer` on `Caravan_PathFollower.StartPath`): activates `OptimisticPathingUtility.Active = true` for player caravans when `GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar`, so `StartPath`'s internal `WorldReachability.CanReach` and `IsPassable` / `Impassable` checks treat unexplored tiles as reachable and passable before `FindPath` runs.
- Add `OptimisticPathingUtility.Active` scoping (`Prefix` + `Finalizer`) on `Dialog_FormCaravan.CheckForErrors` and `Dialog_FormCaravan.TrySend` (in addition to `DoWindowContents` and `Notify_ChoseRoute` in [CaravanPathingResiliencePatches.cs](../Geoscape/Source/Geoscape/Patches/CaravanPathingResiliencePatches.cs)) so forming a caravan from a colony into unexplored black terrain never fails reachability checks.
- Also in `WorldRoutePlanner`: ensure `ShouldUseOptimisticPathing()` covers `IsPlayerPlanningRoute` consistently (including `WorldRoutePlanner.TryAddWaypoint` and mouse-hover route checks).

#### [MODIFY] [OptimisticPathingUtility.cs](../Geoscape/Source/Geoscape/Sensory/OptimisticPathingUtility.cs)

- Track pending/prompted obstacle keys `(caravanId, blockedTileId)` to prevent duplicate dialogs for the same blockage.
- Add `public static Action<Caravan, PlanetTile, string, Action, Action>? OnObstaclePromptForTesting;` for headless test verification.
- Refactor `NotifyCaravanVisionUpdated(Caravan caravan)` (and `TryEnterNextPathTile` callback):
  - Check whether the **immediate path ahead** is blocked (`nextTile` or immediate upcoming path node per Open Question #2, or `destTile` itself when within visual range).
  - When a newly explored obstacle blocks the path:
    - Pause the caravan (`caravan.pather.Paused = true`) and advise the player via a confirmation dialog (`Dialog_NodeTree` / `Dialog_MessageBox`):
      - **Message:** `"{caravan.LabelCap}'s immediate path at sector {blockedTile.tileId} appears blocked by {reason}. Would you like to try circumventing?"`
      - **"Try circumventing":** Unpause and invoke optimistic `StartPath` around the newly revealed obstacle (or `BestGotoDestNear` if destination itself is impassable). If no passable route around it exists, call `StopDead()` and notify the player that the caravan cannot circumvent and has halted.
      - **"Halt here":** Call `caravan.pather.StopDead()` so the caravan holds position awaiting manual orders.
  - Preserve deterministic synchronous behavior in unit tests when `OnObstaclePromptForTesting` or `Current.ProgramState != ProgramState.Playing` is active so existing/new automated tests verify both circumventing and halting cleanly.

---

## Verification Plan

### Automated Tests

Update and run the `Geoscape.Tests` suite:

- **TC-03 Tests:** Add unit tests verifying `PopulateActiveVisionTiles`, `MarkSurroundingTilesExplored`, `MarkSurroundingTilesIdentified`, `HasActiveVision`, and `HasActiveIdentification` ignore land tiles and only expose `WaterCovered` tiles for `IsOceanicSonar` sensors, and do not collide with land sensors on the same tile.
- **Route Masking Tests:** Verify `WorldObject_Incursion` visibility/clipping helper hides segments outside `HasActiveVision` and clips boundary crossings at `0.5f`.
- **Trade Loop Tests:** Verify `ConsumeInterceptOrder` clears `CaravanArrivalAction_InterceptIncursion` and `TickInterval` `1a` respects `IsCaravanOnCooldown`.
- **TC-12 Tests:** Verify `Caravan_PathFollower.StartPath` optimistic scope allows routing to unexplored impassable tiles, and obstacle discovery triggers the circumvention/halt flow cleanly.

### Manual In-Game Verification

1. **TC-03:** Build `Geoscape_SonarArray` on a coastal colony; confirm sonar coverage overlay and Haze clearing highlight **only** water-covered hexes, while adjacent inland hexes remain unmonitored.
2. **Route Masking:** Trigger or spawn an approaching caravan near radar perimeter; select it and confirm its path line stops cleanly at the sensor perimeter edge without revealing origin/destination settlements in the shroud.
3. **Trade Loop:** Right-click `Hail & Trade` on a friendly trade caravan, complete a trade in `Dialog_Trade`, and close it; confirm the caravan does not re-open the encounter/trade dialog and can freely move away.
4. **TC-12:** Under `Classic Haze of War`, order a caravan (or form one from a colony) into an unexplored ocean or mountain-blocked black sector; confirm it accepts the order and plots a route into the black without `"not accessible"`, then pauses and prompts `"immediate path appears blocked — try circumventing?"` upon encountering the obstacle.
