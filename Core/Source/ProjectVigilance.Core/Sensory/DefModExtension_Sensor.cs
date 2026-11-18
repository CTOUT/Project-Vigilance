using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// XML DefModExtension allowing any ThingDef or WorldObjectDef to function as an active planetary sensor.
/// Enables external modders to define radar, optical, or sonar sensory nodes without writing C# code.
/// </summary>
public class DefModExtension_Sensor : DefModExtension
{
    public float detectionRadius = 20f;
    public float identificationRadius = 12f;
    public bool isTechnological = true;
    public bool penetratesTerrainMasking = false;
    public SensorDomainFlags sensorDomain = SensorDomainFlags.Radar;
    public IntelligenceTier maxIntelligenceTier = IntelligenceTier.Analysed;
}
