using Multiplayer.Compat;
using Verse;

namespace MultiplayerRailsAndRoadsOfTheRimContinuedPatch.Source.Mods;

/// <summary>
///     Multiplayer Patch for Rails and Roads of the Rim (Continued) by Mlie, Last Update: 14 Sep @ 9:28pm 2026
///     https://steamcommunity.com/sharedfiles/filedetails/?id=3271115410
/// </summary>
[MpCompatFor("Mlie.RailsAndRoadsOfTheRim")]
public class RailsAndRoadsOfTheRimContinued
{
    internal const string LogPrefix = "[Multiplayer Rails And Roads of the Rim Continued Patch]";

    public RailsAndRoadsOfTheRimContinued(ModContentPack content)
    {
        LongEventHandler.ExecuteWhenFinished(LatePatch);
    }

    private static void LatePatch()
    {
        Log.Message($"{LogPrefix} Initializing...");

        try
        {
            ROTRGizmoSync.Register();
            ROTRConstructionMenuSync.Patch();
            ROTRLegTargetingSync.Setup();
            ROTRSiteLifecycleSync.Setup();
            ROTRFactionHelpSync.Setup();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} LatePatch failed: {exception}");
        }

        Log.Message($"{LogPrefix} Initialized.");
    }
}