using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Compat;
using RailsAndRoadsOfTheRim;
using RimWorld;
using Verse;

namespace MultiplayerRailsAndRoadsOfTheRimContinuedPatch.Source.Mods;

/// <summary>Ally faction construction help (uses Rand, lives on WorldComponent).</summary>
internal static class ROTRFactionHelpSync
{
    internal static void Setup()
    {
        try
        {
            MP.RegisterSyncMethod(typeof(ROTRFactionHelpSync), nameof(SyncedStartHelping));
        }
        catch (Exception exception)
        {
            Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} Failed to register SyncedStartHelping: {exception}");
        }

        // StartHelping uses Rand and lives on WorldComponent (no implicit sync worker for the instance).
        // Redirect interface calls to static SyncedStartHelping with explicit syncable args.
        try
        {
            var startHelping = AccessTools.DeclaredMethod(typeof(WorldComponent_FactionRoadConstructionHelp),
                nameof(WorldComponent_FactionRoadConstructionHelp.StartHelping));
            if (startHelping != null)
                MpCompat.harmony.Patch(startHelping,
                    new HarmonyMethod(typeof(ROTRFactionHelpSync), nameof(PreStartHelping)));
            else
                Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} Could not find StartHelping");
        }
        catch (Exception exception)
        {
            Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} Failed to patch StartHelping: {exception}");
        }
    }

    private static void SyncedStartHelping(Faction faction, RoadConstructionSite site, Pawn negotiator)
    {
        try
        {
            if (faction == null || site == null)
                return;

            var comp = Find.World.GetComponent<WorldComponent_FactionRoadConstructionHelp>();
            if (comp == null)
            {
                Log.Error(
                    $"{RailsAndRoadsOfTheRimContinued.LogPrefix} Could not find WorldComponent_FactionRoadConstructionHelp");
                return;
            }

            comp.StartHelping(faction, site, negotiator);
        }
        catch (Exception exception)
        {
            Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} SyncedStartHelping failed: {exception}");
        }
    }

    private static bool PreStartHelping(WorldComponent_FactionRoadConstructionHelp __instance, Faction faction,
        RoadConstructionSite site, Pawn negotiator)
    {
        try
        {
            if (!MP.IsInMultiplayer)
                return true;

            if (!MP.InInterface)
                return true;

            SyncedStartHelping(faction, site, negotiator);
            return false;
        }
        catch (Exception exception)
        {
            Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} PreStartHelping failed: {exception}");
            return true;
        }
    }
}