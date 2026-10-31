using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedTransitions(FrontendValidationRoot root, bool batched)
    {
        var engine = root._gameplaySession!;
        var rooms = engine.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var transitions = engine.Transitions;
        var player = root._scene.Player;
        var app = new ApplicationValidationFixture(root);
        var game = rooms.SaveData.Game;
        int initialGroup = rooms.ActiveGroup, initialRoom = rooms.CurrentRoom.Id;
        bool rejected = false;
        try { engine.BindTransitions(transitions); } catch (ArgumentException) { rejected = true; }
        FailIf(!rejected || !transitions.UsesOwners(rooms,entities),$"{game}: a session must retain one authoritative transition binding.");
        entities.Clear(); player.Inventory.EquipA(0); player.Inventory.EquipB(0);
        var floor = Enumerable.Range(0,80).Select(index => new Vector2(index % 10 * 16 + 8,index / 10 * 16 + 8))
            .First(point => point.X is >= 24 and <= 136 && point.Y is >= 24 and <= 104 &&
                !player.World.Collides(point) && player.World.GetActiveTerrain(point).Terrain.Hazard == HazardType.None);
        player.WarpTo(floor);
        int position = rooms.CurrentRoom.GetPackedPosition(floor);
        int sourceCalls = 0;
        entities.AddEntity(new ItemPhaseValidationEntity(() => sourceCalls++));
        var warp = new Warp(initialGroup,initialRoom,position,0,2,initialGroup,initialRoom,position,0,0);
        transitions.ApplyWarpWithFadeOut(player,warp);
        app.Step(31,Vector2.Zero,batched:batched);
        FailIf(!transitions.IsTransitioning || transitions.PhaseName != "FadeOut" || sourceCalls != 0,
            $"{game}: native ordinary fade-out must omit objects through update 31.");
        app.Step(1,Vector2.Zero,batched:batched);
        FailIf(transitions.PhaseName != "FadeIn" || sourceCalls != 0 || player.Position != floor,
            $"{game}: terminal fade-out update 32 must load the exact anchor and omit the destination object pass.");
        int destinationCalls = 0;
        entities.AddEntity(new ItemPhaseValidationEntity(() => destinationCalls++));
        app.Step(32,Vector2.Zero,batched:batched);
        FailIf(!transitions.IsTransitioning || destinationCalls != 32 || sourceCalls != 0,
            $"{game}: destination objects must advance once per fade-in update, with the source stream retired.");
        app.Step(1,Vector2.Zero,batched:batched);
        FailIf(transitions.IsTransitioning || destinationCalls != 33,$"{game}: ordinary fade-in must release ownership on native update 33.");
        transitions.ApplyWarpWithFadeOut(player,warp);
        app.Step(5,Vector2.Zero,batched:batched);
        transitions.CancelPendingFadeOutWarp(initialGroup,initialRoom);
        app.Step(1,Vector2.Zero,batched:batched);
        FailIf(transitions.IsTransitioning || destinationCalls != 34 || rooms.CurrentRoom.Id != initialRoom,
            $"{game}: cancelling an uncommitted fade must resume the current room without replacing its objects.");
        entities.Clear();
        var trees = new GaleTreeWarpDatabase(rooms.AssetRoot);
        // Clean-US warpTransition5_01 reads the landing tile only in Seasons;
        // Ages' missing read leaves A=$00 and always takes the collapsed hold.
        FailIf(rooms.CurrentRoom.ActiveCollisions != 0,"Fall landing fixture requires native overworld collision mode $00.");
        transitions.ApplyWarp(player,new Warp(initialGroup,initialRoom,position,0,0,initialGroup,initialRoom,position,0,5));
        entities.Clear();
        byte originalTile = rooms.CurrentRoom.GetMetatile(floor);
        byte originalCollision = rooms.CurrentRoom.GetTerrainInfo(floor).Collision;
        rooms.CurrentRoom.SetPositionTileAndCollision(floor,(byte)(game == OracleGame.Ages ? 0xfa : 0xfd),0,0);
        var landingSounds = root._sound.AttachPlayRequestAudit();
        app.Step(1,Vector2.Zero,batched:batched);
        for (int update = 0; update < 80 && player.IsRoomWarpFalling && !player.RoomWarpFallCollapsed; update++)
            app.Step(1,Vector2.Zero,batched:batched);
        FailIf(game == OracleGame.Ages ? !player.RoomWarpFallCollapsed || landingSounds.RequestsFor(SoundId.SndSplash) != 1 :
            player.IsRoomWarpFalling || landingSounds.RequestsFor(SoundId.SndSplash) != 0,
            $"{game}: native landing on water must retain Ages' collapsed/splash bug and Seasons' direct standing return.");
        rooms.CurrentRoom.SetPositionTileAndCollision(floor,originalTile,originalCollision,0);
        for (int update = 0; update < 100 && transitions.IsTransitioning; update++) app.Step(1,Vector2.Zero,batched:batched);
        FailIf(transitions.IsTransitioning,"Controlled hazard arrival must release its transition owner.");
        entities.Clear(); player.WarpTo(floor);
        if (game == OracleGame.Seasons)
        {
            // Controlled native inputs: an off-center chimney tile and live
            // invincibility. Ordinary tile centering would reject this point.
            var sourceRoom = rooms.CurrentRoom;
            Vector2 chimneyPoint = floor - new Vector2(7,0);
            sourceRoom.SetPositionTileAndCollision(floor,0xeb,0,0);
            player.WarpTo(chimneyPoint); transitions.ClearDeactivatedWarp();
            FailIf(RoomTransitionController.LinkWithinTileWarpBounds(sourceRoom,position,chimneyPoint),
                "Chimney fixture must fail ordinary tile centering.");
            var native = new WarpLookupRom(game).Lookup(initialGroup,initialRoom,position);
            FailIf(!native.Found,"Native Seasons chimney must select its source/default warp.");
            var rom = ValidationRom.LoadCleanUs(game);
            int Word(int at) => rom.Span[at] | rom.Span[at + 1] << 8;
            int index = native.Group == 2 ? (native.Index + rooms.RuntimeState.ReadWramByte(0xc6df)) & 255 : native.Index;
            int targetAt = 4 * 0x4000 + (Word(4 * 0x4000 + 0x2d4e + native.Group * 2) & 0x3fff) + index * 3;
            int nativeRoom = rom.Span[targetAt];
            bool unavailable = !rooms.HasRoom(native.Group,nativeRoom), diagnosed = false;
            player.ApplyInteractionInvincibility(0x20);
            try { app.Step(1,Vector2.Zero,batched:batched); }
            catch (NotSupportedException exception) when (unavailable && exception.Message.Contains("bank4.applyWarpDest_b04",StringComparison.Ordinal))
            { diagnosed = true; }
            FailIf(unavailable ? !diagnosed || transitions.IsTransitioning : !transitions.IsTransitioning,
                "Chimney must bypass centering and either start its native destination or diagnose its missing room content.");
            FailIf(player.InvincibilityFrames != 0,"Native chimney entry must reset Link invincibility before warp initiation.");
            sourceRoom.SetPositionTileAndCollision(floor,originalTile,originalCollision,0);
            for (int update = 0; update < 200 && transitions.IsTransitioning; update++) app.Step(1,Vector2.Zero,batched:batched);
            if (rooms.CurrentRoom.Id != initialRoom) root._seasonsOpening!.DebugTravel(initialGroup,initialRoom);
            entities.Clear(); player.WarpTo(floor);
        }
        var target = trees.Get(rooms,0);
        rooms.SaveData.SetRoomFlag(initialGroup,target.Room,OracleSaveData.RoomFlagVisited);
        FailIf(target.Room == initialRoom,"Gale handoff fixture needs a distinct source and target room.");
        var approach = (from index in Enumerable.Range(0,80)
            let point = new Vector2(index % 10 * 16 + 8,index / 10 * 16 + 8)
            from direction in new[] { Vector2I.Up,Vector2I.Right,Vector2I.Down,Vector2I.Left }
            where Enumerable.Range(0,41).All(distance =>
            {
                Vector2 sample = point + (Vector2)direction * distance;
                return sample.X is >= 16 and <= 144 && sample.Y is >= 16 and <= 112 &&
                    !player.World.Collides(sample) && player.World.GetActiveTerrain(sample).Terrain.Hazard == HazardType.None;
            })
            select new { Point = point,Direction = direction }).FirstOrDefault();
        FailIf(approach is null,$"{game}: Gale input fixture needs a clear native floor approach.");
        floor = approach!.Point;
        player.Inventory.GiveTreasure(TreasureId.SeedSatchel,1);
        player.Inventory.GiveTreasure(TreasureId.GaleSeeds,0x20);
        player.Inventory.SelectSatchelSeeds(3); player.Inventory.EquipA(0); player.Inventory.EquipB(TreasureId.SeedSatchel);
        void OpenPhysicalGale()
        {
            entities.Clear(); player.WarpTo(floor);
            player.Face(approach.Direction);
            int ammo = player.Inventory.GaleSeeds;
            app.Step(1,Vector2.Zero,held:["item"],pressed:["item"],batched:batched);
            FailIf(!player.IsUsingSeedSatchel || entities.Entities<EmberSeedEffect>().Count != 1 ||
                player.Inventory.GaleSeeds != ammo - ((ammo & 15) == 0 ? 7 : 1),
                $"{game}: fresh Gale Satchel input must allocate its child and consume one BCD seed.");
            for (int update = 0; update < 220 && !engine.Menus.Map.IsOpen; update++)
            {
                Vector2 movement = Vector2.Zero;
                var child = entities.Entities<EmberSeedEffect>().FirstOrDefault();
                if (!player.GaleActive && child is { State: not EmberState.Flying } &&
                    (child.PrecisePosition - player.PrecisePosition).Dot((Vector2)approach.Direction) > 4)
                    movement = approach.Direction;
                app.Step(1,movement,batched:batched);
            }
            FailIf(!engine.Menus.Map.IsOpen || !player.GaleActive || !engine.Menus.Pause.IsLeased ||
                root._scene.MapScreen.CursorRoom != target.Room,
                $"{game}: physical Gale capture must reach the shared menu and native first visited tree.");
        }
        OpenPhysicalGale();
        engine.Menus.Map.PromptGale(cancel:true);
        root._scene.Dialogue.SubmitChoiceForValidation(1);
        app.Step(1,Vector2.Zero,batched:batched); app.Step(22,Vector2.Zero,batched:batched);
        FailIf(engine.Menus.Map.IsActive || !player.IsRoomWarpFalling || rooms.CurrentRoom.Id != initialRoom || transitions.IsTransitioning,
            $"{game}: Gale cancellation must return to the current room's Link-owned fall without starting another room warp.");
        for (int update = 0; update < 160 && player.IsRoomWarpFalling; update++) app.Step(1,Vector2.Zero,batched:batched);
        FailIf(player.IsRoomWarpFalling || player.GaleActive,$"{game}: Gale cancellation must finish and permit another capture.");
        OpenPhysicalGale();
        engine.Menus.Map.PromptGale(cancel:false);
        root._scene.Dialogue.SubmitChoiceForValidation(0);
        app.Step(1,Vector2.Zero,batched:batched); app.Step(31,Vector2.Zero,batched:batched);
        FailIf(!engine.Menus.Map.IsActive || rooms.CurrentRoom.Id != initialRoom,$"{game}: Gale travel must retain its source through white-fade update 31.");
        app.Step(1,Vector2.Zero,batched:batched);
        Vector2 anchor = new((target.Position & 15) * 16 + 8,(target.Position >> 4) * 16 + 8);
        FailIf(engine.Menus.Map.IsActive || engine.Menus.Pause.IsLeased || rooms.CurrentRoom.Id != target.Room ||
            !transitions.IsTransitioning || player.IsRoomWarpFalling || player.Position != anchor,
            $"{game}: Gale white update 32: map={engine.Menus.Map.IsActive}, pause={engine.Menus.Pause.IsLeased}, room=${rooms.CurrentRoom.Id:x2}/${target.Room:x2}, " +
            $"transition={transitions.IsTransitioning}/{transitions.PhaseName}, fall={player.IsRoomWarpFalling}, position={player.Position}/{anchor}.");
        app.Step(1,Vector2.Zero,batched:batched);
        int initialZ = Math.Max((-(int)anchor.Y - 8) & 0xff,0x80);
        FailIf(!player.IsRoomWarpFalling || player.Position != anchor + new Vector2(0,-4) || player.ObjectZHigh != initialZ,
            $"{game}: first destination object update must sample objectGetZAboveScreen before subtracting Y=$04.");
        int z = player.RoomWarpFallZ;
        app.Step(1,Vector2.Zero,held:["inventory"],pressed:["inventory"],batched:batched);
        app.Step(22,Vector2.Zero,batched:batched);
        FailIf(!engine.Menus.Inventory.IsActive || !engine.Menus.Pause.IsLeased || player.RoomWarpFallZ != z,
            $"{game}: Start must pause an initialized falling arrival through the shared menu lifecycle.");
        app.Step(1,Vector2.Zero,held:["inventory"],pressed:["inventory"],batched:batched);
        app.Step(22,Vector2.Zero,batched:batched);
        for (int update = 0; update < 160 && transitions.IsTransitioning; update++) app.Step(1,Vector2.Zero,batched:batched);
        FailIf(transitions.IsTransitioning || player.IsRoomWarpFalling || player.GaleActive || engine.Menus.Pause.IsLeased,
            $"{game}: menu release must resume and complete the same arrival without a stale palette or pause owner.");
        app.Step(2,Vector2.Zero,batched:batched);
        if (game == OracleGame.Ages) root.LoadDebugRoom(initialGroup,initialRoom);
        else root._seasonsOpening!.DebugTravel(initialGroup,initialRoom);
        entities.Clear(); player.WarpTo(floor);
    }
}
