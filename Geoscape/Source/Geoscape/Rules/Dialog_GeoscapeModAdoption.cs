using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Modal dialog presented when an existing save that has never run Project Vigilance is loaded for the first time.
/// Allows players to establish initial world interpretation without causing unintended planetary amnesia.
/// </summary>
public sealed class Dialog_GeoscapeModAdoption : Window
{
    private readonly GeoscapeCampaignRules campaign;
    private bool decisionMade;

    public override Vector2 InitialSize => new(680f, 490f);

    public Dialog_GeoscapeModAdoption(GeoscapeCampaignRules campaign)
    {
        this.campaign = campaign;
        forcePause = true;
        doCloseX = false;
        doCloseButton = false;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = true;
    }

    public override void PreClose()
    {
        base.PreClose();
        if (!decisionMade && !campaign.HasRecordedAdoption)
        {
            campaign.ApplyAdoptionProfile(CampaignProfile.FogOfWar, "Dismissal Fallback");
            decisionMade = true;
            SoundDefOf.Click?.PlayOneShotOnCamera();
            Messages.Message("Project Vigilance: Adopted Fog of War (Recommended) baseline. Rules can be adjusted during the 1-day review window in Mod Settings.", MessageTypeDefOf.NeutralEvent, false);
        }
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "Project Vigilance — Strategic Activation");
        Text.Font = GameFont.Small;

        Rect introRect = new(inRect.x, inRect.y + 34f, inRect.width, 38f);
        Widgets.Label(introRect, "This world was established before Project Vigilance was installed.\nSelect an initial campaign ruleset to govern planetary visibility and threat detection:");

        float curY = introRect.yMax + 8f;
        float cardWidth = inRect.width;

        // Option 1: Fog of War (Recommended Default)
        float card1Height = 106f;
        Rect card1Rect = new(inRect.x, curY, cardWidth, card1Height);
        Widgets.DrawMenuSection(card1Rect);
        Widgets.DrawHighlightIfMouseover(card1Rect);
        Rect card1Inner = card1Rect.ContractedBy(8f);

        float btnWidth = 190f;
        float textWidth = card1Inner.width - btnWidth - 12f;

        Text.Font = GameFont.Medium;
        Rect card1TitleRect = new(card1Inner.x, card1Inner.y, textWidth, 24f);
        Widgets.Label(card1TitleRect, "Fog of War");
        Text.Font = GameFont.Small;

        Rect card1DescRect = new(card1Inner.x, card1TitleRect.yMax + 2f, textWidth, 38f);
        GUI.color = new Color(0.85f, 0.85f, 0.85f);
        Widgets.Label(card1DescRect, "The authentic Project Vigilance experience. Unmonitored sectors are shrouded; the globe is charted dynamically through caravans, lookouts, and radar.");
        GUI.color = Color.white;

        Rect card1NoticeRect = new(card1Inner.x, card1DescRect.yMax + 2f, textWidth, 32f);
        GUI.color = new Color(1f, 0.78f, 0.35f);
        Text.Font = GameFont.Tiny;
        Widgets.Label(card1NoticeRect, "Notice: Unmonitored charted territory outside colony sensor range will be shrouded.");
        Text.Font = GameFont.Small;
        GUI.color = Color.white;

        Rect card1BtnRect = new(card1Inner.xMax - btnWidth, card1Inner.y + (card1Inner.height - 32f) * 0.5f, btnWidth, 32f);
        if (Widgets.ButtonText(card1BtnRect, "Adopt Fog of War"))
        {
            decisionMade = true;
            campaign.ApplyAdoptionProfile(CampaignProfile.FogOfWar, "User Selection");
            SoundDefOf.Click?.PlayOneShotOnCamera();
            Messages.Message("Project Vigilance: Adopted Fog of War rules. A 1-day review window is open in Mod Settings.", MessageTypeDefOf.PositiveEvent, false);
            Close();
        }
        curY += card1Height + 8f;

        // Option 2: Planetary Surveillance
        float card2Height = 84f;
        Rect card2Rect = new(inRect.x, curY, cardWidth, card2Height);
        Widgets.DrawMenuSection(card2Rect);
        Widgets.DrawHighlightIfMouseover(card2Rect);
        Rect card2Inner = card2Rect.ContractedBy(8f);

        Text.Font = GameFont.Medium;
        Rect card2TitleRect = new(card2Inner.x, card2Inner.y, textWidth, 24f);
        Widgets.Label(card2TitleRect, "Planetary Surveillance");
        Text.Font = GameFont.Small;

        Rect card2DescRect = new(card2Inner.x, card2TitleRect.yMax + 2f, textWidth, 48f);
        GUI.color = new Color(0.85f, 0.85f, 0.85f);
        Widgets.Label(card2DescRect, "World geography, continents, and static settlements remain fully charted. Hostile incursions, mechanoid sweeps, and dynamic camps are cloaked outside radar range.");
        GUI.color = Color.white;

        Rect card2BtnRect = new(card2Inner.xMax - btnWidth, card2Inner.y + (card2Inner.height - 32f) * 0.5f, btnWidth, 32f);
        if (Widgets.ButtonText(card2BtnRect, "Adopt Surveillance"))
        {
            decisionMade = true;
            campaign.ApplyAdoptionProfile(CampaignProfile.PlanetarySurveillance, "User Selection");
            SoundDefOf.Click?.PlayOneShotOnCamera();
            Messages.Message("Project Vigilance: Adopted Planetary Surveillance rules. A 1-day review window is open in Mod Settings.", MessageTypeDefOf.NeutralEvent, false);
            Close();
        }
        curY += card2Height + 8f;

        // Option 3: Hardcore Vigilance
        float card3Height = 76f;
        Rect card3Rect = new(inRect.x, curY, cardWidth, card3Height);
        Widgets.DrawMenuSection(card3Rect);
        Widgets.DrawHighlightIfMouseover(card3Rect);
        Rect card3Inner = card3Rect.ContractedBy(8f);

        Text.Font = GameFont.Medium;
        Rect card3TitleRect = new(card3Inner.x, card3Inner.y, textWidth, 24f);
        Widgets.Label(card3TitleRect, "Hardcore Vigilance");
        Text.Font = GameFont.Small;

        Rect card3DescRect = new(card3Inner.x, card3TitleRect.yMax + 2f, textWidth, 40f);
        GUI.color = new Color(0.85f, 0.85f, 0.85f);
        Widgets.Label(card3DescRect, "Strict sensory limits: planetary shroud, tightened lookout horizons, terrain masking in mountain valleys, and night-watch vigilance penalties.");
        GUI.color = Color.white;

        Rect card3BtnRect = new(card3Inner.xMax - btnWidth, card3Inner.y + (card3Inner.height - 32f) * 0.5f, btnWidth, 32f);
        if (Widgets.ButtonText(card3BtnRect, "Adopt Hardcore"))
        {
            decisionMade = true;
            campaign.ApplyAdoptionProfile(CampaignProfile.HardcoreVigilance, "User Selection");
            SoundDefOf.Click?.PlayOneShotOnCamera();
            Messages.Message("Project Vigilance: Adopted Hardcore Vigilance rules. A 1-day review window is open in Mod Settings.", MessageTypeDefOf.NeutralEvent, false);
            Close();
        }
        curY += card3Height + 10f;

        // Bottom Action Row: Customise button & Review window note
        Rect bottomRow = new(inRect.x, curY, inRect.width, 30f);
        if (Widgets.ButtonText(new Rect(bottomRow.x, bottomRow.y, 260f, 30f), "Customise Individual Rules..."))
        {
            decisionMade = true;
            campaign.ApplyAdoptionCustom("User Custom");
            SoundDefOf.Click?.PlayOneShotOnCamera();
            Close();
            Find.WindowStack?.Add(new Dialog_GeoscapeCampaignRules(campaign.Rules, campaign));
        }

        Rect footerRect = new(inRect.x, bottomRow.yMax + 4f, inRect.width, 22f);
        GUI.color = new Color(0.7f, 0.85f, 0.7f);
        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Tiny;
        Widgets.Label(footerRect, "A 1-day review grace period (60,000 ticks) remains open to adjust rules in Mod Settings.");
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
    }
}
