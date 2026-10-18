using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.World;

public class WorldObject_Incursion : WorldObject
{
    private IncidentDef? incidentDef;
    private IncidentParms? incidentParms;
    private PlanetTile originTile;
    private PlanetTile destinationTile;
    private List<PlanetTile> pathTiles = new();
    private int currentPathIndex;
    private float tileProgress;
    private float speedTilesPerDay = 24f;

    public IncidentDef? IncidentDef => incidentDef;
    public IncidentParms? IncidentParms => incidentParms;
    public PlanetTile OriginTile => originTile;
    public PlanetTile DestinationTile => destinationTile;
    public IReadOnlyList<PlanetTile> PathTiles => pathTiles;
    public int CurrentPathIndex => currentPathIndex;
    public float TileProgress => tileProgress;
    public float SpeedTilesPerDay => speedTilesPerDay;

    public override Color ExpandingIconColor => Faction?.Color ?? Color.red;

    public void SetupIncursion(
        IncidentDef def,
        IncidentParms parms,
        PlanetTile origin,
        PlanetTile destination,
        List<PlanetTile> path,
        float speed)
    {
        incidentDef = def;
        incidentParms = parms;
        originTile = origin;
        destinationTile = destination;
        pathTiles = path ?? new List<PlanetTile>();
        currentPathIndex = 0;
        tileProgress = 0f;
        speedTilesPerDay = speed > 0f ? speed : 24f;

        if (parms.faction != null)
        {
            SetFaction(parms.faction);
        }

        if (pathTiles.Count > 0)
        {
            Tile = pathTiles[0];
        }
        else
        {
            Tile = origin;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Defs.Look(ref incidentDef, "incidentDef");
        Scribe_Deep.Look(ref incidentParms, "incidentParms");
        Scribe_Values.Look(ref speedTilesPerDay, "speedTilesPerDay", 24f);
        Scribe_Values.Look(ref currentPathIndex, "currentPathIndex", 0);
        Scribe_Values.Look(ref tileProgress, "tileProgress", 0f);

        int originId = originTile.tileId;
        Scribe_Values.Look(ref originId, "originTileId", 0);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            originTile = (PlanetTile)originId;
        }

        int destinationId = destinationTile.tileId;
        Scribe_Values.Look(ref destinationId, "destinationTileId", 0);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            destinationTile = (PlanetTile)destinationId;
        }

        List<int>? serializedPath = null;
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            serializedPath = pathTiles.Select(t => t.tileId).ToList();
        }

        Scribe_Collections.Look(ref serializedPath, "pathTileIds", LookMode.Value);

        if (Scribe.mode == LoadSaveMode.LoadingVars && serializedPath != null)
        {
            pathTiles = serializedPath.Select(id => (PlanetTile)id).ToList();
        }
    }

    protected override void Tick()
    {
        base.Tick();

        if (pathTiles == null || pathTiles.Count == 0)
        {
            IncursionArrivalManager.ExecuteArrival(this);
            return;
        }

        float tilesPerTick = speedTilesPerDay / 60000f;
        tileProgress += tilesPerTick;

        while (tileProgress >= 1f)
        {
            tileProgress -= 1f;
            currentPathIndex++;

            if (currentPathIndex < pathTiles.Count)
            {
                Tile = pathTiles[currentPathIndex];
            }
            else
            {
                IncursionArrivalManager.ExecuteArrival(this);
                return;
            }
        }
    }

    public override string GetInspectString()
    {
        int remainingTiles = Math.Max(0, pathTiles.Count - currentPathIndex);
        float remainingDays = remainingTiles / Math.Max(0.1f, speedTilesPerDay);
        float remainingHours = remainingDays * 24f;

        string factionName = Faction?.Name ?? "Unknown Hostile";
        string raidType = incidentDef?.label ?? "Incursion";

        return $"{raidType.CapitalizeFirst()} ({factionName})\nETA: ~{remainingHours:F1} hours ({remainingTiles} hexes remaining)";
    }
}
