using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ProjectVigilance.Geoscape.World;

/// <summary>
/// Interactive looting and triage dialog presented when a player caravan investigates
/// an Encounter Aftermath site. Displays live caravan mass capacity and enables players
/// to selectively salvage cargo and rescue survivors without unintentional over-encumbrance.
/// </summary>
public sealed class Dialog_ScavengeAftermath : Window
{
    private readonly Caravan caravan;
    private readonly WorldObject_EncounterAftermath aftermath;

    private readonly List<ThingTransferEntry> itemEntries = new();
    private readonly List<SurvivorTransferEntry> survivorEntries = new();

    private Vector2 scrollPosition = Vector2.zero;
    private float lastViewHeight = 400f;

    public override Vector2 InitialSize => new(740f, 620f);

    private sealed class ThingTransferEntry
    {
        public Thing Thing { get; }
        public int MaxCount { get; }
        public int CountToTake { get; set; }
        public float UnitMass { get; }

        public ThingTransferEntry(Thing thing)
        {
            Thing = thing;
            MaxCount = thing.stackCount;
            CountToTake = 0;
            UnitMass = thing.GetStatValue(StatDefOf.Mass);
        }
    }

    private sealed class SurvivorTransferEntry
    {
        public Pawn Pawn { get; }
        public bool TakeWithCaravan { get; set; }
        public float Mass { get; }

        public SurvivorTransferEntry(Pawn pawn)
        {
            Pawn = pawn;
            TakeWithCaravan = false;
            Mass = pawn.GetStatValue(StatDefOf.Mass);
        }
    }

    public Dialog_ScavengeAftermath(Caravan caravan, WorldObject_EncounterAftermath aftermath)
    {
        this.caravan = caravan;
        this.aftermath = aftermath;

        forcePause = true;
        doCloseX = true;
        doCloseButton = false;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = true;

        InitializeEntries();
    }

    private void InitializeEntries()
    {
        itemEntries.Clear();
        if (aftermath.SalvageLoot != null)
        {
            foreach (Thing item in aftermath.SalvageLoot)
            {
                if (item != null && !item.Destroyed && item.stackCount > 0)
                {
                    itemEntries.Add(new ThingTransferEntry(item));
                }
            }
        }

        survivorEntries.Clear();
        if (aftermath.WoundedSurvivors != null)
        {
            foreach (Pawn p in aftermath.WoundedSurvivors)
            {
                if (p != null && !p.Dead && !p.Destroyed)
                {
                    survivorEntries.Add(new SurvivorTransferEntry(p));
                }
            }
        }
    }

    private float CalculateSelectedAdditionalMass()
    {
        float mass = 0f;
        for (int i = 0; i < itemEntries.Count; i++)
        {
            mass += itemEntries[i].CountToTake * itemEntries[i].UnitMass;
        }
        for (int i = 0; i < survivorEntries.Count; i++)
        {
            if (survivorEntries[i].TakeWithCaravan)
            {
                mass += survivorEntries[i].Mass;
            }
        }
        return mass;
    }

    public override void DoWindowContents(Rect inRect)
    {
        // 1. Header
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "Investigate Encounter Aftermath");
        Text.Font = GameFont.Small;

        string vicName = aftermath.VictorFaction?.Name ?? "Rival Force";
        string defName = aftermath.DefeatedFaction?.Name ?? "Enemy Force";
        Rect subHeaderRect = new(inRect.x, inRect.y + 34f, inRect.width, 24f);
        GUI.color = new Color(0.85f, 0.85f, 0.85f);
        Widgets.Label(subHeaderRect, $"Skirmish aftermath site ({vicName} vs {defName}). Select cargo to salvage and survivors to tend:");
        GUI.color = Color.white;

        // 2. Telemetry & Capacity Bar
        float baseUsage = caravan.MassUsage;
        float capacity = caravan.MassCapacity;
        float additionalMass = CalculateSelectedAdditionalMass();
        float projectedTotal = baseUsage + additionalMass;
        float remainingCapacity = capacity - projectedTotal;
        bool isOverEncumbered = projectedTotal > capacity;

        Rect capacityBarRect = new(inRect.x, subHeaderRect.yMax + 6f, inRect.width, 34f);
        Widgets.DrawMenuSection(capacityBarRect);

        Rect capInner = capacityBarRect.ContractedBy(6f);
        string massLabel = $"Caravan Capacity: {projectedTotal:F1} / {capacity:F1} kg  ({baseUsage:F1} kg base + {additionalMass:F1} kg selected)";
        Rect massTextRect = new(capInner.x, capInner.y, 420f, 22f);
        Widgets.Label(massTextRect, massLabel);

        Rect statusRect = new(capInner.xMax - 260f, capInner.y, 260f, 22f);
        Text.Anchor = TextAnchor.MiddleRight;
        if (isOverEncumbered)
        {
            GUI.color = new Color(1.0f, 0.35f, 0.35f);
            Widgets.Label(statusRect, $"⚠ Over-encumbered by {Mathf.Abs(remainingCapacity):F1} kg!");
        }
        else
        {
            GUI.color = new Color(0.4f, 0.95f, 0.5f);
            Widgets.Label(statusRect, $"✓ {remainingCapacity:F1} kg free capacity");
        }
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;

        // 3. Quick Action Selection Bar
        float quickTop = capacityBarRect.yMax + 8f;
        Rect quickBarRect = new(inRect.x, quickTop, inRect.width, 26f);

        if (Widgets.ButtonText(new Rect(quickBarRect.x, quickBarRect.y, 140f, 26f), "Select What Fits"))
        {
            SelectWhatFits(capacity - baseUsage);
            SoundDefOf.Click?.PlayOneShotOnCamera();
        }

        if (Widgets.ButtonText(new Rect(quickBarRect.x + 148f, quickBarRect.y, 100f, 26f), "Select All"))
        {
            SelectAll();
            SoundDefOf.Click?.PlayOneShotOnCamera();
        }

        if (Widgets.ButtonText(new Rect(quickBarRect.x + 256f, quickBarRect.y, 100f, 26f), "Clear All"))
        {
            ClearAll();
            SoundDefOf.ClickReject?.PlayOneShotOnCamera();
        }

        // 4. Scrollable Transfer Rows
        float listTop = quickBarRect.yMax + 8f;
        float bottomBarHeight = 44f;
        Rect outRect = new(inRect.x, listTop, inRect.width, inRect.height - listTop - bottomBarHeight);
        Rect viewRect = new(0f, 0f, outRect.width - 16f, Mathf.Max(lastViewHeight, outRect.height));

        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        float curY = 0f;

        // Section A: Wounded Survivors
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0f, curY, viewRect.width, 28f), $"Wounded Survivors ({survivorEntries.Count})");
        Text.Font = GameFont.Small;
        curY += 30f;
        Widgets.DrawLineHorizontal(0f, curY, viewRect.width);
        curY += 6f;

        if (survivorEntries.Count == 0)
        {
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(10f, curY, viewRect.width, 24f), "No wounded survivors located in the aftermath.");
            GUI.color = Color.white;
            curY += 26f;
        }
        else
        {
            for (int i = 0; i < survivorEntries.Count; i++)
            {
                DrawSurvivorRow(new Rect(0f, curY, viewRect.width, 34f), survivorEntries[i]);
                curY += 36f;
            }
        }

        curY += 10f;

        // Section B: Salvageable Cargo & Provisions
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0f, curY, viewRect.width, 28f), $"Salvageable Cargo ({itemEntries.Count})");
        Text.Font = GameFont.Small;
        curY += 30f;
        Widgets.DrawLineHorizontal(0f, curY, viewRect.width);
        curY += 6f;

        if (itemEntries.Count == 0)
        {
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(10f, curY, viewRect.width, 24f), "No salvageable cargo caches remaining.");
            GUI.color = Color.white;
            curY += 26f;
        }
        else
        {
            for (int i = 0; i < itemEntries.Count; i++)
            {
                DrawItemRow(new Rect(0f, curY, viewRect.width, 32f), itemEntries[i]);
                curY += 34f;
            }
        }

        lastViewHeight = curY + 16f;
        Widgets.EndScrollView();

        // 5. Bottom Action Buttons
        Rect bottomRect = new(inRect.x, inRect.yMax - 36f, inRect.width, 36f);

        Rect confirmBtnRect = new(bottomRect.x, bottomRect.y, 250f, 36f);
        if (Widgets.ButtonText(confirmBtnRect, "Take Selected & Depart"))
        {
            if (isOverEncumbered)
            {
                Dialog_MessageBox warnBox = new(
                    "Warning: Your caravan will be over-encumbered and immobilized on the world map! Are you certain you wish to depart with this load?",
                    "Depart Anyway",
                    () => ExecuteTransferAndClose(),
                    "Stay & Adjust",
                    null,
                    "Caravan Over-Encumbered");
                Find.WindowStack?.Add(warnBox);
            }
            else
            {
                ExecuteTransferAndClose();
            }
        }

        Rect cancelBtnRect = new(bottomRect.xMax - 150f, bottomRect.y, 150f, 36f);
        if (Widgets.ButtonText(cancelBtnRect, "Leave Site"))
        {
            Close();
        }
    }

    private void DrawSurvivorRow(Rect rowRect, SurvivorTransferEntry entry)
    {
        Widgets.DrawMenuSection(rowRect);
        Widgets.DrawHighlightIfMouseover(rowRect);

        Pawn p = entry.Pawn;
        bool isHostile = p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer);

        Rect checkRect = new(rowRect.x + 8f, rowRect.y + 7f, 20f, 20f);
        bool val = entry.TakeWithCaravan;
        Widgets.Checkbox(checkRect.position, ref val);
        entry.TakeWithCaravan = val;

        float textX = checkRect.xMax + 10f;
        string factionStatus = isHostile ? "[Hostile Prisoner]" : "[Neutral Rescue]";
        Color statusColor = isHostile ? new Color(1.0f, 0.45f, 0.35f) : new Color(0.45f, 0.95f, 0.55f);

        Rect nameRect = new(textX, rowRect.y + 7f, 240f, 22f);
        Widgets.Label(nameRect, p.LabelCap);

        Rect statusLabelRect = new(nameRect.xMax + 8f, rowRect.y + 7f, 150f, 22f);
        GUI.color = statusColor;
        Widgets.Label(statusLabelRect, factionStatus);
        GUI.color = Color.white;

        Rect healthRect = new(statusLabelRect.xMax + 8f, rowRect.y + 7f, 120f, 22f);
        GUI.color = new Color(0.85f, 0.85f, 0.85f);
        Widgets.Label(healthRect, "Downed / Wounded");
        GUI.color = Color.white;

        Rect massRect = new(rowRect.xMax - 100f, rowRect.y + 7f, 90f, 22f);
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(massRect, $"{entry.Mass:F1} kg");
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private void DrawItemRow(Rect rowRect, ThingTransferEntry entry)
    {
        Widgets.DrawMenuSection(rowRect);
        Widgets.DrawHighlightIfMouseover(rowRect);

        Rect iconRect = new(rowRect.x + 6f, rowRect.y + 3f, 26f, 26f);
        Widgets.ThingIcon(iconRect, entry.Thing);

        Rect labelRect = new(iconRect.xMax + 8f, rowRect.y + 5f, 230f, 22f);
        Widgets.Label(labelRect, $"{entry.Thing.LabelCap} (avail: {entry.MaxCount})");

        Rect unitMassRect = new(labelRect.xMax + 6f, rowRect.y + 5f, 90f, 22f);
        GUI.color = new Color(0.75f, 0.75f, 0.75f);
        Widgets.Label(unitMassRect, $"{entry.UnitMass:F2} kg ea");
        GUI.color = Color.white;

        // Stepper controls: [ 0 ] [ - ] [ Count ] [ + ] [ All ]
        float btnY = rowRect.y + 4f;
        float rightX = rowRect.xMax - 250f;

        if (Widgets.ButtonText(new Rect(rightX, btnY, 30f, 24f), "0"))
        {
            entry.CountToTake = 0;
            SoundDefOf.ClickReject?.PlayOneShotOnCamera();
        }

        if (Widgets.ButtonText(new Rect(rightX + 34f, btnY, 26f, 24f), "<"))
        {
            if (entry.CountToTake > 0)
            {
                entry.CountToTake = Mathf.Max(0, entry.CountToTake - 1);
                SoundDefOf.Click?.PlayOneShotOnCamera();
            }
        }

        Rect countTextRect = new(rightX + 64f, btnY + 2f, 44f, 20f);
        using (new ProjectVigilance.Core.UI.VigilanceUiWidgets.ImguiStateScope())
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(countTextRect, entry.CountToTake.ToString());
        }

        if (Widgets.ButtonText(new Rect(rightX + 112f, btnY, 26f, 24f), ">"))
        {
            if (entry.CountToTake < entry.MaxCount)
            {
                entry.CountToTake = Mathf.Min(entry.MaxCount, entry.CountToTake + 1);
                SoundDefOf.Click?.PlayOneShotOnCamera();
            }
        }

        float allBtnW = Mathf.Max(34f, Text.CalcSize("All").x + 12f);
        if (Widgets.ButtonText(new Rect(rightX + 142f, btnY, allBtnW, 24f), "All"))
        {
            entry.CountToTake = entry.MaxCount;
            SoundDefOf.Click?.PlayOneShotOnCamera();
        }

        float massX = Mathf.Max(rightX + 182f, rightX + 146f + allBtnW);
        Rect totalItemMassRect = new(massX, btnY + 2f, Mathf.Max(48f, rightX + 242f - massX), 20f);
        using (new ProjectVigilance.Core.UI.VigilanceUiWidgets.ImguiStateScope())
        {
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(totalItemMassRect, $"{(entry.CountToTake * entry.UnitMass):F1} kg");
        }
    }

    private void SelectWhatFits(float allowedMass)
    {
        ClearAll();
        float currentMass = 0f;

        // 1. First take medicine and food if available
        for (int i = 0; i < itemEntries.Count; i++)
        {
            var e = itemEntries[i];
            if (e.Thing.def.IsMedicine || e.Thing.def.IsNutritionGivingIngestible)
            {
                while (e.CountToTake < e.MaxCount && currentMass + e.UnitMass <= allowedMass)
                {
                    e.CountToTake++;
                    currentMass += e.UnitMass;
                }
            }
        }

        // 2. Next take valuable / lightweight components and silver
        for (int i = 0; i < itemEntries.Count; i++)
        {
            var e = itemEntries[i];
            if (e.Thing.def == ThingDefOf.ComponentIndustrial || e.Thing.def == ThingDefOf.Silver)
            {
                while (e.CountToTake < e.MaxCount && currentMass + e.UnitMass <= allowedMass)
                {
                    e.CountToTake++;
                    currentMass += e.UnitMass;
                }
            }
        }

        // 3. Take survivors if capacity allows
        for (int i = 0; i < survivorEntries.Count; i++)
        {
            var s = survivorEntries[i];
            if (currentMass + s.Mass <= allowedMass)
            {
                s.TakeWithCaravan = true;
                currentMass += s.Mass;
            }
        }

        // 4. Fill remaining capacity with remaining items
        for (int i = 0; i < itemEntries.Count; i++)
        {
            var e = itemEntries[i];
            while (e.CountToTake < e.MaxCount && currentMass + e.UnitMass <= allowedMass)
            {
                e.CountToTake++;
                currentMass += e.UnitMass;
            }
        }
    }

    private void SelectAll()
    {
        for (int i = 0; i < itemEntries.Count; i++)
        {
            itemEntries[i].CountToTake = itemEntries[i].MaxCount;
        }
        for (int i = 0; i < survivorEntries.Count; i++)
        {
            survivorEntries[i].TakeWithCaravan = true;
        }
    }

    private void ClearAll()
    {
        for (int i = 0; i < itemEntries.Count; i++)
        {
            itemEntries[i].CountToTake = 0;
        }
        for (int i = 0; i < survivorEntries.Count; i++)
        {
            survivorEntries[i].TakeWithCaravan = false;
        }
    }

    private void ExecuteTransferAndClose()
    {
        int itemsSalvagedCount = 0;
        float itemsMassTaken = 0f;
        int survivorsRescuedCount = 0;

        // 1. Transfer selected items
        for (int i = 0; i < itemEntries.Count; i++)
        {
            var entry = itemEntries[i];
            if (entry.CountToTake > 0 && entry.Thing != null && !entry.Thing.Destroyed)
            {
                int count = Mathf.Clamp(entry.CountToTake, 0, entry.Thing.stackCount);
                if (count == entry.Thing.stackCount)
                {
                    aftermath.SalvageLoot.Remove(entry.Thing);
                    CaravanInventoryUtility.GiveThing(caravan, entry.Thing);
                }
                else
                {
                    Thing split = entry.Thing.SplitOff(count);
                    CaravanInventoryUtility.GiveThing(caravan, split);
                }
                itemsSalvagedCount += count;
                itemsMassTaken += count * entry.UnitMass;
            }
        }

        // 2. Transfer selected survivors
        for (int i = 0; i < survivorEntries.Count; i++)
        {
            var entry = survivorEntries[i];
            if (entry.TakeWithCaravan && entry.Pawn != null && !entry.Pawn.Dead && !entry.Pawn.Destroyed)
            {
                Pawn survivor = entry.Pawn;
                if (survivor.Faction != null && survivor.Faction.HostileTo(Faction.OfPlayer))
                {
                    survivor.guest?.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
                }
                else
                {
                    survivor.guest?.SetGuestStatus(Faction.OfPlayer, GuestStatus.Guest);
                }

                caravan.AddPawn(survivor, addCarriedPawnToWorldPawnsIfAny: false);
                if (Find.WorldPawns != null)
                {
                    Find.WorldPawns.ForcefullyKeptPawns.Remove(survivor);
                }
                aftermath.WoundedSurvivors.Remove(survivor);
                survivorsRescuedCount++;
            }
        }

        // 3. Check if aftermath is depleted
        bool depleted = (aftermath.SalvageLoot == null || aftermath.SalvageLoot.Count == 0) &&
                        (aftermath.WoundedSurvivors == null || aftermath.WoundedSurvivors.Count == 0);

        if (depleted)
        {
            aftermath.Destroy();
        }

        // 4. Audio & Notification feedback
        SoundDefOf.Tick_High?.PlayOneShotOnCamera();
        Messages.Message(
            $"{caravan.LabelCap} completed scavenging: recovered {itemsSalvagedCount} cargo units ({itemsMassTaken:F1} kg) and tended to {survivorsRescuedCount} wounded survivor{(survivorsRescuedCount == 1 ? "" : "s")}.",
            new GlobalTargetInfo(caravan.Tile),
            MessageTypeDefOf.PositiveEvent);

        Close();
    }
}
