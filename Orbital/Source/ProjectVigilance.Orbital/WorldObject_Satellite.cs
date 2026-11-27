using System;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Core.Modules;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Orbital;

/// <summary>
/// Autonomous orbital platform moving along a drifting Keplerian ground track.
/// Acts as a mobile operational asset for orbital factions and planetary defense networks.
/// Implements ISensorProvider for seamless integration with the Geoscape sensory grid and Haze of War.
/// </summary>
public class WorldObject_Satellite : WorldObject, IOrbitalEntity, ISensorProvider
{
    private SatelliteRole role = SatelliteRole.Reconnaissance;
    private OrbitalParameters orbit = new();
    private int parentStationId = -1;
    private bool inServiceArea = false;
    private string satelliteCallsign = "SAT-1";
    private float footprintRadiusInTiles = 14f;

    public SatelliteRole Role
    {
        get => role;
        set => role = value;
    }

    public OrbitalParameters? Orbit => orbit;
    public string OrbitalCallsign => satelliteCallsign;
    public int ParentStationId => parentStationId;
    public bool InServiceArea => inServiceArea;
    public float FootprintRadiusInTiles => footprintRadiusInTiles;

    private Material? cachedFactionMat;
    private Faction? cachedFaction;

    public override bool VisibleInBackground => true;

    private Texture2D? cachedExpandingIcon;
    public override Texture2D ExpandingIcon
    {
        get
        {
            if (cachedExpandingIcon == null)
            {
                if (def?.expandingIconTexture != null)
                {
                    cachedExpandingIcon = ContentFinder<Texture2D>.Get(def.expandingIconTexture, false);
                }
                cachedExpandingIcon ??= ContentFinder<Texture2D>.Get("World/WorldObjects/Expanding/Satellite", false)
                    ?? ContentFinder<Texture2D>.Get("World/WorldObjects/Expanding/TravelingTransportPods", false)
                    ?? base.ExpandingIcon;
            }
            return cachedExpandingIcon ?? base.ExpandingIcon;
        }
    }

    public override Color ExpandingIconColor => Faction != null ? Faction.Color : Color.white;

    public override Material Material =>
        OrbitalVisibilityUtility.ResolveOrbitalMaterial(this, ref cachedFactionMat, ref cachedFaction) ?? base.Material;

    public override void SetFaction(Faction newFaction) =>
        OrbitalVisibilityUtility.SetFactionSafe(this, newFaction, base.SetFaction, ref cachedFactionMat, ref cachedFaction);

    public PlanetTile CurrentSubSatelliteTile => OrbitalFlightModel.CalculateSubSatelliteTile(orbit, Current.Game?.tickManager?.TicksGame ?? 0, (PlanetTile)Tile);

    public Vector3 CurrentOrbitalPosition => OrbitalFlightModel.CalculateOrbitalPosition(
        orbit,
        Current.Game?.tickManager?.TicksGame ?? 0,
        altitudeRenderOffset: 12f);

    public override Vector3 DrawPos => CurrentOrbitalPosition;

    #region ISensorProvider Implementation

    public WorldObject? Source => this;
    public string SensorLabel => LabelCap.NullOrEmpty() ? satelliteCallsign : LabelCap;
    public PlanetTile SensorTile => Tile;
    public float DetectionRadiusInTiles => footprintRadiusInTiles;
    public int IdentificationRadiusInTiles => MaxIntelligenceTier >= IntelligenceTier.Classified
        ? Mathf.Max(1, Mathf.RoundToInt(footprintRadiusInTiles * 0.6f))
        : 0;

    public IntelligenceTier MaxIntelligenceTier => role switch
    {
        SatelliteRole.Reconnaissance => IntelligenceTier.Analysed,
        SatelliteRole.Military => IntelligenceTier.Analysed,
        _ => IntelligenceTier.Contact
    };

    public bool IsSensorActive
    {
        get
        {
            if (role != SatelliteRole.Reconnaissance && role != SatelliteRole.Military)
            {
                return false;
            }

            if (ProjectVigilance.Geoscape.Sensory.SensoryDisruptionUtility.IsSolarFlareActive(Tile))
            {
                return false;
            }

            // In active gameplay, only player satellites provide sensor coverage to the player.
            // In unit test harnesses without World initialized, allow testing sensor properties when Faction is null.
            if (Faction != null)
            {
                return Faction.IsPlayer;
            }

            return Find.World == null || Find.FactionManager == null;
        }
    }
    public bool IsStationary => false;
    public bool IsTechnological => true;
    public bool PenetratesTerrainMasking => true;

    #endregion

    public void InitializeSatellite(
        SatelliteRole assignedRole,
        OrbitalParameters orbitalParameters,
        int stationId,
        string callsign,
        float footprintRadius = 14f)
    {
        role = assignedRole;
        orbit = orbitalParameters;
        parentStationId = stationId;
        satelliteCallsign = callsign;
        footprintRadiusInTiles = footprintRadius;
        SetTileSafe(CurrentSubSatelliteTile);
    }

    private void SetTileSafe(PlanetTile newTile)
    {
        try
        {
            if (Find.World?.worldObjects != null && newTile.Tile != null)
            {
                Tile = newTile;
                return;
            }
        }
        catch (Exception)
        {
            // Fallback for layers or world states lacking ground tile definitions (e.g. FastTileFinder NRE)
        }

        WorldObjectAccessUtility.SetTileRaw(this, newTile);
    }

    internal void ReassignParentStation(int newParentStationId) => parentStationId = newParentStationId;

    public override void SpawnSetup()
    {
        base.SpawnSetup();
        OrbitalManager.Instance?.RegisterSatellite(this);

        if (string.IsNullOrEmpty(satelliteCallsign) || satelliteCallsign == "SAT-1")
        {
            satelliteCallsign = LabelCap.NullOrEmpty() ? $"{role}-{ID}" : LabelCap;
        }

        SetTileSafe(CurrentSubSatelliteTile);

        if (IsSensorActive && (Faction?.IsPlayer == true || (Faction == null && Find.World == null)))
        {
            VigilanceModuleManager.RegisterDynamicSensor(this);
        }
    }

    public override void PostRemove()
    {
        if (Current.ProgramState == ProgramState.Playing)
        {
            base.PostRemove();
        }

        OrbitalManager.Instance?.DeregisterSatellite(this);

        if (IsSensorActive)
        {
            VigilanceModuleManager.DeregisterDynamicSensor(this);
        }
    }

    public string ParentStationName
    {
        get
        {
            if (parentStationId < 0 || Find.World?.worldObjects == null) return "Station Hub";
            WorldObject? station = OrbitalManager.Instance?.GetStationById(parentStationId);
            if (station != null) return station.LabelCap;

            var all = Find.World.worldObjects.AllWorldObjects;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].ID == parentStationId)
                {
                    return all[i].LabelCap;
                }
            }
            return "Station Hub";
        }
    }

    protected override void Tick()
    {
        base.Tick();

        int currentTick = Current.Game?.tickManager?.TicksGame ?? 0;
        if (currentTick % 60 == 0)
        {
            UpdateSubSatelliteTile(currentTick);
        }

        if (currentTick % 250 == 0 && parentStationId >= 0 && Find.World?.worldObjects != null)
        {
            CheckServiceAreaProximity(currentTick);
        }
    }

    private void CheckServiceAreaProximity(int currentTick)
    {
        WorldObject? parentStation = OrbitalManager.Instance?.GetStationById(parentStationId);
        if (parentStation == null && Find.World?.worldObjects != null)
        {
            var all = Find.World.worldObjects.AllWorldObjects;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].ID == parentStationId)
                {
                    parentStation = all[i];
                    break;
                }
            }
        }

        if (parentStation != null && parentStation.Tile.Valid && Find.World?.grid != null)
        {
            Vector3 stationDir = Find.World.grid.GetTileCenter(parentStation.Tile).normalized;
            Vector3 satDir = OrbitalFlightModel.CalculateSubSatelliteDirection(orbit, currentTick);
            bool inZone = OrbitalFlightModel.AngularDistanceDeg(stationDir, satDir) <= 35f;

            if (inZone && !inServiceArea)
            {
                OnEnterServiceArea(parentStation);
            }
            else if (!inZone && inServiceArea)
            {
                OnLeaveServiceArea(parentStation);
            }
        }
    }

    public void UpdateSubSatelliteTile(int currentTick)
    {
        PlanetTile newTile = OrbitalFlightModel.CalculateSubSatelliteTile(orbit, currentTick, (PlanetTile)Tile);
        if (newTile.Valid && newTile != Tile)
        {
            SetTileSafe(newTile);

            // When reconnaissance satellite traverses onto a new sector, sweep sensors
            if (role == SatelliteRole.Reconnaissance && Faction?.IsPlayer == true)
            {
                VigilanceModuleManager.PerformSensorySweep();
            }
        }
    }

    public void TickForTesting(int currentTick)
    {
        UpdateSubSatelliteTile(currentTick);
        if (parentStationId >= 0)
        {
            CheckServiceAreaProximity(currentTick);
        }
    }

    public virtual void OnEnterServiceArea(WorldObject station)
    {
        inServiceArea = true;
    }

    public virtual void OnLeaveServiceArea(WorldObject station)
    {
        inServiceArea = false;
    }

    public bool CoversTile(PlanetTile targetTile)
    {
        if (orbit == null || !targetTile.Valid) return false;
        int currentTick = Current.Game?.tickManager?.TicksGame ?? 0;
        Vector3 satDir = OrbitalFlightModel.CalculateSubSatelliteDirection(orbit, currentTick);
        return OrbitalFlightModel.IsTileWithinFootprint(satDir, targetTile, footprintRadiusInTiles);
    }

    public string GetOrbitalInspectString()
    {
        StringBuilder sb = new();
        sb.Append("Role: ").AppendLine(role.ToString());
        sb.Append("Altitude: ").Append(orbit.AltitudeKm.ToString("F0")).Append(" km (Period: ")
          .Append((orbit.PeriodTicks / 2500f).ToString("F1")).AppendLine(" hrs)");
        sb.Append("Inclination: ").Append(orbit.InclinationDeg.ToString("F1")).Append("° (Precession: ")
          .Append(orbit.PrecessionRateDegPerDay.ToString("F1")).AppendLine("°/day)");

        float vel = OrbitalFlightModel.CalculateOrbitalVelocityKmS(orbit);
        sb.Append("Orbital Velocity: ").Append(vel.ToString("F2")).AppendLine(" km/s");
        sb.Append("Coverage Footprint: ").Append(footprintRadiusInTiles.ToString("F0")).AppendLine(" tiles");

        if (inServiceArea)
        {
            sb.Append("Status: In Station Service Corridor (Maintenance & Sync: ").Append(ParentStationName).Append(")");
        }
        else
        {
            sb.Append("Status: Autonomous Mission (Drifting Ground Track)");
        }

        return sb.ToString().TrimEnd();
    }

    public override string GetInspectString()
    {
        StringBuilder sb = new();
        string baseString = base.GetInspectString();
        if (!string.IsNullOrEmpty(baseString))
        {
            sb.AppendLine(baseString);
        }
        sb.Append(GetOrbitalInspectString());
        return sb.ToString().TrimEnd();
    }

    public override void DrawExtraSelectionOverlays()
    {
        if (!OrbitalVisibilityUtility.IsOrbitalEntityDetected(this))
        {
            return;
        }

        base.DrawExtraSelectionOverlays();
        PlanetTile subTile = CurrentSubSatelliteTile;
        if (subTile.Valid)
        {
            GenDraw.DrawWorldRadiusRing(subTile, Mathf.RoundToInt(footprintRadiusInTiles));
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref role, "role", SatelliteRole.Reconnaissance);
        Scribe_Deep.Look(ref orbit, "orbit");
        Scribe_Values.Look(ref parentStationId, "parentStationId", -1);
        Scribe_Values.Look(ref inServiceArea, "inServiceArea", false);
        Scribe_Values.Look(ref satelliteCallsign, "satelliteCallsign", "SAT-1");
        Scribe_Values.Look(ref footprintRadiusInTiles, "footprintRadiusInTiles", 14f);

        orbit ??= new OrbitalParameters();

        // Migrate legacy fast periods (< 30,000 ticks) from pre-patch saves to storyteller-aligned periods
        if (Scribe.mode == LoadSaveMode.PostLoadInit && orbit.PeriodTicks < 30000)
        {
            orbit.PeriodTicks = OrbitalManager.GetDefaultPeriodForRole(role);
        }
    }
}
