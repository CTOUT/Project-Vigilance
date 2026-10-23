using RimWorld;
using Verse;

namespace ProjectVigilance.Geoscape.World;

[DefOf]
public static class PV_LetterDefOf
{
    public static LetterDef PV_TacticalEncounter_Threat = null!;
    public static LetterDef PV_TacticalEncounter_Positive = null!;
    public static LetterDef PV_TacticalEncounter_Neutral = null!;

    static PV_LetterDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(PV_LetterDefOf));
    }
}
