using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Soft compatibility bridge for external community world-fog mods (such as D4egon's World Fog of War,
/// Rimworld Exploration Mode, or VEF-compatible fog frameworks) without hard assembly dependencies.
/// Uses high-performance cached reflection delegates with zero allocation on query hot paths.
/// </summary>
public static class ExternalFogCompatibility
{
    private static bool initialized;
    private static bool isExternalFogModActive;
    private static string? detectedModName;

    // Cached delegates / accessors for external revealed checks
    private static Func<int, bool>? isTileRevealedFunc;
    private static Action<int>? revealTileAction;

    // Direct collection references if exposed as fields/properties
    private static HashSet<int>? revealedHashSet;
    private static bool[]? revealedBoolArray;
    private static BitArray? revealedBitArray;

    /// <summary>
    /// Indicates whether an external world fog mod is detected and active in the running environment.
    /// </summary>
    public static bool IsExternalFogModActive
    {
        get
        {
            if (!initialized)
            {
                Initialize();
            }
            return isExternalFogModActive;
        }
    }

    /// <summary>
    /// The name of the detected external fog mod, or null if none is present.
    /// </summary>
    public static string? ActiveModName => detectedModName;

    /// <summary>
    /// Checks whether the specified tile is marked as revealed/explored by an external fog mod.
    /// Hot path: zero allocation.
    /// </summary>
    public static bool IsTileRevealedByExternalMod(int tileId)
    {
        if (!IsExternalFogModActive || tileId < 0)
        {
            return false;
        }

        try
        {
            if (revealedHashSet != null)
            {
                return revealedHashSet.Contains(tileId);
            }

            if (revealedBoolArray != null)
            {
                return tileId < revealedBoolArray.Length && revealedBoolArray[tileId];
            }

            if (revealedBitArray != null)
            {
                return tileId < revealedBitArray.Length && revealedBitArray.Get(tileId);
            }

            if (isTileRevealedFunc != null)
            {
                return isTileRevealedFunc(tileId);
            }
        }
        catch (Exception ex)
        {
            Log.WarningOnce($"[Project Vigilance] Error querying external fog mod '{detectedModName}': {ex.Message}", 73629104);
        }

        return false;
    }

    /// <summary>
    /// Notifies the external fog mod that a tile has been revealed by Project Vigilance sensors/radar.
    /// Hot path: zero allocation.
    /// </summary>
    public static void NotifyExternalModTileRevealed(int tileId)
    {
        if (!IsExternalFogModActive || tileId < 0)
        {
            return;
        }

        try
        {
            revealTileAction?.Invoke(tileId);

            if (revealedHashSet != null)
            {
                revealedHashSet.Add(tileId);
            }
            else if (revealedBoolArray != null && tileId < revealedBoolArray.Length)
            {
                revealedBoolArray[tileId] = true;
            }
            else if (revealedBitArray != null && tileId < revealedBitArray.Length)
            {
                revealedBitArray.Set(tileId, true);
            }
        }
        catch (Exception ex)
        {
            Log.WarningOnce($"[Project Vigilance] Error updating external fog mod '{detectedModName}': {ex.Message}", 73629105);
        }
    }

    /// <summary>
    /// Softly discovers external fog mods across loaded assemblies and active WorldComponents.
    /// </summary>
    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        isExternalFogModActive = false;
        detectedModName = null;
        isTileRevealedFunc = null;
        revealTileAction = null;
        revealedHashSet = null;
        revealedBoolArray = null;
        revealedBitArray = null;

        try
        {
            // 1. Scan loaded WorldComponents on Find.World for fog/exploration managers
            if (Find.World?.components != null)
            {
                for (int i = 0; i < Find.World.components.Count; i++)
                {
                    WorldComponent comp = Find.World.components[i];
                    if (comp == null || comp is SensoryManager)
                    {
                        continue;
                    }

                    Type compType = comp.GetType();
                    string typeName = compType.Name;
                    string ns = compType.Namespace ?? string.Empty;

                    if (typeName.IndexOf("Fog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        typeName.IndexOf("Exploration", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        ns.IndexOf("WorldFog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        ns.IndexOf("ExplorationMode", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (TryBindWorldComponent(comp))
                        {
                            isExternalFogModActive = true;
                            detectedModName = compType.Assembly.GetName().Name ?? typeName;
                            Log.Message($"[Project Vigilance] External world fog component detected ({compType.FullName} in {detectedModName}). Soft compatibility engaged.");
                            return;
                        }
                    }
                }
            }

            // 2. Scan loaded assemblies for external fog static utility classes
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly asm = assemblies[i];
                string asmName = asm.GetName().Name ?? string.Empty;

                if (asmName.IndexOf("WorldFog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    asmName.IndexOf("World_Fog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    asmName.IndexOf("ExplorationMode", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    asmName.IndexOf("D4egon", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (TryBindAssembly(asm))
                    {
                        isExternalFogModActive = true;
                        detectedModName = asmName;
                        Log.Message($"[Project Vigilance] External world fog assembly detected ({asmName}). Soft compatibility engaged.");
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[Project Vigilance] Failed to initialize external fog soft compatibility: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets cached reflection state to allow rescanning (e.g. upon world load or mod list changes).
    /// </summary>
    public static void ResetAndRescan()
    {
        initialized = false;
        Initialize();
    }

    private static bool TryBindWorldComponent(WorldComponent comp)
    {
        Type type = comp.GetType();
        bool boundAny = false;

        // Query methods
        string[] queryNames = ["IsRevealed", "IsTileRevealed", "IsExplored", "IsTileExplored", "IsVisible", "IsTileVisible"];
        for (int i = 0; i < queryNames.Length; i++)
        {
            MethodInfo? method = type.GetMethod(queryNames[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(int)], null)
                ?? type.GetMethod(queryNames[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(PlanetTile)], null);

            if (method != null && method.ReturnType == typeof(bool))
            {
                if (method.GetParameters()[0].ParameterType == typeof(int))
                {
                    isTileRevealedFunc = (Func<int, bool>)Delegate.CreateDelegate(typeof(Func<int, bool>), comp, method);
                }
                else
                {
                    isTileRevealedFunc = tileId => (bool)method.Invoke(comp, [new PlanetTile(tileId, Find.WorldGrid.Surface)])!;
                }
                boundAny = true;
                break;
            }
        }

        // Reveal methods
        string[] revealNames = ["Reveal", "RevealTile", "SetRevealed", "Discover", "DiscoverTile", "Unfog", "UnfogTile"];
        for (int i = 0; i < revealNames.Length; i++)
        {
            MethodInfo? method = type.GetMethod(revealNames[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(int)], null);
            if (method != null)
            {
                revealTileAction = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), comp, method);
                boundAny = true;
                break;
            }
        }

        // Collections (HashSet<int>, bool[], BitArray)
        string[] collectionNames = ["revealedTiles", "revealedTileIds", "exploredTiles", "exploredTileIds", "revealed", "explored", "isRevealed"];
        for (int i = 0; i < collectionNames.Length; i++)
        {
            FieldInfo? field = type.GetField(collectionNames[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                object? val = field.GetValue(comp);
                if (val is HashSet<int> set)
                {
                    revealedHashSet = set;
                    boundAny = true;
                    break;
                }
                if (val is bool[] arr)
                {
                    revealedBoolArray = arr;
                    boundAny = true;
                    break;
                }
                if (val is BitArray bitArr)
                {
                    revealedBitArray = bitArr;
                    boundAny = true;
                    break;
                }
            }

            PropertyInfo? prop = type.GetProperty(collectionNames[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
            {
                object? val = prop.GetValue(comp);
                if (val is HashSet<int> set)
                {
                    revealedHashSet = set;
                    boundAny = true;
                    break;
                }
                if (val is bool[] arr)
                {
                    revealedBoolArray = arr;
                    boundAny = true;
                    break;
                }
                if (val is BitArray bitArr)
                {
                    revealedBitArray = bitArr;
                    boundAny = true;
                    break;
                }
            }
        }

        return boundAny;
    }

    private static bool TryBindAssembly(Assembly asm)
    {
        Type[] types;
        try
        {
            types = asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray()!;
        }

        bool boundAny = false;
        for (int i = 0; i < types.Length; i++)
        {
            Type t = types[i];
            if (t == null)
            {
                continue;
            }

            // Look for static utility methods e.g. WorldFogUtility.IsRevealed(int tileId)
            string[] queryNames = ["IsRevealed", "IsTileRevealed", "IsExplored", "IsTileExplored"];
            for (int q = 0; q < queryNames.Length; q++)
            {
                MethodInfo? method = t.GetMethod(queryNames[q], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, [typeof(int)], null);
                if (method != null && method.ReturnType == typeof(bool))
                {
                    isTileRevealedFunc = (Func<int, bool>)Delegate.CreateDelegate(typeof(Func<int, bool>), method);
                    boundAny = true;
                    break;
                }
            }

            string[] revealNames = ["Reveal", "RevealTile", "SetRevealed", "Discover", "DiscoverTile"];
            for (int r = 0; r < revealNames.Length; r++)
            {
                MethodInfo? method = t.GetMethod(revealNames[r], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, [typeof(int)], null);
                if (method != null)
                {
                    revealTileAction = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), method);
                    boundAny = true;
                    break;
                }
            }

            if (boundAny)
            {
                break;
            }
        }

        return boundAny;
    }
}
