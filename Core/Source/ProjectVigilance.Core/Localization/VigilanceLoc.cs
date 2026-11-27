using Verse;

namespace ProjectVigilance.Core.Localization;

/// <summary>
/// Safe keyed localisation lookup helpers with deterministic English fallback when XML keys are unavailable.
/// </summary>
public static class VigilanceLoc
{
    public static string Tr(string key, string fallback)
    {
        return !string.IsNullOrEmpty(key) && key.CanTranslate()
            ? key.Translate().ToString()
            : fallback;
    }

    public static string Tr(string key, string fallback, params NamedArgument[] args)
    {
        return !string.IsNullOrEmpty(key) && key.CanTranslate()
            ? key.Translate(args).ToString()
            : fallback;
    }
}
