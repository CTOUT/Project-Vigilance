# Archived Plans & Documents

Historical plans, test suites, reviews, and changelogs archived after milestone completion, verification, or supersession.

Active plans, specs, and operations guides reside in the parent `docs/` folder.

---

## Milestone 1.2 (v0.3.0)

- **[Milestone 1.2 Test Plan](milestone_1_2_test_plan.md):** Quality assurance test suite for Milestone 1.2 release.
- **[Workshop Change Notes v0.3.0](workshop_changenotes_v0.3.0.txt):** Steam Workshop release notes for v0.3.0.

---

## Milestone 1.3 (v0.4.0)

- **[Workshop Change Notes v0.4.0](workshop_changenotes_v0.4.0.txt):** Steam Workshop release notes for v0.4.0.
- **[Campaign Rules & Mutability Blueprint](Campaign_Rules_And_Mutability_Blueprint.md):** Specification for campaign creation wizard, mutability framework, and audit ledger.
- **[Orbital Logistics & Drifting Satellites Blueprint](Orbital_Logistics_And_Drifting_Satellites_Blueprint.md):** Specification for persistent orbital stations, drifting Keplerian ground tracks, and satellite roles.
- **[Classic X-COM Geoscape Blueprint](Classic_XCom_Geoscape_Blueprint.md):** Design blueprint for translating classic 1994 X-COM Geoscape aesthetics and temporal pacing into RimWorld.
- **[Geoscape Command Deck Interactive Mockup](Mockups/geoscape_command_deck_mockup.html):** Standalone HTML5/Canvas/WebAudio interactive art prototype.
- **[Milestone 1.3 Test Plan](milestone_1_3_test_plan.md):** 32-test quality assurance and playtest suite for Milestone 1.3 release.
- **[Milestone 1.3 Optimization Plan](milestone_1_3_optimization_plan.md):** Code hardening, security reviews, accessibility checks, and performance optimization plan.
- **[Walkthrough: Milestone 1.3 Optimization](walkthrough_milestone_1_3_optimization.md):** Accomplishments and verification walkthrough for the Milestone 1.3 optimization pass.
- **[Milestone 1.3 Playtest Fixes Plan](milestone_1_3_playtest_fixes_plan.md):** Fixes for in-game playtest findings including orbital flight, contact disambiguation, and outposts.
- **[Milestone 1.3 Sonar Line-of-Sight Plan](milestone_1_3_sonar_line_of_sight_plan.md):** Closed decision document (sonar line-of-sight deferred and extracted to `Vigilance.Aquatics` in Milestone 1.4+).
- **[UI Anti-Slop Review Plan](ui_anti_slop_review_plan.md):** Custom UI audit ensuring alignment with native RimWorld widget standards and margins.
- **[Learning Proposal: RimWorld Development Patterns](learning_proposal.md):** Initial proposal for development invariants, superseded by durable rules in `GEMINI.md`.
- **[Architectural Review & Modular Separation Plan](architectural_review_modular_separation_plan.md):** Architectural separation analysis, superseded by `docs/architecture/` and `docs/modules/`.

---

## Milestone 1.4 (v0.4.1)

- **[Milestone 1.4 Test Plan](milestone_1_4_test_plan.md):** Verification suite (`TC-01`–`TC-07`, `441/441` automated tests) for build telemetry, custom visual assets, faction livery tinting, and programmatic thermal integrity.
- **Completed Streams:** Headless CI reference assembly stubs (`refasmer`), custom visual asset pipeline with neutral grayscale livery masks, programmatic post-def thermal integrity engine (`ThermalIntegrityPostDefProcessor`), zero-allocation Keplerian solver optimization, Orbit-layer ground decluttering, deep-orbit raycast targeting, and WCAG AA accessibility audit. Active specifications live in `docs/architecture/`, `docs/modules/`, and parent `docs/`.
