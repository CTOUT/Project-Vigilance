using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// XML DefModExtension allowing any WorldObjectDef or ThingDef to customize its planetary radar/sensory profile.
/// Enables external modders to define stealth craft, signatures, and tracking behaviours without writing C# code.
/// </summary>
public class DefModExtension_Detectable : DefModExtension
{
    public float radarSignatureMultiplier = 1.0f;
    public bool isStealthCraft = false;
    public IntelligenceTier defaultClassificationTier = IntelligenceTier.Contact;
    public bool canBeMaskedByTerrain = true;
}
