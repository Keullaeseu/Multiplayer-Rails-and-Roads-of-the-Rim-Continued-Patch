using Multiplayer.API;
using Multiplayer.Compat;
using RailsAndRoadsOfTheRim;
using Verse;

namespace MultiplayerRailsAndRoadsOfTheRimContinuedPatch.Source.Mods;

/// <summary>Caravan gizmo sync (Construct / Work / Stop) and construction-leg removal.</summary>
internal static class ROTRGizmoSync
{
    internal static void Register()
    {
        // AddConstructionSite opens the ConstructionMenu window, the window itself is kept
        // local to the acting client (see ROTRConstructionMenuSync).
        // RemoveConstructionSite may open a confirmation dialog, so it stays local and only
        // the confirmed deletion is synced (see ROTRSiteLifecycleSync).
        try
        {
            MpCompat.RegisterLambdaDelegate(typeof(RailsAndRoadsOfTheRim.RailsAndRoadsOfTheRim),
                nameof(RailsAndRoadsOfTheRim.RailsAndRoadsOfTheRim.AddConstructionSite), 0);
        }
        catch (Exception exception)
        {
            Log.Error(
                $"{RailsAndRoadsOfTheRimContinued.LogPrefix} Failed to sync AddConstructionSite gizmo: {exception}");
        }

        try
        {
            MpCompat.RegisterLambdaDelegate(typeof(RailsAndRoadsOfTheRim.RailsAndRoadsOfTheRim),
                nameof(RailsAndRoadsOfTheRim.RailsAndRoadsOfTheRim.WorkOnSite), 0);
            MpCompat.RegisterLambdaDelegate(typeof(RailsAndRoadsOfTheRim.RailsAndRoadsOfTheRim),
                nameof(RailsAndRoadsOfTheRim.RailsAndRoadsOfTheRim.StopWorkingOnSite), 0);
        }
        catch (Exception exception)
        {
            Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} Failed to sync Work/Stop gizmos: {exception}");
        }

        // Removing legs when re-targeting (static void, takes WorldObject-derived leg).
        try
        {
            MP.RegisterSyncMethod(typeof(RoadConstructionLeg), nameof(RoadConstructionLeg.Remove));
        }
        catch (Exception exception)
        {
            Log.Error(
                $"{RailsAndRoadsOfTheRimContinued.LogPrefix} Failed to sync RoadConstructionLeg.Remove: {exception}");
        }
    }
}