using System.Text;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

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

    public PlanetTile CurrentSubSatelliteTile => OrbitalFlightModel.CalculateSubSatelliteTile(orbit, Current.Game?.tickManager?.TicksGame ?? 0);

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
    public int IdentificationRadiusInTiles => Mathf.Max(1, Mathf.RoundToInt(footprintRadiusInTiles * 0.6f));

    public IntelligenceTier MaxIntelligenceTier => role switch
    {
        SatelliteRole.Reconnaissance => IntelligenceTier.Analysed,
        SatelliteRole.Military => IntelligenceTier.Analysed,
        _ => IntelligenceTier.Contact
    };

    public bool IsSensorActive => role == SatelliteRole.Reconnaissance || role == SatelliteRole.Military;
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
        if (Find.World?.worldObjects != null)
        {
            Tile = newTile;
        }
        else
        {
            HarmonyLib.Traverse.Create(this).Field("tile").SetValue(newTile);
        }
    }

    public override void SpawnSetup()
    {
        base.SpawnSetup();
        if (string.IsNullOrEmpty(satelliteCallsign) || satelliteCallsign == "SAT-1")
        {
            satelliteCallsign = LabelCap.NullOrEmpty() ? $"{role}-{ID}" : LabelCap;
        }

        Tile = CurrentSubSatelliteTile;
        OrbitalManager.Instance?.RegisterSatellite(this);

        if (IsSensorActive)
        {
            SensoryManager.Instance?.RegisterSensor(this);
        }
    }

    public override void PostRemove()
    {
        base.PostRemove();
        OrbitalManager.Instance?.DeregisterSatellite(this);

        if (IsSensorActive)
        {
            SensoryManager.Instance?.DeregisterSensor(this);
        }
    }

    public string ParentStationName
    {
        get
        {
            if (parentStationId < 0 || Find.World?.worldObjects == null) return "Station Hub";
            return Find.World.worldObjects.AllWorldObjects.Find(wo => wo.ID == parentStationId)?.LabelCap ?? "Station Hub";
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
        WorldObject? parentStation = Find.World?.worldObjects?.AllWorldObjects.Find(wo => wo.ID == parentStationId);
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
        PlanetTile newTile = OrbitalFlightModel.CalculateSubSatelliteTile(orbit, currentTick);
        if (newTile.Valid && newTile != Tile)
        {
            SetTileSafe(newTile);

            // When reconnaissance satellite traverses onto a new sector, sweep sensors
            if (role == SatelliteRole.Reconnaissance && Faction == RimWorld.Faction.OfPlayer)
            {
                SensoryManager.Instance?.PerformSensorySweep();
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
    }
}
