namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Transient draft rule set configured on the world-generation setup page.
/// Seeded when the page opens; consumed once upon world creation; never persisted to disk.
/// </summary>
public static class NewCampaignRuleDraft
{
    public static GeoscapeRuleSet? Current { get; private set; }

    public static void BeginFromDefaults(GeoscapeSettings defaults)
    {
        Current = new GeoscapeRuleSet();
        Current.CopyFromDefaults(defaults);
    }

    public static GeoscapeRuleSet GetOrCreate(GeoscapeSettings defaults)
    {
        if (Current == null)
        {
            BeginFromDefaults(defaults);
        }
        return Current!;
    }

    public static void Clear()
    {
        Current = null;
    }
}
