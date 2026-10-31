using Godot;
using System;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedWarpState(FrontendValidationRoot root, bool batched)
    {
        var engine = root._gameplaySession!;
        var rooms = engine.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var transitions = engine.Transitions;
        var app = new ApplicationValidationFixture(root);
        OracleGame game = rooms.SaveData.Game;
        byte sourceMenu = rooms.RuntimeState.ReadField("wMenuDisabled");
        var native = new WarpDestinationRom(game);
        int group = rooms.ActiveGroup, room = rooms.CurrentRoom.Id;
        player.Inventory.EquipA(0); player.Inventory.EquipB(0);
        if (game == OracleGame.Seasons)
        {
            int pack = root._seasonsOpening!.Data.World.RoomPack(room);
            int baseline = native.ResolveSeason(rooms.SaveData,pack,0);
            int sourceSeason = (baseline + 1) & 3;
            foreach (bool preserve in new[] { false,true,false })
            {
                entities.Clear(); player.WarpTo(new(72,80));
                rooms.RuntimeState.SetField("wRoomStateModifier",(byte)sourceSeason);
                bool selectsSeason = native.Apply(group,rooms.CurrentDungeonIndex,0x80 | group | (preserve ? 0x40 : 0),room,0x54);
                int expected = selectsSeason ? native.ResolveSeason(rooms.SaveData,pack,sourceSeason) : sourceSeason;
                // The diagnostic source group is deliberately stale; native uses
                // the live room owner at applyWarpDest_b04, not that request copy.
                transitions.ApplyWarpWithFadeOut(player,new Warp(3,room,0x54,0,0,group,room,0x54,0,0,
                    PreserveDestinationSeason:preserve));
                app.Step(31,Vector2.Zero,batched:batched);
                FailIf(rooms.RuntimeState.ReadField("wRoomStateModifier") != sourceSeason,
                    "Seasons destination season must remain unchanged through source fade update 31.");
                app.Step(1,Vector2.Zero,batched:batched);
                FailIf(rooms.RuntimeState.ReadField("wRoomStateModifier") != expected ||
                    rooms.RuntimeState.ReadField("wRoomPack") != pack || rooms.RuntimeState.ReadField("wLoadingRoomPack") != pack,
                    $"Seasons: native destination bit $40 and live source identity must select season {expected}, preserve={preserve}.");
                app.Step(33,Vector2.Zero,batched:batched);
                FailIf(transitions.IsTransitioning,"Season-preserving arrival must finish through the common lifecycle.");
            }
        }
        else
        {
            foreach (int origin in new[] { 0,1 })
            {
                root.LoadDebugRoom(origin,0x39); entities.Clear(); player.WarpTo(new(40,40));
                byte originalFlags = rooms.RuntimeState.ReadField("wTilesetFlags");
                byte liveFlags = (byte)(originalFlags ^ 0x80); // Declared live input change after the warp request.
                rooms.RuntimeState.SetField("wcc50",0x55);
                var age = (AgesRoomTransitionController)transitions;
                age.ApplyHarpTimeWarp(player,player.Position);
                app.Step(AgesRoomTransitionController.TimeWarpInitializeFrames + AgesRoomTransitionController.TimeWarpDissolveFrames,
                    Vector2.Zero,batched:batched);
                FailIf(age.TimeWarpPhaseName != "TimeWarpSetup" || rooms.RuntimeState.ReadField("wcc50") != 0x55,
                    "Ages time-warp request/dissolve must not publish the source palette selector early.");
                rooms.RuntimeState.SetField("wTilesetFlags",liveFlags);
                app.Step(1,Vector2.Zero,batched:batched);
                FailIf(rooms.RuntimeState.ReadField("wcc50") != 0x55,"The first tilemap setup update must retain the selector byte.");
                var stateNative = new TimeWarpStateRom();
                byte selector = stateNative.PublishSource(liveFlags,0x55);
                app.Step(1,Vector2.Zero,batched:batched);
                var source = age.ActiveTimeWarpEffect ?? throw new InvalidOperationException("Native source effect was not created.");
                FailIf(age.TimeWarpPhaseName != "TimeWarpSourceEffect" || rooms.RuntimeState.ReadField("wcc50") != selector ||
                    source.UsesIndoorBeamPalette != (selector == 2),"Source-effect birth must publish and capture native wcc50 from live tileset flags.");
                rooms.RuntimeState.SetField("wTilesetFlags",originalFlags);
                byte changed = (byte)(selector == 1 ? 2 : 1);
                rooms.RuntimeState.SetField("wcc50",changed);
                app.Step(4,Vector2.Zero,batched:batched);
                FailIf(source.UsesIndoorBeamPalette != (selector == 2),"An existing native effect retains its own copied var03.");
                for (int update = 0; update < 600 && age.TimeWarpPhaseName != "TimeWarpArrivalEffect"; update++)
                    app.Step(1,Vector2.Zero,batched:batched);
                byte copied = stateNative.CopyArrival(changed);
                FailIf(age.TimeWarpPhaseName != "TimeWarpArrivalEffect" || age.ActiveTimeWarpEffect is not { } arrival ||
                    ReferenceEquals(arrival,source) || arrival.UsesIndoorBeamPalette != (copied == 2) ||
                    rooms.RuntimeState.ReadField("wcc50") != changed,"Arrival effects must copy the current native selector instead of a controller's earlier snapshot.");
                for (int update = 0; update < 1000 && transitions.IsTransitioning; update++) app.Step(1,Vector2.Zero,batched:batched);
                FailIf(transitions.IsTransitioning,"Selector publication fixture must finish the same native time-warp protocol.");
            }
            root.LoadDebugRoom(group,room);
        }
        var database = WarpDatabase.For(rooms.AssetRoot);
        Warp request = default;
        bool found = false;
        for (int sourceGroup = 0; sourceGroup < 8 && !found; sourceGroup++)
        for (int sourceRoom = 0; sourceRoom < 256 && !found; sourceRoom++)
        {
            if (!database.TryGetTileWarpSource(sourceGroup,sourceRoom,0x44,out var candidate) ||
                candidate.DestinationGroup != group || candidate.SourceTransition is not (2 or 3 or 4)) continue;
            native.Apply(group,rooms.CurrentDungeonIndex,candidate.DestinationGroup,candidate.DestinationRoom,0);
            if (!rooms.HasRoom(native.Group,native.Room) || candidate.DestinationRoom == native.Room ||
                (native.Transition & 15) is not (1 or 4 or 0x0e)) continue;
            request = candidate; found = true;
        }
        FailIf(!found,$"{game}: deferred-load fixture requires an actual native indexed standing destination.");
        int expectedGroup = native.Group, expectedRoom = native.Room;
        Vector2 expectedPosition = native.Position;
        byte expectedMarker = native.SetEnteredWarpPosition((int)expectedPosition.X, (int)expectedPosition.Y);
        if ((native.Transition & 15) == 0x0e) expectedPosition.X -= 8;
        entities.Clear(); player.WarpTo(new(72,80));
        int loads = 0;
        void Loaded(int changedGroup,OracleRoomData changedRoom) => loads++;
        rooms.RoomChanged += Loaded;
        try
        {
            // A staged native source request isolates commit timing; this is
            // not a natural doorway-approach or new-content admission claim.
            transitions.ApplyWarpWithFadeOut(player,request);
            app.Step(31,Vector2.Zero,batched:batched);
            FailIf(loads != 0,"An indexed destination must remain pending through fade update 31.");
            app.Step(1,Vector2.Zero,batched:batched);
            FailIf(loads != 1 || rooms.ActiveGroup != expectedGroup || rooms.CurrentRoom.Id != expectedRoom ||
                rooms.RuntimeState.ReadField("wEnteredWarpPosition") != expectedMarker,
                $"{game}: the load boundary must resolve the native index into its actual destination room.");
            app.Step(1,Vector2.Zero,batched:batched);
            FailIf(player.PrecisePosition != expectedPosition,$"{game}: the first destination Link update must use the resolved native position/mode.");
            app.Step(32,Vector2.Zero,batched:batched);
            FailIf(transitions.IsTransitioning,"Indexed standing arrival must finish its shared lifecycle.");
        }
        finally { rooms.RoomChanged -= Loaded; }
        rooms.RuntimeState.SetField("wEnteredWarpPosition", 0x54);
        rooms.SetLoadedRoom(rooms.ActiveGroup, rooms.CurrentRoom);
        byte clearedMarker = native.ClearScrollingRoomVariables(0x54);
        FailIf(rooms.RuntimeState.ReadField("wEnteredWarpPosition") != clearedMarker || clearedMarker != 0,
            $"{game}: scrolling activation must clear the raw marker in func_49c9's shared-variable range.");
        rooms.RuntimeState.SetField("wEnteredWarpPosition", 0x54);
        rooms.Load(rooms.ActiveGroup, rooms.CurrentRoom.Id);
        FailIf(rooms.RuntimeState.ReadField("wEnteredWarpPosition") != 0,
            $"{game}: full loading must clear the same raw marker before its destination writer.");
        if (game == OracleGame.Ages) root.LoadDebugRoom(group,room);
        else root._seasonsOpening!.DebugTravel(group,room);
        entities.Clear(); player.WarpTo(new(72,80));
        byte marker = native.SetEnteredWarpPosition(72, 80);
        transitions.DeactivateWarpAtPlayerPosition(player);
        FailIf(rooms.RuntimeState.ReadField("wEnteredWarpPosition") != marker || marker != 0x54,
            $"{game}: typed warp deactivation must publish the native packed position to the shared byte.");
        rooms.RuntimeState.SetField("wEnteredWarpPosition", 0x22);
        transitions.CheckTileWarp(player);
        FailIf(rooms.RuntimeState.ReadField("wEnteredWarpPosition") != 0xff,
            $"{game}: checking a different standing tile must clear a raw-written deactivated marker.");
        rooms.RuntimeState.SetField("wEnteredWarpPosition", marker);
        FailIf(transitions.CheckTileWarp(player) || rooms.RuntimeState.ReadField("wEnteredWarpPosition") != marker,
            $"{game}: a raw-written current tile marker must suppress warp activation and retain its byte.");
        transitions.ClearDeactivatedWarp();
        FailIf(rooms.RuntimeState.ReadField("wMenuDisabled") != sourceMenu,
            $"{game}: returning from the warp fixture must release the interrupted room script's native menu byte.");
    }
}
