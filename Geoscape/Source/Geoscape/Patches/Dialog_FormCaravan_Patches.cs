using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Harmony postfix on Dialog_FormCaravan.PostOpen to ensure required outpost
/// construction materials are automatically pre-selected in transferables
/// once transferables have been computed.
/// </summary>
[HarmonyPatch(typeof(Dialog_FormCaravan), nameof(Dialog_FormCaravan.PostOpen))]
public static class Dialog_FormCaravan_PostOpen_Patch
{
    public static void Postfix(Dialog_FormCaravan __instance)
    {
        if (__instance == null) return;

        if (CaravanExpeditionUtility.TryGetPendingDirectiveForDialog(__instance, out var directive) && directive != null)
        {
            if (directive.DirectiveKind == OutpostDirectiveKind.Construct)
            {
                CaravanExpeditionUtility.PreallocateConstructionMaterials(__instance, directive.Kind, directive.StuffDef);
                CaravanExpeditionUtility.PreallocateExpeditionFood(__instance, directive);
            }
            else if (directive.DirectiveKind == OutpostDirectiveKind.Dismantle)
            {
                CaravanExpeditionUtility.PreallocateExpeditionFood(__instance, directive);
                var (salvageMass, salvageDesc) = CaravanExpeditionUtility.GetExpectedDismantleYield(directive.TargetOutpost, directive.Kind, directive.StuffDef);
                string outpostLabel = directive.TargetOutpost?.LabelCap ?? CaravanExpeditionUtility.GetListeningPostKindLabel(directive.Kind);
                Messages.Message($"[Project Vigilance] Dismantle expedition route set to {outpostLabel} at Tile {directive.TargetTile}. Expected salvage: {salvageDesc} (~{salvageMass:F1} kg). Ensure sufficient pawns or pack animals are selected to carry recovered materials.", MessageTypeDefOf.CautionInput, false);
            }
        }
    }
}

/// <summary>
/// Validates that an outpost construction caravan includes at least one colonist capable of Construction.
/// </summary>
[HarmonyPatch(typeof(Dialog_FormCaravan), "CheckForErrors", typeof(List<Pawn>))]
public static class Dialog_FormCaravan_CheckForErrors_Patch
{
    public static void Postfix(Dialog_FormCaravan __instance, List<Pawn> pawns, ref bool __result)
    {
        if (__result) return; // In vanilla, CheckForErrors returns true if ANY error exists, false if OK

        if (!CaravanExpeditionUtility.TryGetPendingDirectiveForDialog(__instance, out var directive) ||
            directive == null)
        {
            return;
        }

        if (directive.DirectiveKind == OutpostDirectiveKind.Construct)
        {
            if (!OutpostAssemblySpeedUtility.HasCapableBuilder(pawns))
            {
                Messages.Message("Cannot send outpost expedition without at least one colonist capable of Construction.", MessageTypeDefOf.RejectInput, false);
                __result = true; // Signals error present to halt caravan formation
            }
        }
        else if (directive.DirectiveKind == OutpostDirectiveKind.Dismantle)
        {
            float capacity = CaravanExpeditionUtility.GetCaravanMassCapacity(__instance);
            float usage = CaravanExpeditionUtility.GetCaravanMassUsage(__instance);
            float spareCapacity = capacity - usage;
            var (salvageMass, _) = CaravanExpeditionUtility.GetExpectedDismantleYield(directive.TargetOutpost, directive.Kind, directive.StuffDef);

            if (capacity > 0f && spareCapacity < salvageMass)
            {
                Messages.Message($"Cannot send dismantle expedition: Insufficient carry capacity for recovered materials (Requires ~{salvageMass:F1} kg spare capacity, have ~{Mathf.Max(0f, spareCapacity):F1} kg). Add more colonists or pack animals.", MessageTypeDefOf.RejectInput, false);
                __result = true;
            }
        }
    }
}

/// <summary>
/// Intercepts the "Send" button on Dialog_FormCaravan for outpost expeditions to present
/// a clear confirmation dialog with on-site assembly timescale details before departure.
/// </summary>
[HarmonyPatch(typeof(Dialog_FormCaravan), "TrySend")]
public static class Dialog_FormCaravan_TrySend_Patch
{
    public static bool Prefix(Dialog_FormCaravan __instance)
    {
        if (__instance == null) return true;

        if (!CaravanExpeditionUtility.TryGetPendingDirectiveForDialog(__instance, out var directive) ||
            directive == null)
        {
            return true;
        }

        if (directive.DirectiveKind == OutpostDirectiveKind.Construct)
        {
            List<TransferableOneWay>? transferables = CaravanExpeditionUtility.TransferablesField?.GetValue(__instance) as List<TransferableOneWay>;
            List<Pawn> selectedPawns = new List<Pawn>();
            if (transferables != null)
            {
                for (int i = 0; i < transferables.Count; i++)
                {
                    TransferableOneWay t = transferables[i];
                    if (t.ThingDef != null && t.ThingDef.category == ThingCategory.Pawn && t.CountToTransfer > 0 && t.things != null)
                    {
                        selectedPawns.AddRange(t.things.OfType<Pawn>().Take(t.CountToTransfer));
                    }
                }
            }

            if (!OutpostAssemblySpeedUtility.HasCapableBuilder(selectedPawns))
            {
                if (Find.WindowStack != null)
                {
                    Find.WindowStack.Add(new Dialog_MessageBox(
                        "Cannot dispatch outpost expedition:\n\nNo colonist capable of Construction is selected.\n\nAssembly of the listening post requires at least one builder.",
                        "Go Back",
                        null,
                        null,
                        null,
                        "Missing Builder",
                        false));
                }
                return false;
            }

            string outpostTitle = CaravanExpeditionUtility.GetListeningPostKindLabel(directive.Kind);
            var (lead, teamSpeed) = OutpostAssemblySpeedUtility.CalculateTeamSpeed(selectedPawns);
            int durationTicks = OutpostAssemblySpeedUtility.CalculateAssemblyDurationTicks(selectedPawns, directive.Kind);
            float assemblyHours = durationTicks / 2500f;
            float assemblyDays = durationTicks / 60000f;

            int transitTicks = CaravanExpeditionUtility.GetEstimatedTransitTicks(__instance, directive.TargetTile);
            float transitDays = transitTicks > 0 ? (transitTicks / 60000f) : 0.25f;
            float roundTripTransitDays = transitDays * 2f;
            float missionTotalDays = roundTripTransitDays + assemblyDays;

            float daysWorthOfFood = CaravanExpeditionUtility.GetDaysWorthOfFood(__instance);
            bool foodSufficient = daysWorthOfFood >= missionTotalDays;

            var capablePawns = selectedPawns.Where(OutpostAssemblySpeedUtility.IsCapableBuilder).ToList();
            string leadText = lead != null ? $"{lead.LabelShort} (Speed: {OutpostAssemblySpeedUtility.GetBuilderSpeed(lead):P0})" : "None";
            string helpersText = capablePawns.Count > 1
                ? string.Join(", ", capablePawns.Where(p => p != lead).Select(p => p.LabelShort))
                : "None (Solo builder)";

            string foodStatus = foodSufficient
                ? $"Food Provisions: {daysWorthOfFood:F1} days (SUFFICIENT for ~{missionTotalDays:F1} days round-trip mission)"
                : $"Food Provisions: {daysWorthOfFood:F1} days (WARNING: Less than estimated ~{missionTotalDays:F1} days round-trip mission)";

            string msg = $"Deploy Outpost Expedition\n\n" +
                         $"Target Location: Tile {directive.TargetTile}\n" +
                         $"Structure: {outpostTitle}\n\n" +
                         $"Lead Builder: {leadText}\n" +
                         $"Assistants (+50% bonus): {helpersText}\n" +
                         $"Team Assembly Speed: {teamSpeed:P0}\n\n" +
                         $"Estimated On-Site Assembly: ~{assemblyHours:F1} in-game hours\n" +
                         $"Estimated Round-Trip Transit: ~{roundTripTransitDays:F1} days\n" +
                         $"Total Estimated Mission Time: ~{missionTotalDays:F1} days\n\n" +
                         $"{foodStatus}\n\n" +
                         $"Proceed with expedition deployment?";

            if (Find.WindowStack != null)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    msg,
                    () =>
                    {
                        if (CaravanExpeditionUtility.TryFormAndSendCaravan(__instance))
                        {
                            SoundDefOf.Tick_High?.PlayOneShotOnCamera();
                            __instance.Close(false);
                        }
                    },
                    destructive: !foodSufficient,
                    title: "Confirm Outpost Expedition"));
            }

            return false;
        }
        else if (directive.DirectiveKind == OutpostDirectiveKind.Dismantle)
        {
            float capacity = CaravanExpeditionUtility.GetCaravanMassCapacity(__instance);
            float usage = CaravanExpeditionUtility.GetCaravanMassUsage(__instance);
            float spareCapacity = capacity - usage;
            var (salvageMass, salvageDesc) = CaravanExpeditionUtility.GetExpectedDismantleYield(directive.TargetOutpost, directive.Kind, directive.StuffDef);

            if (capacity > 0f && spareCapacity < salvageMass)
            {
                float shortfall = salvageMass - spareCapacity;
                if (Find.WindowStack != null)
                {
                    Find.WindowStack.Add(new Dialog_MessageBox(
                        $"Cannot dispatch dismantle expedition:\n\n" +
                        $"Selected colonists and animals do not have sufficient carry capacity to bring salvaged materials home.\n\n" +
                        $"Expected Salvage: {salvageDesc} (~{salvageMass:F1} kg)\n" +
                        $"Caravan Spare Capacity: ~{Mathf.Max(0f, spareCapacity):F1} kg\n" +
                        $"Capacity Shortfall: ~{shortfall:F1} kg\n\n" +
                        $"Please add additional colonists or pack animals (e.g. Muffalos, Dromedaries, Elephants, Donkeys, Horses) to carry the salvage.",
                        "Go back",
                        null,
                        null,
                        null,
                        "Insufficient Carry Capacity",
                        false));
                }
                return false;
            }

            string outpostTitle = directive.TargetOutpost?.LabelCap ?? CaravanExpeditionUtility.GetListeningPostKindLabel(directive.Kind);
            int transitTicks = CaravanExpeditionUtility.GetEstimatedTransitTicks(__instance, directive.TargetTile);
            float transitDays = transitTicks > 0 ? (transitTicks / 60000f) : 0.25f;
            float roundTripTransitDays = transitDays * 2f;
            float dismantleHours = 1500f / 2500f;
            float dismantleDays = 1500f / 60000f;
            float missionTotalDays = roundTripTransitDays + dismantleDays;

            float daysWorthOfFood = CaravanExpeditionUtility.GetDaysWorthOfFood(__instance);
            bool foodSufficient = daysWorthOfFood >= missionTotalDays;

            string foodStatus = foodSufficient
                ? $"Food Provisions: {daysWorthOfFood:F1} days (SUFFICIENT for ~{missionTotalDays:F1} days round-trip mission)"
                : $"Food Provisions: {daysWorthOfFood:F1} days (WARNING: Less than estimated ~{missionTotalDays:F1} days round-trip mission)";

            string capacityStatus = capacity > 0f
                ? $"Caravan Spare Capacity: ~{spareCapacity:F1} kg available (SUFFICIENT for ~{salvageMass:F1} kg salvage)"
                : $"Expected Salvage Mass: ~{salvageMass:F1} kg";

            string msg = $"Deploy Outpost Dismantle Expedition\n\n" +
                         $"Target Location: Tile {directive.TargetTile}\n" +
                         $"Structure: {outpostTitle}\n\n" +
                         $"Expected Salvage: {salvageDesc}\n" +
                         $"{capacityStatus}\n\n" +
                         $"Estimated On-Site Dismantling: ~{dismantleHours:F1} in-game hours\n" +
                         $"Estimated Round-Trip Transit: ~{roundTripTransitDays:F1} days\n" +
                         $"Total Estimated Mission Time: ~{missionTotalDays:F1} days\n\n" +
                         $"{foodStatus}\n\n" +
                         $"Proceed with dismantle expedition deployment?";

            if (Find.WindowStack != null)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    msg,
                    () =>
                    {
                        if (CaravanExpeditionUtility.TryFormAndSendCaravan(__instance))
                        {
                            SoundDefOf.Tick_High?.PlayOneShotOnCamera();
                            __instance.Close(false);
                        }
                    },
                    destructive: !foodSufficient,
                    title: "Confirm Outpost Dismantle Expedition"));
            }

            return false;
        }

        return true;
    }
}
