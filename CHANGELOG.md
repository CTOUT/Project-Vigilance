# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial repository structure, boilerplate, and project standardization.
- Master architectural vision incorporating 1994 X-COM: UFO Defense, Terror from the Deep (TftD) aquatic mechanics, and modern Firaxis pacing elements.
- Modular decomposition roadmap dividing the suite into standalone and dependent layers.
- Detailed Geoscape & Radar technical specification (Module 1).
- Upgraded architectural target to RimWorld 1.7 (Waylith) and `.NET 10` (`net10.0`), leveraging the BRUTAL framework for zero-allocation planetary geometry and high TPS performance.
- Formalised Standalone vs Dependent modular architecture, establishing zero-thematic-leakage rules and contract-driven APIs for framework modules (Geoscape, Interception, Containment, Aquatics).
- Specified the Physical Incursion System in Geoscape to track non-player raids, visitors, travellers, creep-joiners, and caravans with real-world origin points on the WorldGrid.
- Added Item-Triggered Research Discovery ("Fog of Research") requiring physical field salvage, medical autopsies, or live interrogations to reveal tech tree projects.
- Integrated Odyssey DLC (1.6) gravships, player shuttles, and orbital layer travel for high-tier craft and space missions.
- Authored full DLC Integration Architecture mapping Royalty psionics/Psi-Labs, Ideology EXALT cults/precepts, Biotech alien xenotypes/gene-modding, Anomaly containment/Chryssalids, and Odyssey orbital space.
