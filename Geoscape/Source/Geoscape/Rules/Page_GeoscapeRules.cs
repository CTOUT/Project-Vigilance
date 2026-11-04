using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// First-class campaign creation wizard page inserted between Storyteller selection and World Generation.
/// Allows players to select their planetary intelligence profile (Fog of War, Planetary Surveillance,
/// Hardcore Vigilance, Omniscient) and optionally customise fine-grained simulation rules via a dedicated modal dialog.
/// </summary>
public class Page_GeoscapeRules : Page
{
    private GeoscapeRuleSet? draft;

    public override string PageTitle => "Geoscape Campaign Rules";

    public override Vector2 InitialSize => new(1020f, 764f);

    public override void PreOpen()
    {
        base.PreOpen();
        draft = NewCampaignRuleDraft.GetOrCreate(GeoscapeMod.Settings);
    }

    public override void DoWindowContents(Rect inRect)
    {
        DrawPageTitle(inRect);

        draft ??= NewCampaignRuleDraft.GetOrCreate(GeoscapeMod.Settings);
        CampaignProfile activeProfile = draft.DetectCurrentProfile();

        Rect mainRect = GetMainRect(inRect);

        // Subtitle / Narrative Header
        Rect descRect = new(mainRect.x, mainRect.y, mainRect.width, 36f);
        Text.Font = GameFont.Small;
        GUI.color = new Color(0.85f, 0.85f, 0.85f);
        Widgets.Label(descRect, "Select a planetary intelligence and reconnaissance profile for your campaign. Once world generation begins, simulation rules lock permanently to ensure strategic and narrative integrity.");
        GUI.color = Color.white;

        // 2x2 Preset Profile Selection Cards (Full height, never truncated)
        float cardsTop = descRect.yMax + 8f;
        float cardHeight = 132f;
        float cardWidth = (mainRect.width - 16f) / 2f;

        // Card 1: Fog of War (Recommended)
        Rect card1Rect = new(mainRect.x, cardsTop, cardWidth, cardHeight);
        DrawProfileCard(
            card1Rect,
            CampaignProfile.FogOfWar,
            "Fog of War",
            "Recommended",
            new Color(0.25f, 0.85f, 0.35f),
            "The standard Project Vigilance experience. Unexplored hexes are shrouded in darkness. You must dispatch caravans, build radar arrays, and launch satellites to discover factions, sites, and terrain.",
            activeProfile == CampaignProfile.FogOfWar);

        // Card 2: Planetary Surveillance
        Rect card2Rect = new(mainRect.x + cardWidth + 16f, cardsTop, cardWidth, cardHeight);
        DrawProfileCard(
            card2Rect,
            CampaignProfile.PlanetarySurveillance,
            "Planetary Surveillance",
            "Orbital Cartography",
            new Color(0.35f, 0.7f, 1.0f),
            "Surface geography and biomes are known from historical orbital scans. Settlements, outposts, and incoming hostile incursions remain concealed outside radar and scout envelopes.",
            activeProfile == CampaignProfile.PlanetarySurveillance);

        // Card 3: Hardcore Vigilance
        Rect card3Rect = new(mainRect.x, cardsTop + cardHeight + 10f, cardWidth, cardHeight);
        DrawProfileCard(
            card3Rect,
            CampaignProfile.HardcoreVigilance,
            "Hardcore Vigilance",
            "Challenging",
            new Color(1.0f, 0.45f, 0.25f),
            "Strict physical sensory limits. Tightened scout horizons, terrain masking shields incursions in mountain corridors, and tactical flanking avoids predictable approach routes.",
            activeProfile == CampaignProfile.HardcoreVigilance);

        // Card 4: Omniscient (Vanilla)
        Rect card4Rect = new(mainRect.x + cardWidth + 16f, cardsTop + cardHeight + 10f, cardWidth, cardHeight);
        DrawProfileCard(
            card4Rect,
            CampaignProfile.Omniscient,
            "Omniscient",
            "Vanilla Visibility",
            new Color(0.75f, 0.75f, 0.75f),
            "Standard vanilla RimWorld global visibility. All settlements, terrain, and landmarks are fully revealed from day one. Enjoy early-warning radars and physical satellites without planetary fog of war.",
            activeProfile == CampaignProfile.Omniscient);

        // Customise Individual Rules Bar (Anomaly-style modal launcher)
        float actionTop = cardsTop + (cardHeight * 2f) + 16f;
        Rect actionRect = new(mainRect.x, actionTop, mainRect.width, 32f);

        if (Widgets.ButtonText(new Rect(actionRect.x, actionRect.y, 290f, 32f), "Customise Individual Rules..."))
        {
            Find.WindowStack?.Add(new Dialog_GeoscapeCampaignRules(draft));
            SoundDefOf.Click?.PlayOneShotOnCamera();
        }

        // Active Profile Status Label
        Rect statusRect = new(actionRect.x + 305f, actionRect.y + 4f, actionRect.width - 305f, 24f);
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = activeProfile == CampaignProfile.Custom ? new Color(1.0f, 0.85f, 0.35f) : new Color(0.7f, 0.9f, 1.0f);
        Widgets.Label(statusRect, $"Active Profile: {activeProfile.GetDisplayName()}");
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;

        // Comprehensive Rule Summary Card
        float summaryTop = actionRect.yMax + 12f;
        float summaryHeight = mainRect.yMax - summaryTop;
        Rect summaryRect = new(mainRect.x, summaryTop, mainRect.width, summaryHeight);
        Widgets.DrawMenuSection(summaryRect);
        Rect innerSummary = summaryRect.ContractedBy(14f);

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(innerSummary.x, innerSummary.y, innerSummary.width, 28f), $"{activeProfile.GetCleanName()} Overview");
        Text.Font = GameFont.Small;

        float lineY = innerSummary.y + 32f;
        DrawSummaryLine(innerSummary.x, ref lineY, innerSummary.width, "Planetary Visibility", draft.Visibility.CurrentValueLabel);
        DrawSummaryLine(innerSummary.x, ref lineY, innerSummary.width, "Lookout Scouting Horizon", $"{draft.ColonyScoutRadius.CurrentValueLabel} (Colony) / {draft.CaravanScoutRadius.CurrentValueLabel} (Caravan)");
        DrawSummaryLine(innerSummary.x, ref lineY, innerSummary.width, "Nighttime Vigilance", draft.NighttimeSensoryReduction.Value ? "Active (Watchmen vigilance halved at night; sleeping posts drop to perimeter)" : "Disabled (Constant 24-hour visual scouting)");
        DrawSummaryLine(innerSummary.x, ref lineY, innerSummary.width, "Early Warning Radars", draft.EarlyWarningLetters.Value ? "Active Radar Telemetry & Audio Dispatch" : "Passive Only");
        DrawSummaryLine(innerSummary.x, ref lineY, innerSummary.width, "Hostile Ingress Doctrine", draft.TacticalIngressFlanking.Value ? "Active (Raids flank off-road to evade fortified choke points)" : "Direct (Raids take direct approach corridors)");
        DrawSummaryLine(innerSummary.x, ref lineY, innerSummary.width, "Incursion Storyteller Rules", $"{(draft.IncursionsRestAtNight.Value ? "Camp at Night" : "Continuous Movement")}, {(draft.SynchronizeMultiVectorRaids.Value ? "Multi-Vector Synchronised" : "Independent Waves")}");
        DrawSummaryLine(innerSummary.x, ref lineY, innerSummary.width, "Rule Mutability", "Locked permanently upon world generation (Visibility may be escalated)");

        // Standard Bottom Navigation Buttons (Back, Reset to Defaults, Next)
        DoBottomButtons(
            inRect,
            "Next".Translate(),
            "Reset to Defaults",
            () =>
            {
                draft.CopyFromDefaults(GeoscapeMod.Settings);
                SoundDefOf.ClickReject?.PlayOneShotOnCamera();
                Messages.Message("Draft reset to your default campaign rules.", MessageTypeDefOf.NeutralEvent, false);
            });
    }

    private void DrawProfileCard(
        Rect rect,
        CampaignProfile profile,
        string title,
        string badge,
        Color badgeColor,
        string description,
        bool isSelected)
    {
        Widgets.DrawMenuSection(rect);

        if (isSelected)
        {
            Widgets.DrawHighlightSelected(rect);
            GUI.color = new Color(0.2f, 0.8f, 0.4f, 0.9f);
            Widgets.DrawBox(rect, 2);
            GUI.color = Color.white;
        }

        Rect inner = rect.ContractedBy(10f);

        // Title and Badge
        Text.Font = GameFont.Medium;
        float titleWidth = Text.CalcSize(title).x;
        Rect titleRect = new(inner.x, inner.y, titleWidth, 26f);
        Widgets.Label(titleRect, title);

        if (!badge.NullOrEmpty())
        {
            Text.Font = GameFont.Tiny;
            GUI.color = badgeColor;
            Rect badgeRect = new(titleRect.xMax + 10f, inner.y + 5f, inner.width - titleWidth - 10f, 20f);
            Widgets.Label(badgeRect, $"[{badge}]");
            GUI.color = Color.white;
        }

        // Description
        Text.Font = GameFont.Small;
        GUI.color = new Color(0.85f, 0.85f, 0.85f);
        Rect descRect = new(inner.x, inner.y + 28f, inner.width, inner.height - 28f);
        Widgets.Label(descRect, description);
        GUI.color = Color.white;

        // Interaction
        if (Widgets.ButtonInvisible(rect))
        {
            draft?.ApplyProfile(profile);
            SoundDefOf.Click?.PlayOneShotOnCamera();
        }

        Widgets.DrawHighlightIfMouseover(rect);
    }

    private static void DrawSummaryLine(float x, ref float y, float width, string label, string value)
    {
        Rect labelRect = new(x, y, 220f, 22f);
        Rect valueRect = new(x + 230f, y, width - 230f, 22f);

        GUI.color = new Color(0.75f, 0.75f, 0.75f);
        Widgets.Label(labelRect, $"{label}:");
        GUI.color = Color.white;
        Widgets.Label(valueRect, value);

        y += 24f;
    }
}
