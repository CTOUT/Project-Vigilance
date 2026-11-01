using HarmonyLib;
using ProjectVigilance.Geoscape.Rules;
using RimWorld;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Hooks Page_CreateWorldParams to seed the draft campaign rules from defaults,
/// restore defaults on Reset, and render the "Geoscape Campaign Rules..." button in the bottom bar gap.
/// </summary>
public static class Page_CreateWorldParams_Patch
{
    private static readonly Vector2 DefaultBottomButSize = new(150f, 38f);
    private static Vector2? cachedBottomButSize;

    private static Vector2 BottomButSize
    {
        get
        {
            if (cachedBottomButSize.HasValue)
            {
                return cachedBottomButSize.Value;
            }

            try
            {
                var field = AccessTools.Field(typeof(Page), "BottomButSize");
                if (field != null && field.GetValue(null) is Vector2 v && v != Vector2.zero)
                {
                    cachedBottomButSize = v;
                    return v;
                }
            }
            catch
            {
                // Fallback to default RimWorld button size
            }

            cachedBottomButSize = DefaultBottomButSize;
            return DefaultBottomButSize;
        }
    }

    [HarmonyPatch(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.PreOpen))]
    public static class PreOpen_Patch
    {
        public static void Postfix()
        {
            NewCampaignRuleDraft.BeginFromDefaults(GeoscapeMod.Settings);
        }
    }

    [HarmonyPatch(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.Reset))]
    public static class Reset_Patch
    {
        public static void Postfix()
        {
            NewCampaignRuleDraft.BeginFromDefaults(GeoscapeMod.Settings);
        }
    }

    [HarmonyPatch(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.DoWindowContents))]
    public static class DoWindowContents_Patch
    {
        public static void Postfix(Rect rect)
        {
            if (NewCampaignRuleDraft.Current == null)
            {
                NewCampaignRuleDraft.BeginFromDefaults(GeoscapeMod.Settings);
            }

            Vector2 butSize = BottomButSize;

            // Gap between Back (left button) and the centred Reset button
            float gapStart = rect.x + butSize.x + 10f;
            float gapEnd = rect.x + rect.width / 2f - butSize.x / 2f - 10f;
            float availableWidth = gapEnd - gapStart;

            // If screen or UI scaling leaves too little room, skip rendering to prevent overlap
            if (availableWidth < 160f)
            {
                return;
            }

            float btnWidth = Mathf.Min(220f, availableWidth);
            Rect btnRect = new(gapStart, rect.yMax - butSize.y, btnWidth, butSize.y);

            if (Widgets.ButtonText(btnRect, "Geoscape Rules..."))
            {
                Find.WindowStack?.Add(new Dialog_GeoscapeCampaignRules(NewCampaignRuleDraft.GetOrCreate(GeoscapeMod.Settings)));
            }

            TooltipHandler.TipRegion(btnRect, "Configure Geoscape campaign rules for this new world. Most simulation rules lock permanently upon world generation.");
        }
    }
}
