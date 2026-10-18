using HarmonyLib;
using Verse;

namespace ProjectVigilance.Geoscape;

public class GeoscapeMod : Mod
{
    public const string PackageId = "ProjectVigilance.Geoscape";

    public GeoscapeMod(ModContentPack content) : base(content)
    {
        var harmony = new Harmony(PackageId);
        harmony.PatchAll();

        Log.Message("[Project Vigilance] Initialized Module 1: Geoscape & Radar Grid (1.7 Waylith / .NET 10). Harmony patches applied.");
    }
}
