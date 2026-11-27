using System;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Core.UI;

/// <summary>
/// Shared WCAG AA accessible colour tokens, IMGUI state guards, and widget helpers for Project Vigilance UI.
/// </summary>
public static class VigilanceUiWidgets
{
    /// <summary>
    /// WCAG AA contrast-safe disabled foreground tint on dark RimWorld panels (#21252B).
    /// </summary>
    public static readonly Color DisabledTextColor = new(1f, 1f, 1f, 0.55f);

    /// <summary>
    /// Accessible active module status green (>= 4.5:1 against dark backdrops).
    /// </summary>
    public static readonly Color StatusActiveColor = new(0.35f, 0.95f, 0.45f);

    /// <summary>
    /// Accessible warning / embedded module status amber (>= 4.5:1 against dark backdrops).
    /// </summary>
    public static readonly Color StatusWarningColor = new(1f, 0.72f, 0.25f);

    /// <summary>
    /// Accessible inactive module status slate (>= 4.5:1 against dark backdrops).
    /// </summary>
    public static readonly Color StatusInactiveColor = new(0.65f, 0.70f, 0.75f);

    /// <summary>
    /// Accessible alert red foreground for dark warning banners (>= 4.5:1 against dark backdrops).
    /// </summary>
    public static readonly Color AlertTextColor = new(1f, 0.55f, 0.55f);

    /// <summary>
    /// Draws a medium section header with an optional accent colour and underline gap.
    /// </summary>
    public static void DrawSectionHeader(Listing_Standard listing, string text, Color? color = null)
    {
        using (new ImguiStateScope())
        {
            Text.Font = GameFont.Medium;
            if (color.HasValue)
            {
                GUI.color = color.Value;
            }
            listing.Label(text);
        }
        listing.GapLine();
    }

    /// <summary>
    /// Resolves standard status badge text and WCAG AA colour for a module row.
    /// </summary>
    public static (string label, Color color) GetModuleStatusBadge(
        bool isActive,
        bool isEmbeddedOrWarning,
        string activeLabel,
        string embeddedLabel,
        string missingLabel)
    {
        if (isActive)
        {
            return (activeLabel, StatusActiveColor);
        }

        return isEmbeddedOrWarning
            ? (embeddedLabel, StatusWarningColor)
            : (missingLabel, StatusInactiveColor);
    }

    /// <summary>
    /// Zero-allocation RAII guard preserving IMGUI colour, font, anchor, and word-wrap state across draws.
    /// </summary>
    public readonly ref struct ImguiStateScope
    {
        private readonly Color prevColor;
        private readonly GameFont prevFont;
        private readonly TextAnchor prevAnchor;
        private readonly bool prevWordWrap;

        public ImguiStateScope()
        {
            prevColor = GUI.color;
            prevFont = Text.Font;
            prevAnchor = Text.Anchor;
            prevWordWrap = Text.WordWrap;
        }

        public void Dispose()
        {
            GUI.color = prevColor;
            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
            Text.WordWrap = prevWordWrap;
        }
    }
}
