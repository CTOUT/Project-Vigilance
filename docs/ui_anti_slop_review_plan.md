# Implementation Plan: UI Anti-Slop Audit & Polish

## Executive Summary

Audit of all custom user interfaces implemented in **Project Vigilance** against the [anti-ui-slop skill](file:///D:/Repos/GitHub/Project-Vigilance/.github/skills/anti-ui-slop/SKILL.md) (`reference/audit.md`) and RimWorld's native UI standards (`Verse.Widgets`, `Listing_Standard`, standard margins, and game font metrics).

Findings are strictly grounded in observable implementation evidence and rendered behaviour. Each surface is evaluated for:

- Task clarity and visual hierarchy
- Consistency with RimWorld's native widget and window conventions
- Layout stability, overflow, and truncation risks
- Interactive states (hover, disabled, active) and feedback
- Typography, contrast, and dead space

Each interface has at most three material findings, ordered by user impact, paired with the smallest concrete correction.

---

## Surface 1: Save Adoption Dialog (`Dialog_GeoscapeModAdoption.cs`)

Modal presented when loading an existing campaign save for the first time without Project Vigilance.

| #       | Inspection Dimension      | Observable Evidence                                                                                                                                                                                                                                | Smallest Concrete Correction                                                                                                                                               |
| ------- | ------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **1.1** | **Layout & Spacing**      | Hardcoded card heights (`126f`, `88f`, `80f`) terminate at `curY = 400f`. Bottom buttons sit at `yMax - 64f` (576f). Leaves **176px of blank dead space** in the dialog centre. Meanwhile, Card 1's "Notice:" text is constrained to a 36px label. | Resize window height from `640f` to `520f` or proportionally space cards to eliminate dead space; allow dynamic label height for notices.                                  |
| **1.2** | **Visual Hierarchy**      | Buttons (`Widgets.ButtonText`) are placed at the top of each card (`cardInner.y`) _before_ the player reads what the option does. Clicking instantly commits and closes the modal.                                                                 | Move buttons to the card bottom or make whole card selectable with a single clear confirmation button at dialog footer, matching vanilla Storyteller / Ideology selection. |
| **1.3** | **Interaction Semantics** | Window has `doCloseX = true`. When player clicks 'X', `PreClose()` silently executes `ApplyAdoptionProfile(FogOfWar, "Dismissal Fallback")`. RimWorld players expect 'X' to cancel or close without side-effects.                                  | Set `doCloseX = false` (forcing explicit player choice) or provide an explicit "Dismiss to Vanilla Defaults" button.                                                       |

Relevant source: [Dialog_GeoscapeModAdoption.cs](file:///D:/Repos/GitHub/Project-Vigilance/Geoscape/Source/Geoscape/Rules/Dialog_GeoscapeModAdoption.cs#L41-L140)

---

## Surface 2: Campaign Creation Wizard (`Page_GeoscapeRules.cs`)

Full-screen campaign setup page inserted between Storyteller selection and World Generation.

| #       | Inspection Dimension                 | Observable Evidence                                                                                                                                                                                                                | Smallest Concrete Correction                                                                                                  |
| ------- | ------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| **2.1** | **Layout & Truncation**              | 2x2 grid cards use fixed `cardHeight = 132f`. Card 1 description has 196 characters (~4 lines of `GameFont.Small`), leaving only ~4px vertical clearance. Under UI scaling (>1.0x) or translation, text overflows card bounds.     | Increase `cardHeight` to `144f` and expand card inner padding for text breathing room.                                        |
| **2.2** | **Typography & Multi-line Stacking** | `DrawSummaryLine` hardcodes row height to `22f` and increments `y += 24f`. Long lines like `Nighttime Vigilance: Active (...)` (~87 chars) wrap to line 2 on narrower aspect ratios, causing text to overlap the row below.        | Compute dynamic line height with `Text.CalcHeight(value, width - 230f)` and advance `y` by `Mathf.Max(24f, textHeight + 2f)`. |
| **2.3** | **Interactive States**               | In `DrawProfileCard`, `Widgets.ButtonInvisible` is called, followed afterwards by `Widgets.DrawHighlightIfMouseover`. In Unity IMGUI, calling highlight after input processing causes a 1-frame highlight visual flicker on click. | Group input handling and mouseover highlight together in standard order before drawing borders.                               |

Relevant source: [Page_GeoscapeRules.cs](file:///D:/Repos/GitHub/Project-Vigilance/Geoscape/Source/Geoscape/Rules/Page_GeoscapeRules.cs#L54-L219)

---

## Surface 3: Rule Customisation Modal (`Dialog_GeoscapeCampaignRules.cs` & `CampaignRulesPanel.cs`)

Anomaly-style modal window for tuning simulation rules, lookout radii, and incursion behaviour.

| #       | Inspection Dimension              | Observable Evidence                                                                                                                                                                                     | Smallest Concrete Correction                                                                                                       |
| ------- | --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| **3.1** | **Clarity & Affordance**          | Preset bar buttons use awkward truncated labels: `"Planetary Surv."` and `"Hardcore"`. Buttons have no tooltips (`TooltipHandler.TipRegion`), offering no preview of what rules change before clicking. | Add `TooltipHandler.TipRegion` on each preset button with profile descriptions; use full name or smaller font to avoid truncation. |
| **3.2** | **Scroll View Stability**         | `lastViewHeight` is hardcoded to `720f` on initialization, while `CampaignRulesPanel.DrawAllSections` renders ~950f of controls. Causes scrollbar to jump and resize between frame 1 and frame 2.       | Initialize `lastViewHeight` to `960f` to prevent first-frame scroll snapping.                                                      |
| **3.3** | **Dialog Confirmation Semantics** | Escalation confirmation dialog uses vanilla `CreateConfirmation` with generic "OK" / "Cancel" buttons for irreversible campaign-wide changes.                                                           | Supply explicit action buttons: `"Escalate Rule"` and `"Keep Current"` to prevent misclicks.                                       |

Relevant sources:

- [Dialog_GeoscapeCampaignRules.cs](file:///D:/Repos/GitHub/Project-Vigilance/Geoscape/Source/Geoscape/Rules/Dialog_GeoscapeCampaignRules.cs#L58-L96)
- [CampaignRulesPanel.cs](file:///D:/Repos/GitHub/Project-Vigilance/Geoscape/Source/Geoscape/Rules/CampaignRulesPanel.cs#L12-L268)
- [CampaignRuleWidgets.cs](file:///D:/Repos/GitHub/Project-Vigilance/Geoscape/Source/Geoscape/Rules/CampaignRuleWidgets.cs#L223-L254)

---

## Surface 4: Mod Settings & Radar HUD Scope (`GeoscapeSettings.cs` & `RadarScopePreview.cs`)

Tabbed settings interface with live interactive 180x180 tactical radar CRT HUD preview.

| #       | Inspection Dimension             | Observable Evidence                                                                                                                                                                                      | Smallest Concrete Correction                                                                            |
| ------- | -------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| **4.1** | **Layout Synchronization**       | `tabHeights = { 460f, 520f, 320f, 480f, 420f }`. Sensory tab (tab 0) contains ~950f of content, causing a visible scrollbar snap when switching to it.                                                   | Update initial `tabHeights[0] = 960f` (or compute lazily per tab).                                      |
| **4.2** | **Layout & Spacing**             | `radarColumnsHeight = 170f` hardcodes the two-column detection vs identification layout. At higher font scalings or translations, slider labels clip the bottom boundary.                                | Dynamically compute column height: `Mathf.Max(leftListing.CurHeight, rightListing.CurHeight)`.          |
| **4.3** | **Affordance & Discoverability** | The 180x180 CRT scope has a rich comprehensive legend tooltip (`ScopeLegendTooltip`), but the scope itself has no subtle visual prompt (like a corner `(?)` or hover glow) indicating it is inspectable. | Add a subtle corner icon `(?)` and hover outline to `RadarScopePreview` so players discover the legend. |

Relevant sources:

- [GeoscapeSettings.cs](file:///D:/Repos/GitHub/Project-Vigilance/Geoscape/Source/Geoscape/GeoscapeSettings.cs#L63-L450)
- [RadarScopePreview.cs](file:///D:/Repos/GitHub/Project-Vigilance/Geoscape/Source/Geoscape/UI/RadarScopePreview.cs#L31-L140)

---

## Surface 5: Encounter Letter & World Menus (`ChoiceLetter_TacticalEncounter.cs` & World Context Menus)

Tactical encounter resolution letter, retreat actions, and right-click Geoscape dispatch options.

| #       | Inspection Dimension        | Observable Evidence                                                                                                                                                                          | Smallest Concrete Correction                                                                                                                  |
| ------- | --------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| **5.1** | **Task Clarity & Ordering** | In `ChoiceLetter_TacticalEncounter`, "Reform Caravan Now" is Option 3 (below "Go to cell"). When an ambush or encounter has ended, reforming the caravan is the primary recovery action.     | Elevate "Reform Caravan Now" to Option 1 when `canReformCaravan` is true and threats are cleared.                                             |
| **5.2** | **Menu Clutter & Priority** | `FloatMenuMakerWorld_ChoicesAtFor_Patch` injects reconnaissance post/beacon actions alongside movement into a flat unprioritised list. Up to 8-10 options appear at once on congested tiles. | Use `MenuOptionPriority` to keep movement/combat actions at `High`/`Default` and secondary construction/deployment actions grouped logically. |

Relevant sources:

- [ChoiceLetter_TacticalEncounter.cs](file:///D:/Repos/GitHub/Project-Vigilance/Geoscape/Source/Geoscape/World/ChoiceLetter_TacticalEncounter.cs#L54-L125)
- [FloatMenuMakerWorld_ChoicesAtFor_Patch.cs](file:///D:/Repos/GitHub/Project-Vigilance/Geoscape/Source/Geoscape/Patches/FloatMenuMakerWorld_ChoicesAtFor_Patch.cs#L115-L589)

---

## Proposed Execution Phases

### Phase 1: Campaign Setup & Adoption Polish

- Refactor `Dialog_GeoscapeModAdoption.cs`: remove 176px dead space, invert button/card hierarchy, prevent accidental 'X' dismissal side-effects.
- Refactor `Page_GeoscapeRules.cs`: expand card height to 144f, implement dynamic line height for summary lines to prevent wrapping collision.

### Phase 2: Rules Customisation & Settings Polish

- Refactor `Dialog_GeoscapeCampaignRules.cs`: add tooltips to preset buttons, fix initial scroll height.
- Refactor `GeoscapeSettings.cs` & `RadarScopePreview.cs`: fix tab 0 scroll height snap, make radar columns height dynamic, add inspectable affordance to HUD scope.

### Phase 3: ChoiceLetter & Context Menu Polish

- Refactor `ChoiceLetter_TacticalEncounter.cs`: reorder choices to prioritize "Reform Caravan Now" when safe.
- Refactor `FloatMenuMakerWorld_ChoicesAtFor_Patch.cs`: apply menu option priorities for clean grouping.

### Phase 4: Verification & Automated Tests

- Run full test suite (`dotnet test Geoscape/Source/Geoscape.slnx`) to ensure 414+ tests pass.
- Deploy mod build to RimWorld installation and verify UI rendering in-game.

---

## Verification Plan

### Automated Tests

- `dotnet test Geoscape/Source/Geoscape.slnx`
- Validate that all rules, draft synchronization, and letter generation tests pass without regression.

### Manual In-Game Inspection

1. **Adoption Dialog:** Load existing pre-Vigilance save -> observe compact dialog without dead space, check clear button layout.
2. **Campaign Setup Page:** Start new game -> verify 2x2 cards have ample margin, inspect summary lines for zero text clipping.
3. **Rules Modal:** Open customisation modal -> hover preset buttons to see tooltips, check smooth scroll view without snapping.
4. **Mod Settings:** Switch between tabs -> check instant scroll stability, check radar HUD scope hover indicator.
5. **Encounter Letter:** Trigger skirmish or ambush -> check "Reform Caravan Now" is primary choice upon victory.
