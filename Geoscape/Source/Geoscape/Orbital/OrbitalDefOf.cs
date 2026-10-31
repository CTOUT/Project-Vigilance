using RimWorld;

namespace ProjectVigilance.Geoscape.Orbital;

/// <summary>
/// DefOf class referencing orbital stations, satellites, and orbital infrastructure Defs.
/// </summary>
[DefOf]
public static class OrbitalDefOf
{
    public static WorldObjectDef? Vigilance_OrbitalStation;
    public static WorldObjectDef? Vigilance_Satellite_Recon;
    public static WorldObjectDef? Vigilance_Satellite_Trade;
    public static WorldObjectDef? Vigilance_Satellite_Nav;
    public static WorldObjectDef? Vigilance_Satellite_Comms;
    public static WorldObjectDef? Vigilance_Satellite_Military;

    static OrbitalDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(OrbitalDefOf));
    }
}
