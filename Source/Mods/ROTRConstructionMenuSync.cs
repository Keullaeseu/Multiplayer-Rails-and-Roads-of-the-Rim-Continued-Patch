using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Compat;
using RailsAndRoadsOfTheRim;
using Verse;

namespace MultiplayerRailsAndRoadsOfTheRimContinuedPatch.Source.Mods;

/// <summary>Construction menu window handling: acting-client-only window and roadDef field sync.</summary>
internal static class ROTRConstructionMenuSync
{
    private static ISyncField roadDefSyncField;

    internal static void Patch()
    {
        // Sync field for road selection in ConstructionMenu (RoadConstructionSite.roadDef is a public Def field).
        try
        {
            roadDefSyncField =
                MP.RegisterSyncField(typeof(RoadConstructionSite), nameof(RoadConstructionSite.roadDef));
        }
        catch (Exception exception)
        {
            Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} Failed to register roadDef sync field: {exception}");
        }

        // Only the acting client should get the ConstructionMenu window.
        // AddConstructionSite lambda is synced, so it runs on all clients - cancel the window on non-acting clients.
        try
        {
            MpCompat.harmony.Patch(
                AccessTools.DeclaredMethod(typeof(WindowStack), nameof(WindowStack.Add)),
                new HarmonyMethod(typeof(ROTRConstructionMenuSync), nameof(PreWindowStackAdd)));
        }
        catch (Exception exception)
        {
            Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} Failed to patch WindowStack.Add: {exception}");
        }

        // Watch roadDef selection in ConstructionMenu so it propagates to all clients.
        // Targeting (CurrentlyTargeting/Caravan/WorldTargeter) stays local to the acting client.
        try
        {
            var doWindowContents =
                AccessTools.DeclaredMethod(typeof(ConstructionMenu), nameof(Window.DoWindowContents));
            if (doWindowContents != null)
                MpCompat.harmony.Patch(doWindowContents,
                    new HarmonyMethod(typeof(ROTRConstructionMenuSync),
                        nameof(PreConstructionMenuDoWindowContents)),
                    new HarmonyMethod(typeof(ROTRConstructionMenuSync),
                        nameof(PostConstructionMenuDoWindowContents)));
            else
                Log.Error(
                    $"{RailsAndRoadsOfTheRimContinued.LogPrefix} Could not find ConstructionMenu.DoWindowContents");
        }
        catch (Exception exception)
        {
            Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} Failed to patch ConstructionMenu: {exception}");
        }
    }

    private static bool PreWindowStackAdd(Window window)
    {
        try
        {
            if (!MP.IsInMultiplayer)
                return true;

            // AddConstructionSite gizmo lambda is synced, so it executes on all clients.
            // Only the acting client should actually open the road selection menu.
            if (MP.IsExecutingSyncCommand && !MP.IsExecutingSyncCommandIssuedBySelf && window is ConstructionMenu)
                return false;
        }
        catch (Exception exception)
        {
            Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} PreWindowStackAdd failed: {exception}");
        }

        return true;
    }

    private static void PreConstructionMenuDoWindowContents(ConstructionMenu __instance, out bool __state)
    {
        __state = false;

        try
        {
            if (!MP.IsInMultiplayer || roadDefSyncField == null)
                return;

            var site = GetConstructionMenuSite(__instance);
            if (site == null)
                return;

            MP.WatchBegin();
            roadDefSyncField.Watch(site);
            __state = true;
        }
        catch (Exception exception)
        {
            Log.Error(
                $"{RailsAndRoadsOfTheRimContinued.LogPrefix} PreConstructionMenuDoWindowContents failed: {exception}");
        }
    }

    private static void PostConstructionMenuDoWindowContents(bool __state)
    {
        if (!__state)
            return;

        try
        {
            if (!MP.IsInMultiplayer || roadDefSyncField == null)
                return;

            MP.WatchEnd();
        }
        catch (Exception exception)
        {
            Log.Error(
                $"{RailsAndRoadsOfTheRimContinued.LogPrefix} PostConstructionMenuDoWindowContents failed: {exception}");
        }
    }

    private static RoadConstructionSite GetConstructionMenuSite(ConstructionMenu window)
    {
        if (window == null)
            return null;

        // Primary-ctor params become fields; search by type to stay robust against generated names.
        try
        {
            foreach (var field in AccessTools.GetDeclaredFields(typeof(ConstructionMenu)))
                if (field.FieldType == typeof(RoadConstructionSite))
                {
                    var value = field.GetValue(window) as RoadConstructionSite;
                    if (value != null)
                        return value;
                }
        }
        catch (Exception exception)
        {
            Log.Error($"{RailsAndRoadsOfTheRimContinued.LogPrefix} GetConstructionMenuSite failed: {exception}");
        }

        return null;
    }
}