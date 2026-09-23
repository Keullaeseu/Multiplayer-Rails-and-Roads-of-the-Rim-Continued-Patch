using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Compat;
using RailsAndRoadsOfTheRim;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MultiplayerRailsAndRoadsOfTheRimContinuedPatch.Source.Mods;

/// <summary>World-targeting leg placement: local validation/UI, synced leg creation.</summary>
internal static class ROTRLegTargetingSync
{
    private static AccessTools.FieldRef<RoadConstructionLeg, RoadConstructionSite> legSiteField;

    internal static void Setup()
    {
        // Shared field ref for the private RoadConstructionLeg.site field.
        try
        {
            legSiteField = AccessTools.FieldRefAccess<RoadConstructionLeg, RoadConstructionSite>("site");
        }
        catch (Exception exception)
        {
            Log.Error(
                $"{RailsAndRoadsOfTheRimContinued.LogPrefix} Failed to get RoadConstructionLeg.site field: {exception}");
        }

        try
        {
            MP.RegisterSyncMethod(typeof(ROTRLegTargetingSync), nameof(SyncedAddLeg));
        }
        catch (Exception exception)
        {
            Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} Failed to register SyncedAddLeg: {exception}");
        }

        // World targeting callback has a bool return (continue/stop targeting) which cannot be synced directly.
        // Handle validation/targeting UI locally and sync only the world change via SyncedAddLeg/Remove.
        try
        {
            var actionOnTile = AccessTools.DeclaredMethod(typeof(RoadConstructionLeg), "ActionOnTile");
            if (actionOnTile != null)
                MpCompat.harmony.Patch(actionOnTile,
                    new HarmonyMethod(typeof(ROTRLegTargetingSync), nameof(PreActionOnTile)));
            else
                Log.Error(
                    $"{RailsAndRoadsOfTheRimContinued.LogPrefix} Could not find RoadConstructionLeg.ActionOnTile");
        }
        catch (Exception exception)
        {
            Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} Failed to patch ActionOnTile: {exception}");
        }
    }

    private static void SyncedAddLeg(RoadConstructionSite site, PlanetTile tile)
    {
        if (site == null)
            return;

        try
        {
            var legDef = DefDatabase<WorldObjectDef>.GetNamed("RoadConstructionLeg");
            var siteDef = DefDatabase<WorldObjectDef>.GetNamed("RoadConstructionLeg");

            var newLeg = (RoadConstructionLeg)WorldObjectMaker.MakeWorldObject(legDef);
            newLeg.Tile = tile;

            if (legSiteField != null)
            {
                legSiteField(newLeg) = site;
            }
            else
            {
                var siteFieldInfo = AccessTools.Field(typeof(RoadConstructionLeg), "site");
                if (siteFieldInfo != null)
                    siteFieldInfo.SetValue(newLeg, site);
            }

            if (site.LastLeg != null && site.LastLeg.def == siteDef)
            {
                if (site.LastLeg is RoadConstructionLeg lastLeg)
                {
                    lastLeg.Next = newLeg;
                    newLeg.Previous = lastLeg;
                }
            }
            else
            {
                newLeg.Previous = null;
            }

            newLeg.Next = null;
            Find.WorldObjects.Add(newLeg);
            site.LastLeg = newLeg;
        }
        catch (Exception exception)
        {
            Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} SyncedAddLeg failed: {exception}");
        }
    }

    private static bool PreActionOnTile(RoadConstructionSite site, PlanetTile tile, ref bool __result)
    {
        // Only intercept interface calls (player clicking world tiles). Synced leg creation goes via SyncedAddLeg.
        try
        {
            if (!MP.IsInMultiplayer)
                return true;

            if (!MP.InInterface)
                return true;

            if (site == null)
            {
                __result = true;
                return false;
            }

            if (site.def != DefDatabase<WorldObjectDef>.GetNamed("RoadConstructionSite"))
            {
                Log.Error("[RotR] - The RoadConstructionSite given is somehow wrong");
                __result = true;
                return false;
            }

            try
            {
                foreach (var obj in Find.WorldObjects.ObjectsAt(tile))
                {
                    if (obj.def == DefDatabase<WorldObjectDef>.GetNamed("RoadConstructionSite") &&
                        (RoadConstructionSite)obj == site)
                    {
                        __result = true;
                        return false;
                    }

                    if (obj.def == DefDatabase<WorldObjectDef>.GetNamed(nameof(RoadConstructionLeg)))
                    {
                        var existingLeg = (RoadConstructionLeg)obj;
                        RoadConstructionSite existingSite = null;
                        if (legSiteField != null)
                            existingSite = legSiteField(existingLeg);
                        else
                            existingSite = existingLeg.GetSite();

                        if (existingSite == site)
                        {
                            // Removal is synced via MP.RegisterSyncMethod(Remove), targeting stays local.
                            RoadConstructionLeg.Remove(existingLeg);
                            RoadConstructionLeg.Target(site);
                            __result = false;
                            return false;
                        }
                    }
                }

                var neighbors = new List<PlanetTile>();
                Find.WorldGrid.GetTileNeighbors(tile, neighbors);
                if (site.LastLeg == null || !neighbors.Contains(site.LastLeg.Tile))
                {
                    RoadConstructionLeg.Target(site);
                    __result = false;
                    return false;
                }

                if (site.roadDef == null)
                {
                    RoadConstructionLeg.Target(site);
                    __result = false;
                    return false;
                }

                if (!DefModExtension_RotR_RoadDef.BiomeAllowed(tile, site.roadDef, out var biomeHere))
                {
                    Messages.Message(
                        "RoadsOfTheRim_BiomePreventsConstruction".Translate(site.roadDef.label,
                            biomeHere?.label ?? "?"), MessageTypeDefOf.RejectInput);
                    RoadConstructionLeg.Target(site);
                    __result = false;
                    return false;
                }

                if (!DefModExtension_RotR_RoadDef.ImpassableAllowed(tile, site.roadDef))
                {
                    Messages.Message(
                        "RoadsOfTheRim_BiomePreventsConstruction".Translate(site.roadDef.label,
                            " impassable mountains"), MessageTypeDefOf.RejectInput);
                    RoadConstructionLeg.Target(site);
                    __result = false;
                    return false;
                }

                SyncedAddLeg(site, tile);
                RoadConstructionLeg.Target(site);
                __result = false;
                return false;
            }
            catch (Exception exception)
            {
                Log.Error($"[RotR] Exception : {exception}");
                __result = true;
                return false;
            }
        }
        catch (Exception exception)
        {
            Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} PreActionOnTile failed: {exception}");
            return true;
        }
    }
}