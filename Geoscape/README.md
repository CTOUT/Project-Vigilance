# Module 1: Geoscape & Radar Grid

This directory hosts the C# source code, XML Defs, and mod package for **Module 1: Geoscape & Radar Grid**.

## System Scope

- Planetary sensory framework (`WorldObjectComp_Radar`).
- Sensory blind spots and planetary fog of war on the `WorldGrid`.
- Radar detection probability algorithms and scan pulse scheduler (`TickRare`).
- Support for moving world craft (`WorldObject_FlyingCraft`, `WorldObject_UFO`, `WorldObject_USO`).
- Sonar coverage for oceanic and coastal operations (Terror from the Deep integration).

For the detailed technical specification, see [docs/modules/geoscape-and-radar.md](../docs/modules/geoscape-and-radar.md).

## Planned Directory Layout

```
Geoscape/
├── 1.5/
│   ├── Assemblies/           # Compiled C# binaries
│   ├── Defs/                 # XML Defs (Buildings, Comps, Research)
│   └── Textures/             # UI blips, radar overlays, building art
├── About/
│   ├── About.xml             # RimWorld mod metadata
│   └── Preview.png           # Workshop preview image
└── Source/                   # C# Visual Studio / dotnet solution
    ├── Geoscape.sln
    └── Geoscape/
        ├── Comps/
        ├── World/
        ├── Detection/
        └── Rendering/
```
