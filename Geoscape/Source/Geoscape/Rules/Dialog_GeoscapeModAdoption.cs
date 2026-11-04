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

    public override Vector2 InitialSize => new(660f, 620f);

    public Dialog_GeoscapeModAdoption(GeoscapeCampaignRules campaign)
    {
        this.campaign = campaign;
        forcePause = true;
        doCloseX = true;
        doCloseButton = false;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = true;
    }

    public override void PreClose()
    {
        base.PreClose();
        if (!decisionMade && !campaign.HasRecordedAdoption)
        {
            campaign.ApplyAdoptionProfile(CampaignProfile.PlanetarySurveillance, "Dismissal Fallback");
            decisionMade = true;
            SoundDefOf.Click?.PlayOneShotOnCamera();
            Messages.Message("Project Vigilance: Adopted Planetary Surveillance baseline. Rules can be adjusted during the 1-day review window in Mod Settings.", MessageTypeDefOf.NeutralEvent, false);
        }
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "Project Vigilance — Strategic Activation");
        Text.Font = GameFont.Small;

        Rect introRect = new(inRect.x, inRect.y + 36f, inRect.width, 68f);
        Widgets.Label(introRect, "This world was established before Project Vigilance was installed.\n\nThe Geoscape reconnaissance grid requires an initial campaign ruleset to govern visibility and threat detection.\n\nHow would you like to interpret this colony's accumulated world knowledge?");

        float curY = introRect.yMax + 8f;
        float cardWidth = inRect.width;

        // Option 1: Planetary Surveillance (Recommended for Existing Colonies)
        float card1Height = 84f;
        Rect card1Rect = new(inRect.x, curY, cardWidth, card1Height);
        Widgets.DrawMenuSection(card1Rect);
        Rect card1Inner = card1Rect.ContractedBy(8f);

        Rect card1BtnRect = new(card1Inner.x, card1Inner.y, 240f, 30f);
        if (Widgets.ButtonText(card1BtnRect, "Planetary Surveillance (Rec.)"))
        {
            decisionMade = true;
            campaign.ApplyAdoptionProfile(CampaignProfile.PlanetarySurveillance, "User Selection");
            SoundDefOf.Click?.PlayOneShotOnCamera();
            Messages.Message("Project Vigilance: Adopted Planetary Surveillance rules. A 1-day review window is open in Mod Settings.", MessageTypeDefOf.PositiveEvent, false);
            Close();
        }

        Rect card1DescRect = new(card1Inner.x, card1BtnRect.yMax + 4f, card1Inner.width, 36f);
        GUI.color = new Color(0.85f, 0.85f, 0.85f);
        Widgets.Label(card1DescRect, "World geography, continents, and static settlements remain charted. Hostile incursions, mechanoid sweeps, and dynamic camps are cloaked outside radar range.");
        GUI.color = Color.white;
        curY += card1Height + 10f;

        // Option 2: Classic Haze of War ("The Great Blackout" Challenge)
        float card2Height = 138f;
        Rect card2Rect = new(inRect.x, curY, cardWidth, card2Height);
        Widgets.DrawMenuSection(card2Rect);
        Rect card2Inner = card2Rect.ContractedBy(8f);

        Rect card2BtnRect = new(card2Inner.x, card2Inner.y, 240f, 30f);
        if (Widgets.ButtonText(card2BtnRect, "Classic Haze of War"))
        {
            decisionMade = true;
            campaign.ApplyAdoptionProfile(CampaignProfile.ClassicHaze, "User Selection");
            SoundDefOf.Click?.PlayOneShotOnCamera();
            Messages.Message("Project Vigilance: Adopted Classic Haze of War ('The Great Blackout'). A 1-day review window is open in Mod Settings.", MessageTypeDefOf.NeutralEvent, false);
            Close();
        }

        Rect card2DescRect = new(card2Inner.x, card2BtnRect.yMax + 4f, card2Inner.width, 36f);
        GUI.color = new Color(0.85f, 0.85f, 0.85f);
        Widgets.Label(card2DescRect, "All unmonitored sectors are plunged into fog of war. The planet must be re-charted from scratch by caravans and lookouts.");
        GUI.color = Color.white;

        Rect card2WarnRect = new(card2Inner.x, card2DescRect.yMax + 4f, card2Inner.width, 48f);
        GUI.color = new Color(1f, 0.65f, 0.3f);
        Widgets.Label(card2WarnRect, "Warning:\nExisting geographic knowledge will be lost. Recommended only for players intentionally starting a re-discovery challenge.");
        GUI.color = Color.white;
        curY += card2Height + 10f;

        // Option 3: Hardcore Vigilance
        float card3Height = 74f;
        Rect card3Rect = new(inRect.x, curY, cardWidth, card3Height);
        Widgets.DrawMenuSection(card3Rect);
        Rect card3Inner = card3Rect.ContractedBy(8f);

        Rect card3BtnRect = new(card3Inner.x, card3Inner.y, 240f, 30f);
        if (Widgets.ButtonText(card3BtnRect, "Hardcore Vigilance"))
        {
            decisionMade = true;
            campaign.ApplyAdoptionProfile(CampaignProfile.HardcoreVigilance, "User Selection");
            SoundDefOf.Click?.PlayOneShotOnCamera();
            Messages.Message("Project Vigilance: Adopted Hardcore Vigilance rules. A 1-day review window is open in Mod Settings.", MessageTypeDefOf.NeutralEvent, false);
            Close();
        }

        Rect card3DescRect = new(card3Inner.x, card3BtnRect.yMax + 4f, card3Inner.width, 32f);
        GUI.color = new Color(0.85f, 0.85f, 0.85f);
        Widgets.Label(card3DescRect, "Planetary shroud, reduced scout horizons, and night-watch sensory penalties.");
        GUI.color = Color.white;

        // Bottom Action Row: Customise button & Review window note
        Rect bottomRow = new(inRect.x, inRect.yMax - 64f, inRect.width, 30f);
        if (Widgets.ButtonText(new Rect(bottomRow.x, bottomRow.y, 240f, 30f), "Customise Individual Rules..."))
        {
            decisionMade = true;
            campaign.ApplyAdoptionCustom("User Custom");
            SoundDefOf.Click?.PlayOneShotOnCamera();
            Close();
            Find.WindowStack?.Add(new Dialog_GeoscapeCampaignRules(campaign.Rules, campaign));
        }

        Rect footerRect = new(inRect.x, bottomRow.yMax + 6f, inRect.width, 24f);
        GUI.color = new Color(0.7f, 0.85f, 0.7f);
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(footerRect, "A 1-day review grace period (60,000 ticks) will remain open to adjust rules in Mod Settings.");
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
    }
}
