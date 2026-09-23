using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Compat;
using RailsAndRoadsOfTheRim;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MultiplayerRailsAndRoadsOfTheRimContinuedPatch.Source.Mods;

/// <summary>Site lifecycle: targeting finalise and tile-based deletion.</summary>
internal static class ROTRSiteLifecycleSync
{
    internal static void Setup()
    {
        try
        {
            MP.RegisterSyncMethod(typeof(ROTRSiteLifecycleSync), nameof(SyncedFinalise));
            MP.RegisterSyncMethod(typeof(ROTRSiteLifecycleSync), nameof(SyncedDeleteAtTile));
        }
        catch (Exception exception)
        {
            Log.Error(
                $"{RailsAndRoadsOfTheRimContinued.LogPrefix} Failed to register site lifecycle sync methods: {exception}");
        }

        // Finalise needs the specific caravan (stored locally in RoadBuildingState on acting client).
        // Redirect to SyncedFinalise(site, caravan) with explicit args.
        try
        {
            var finalise = AccessTools.DeclaredMethod(typeof(RailsAndRoadsOfTheRim.RailsAndRoadsOfTheRim),
                nameof(RailsAndRoadsOfTheRim.RailsAndRoadsOfTheRim.FinaliseConstructionSite));
            if (finalise != null)
                MpCompat.harmony.Patch(finalise,
                    new HarmonyMethod(typeof(ROTRSiteLifecycleSync), nameof(PreFinaliseConstructionSite)));
            else
                Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} Could not find FinaliseConstructionSite");
        }
        catch (Exception exception)
        {
            Log.Error(
                $"{RailsAndRoadsOfTheRimContinued.LogPrefix} Failed to patch FinaliseConstructionSite: {exception}");
        }

        // Confirmed deletion goes via safe tile-based wrapper (original throws NRE when the synced
        // site reference resolves to null on a remote client). The confirmation dialog itself stays local.
        try
        {
            var deleteConfirmed = AccessTools.DeclaredMethod(typeof(RailsAndRoadsOfTheRim.RailsAndRoadsOfTheRim),
                "DeleteConstructionSiteConfirmed");
            if (deleteConfirmed != null)
                MpCompat.harmony.Patch(deleteConfirmed,
                    new HarmonyMethod(typeof(ROTRSiteLifecycleSync), nameof(PreDeleteConstructionSiteConfirmed)));
            else
                Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} Could not find DeleteConstructionSiteConfirmed");
        }
        catch (Exception exception)
        {
            Log.Error(
                $"{RailsAndRoadsOfTheRimContinued.LogPrefix} Failed to patch DeleteConstructionSiteConfirmed: {exception}");
        }
    }

    private static void SyncedFinalise(RoadConstructionSite site, Caravan caravan)
    {
        if (site == null)
            return;

        try
        {
            if (site.GetNextLeg() != null)
            {
                site.GetComponent<WorldObjectComp_ConstructionSite>().SetCosts();
                if (caravan != null)
                    caravan.GetComponent<WorldObjectComp_Caravan>().StartWorking();
                else
                    RailsAndRoadsOfTheRim.RailsAndRoadsOfTheRim.RoadBuildingState?.Caravan
                        ?.GetComponent<WorldObjectComp_Caravan>()?.StartWorking();
            }
            else
            {
                RoadConstructionSite.DeleteSite(site);
            }
        }
        catch (Exception exception)
        {
            Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} SyncedFinalise failed: {exception}");
        }
    }

    // Tile-based so every client deletes its own site instance at that tile
    // (WorldObject references can resolve to null on remotes if IDs diverged).
    private static void SyncedDeleteAtTile(PlanetTile tile)
    {
        try
        {
            if (Find.World == null || Find.WorldObjects == null)
                return;

            var site = Find.WorldObjects.WorldObjectOfDefAt(
                DefDatabase<WorldObjectDef>.GetNamed("RoadConstructionSite"), tile) as RoadConstructionSite;
            if (site == null)
                return;

            try
            {
                if (site.helpFromFaction != null)
                    RailsAndRoadsOfTheRim.RailsAndRoadsOfTheRim.FactionsHelp?.HelpFinished(site.helpFromFaction);
            }
            catch (Exception exception)
            {
                Log.Error(
                    $"{RailsAndRoadsOfTheRimContinued.LogPrefix} SyncedDeleteAtTile faction cleanup failed: {exception}");
            }

            RoadConstructionSite.DeleteSite(site);
        }
        catch (Exception exception)
        {
            Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} SyncedDeleteAtTile failed: {exception}");
        }
    }

    private static bool PreFinaliseConstructionSite(RoadConstructionSite site)
    {
        try
        {
            if (!MP.IsInMultiplayer)
                return true;

            // Let synced execution run the original logic (via SyncedFinalise which replicates it with explicit caravan).
            if (!MP.InInterface)
                return true;

            var caravan = RailsAndRoadsOfTheRim.RailsAndRoadsOfTheRim.RoadBuildingState?.Caravan;

            SyncedFinalise(site, caravan);
            return false;
        }
        catch (Exception exception)
        {
            Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} PreFinaliseConstructionSite failed: {exception}");
            return true;
        }
    }

    // Note: original parameter is named "ConstructionSite" (capital C) - prefix must match it for Harmony binding.
    private static bool PreDeleteConstructionSiteConfirmed(RoadConstructionSite ConstructionSite)
    {
        try
        {
            if (!MP.IsInMultiplayer)
                return true;

            if (!MP.InInterface)
                return true;

            if (ConstructionSite == null)
                return false;

            SyncedDeleteAtTile(ConstructionSite.Tile);
            return false;
        }
        catch (Exception exception)
        {
            Log.Error(
                $"{RailsAndRoadsOfTheRimContinued.LogPrefix} PreDeleteConstructionSiteConfirmed failed: {exception}");
            return true;
        }
    }
}