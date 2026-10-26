# Project Vigilance — Milestone 1.2 Verification & Test Plan

This test plan provides comprehensive, step-by-step procedures to validate the systems implemented across **Milestone 1.2: Ground Interception, Forward Ambush, Scouting Recon & Kidnap Recovery**. It covers overland pursuit, unaware marching column ambushes, early-game scouting recon with 1:1 force persistence, in-map retreat and reform controls, roadside trading encounters with cargo lifecycle synchronization, physiological simulation on the trail, base-assault prisoner rescue, empty settlement tactical hierarchies, evacuee pursuit, Contact Registry auditing, modular settings navigation, and Scribe save/load persistence.

---

## Test Matrix Overview

| Test Case | Feature Under Test                                  | Expected Behaviour                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        | Status             | Primary Screen                     |
| :-------- | :-------------------------------------------------- | :---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | :----------------- | :--------------------------------- |
| **TC-01** | Right-Click Float Menus & Pursuit                   | Contextual options (`Intercept`, `Hail & Trade`, threat-gated `Warn Away`); caravan dynamically tracks moving target hex.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | **Verified**       | World Map / Caravan                |
| **TC-02** | Field Interception, Unaware Ambush & Scouting       | • Unaware marching column with automatic Hold Fire and 18-cell Line of Sight trigger.<br>• **"Hold Fire & Let Them Pass" Scouting:** 1:1 force makeup persistence across re-engagements and colony arrival.<br>• **Tactical Retreat & Closest Edge:** Multi-colonist selection deduplication; each colonist independently escapes via their own closest map edge; clean window stack dismissal upon emergency evacuation.<br>• **Retreat Faction & Animal Safety:** Retreating colonists never convert to foreign factions or prisoner state; colony animals/mechs automatically swept into the caravan to prevent infinite exit-job loops.<br>• **Situational Battle Outcome Reporting:** Dynamic letter classification (`Caravan Battle Won`, `Costly Victory`, `Caravan Defeated`) with interactive Geoscape/Battlescape jump buttons and live reinforcement countdown.<br>• **No-Steal Encounter Stability:** Hostiles fight aggressively and do not break combat to steal caravan items or become stuck.<br>• Camp lookouts and forward settlement collisions engage active defense. | **Verified**       | World Map / Encounter Map          |
| **TC-03** | Night Camp Ambush & Night-Owl Caravans              | • Intercepting enemies camped overnight triggers a nighttime tactical ambush with sleeping enemies.<br>• **Night-Owl Marching:** Pure Night-Owl caravans travel nocturnal schedules and rest during daylight.<br>• **Mixed Caravans:** Night-Owls maintain circadian alertness and enhanced night lookout radius during camps and encounters.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             | **Ready for Test** | World Map / Encounter Map          |
| **TC-04** | X-COM "Contact Lost" Dialog                         | Losing contact in shroud or darkness pauses caravan with a choice dialog and food provision safety telemetry.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             | **Ready for Test** | World Map / Dialog                 |
| **TC-05** | Roadside Encounter Dialog & Trade Cargo Lifecycle   | • Crossing paths opens interactive dialog (`Trade`, `Attack [Declare War]`, `Move on`); resolves infinite re-open loops.<br>• **Bidirectional Cargo Sync:** Goods sync between `TradeStock` and pack animal inventories without dumping loot on combat maps.<br>• **Trader Loss Abort Lifecycle:** Heavy casualties or loss of trader leader causes traders to break combat, take evasive exit, and return home on the Geoscape.<br>• **Inventory Consistency:** Road purchases remain deducted from caravan stock upon arrival at colonies.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              | **Ready for Test** | World Map / Encounter Dialog       |
| **TC-06** | Threat-Gated Warn Away / Turn Back                  | Warn Away requires imminent danger near destination or route; awards `+5` goodwill once with exploit prevention.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          | **Ready for Test** | World Map / Letter Stack           |
| **TC-07** | Raid Retreat & 1:1 Pawn Fidelity                    | Raiders fleeing map edge do not vanish; they group into a retreating incursion preserving exact wounds, gear, and captives without Scribe pawn duplication.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               | **Verified**       | Colony Map / World Map             |
| **TC-08** | Overland Physiological Simulation                   | Raiders, captives, and scouted incursions on the road bleed, undergo doctor field tending with carried medicine, sleep at night, and eat provisions.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      | **Ready for Test** | Dev Mode / Incursion Pawns         |
| **TC-09** | Hot-Pursuit Hostage Recovery                        | Intercepting a retreating kidnapper band spawns the captive colonist directly on the field for combat rescue without Lord crashes.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        | **Verified**       | World Map / Encounter Map          |
| **TC-10** | Settlement Incarceration & Base Assault             | Kidnappers reaching their stronghold incarcerate captives there; assaulting that specific base spawns them inside as prisoners upon map generation.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       | **Verified**       | World Map / Settlement Attack      |
| **TC-11** | Tactical Radar Outlines, Haze of War & Mod Settings | • Glowing boundary rings with inward distance fade (80% down to 12%), concentric identification horizon ring, `BoundaryOnly` mode, and 4-tab modular settings suite.<br>• **Haze of War Performance:** In-place vertex color updates eliminate caravan movement hitching; discrete topological hex steps ensure geometric symmetry.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       | **Verified**       | World Map / Mod Settings           |
| **TC-12** | Multi-Incursion Convergence & Separate Retreat      | Two raids attacking the same cell maintain separate identities; ongoing battles receive reinforcement alerts; incursions retreat as two separate combat raids without combining or disappearing.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          | **Ready for Test** | World Map / Encounter Map          |
| **TC-13** | Information Fog & Epistemic Discovery               | Unidentified contacts (`Contact-X` / `#X`) conceal faction, threat points, and force makeup; float menu displays neutral `Investigate`; discovery requires active scouting or crossing the Identification Horizon.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        | **Ready for Test** | World Map / Inspector / Float Menu |
| **TC-14** | Scribe Save/Load & Memory Integrity                 | Saving and reloading while incursions are tracking, retreating, or engaged produces zero Scribe duplicate pawn errors, zero `Id already used by` errors, and 100% state restoration.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      | **Ready for Test** | Save / Load Menu / Debug Log       |
| **TC-15** | Battlescape Sector Transit & Emergent Encounters    | Third-party forces traversing an active battlefield/camp hex march across the Battlescape; hostiles transit unaware and assess combat strength upon detection (assault if confident vs. evade if weakened); neutrals give active firefights a wide berth.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | **Ready for Test** | Encounter Map / Battlescape        |
| **TC-16** | Empty Settlement Hierarchy & Evacuee Pursuit        | • **Peaceful Visitors:** Reverse course on the Geoscape with status `"Returning home (No one home to trade/visit)"` instead of vanishing.<br>• **Hostile Raids:** Pillage abandoned structures/crops, then probabilistically acquire pursuit tracking on evacuated player caravans (`IsPursuingCaravan = true`), chasing them across the Geoscape.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        | **Ready for Test** | World Map / Settlement / Geoscape  |
| **TC-17** | Contact Registry Lifecycle & Strategic Telemetry    | All contacts log full lifecycle transitions (Created, Detected, Engaged, Resolved) with accurate outcome telemetry (`VisitedColony`, `TradedAtColony`, `AssaultedColony`, `PillagedAbandonedSettlement`, `PursuingEvacuees`, `ReturnedHomeSafely`, `Neutralized`, `EvadedEncounter`).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     | **Ready for Test** | World Map / Dev Console / Registry |

---

## Prerequisites & Setup

1. **Launch RimWorld 1.7** with Project Vigilance loaded.
2. **Enable Development Mode** (`Options → Development mode`).
3. Prepare a test save with:
   - At least 3–4 colonists (including 1 with the **Night-Owl** trait and 1 with decent **Medical** skill).
   - A colony pet (e.g. Labrador Retriever) and/or a colony mechanoid.
   - A supply of food (packaged survival meals/pemmican) and medicine.
   - Pack animals (muffalo, dromedary, or yak) with carried goods.
   - A forward camp or ability to set up camp (`Set Up Camp` or world caravan).

---

## Step-by-Step Test Procedures

### Test 1: Right-Click Float Menu & Dynamic Hex Pursuit (`TC-01`)

_Status: Verified_

1. From your colony, form a small combat caravan on the world map.
2. Trigger an incoming raid incursion via Dev Mode:
   - `Debug Actions Menu` → `Incidents` → `Execute Incident...` → `RaidEnemy` (walk-in).
3. Switch to the World Map (`Tab`).
4. Select your player caravan, then **Right-Click** the approaching hostile incursion blip:
   - **Verify:** A contextual float menu option appears: `Intercept [Contact-X]` (or `Intercept [Hostile Warband]`).
5. Also right-click a peaceful friendly travelling caravan when no threats are nearby:
   - **Verify:** Float menu option displays disabled: `Warn away [Contact-X] (No imminent threat nearby)`.
6. Click **Intercept** on the hostile incursion:
   - **Verify:** Your caravan immediately begins marching toward the incursion's current hex.
   - Fast-forward time (`Speed 3`): as the enemy incursion steps forward across adjacent tiles, verify your caravan's destination tile dynamically updates to track the enemy's moving position.

---

### Test 2: Field Interception, Unaware Ambush & Scouting Recon (`TC-02`)

_Status: Verified (Includes loot steal fix, Lord lifecycle fix, reinforcement timer, clean map deallocation, colonist faction preservation, and animal auto-sweep)_

#### 2.1 Active Interception Pursuit & Skirmish AI

1. Order your caravan to intercept an incoming hostile incursion on the move.
2. When your caravan collides with the incursion hex:
   - A high-priority letter arrives:
     > **Field Interception: [Contact-X]**  
     > _"[CaravanName] has intercepted [Contact-X] from [FactionName] on the overland march! Prepare your forces for a field engagement."_
   - Tactical battle map generates with terrain matching the tile.
   - **No-Steal Check:** Enemies do **not** attempt to steal caravan items or loot under colonists (`canSteal: false`). They actively advance and engage your squad.
   - **Lord Assignment Check:** Verify no Lord crashes or errors occur in the log (`~`).
   - **Incursion State Check:** The world incursion remains active in memory (`IsEngagedInTacticalMap = true`).

#### 2.2 Stationary Collision (Overland Ambush — Unaware Marching Column)

1. Station a caravan on a road tile and let a hostile incursion march directly into your position.
2. **Concealment & Surprise Check:**
   - A positive tactical letter arrives:
     > **Ambush Position: [Contact-X]**  
     > _"[CaravanName] was stationed in concealment at tile [Tile] when [Contact-X] from [FactionName] entered your sector! TACTICAL ADVANTAGE (SURPRISE): The approaching enemy column is currently unaware of your presence and is marching through the area..."_
   - **Hold Fire Check:** Verify your colonists have their `Fire at Will` automatically set to **Hold Fire** so they do not prematurely alert enemies before you position them.
   - **Enemy Marching Column:** Enemies spawn on one side of the map and walk in formation toward the opposite map edge via `LordJob_TravelAndExit`.
   - **Top-Center Banner:** Verify a tactical banner appears at top center:
     `[Concealment: Holding Fire] [Quietly Disengage]`
3. **Ambush Trigger Testing:**
   - _Non-Player Damage Ignored:_ If wildlife or weather damages an enemy, verify concealment is **not** broken.
   - _Hostility Trigger:_ Order a colonist to fire a weapon, attack in melee, or walk within clear Line of Sight (≤ 18 cells) of an awake enemy:
     - A high-priority red alert immediately arrives:
       > **Ambush Sprung: [Contact-X]**  
       > _"The enemy column has broken travel formation and is scrambling into combat positions to counterattack!"_
     - Enemies break formation, take cover, and aggressively counterattack (`LordJob_AssaultColony`).

#### 2.3 "Hold Fire & Let Them Pass" Scouting & Force Makeup Persistence

1. Position a small forward scouting party in an ambush tile.
2. When the enemy column enters, keep all colonists concealed in bushes/cover and hold fire.
3. Inspect the enemy combatants: note down the pawns' names, equipment (weapons/apparel), and any pre-existing injuries.
4. Fast-forward and allow the enemy column to traverse the entire map and depart across the opposite edge.
5. **Verify Departure:**
   - When the last enemy exits the map, a letter arrives:
     > **Sector Clear: [Contact-X]**  
     > _"The enemy column from [FactionName] has traversed your sector and departed without detecting your scouts. Scout observations confirmed [X] combatants in the force..."_
   - The hostile incursion resumes its overland march on the geoscape (`AdvancePathTile()`).
6. **Force Makeup Fidelity Check:**
   - Intercept the incursion at the next tile, or wait for them to arrive at your colony.
   - **Verify:** The incoming enemy force contains the **exact same pawns** (identical names, gear, apparel, and wounds).

#### 2.4 In-Map Tactical Disengage, Retreat, Faction Preservation & Animal Sweep

1. **Stealth Disengage (During Concealment):**
   - While enemies are marching past, select a colonist and click the **Quietly Disengage** gizmo (or click `[Quietly Disengage]` on the top-center banner).
   - Confirm the prompt:
     - Your squad immediately packs up and reforms into a caravan on the geoscape.
     - The enemy incursion continues marching on the geoscape with its force makeup intact.
     - The tactical encounter map is cleanly de-initialized via `parent.Destroy()`.
2. **Attempt Retreat (Multi-Selection & Dialog Stack Hygiene):**
   - Engage the enemy in combat. Box-select multiple colonists and click **Attempt Retreat**:
     - **Verify:** Exactly **one** "Tactical Retreat" dialog opens.
     - Verify clicking **Cancel** dismisses the prompt cleanly.
3. **Order Retreat to Edge (Per-Colonist Closest Edge Routing):**
   - Position colonists in different areas of the map (West, North, East).
   - Click **Order Retreat to Edge**:
     - **Verify:** Each colonist heads directly to their individual closest reachable map boundary.
     - Colonists do **not** funnel together to the South-East corner.
     - As each pawn touches their nearest boundary, they exit the map and auto-join the reformed caravan on the Geoscape.
4. **Colonist Faction & Non-Prisoner Integrity Check:**
   - Inspect the retreating colonists as they arrive on the Geoscape:
     - **Verify:** Colonists remain 100% under `Faction.OfPlayer`.
     - **Verify:** Colonists are **never** reassigned to a random world faction (e.g. pirate faction Bakdurbyon) and are **never** flagged as `Abandoned` or put `"In restraints (slowed)"` with a prisoner tab.
5. **Colony Animal & Mech Auto-Sweep Check:**
   - Ensure a colony pet (e.g. Labrador Retriever) or mech is present on the encounter map during retreat.
   - Allow all conscious colonists to reach the map boundary and retreat.
   - **Verify:** The animal is **not** left stranded alone on the battlefield.
   - **Verify:** The animal does **not** enter an infinite 10-jobs-in-one-tick loop with `Verse.AI.JobGiver_ExitMapBest`.
   - **Verify:** As the last colonist retreats, the animal is automatically swept into the player caravan and cleanly despawned from the map.
6. **Emergency Evacuation (Instant Extraction & Cleanup):**
   - During combat with downed colonists, click **Emergency Evacuation**:
     - The "Tactical Retreat" dialog closes immediately.
     - Mobile colonists form a caravan on the Geoscape; any downed colonists left behind are registered as captured on the incursion.
     - The tactical map is cleanly destroyed without errors.
7. **Reform Caravan (When Sector is Clear):**
   - Once hostiles are defeated or departed:
   - Verify on-screen banner: `[Sector Clear] [Reform Caravan]`.
   - Click either the banner or a colonist's **Reform Caravan** gizmo: the standard reform window opens immediately on the current map.

#### 2.5 Situational Combat Outcome Letters & Reinforcement Countdown

1. **Flawless Victory:**
   - Defeat all enemies with zero friendly casualties.
   - **Verify:** A letter arrives titled **Caravan Battle Won: [Contact-X]**.
   - **Reinforcement Timer Check:** If timed detection is active, verify the letter includes:
     > _"Enemy reinforcements will detect this position in [X.X days/hours]. Reform your caravan and depart before hostile reinforcements arrive."_
2. **Costly / Pyrrhic Victory:**
   - Win the engagement after suffering casualties (downed or killed colonist, or a raider escaped with a captive).
   - **Verify:** A letter arrives titled **Costly Victory: [Contact-X]** detailing the casualty report (Conscious, Incapacitated, KIA, and Abducted).
   - If captives were taken, verify the button displays **Track Kidnappers on Geoscape**.
3. **Squad Defeat / Total Loss:**
   - Allow all colonists to be incapacitated or killed.
   - **Verify:** A high-threat letter arrives titled **Caravan Defeated: [Contact-X]**.
   - Verify `canReformCaravan` is disabled.

---

### Test 3: Night Camp Ambush & Night-Owl Caravans (`TC-03`)

_Status: Ready for Test (Includes Night-Owl trait schedules and alertness)_

1. **Night Camp Detection:**
   - Track an incoming biological mortal incursion (Tribals, Pirates, or Outlanders).
   - Fast-forward until local solar night falls between 22:00 and 06:00:
   - Inspect the incursion: verify its status displays _"Camped for the night (Resuming march at dawn)"_.
2. **Night Camp Ambush Encounter:**
   - Order your caravan to intercept them while camped:
   - **Verify on Encounter Map:**
     - The encounter generates as a **Tactical Ambush**.
     - Enemy combatants start scattered and asleep in their bedrolls.
     - In-map **Attempt Retreat** and **Reform Caravan** gizmos are active.
3. **Night-Owl Caravan Travel Dynamics (`Caravan_NightResting_Patch`):**
   - Form a caravan consisting exclusively of **Night-Owl** colonists.
   - **Verify:** The caravan marches through the night and rests during the day (11:00 to 18:00).
   - Form a mixed caravan (standard colonists + 1 Night-Owl):
     - Verify the caravan rests normally at night for the group, but the Night-Owl member is awake/alert during night hours.
     - When camped at night, the Night-Owl provides increased lookout sensory radius and early-warning alerts against incoming patrols.

---

### Test 4: X-COM "Contact Lost" Telemetry & Survival Dialog (`TC-04`)

_Status: Ready for Test_

1. Dispatch your caravan to intercept a distant incursion located near the edge of your sensory radar horizon.
2. When the incursion steps out of sensory range (or during nighttime lookout shrinkage):
3. **Verify:**
   - Caravan travel **pauses immediately**.
   - An X-COM style prompt dialog appears titled **Contact Lost: [Contact-X]** with current caravan rations telemetry.
4. Test both options:
   - **Proceed to Last Known Position:** Unpauses the caravan and continues marching to the last verified coordinates.
   - **Return Home:** Halts pursuit and automatically plots a path back to your nearest player settlement.

---

### Test 5: Roadside Encounter Dialog & Trade Cargo Lifecycle (`TC-05`)

_Status: Ready for Test (Includes bidirectional cargo sync, pack animal cargo retention, and trader abort lifecycle)_

1. **Encounter Initiation:**
   - Spawn a friendly trade caravan via Dev Mode (`TraderCaravanArrival`).
   - Move your player caravan into their path (or right-click and choose `Hail & Trade`).
   - When both groups meet on the same tile:
     - **Verify:** An interactive prompt opens titled **Encounter: [TraderCaravan]** offering:
       1. **Trade with caravan:** Opens `Dialog_Trade` cleanly.
       2. **Attack caravan (Declare War):** Displays a confirmation warning of -100 goodwill drop.
       3. **Move on / Part ways:** Resumes travel peacefully.
2. **Loop Prevention Check:**
   - Closing `Dialog_Trade` or choosing `Move on` does not re-open the window in an infinite loop.
3. **Bidirectional Cargo Lifecycle & Combat Cargo Retention:**
   - Inspect the trade caravan's pack animals (muffalo, dromedaries): verify goods are stored in their inventories.
   - Initiate combat via `Attack caravan (Declare War)`:
     - **Verify:** Pack animals do **not** dump their entire cargo onto the ground upon map spawn.
     - Pack animals carry their merchandise in their inventories during the skirmish.
     - Slain pack animals drop their inventory as loot; surviving pack animals keep their cargo intact.
4. **Trader Loss Abort Lifecycle:**
   - In combat against a trade caravan, eliminate the trade leader or inflict 50%+ casualties on their escort:
     - **Verify Notification:** A message alerts that the traders have aborted their trading mission.
     - Surviving traders transition to `LordToil_ExitMap` in a defensive retreat.
     - When exiting the map, the surviving caravan forms a retreating incursion on the Geoscape with status `"Returning home (Aborted trade run)"`.
5. **No Double-Dipping Check:**
   - Trade peacefully on the road and purchase specific high-value items (e.g. glitterworld medicine or a persona weapon).
   - Allow the trade caravan to complete its journey to your colony:
   - **Verify:** Purchased items are no longer in their stock when trading with them at your colony.

---

### Test 6: Threat-Gated Warn Away & Goodwill Balance (`TC-06`)

_Status: Ready for Test_

1. Right-click a friendly incursion when no dangers exist along their path:
   - **Verify:** Option shows disabled: `Warn away [Contact-X] (No imminent threat nearby)`.
2. Trigger an active danger near their destination settlement (or spawn a hostile raid within 4 tiles of their route).
3. Right-click the friendly incursion again:
   - **Verify:** Option `Warn away / Turn back [Contact-X]` is now active and clickable.
4. Click **Warn away**:
   - A letter confirms the caravan turned back.
   - Player goodwill increases by `+5`.
   - The incursion reverses direction and heads home.
5. Right-click them again: verify option displays `Warn away [Contact-X] (Already retreating)` to prevent reputation farming.

---

### Test 7: Raid Retreat & 1:1 Pawn Fidelity (`TC-07`)

_Status: Verified (Includes Scribe reference fix and Lord resolution)_

1. Trigger a standard walk-in raid on your colony.
2. Engage the raiders until they break and flee (`Give up and leave`).
3. Inflict bleeding gunshot wounds on one or two fleeing raiders without killing them.
4. Watch the fleeing raiders cross the colony map boundary:
   - **Verify:** A notification fires: `Raiders in Retreat: [Contact-X]`.
   - **Verify:** No Lord errors occur in the log.
5. Switch to the World Map:
   - Verify an incursion named `Hostile Warband [Contact-X] [Retreating]` spawns at your colony tile and marches toward their nearest stronghold.
   - Pawns are safely registered in `Find.WorldPawns` with `KeepForever` and saved via reference.

---

### Test 8: Overland Physiological Simulation (`TC-08`)

_Status: Ready for Test_

1. While a retreating or scouted incursion is travelling overland, inspect its pawns via Dev Mode.
2. **Verify:**
   - **Bleeding & Mortality:** Pawns with untended bleeding wounds accumulate blood loss and can succumb on the march.
   - **Field Tending:** Conscious doctors tend untended injuries every 250 ticks using carried medicine (`TendUtility.DoTend`).
   - **Sleeping at Night:** Mortal incursions camp between 22:00 and 06:00, replenishing rest.
   - **Provisions:** Raiders eat carried food when hunger falls below 35%.

---

### Test 9: Hot-Pursuit Kidnap Recovery (`TC-09`)

_Status: Verified (Includes Scribe reference fix and captive preservation)_

1. On your colony map, down a colonist during a raid and allow a hostile raider to carry them off the map edge.
2. **Verify on Exit:**
   - A red alert letter fires: `Kidnappers in Retreat: [Contact-X]`.
   - The incursion on the world map is designated: `[Contact-X] [Kidnappers]`.
   - The captive is registered as an abducted colonist on the incursion and in `IncursionRetreatManager`.
3. Intercept the kidnappers with a fast pursuit caravan:
4. **Verify on Encounter Map:**
   - Encounter generates with alert: `RESCUE OPERATION: [Contact-X]`.
   - **Your captured colonist is physically present on the battlefield** near the kidnappers.
   - **No Lord Errors:** Hostiles engage without crashing or leaving the captive abandoned in limbo.
5. Defeat the captors and rescue your colonist; verify they rejoin your caravan in full health fidelity.

---

### Test 10: Settlement Incarceration & Base-Assault Rescue (`TC-10`)

_Status: Verified (Includes AttackNow synchronous map generation fix)_

1. Allow a retreating kidnapper band to complete their march across the globe into their home stronghold.
2. **Verify on Arrival:**
   - A letter arrives: `Colonists Incarcerated: [Contact-X]` naming the captive and the destination settlement.
3. Form a strike caravan, travel to that exact enemy settlement tile, and click **Attack**:
4. **Verify on Settlement Battle Map:**
   - The stronghold map generates with buildings and defenders.
   - The rescue banner announces:
     > _"RESCUE MISSION: Captured colonist [ColonistName] is incarcerated inside this stronghold! Secure them during the assault."_
   - **Synchronous Spawning Check:** Your kidnapped colonist is physically spawned inside the enemy base complex as a prisoner (`GuestStatus.Prisoner`).
   - Verify the captive did not fail to spawn due to asynchronous map generation.

---

### Test 11: Tactical Radar Outlines, Haze of War & Mod Settings (`TC-11`)

_Status: Verified (Includes 4-tab modular UI, dynamic heights, landmark cache fix, and vertex color optimization)_

1. Open `Options → Mod Settings → Project Vigilance: Geoscape`:
   - **Verify 4-Tab Bar:** Header renders 4 distinct tabs: `Sensory & Radar`, `Incursions & AI`, `Early Warning & Intel`, `About & Diagnostics`.
   - **Verify Selection Indicator:** Active tab has a glowing cyan underline and highlight.
   - **Verify Scrolling & Heights:** Switch between all 4 tabs; verify each tab calculates its content height dynamically without clipping, overlapping, or throwing GUI errors.
2. **Radar Customization:**
   - In `Sensory & Radar`, toggle `Use Player Faction Color`: radar outlines dynamically match your colony banner color.
   - Uncheck to test custom colors: verify hex text input, RGB sliders, and preset buttons (`Cyan`, `Amber`, `Emerald`, `Crimson`, `White`) update the globe in real time.
   - Switch between `Boundary Ring & Honeycomb Grid` and `Boundary Ring Only`.
3. **Haze of War Performance & Hex Topography:**
   - Set visibility to `Classic Haze of War`.
   - Pan and zoom the world map while caravans are moving:
     - **Verify:** Caravan motion is completely smooth with zero frame drops or hitching (in-place mesh vertex colouring active).
     - **Verify:** Topological hex step boundaries render with 100% geometric symmetry.
4. **Landmark Fog of War Check:**
   - Explore a tile containing a planetary landmark:
   - **Verify:** The landmark reveals correctly.
   - **Verify (Cache Protection):** Vanilla's `cachedLandmarksToShow` is not corrupted, and landmarks across other tiles remain intact in memory.

---

### Test 12: Multi-Incursion Convergence & Independent Group Retreat (`TC-12`)

_Status: Ready for Test_

1. **Convergence on the Same Cell:**
   - Trigger two hostile incursions heading toward the same settlement or camp.
   - Incursion 1 arrives and engages (`IsEngagedInTacticalMap = true`).
   - Incursion 2 arrives while combat is underway:
     - A reinforcement alert arrives: `Overland Raid (Reinforcements): [Contact-2]`.
     - Incursion 2's combatants spawn at the map edge and join the fight.
     - Both incursions remain visible on the Geoscape at the hex with active combat status.
2. **Independent Pawns & Casualty Attribution:**
   - Defeat Incursion 1's combatants or force them to flee:
     - Incursion 1 disengages, becomes `[Retreating]` on the Geoscape, and marches home with its survivors.
     - **Incursion 2 continues actively fighting** on the tactical map without interruption.
3. **Separation Guarantee:**
   - When Incursion 2 later retreats, it forms its own distinct retreating incursion.
   - Incursions 1 and 2 do **not** combine or pool pawns.

---

### Test 13: "You Don't Know What You Don't Know" — Epistemic Discovery (`TC-13`)

_Status: Ready for Test_

1. **Unidentified Contact Ambiguity:**
   - In `Planetary Surveillance` or `Classic Haze of War`, allow a contact to enter detection range (Tier 2 `Contact` / Spotted):
     - Label displays purely as `Contact-X` (or `Strangers #X`).
     - Faction name, emblem, threat points, and headcount are completely hidden.
     - Right-clicking the contact displays `Investigate [Contact-X]` without revealing hostile or peaceful alignment.
2. **Active Classification:**
   - Advance within 2 hexes with a scout caravan, or let the contact cross the 45% Identification Horizon:
     - Faction crest appears; label updates to `Hostile Warband` or `Trade Caravan`.
     - Contextual menu unlocks `Intercept` or `Hail & Trade`.
     - Identification Alert letter documents the discovery.
3. **Pawn-Level Intel (Tier 4):**
   - Station a scouting team in concealment (as in `TC-02.3`) to observe the marching column and reveal exact gear and loadouts.

---

### Test 14: Scribe Save/Load & Memory Integrity (`TC-14`)

_Status: Ready for Test (Guarantees zero duplicate pawn registrations)_

1. **Mid-Encounter Save/Load:**
   - Start a tactical interception battle (`TC-02`).
   - Save the game (`ESC → Save Game`).
   - Reload the save immediately:
     - **Verify:** Zero red errors in the console (`~`).
     - **Verify:** Zero `Cannot register Pawn ... Id already used by ...` Scribe errors.
     - Colonists, enemies, and concealment states resume without desynchronisation.
2. **Overland Kidnapper Pursuit Save/Load:**
   - Allow a colonist to be kidnapped and begin overland pursuit on the Geoscape (`TC-09`).
   - Save the game while the incursion is travelling on the world map.
   - Reload the save:
     - **Verify:** The captive colonist remains attached to the incursion.
     - **Verify:** Scribe loads the captive reference from `Find.WorldPawns` cleanly without duplicating the colonist.
3. **Retreat Batch Save/Load:**
   - Force raiders to flee your colony map and save while they are grouping into a retreat batch.
   - Reload the save:
     - Verify the retreat batch finalizes on schedule and spawns the retreating incursion on the Geoscape.

---

### Test 15: Battlescape Sector Transit & Emergent Encounters (`TC-15`)

_Status: Ready for Test (Supports unaware marching patrols, strength evaluation, and neutral wide-berth combat avoidance)_

#### 15.1 Unaware Hostile Patrol & Stealth Passage

1. Engage a small friendly visitor or set up a temporary camp (`CaravansBattlefield` or temporary map parent).
2. Remain on that map without reforming caravan.
3. Spawn a hostile incursion on an adjacent Geoscape tile heading through your current hex (`Execute Incident... → RaidEnemy`).
4. As the hostile incursion steps into your hex:
   - **Verify Notification:** A subtle message banner appears:
     > _"Unaware Hostile Patrol: A Hostile Warband from [Faction] has entered the sector from the [Direction] edge and is transiting the area. If you remain concealed, they may pass without noticing your squad."_
   - **Verify Directional Entry:** Enemies spawn along the map border matching their Geoscape approach vector.
   - **Verify Unaware March:** Enemies are assigned `LordJob_IncursionTransit` and quietly march across the map toward their destination exit edge (`DutyDefOf.TravelOrLeave`) without automatically targeting your squad across the fog of war.
   - **Stealth Check:** Keep your colonists drafted and positioned behind cover/walls with "Hold Fire" enabled.
   - **Verify Peaceful Exit:** The patrol marches across to the opposite edge and despawns.
   - **Verify Geoscape Continuity:** The incursion resumes its overland journey on the Geoscape with 100% pawns intact without reversing course or retreating.

#### 15.2 Threat Evaluation & Confident Assault

1. Repeat the scenario with a standard-strength hostile patrol transiting the map.
2. While the patrol is marching across the map, break concealment:
   - Discharge a firearm within hearing range (~45 cells), or
   - Walk an undrafted colonist into direct unobstructed Line of Sight within ~25 cells, or
   - Engage them directly with weapon fire.
3. **Verify Strength Evaluation:**
   - Because the incursion is unweakened ($\text{Combat Ratio} \ge 0.75$), they assess the engagement as favourable.
   - **Alert Horn:** Threat alert sounds with message:
     > _"Hostile Patrol Alerted: [Faction] spotted your squad and launched an immediate assault!"_
   - The patrol transitions to `LordToil_AssaultColony` and actively advances to eliminate your squad.

#### 15.3 Threat Evaluation & Intimidated Evasion (Risk vs. Reward)

1. Using Dev Mode (`Damage until Downed` / `Damage to Death`), reduce an incursion to a single wounded survivor or heavily out-gunned combatant ($\text{Combat Ratio} < 0.75$).
2. Have the weakened survivor enter a sector occupied by 3–4 heavily armed, healthy colonists.
3. Break concealment so the survivor detects your squad.
4. **Verify Evasion Behaviour:**
   - The weakened enemy recognizes they are severely out-matched and does **not** commit to a suicide charge.
   - A notification appears:
     > _"Hostile Patrol Evading: [Faction] detected your presence, but assessing their weakened numbers, they are hastening towards the perimeter!"_
   - The enemy transitions to `LordToil_ExitMap` in a double-time jog (`LocomotionUrgency.Jog`) with active avoidance grid (`useAvoidGrid = true`), pathing towards the perimeter to escape.

#### 15.4 Neutral & Trade Caravan Wide-Berth Avoidance

1. While an active firefight is ongoing on an encounter map (`CaravansBattlefield`), spawn a neutral or trade caravan heading through the hex (`Execute Incident... → TraderCaravanArrival`).
2. As the caravan steps onto the map:
   - **Verify Notification:**
     > _"Passing Travelers: Trade Caravan from [Faction] has entered the sector from the [Direction] edge, peacefully traversing the area."_
3. When gunfire sounds or active combatants are within sensory range:
   - The travellers immediately identify the hot zone.
   - **Verify Wide-Berth Notification:**
     > _"Gunfire ahead! [Faction] travellers have detected danger, giving the combat zone a wide berth as they hurry toward the exit!"_
   - **Verify Perimeter Pathing:** The caravan dynamically re-routes along the map perimeter away from the center of combat.
   - **Verify Goodwill Protection:** The caravan safely exits the map without walking into crossfire or triggering accidental hostility.
4. In a calm sector without active combat, verify colonists can right-click the caravan leader to open `Dialog_Trade` and conduct business normally.

---

### Test 16: Empty Settlement Tactical Hierarchy & Evacuee Pursuit (`TC-16`)

_Status: Ready for Test (Covers peaceful return march, empty settlement pillaging, and dynamic overland pursuit)_

#### 16.1 Peaceful Visitors / Traders Arriving at an Empty Settlement

1. Evacuate all colonists from a player settlement into a caravan or form a new settlement and leave it unpopulated.
2. Direct a peaceful trade caravan or visitor group to visit the empty settlement.
3. When the peaceful group reaches the settlement tile:
   - **Verify Notification:** A message alerts that visitors found the settlement abandoned/empty.
   - **Physical Return March:** The incursion does **not** vanish or get destroyed.
   - **Geoscape Continuity:** The incursion physically turns around on the Geoscape (`ReverseCourse`), displaying status:
     `Status: Returning home (No one home to trade/visit)` with a live ETA to their home settlement.
   - When they reach their home settlement, `ContactRegistry` logs their outcome as `ReturnedHomeSafely`.

#### 16.2 Hostile Warband Tactical Hierarchy: Pillage vs. Pursuit

1. Form a player caravan and evacuate your colony tile, moving 1–2 tiles away on the Geoscape.
2. Trigger an incoming hostile raid targeting the now-empty settlement.
3. When the raid arrives at the empty colony tile:
   - **Verify Hierarchy Evaluation:**
     - **Evaluation 1 (Evacuee Pursuit):** If the fleeing player caravan is within pursuit range and detected, the hostile warband dynamically acquires tracking on your caravan:
       - Incursion status updates to: `Pursuing [CaravanName]` (`IsPursuingCaravan = true`).
       - An urgent letter arrives:
         > **Hostile Warband in Pursuit: [Contact-X]**  
         > _"The enemy raid arrived at your evacuated settlement and discovered fresh tracks! They have altered course and are actively pursuing your fleeing caravan on the overland map!"_
       - The enemy incursion plots a direct intercept path toward your moving caravan.
     - **Evaluation 2 (Pillaging & Looting):** If no fleeing caravans are found:
       - The raiders pillage the abandoned buildings and fields.
       - A letter notifies: `Settlement Pillaged: [FactionName]` detailing destroyed structures and stolen supplies.
       - The incursion reverses course and marches home with their plunder (`PillagedAbandonedSettlement`).

---

### Test 17: Contact Registry Lifecycle & Strategic Telemetry (`TC-17`)

_Status: Ready for Test (Ensures 100% audit trail for all Geoscape contacts)_

1. Trigger various incursions across the campaign (raids, trade caravans, visitors, kidnappers).
2. Follow each contact through its complete lifecycle to resolution (combat victory, retreat, peaceful trade, or safe return home).
3. Open the Dev Mode console (`~`) or inspect the `ContactRegistry`:
   - **Verify Record Integrity:** Every contact has a unique `TrackingId` and maintains a complete timestamped log:
     - Creation (`SpawnTick`, `OriginTile`, `Faction`, `GroupKind`)
     - First Detection (`FirstDetectedTick`, `DetectionTier`)
     - Tactical Engagements (`EngagementTicks`, `BattlefieldTiles`)
     - Final Resolution (`ResolutionTick`, `Outcome`)
   - **Verify Outcome Categorization:**
     - Successful colony visit: `VisitedColony` / `TradedAtColony`
     - Assaulted colony: `AssaultedColony`
     - Pillaged empty base: `PillagedAbandonedSettlement`
     - Overland chase: `PursuingEvacuees`
     - Safe return to base: `ReturnedHomeSafely` (never falsely logged as `Neutralized`)
     - Destroyed in combat: `Neutralized`
     - Escaped/Evaded: `EvadedEncounter`

---

## Log & Regression Checks

During and after completing these test cases:

1. Open the RimWorld debug log (`~` key).
2. Verify **zero red errors**, zero `NullReferenceException`, zero `Lord` lifecycle errors, zero `Cannot register Pawn ... Id already used by ...` errors, and zero orphaned Scribe cross-references.
3. Save and reload the game under each operational state; verify 100% state restoration and zero duplicate pawn warnings.
