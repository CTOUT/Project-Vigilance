# Contributing to the Rimworld X-COM Suite

Thank you for your interest in contributing.

## Architecture & Modular Philosophy

This project is built around a decoupled, modular architecture:

- **Modularity:** Core systems (such as the Geoscape radar grid) must function as standalone utilities wherever possible.
- **Dependency Inversion:** Higher-tier content packs depend on core framework modules, never the reverse.
- **KISS & DRY:** Keep implementations simple, modular, and performant on both the local map and the planetary `WorldGrid`.

## Reporting Issues

Open a GitHub issue and include:

- RimWorld version and active DLCs (Royalty, Ideology, Biotech, Anomaly).
- Mod load order / player log (Player.log or in-game log link).
- Steps to reproduce the issue.
- Relevant stack traces from player logs.

## Pull Requests

1. Fork the repository and create a focused feature branch.
2. Keep changes scoped — one module or architectural concern per PR.
3. Add an entry under `[Unreleased]` in [CHANGELOG.md](CHANGELOG.md).
4. Run formatting and spellcheck before submitting.
5. Open the PR against `main`.
