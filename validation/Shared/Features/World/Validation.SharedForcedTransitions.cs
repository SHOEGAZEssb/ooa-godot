using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedForcedTransitions(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var inventory = player.Inventory;
        var transitions = root._gameplaySession.Transitions;
        var memory = rooms.RuntimeState;
        var app = new ApplicationValidationFixture(root);
        var game = rooms.SaveData.Game;
        var graphics = new ScreenTransitionGraphicsDatabase(rooms.AssetRoot);
        int originalGroup = rooms.ActiveGroup, originalRoom = rooms.CurrentRoom.Id;
        var disabled = transitions.ScreenTransitionsDisabledSource;
        byte floor = (byte)(game == OracleGame.Ages ? 0x3a : 0x04);
        void Load(int group, int room)
        {
            if (game == OracleGame.Ages) root.LoadDebugRoom(group,room);
            else root._seasonsOpening!.DebugTravel(group,room);
            entities.Clear(); inventory.EquipA(0); inventory.EquipB(0); inventory.SetScriptedActiveRing(0xff);
        }
        void ClearFloor(int group, OracleRoomData room)
        {
            for (int y = 0; y < room.HeightInTiles; y++)
            for (int x = 0; x < room.WidthInTiles; x++)
                room.SetPositionTileAndCollision(new(x * 16 + 8,y * 16 + 8),floor,0,0);
        }
        try
        {
            for (int direction = 0; direction < 4; direction++)
            foreach (bool inside in new[] { false,true })
            {
                Vector2I vector = direction switch { 0 => Vector2I.Up,1 => Vector2I.Right,2 => Vector2I.Down,_ => Vector2I.Left };
                int destination = -1;
                foreach (int roomId in game == OracleGame.Ages ? new[] { 0x33,0x11,0x45 } : new[] { 0xc9,0xa6,0xa7,0xb6 })
                {
                    Load(0,roomId);
                    if (transitions.TryGetScreenTransitionDestination(vector,out destination) &&
                        rooms.HasRoom(0,destination) && !rooms.RequiresRoomPackFade(0,roomId,destination)) break;
                    destination = -1;
                }
                FailIf(destination < 0,$"{game}: forced transition requires a native same-pack neighbor in direction ${direction:x2}.");
                var source = rooms.CurrentRoom;
                ClearFloor(0,source);
                Vector2 start = direction switch
                {
                    0 => new(80.25f,inside ? 8.5f : 3.5f),
                    1 => new(inside ? 152.25f : 156.25f,64.5f),
                    2 => new(80.25f,inside ? 119.5f : 123.5f),
                    _ => new(inside ? 8.25f : 3.25f,64.5f)
                };
                player.WarpTo(start); player.Face(Vector2I.Down);
                bool airborne = direction == 2 && !inside;
                if (airborne) { inventory.GiveTreasure(TreasureId.Feather,1); inventory.EquipB(TreasureId.Feather); }
                var native = new BraceletRom(source,start,2,0xff,game:game);
                native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
                native[0xd009] = unchecked((byte)player.NativePlanarAngle);
                native[0xcc2d] = (byte)rooms.ActiveGroup;
                native[0xcd00] = 1; native[0xcd04] = 2; native[0xcd0c] = 154; native[0xcd0d] = 121;
                int calls = 0, samples = 0, loadSamples = 0;
                Vector2 expected = default;
                string context = $"{game}: forced ${direction:x2}, inside={inside}, batch={batched}";
                void ObserveRequest()
                {
                    if (loadSamples == 0 && memory.ReadField("wScrollMode") == 8 && ReferenceEquals(rooms.CurrentRoom,source))
                    {
                        loadSamples++;
                        FailIf(rooms.PendingTileGraphics != ((native[0xcce0] - native[0xccdf]) & 31) || rooms.PendingTileGraphics != 1,
                            $"{context}: the real shared tile-graphics tail must retain the queued write under pending scroll mode $04.");
                    }
                    if (samples != 0 || memory.ReadField("wScrollMode") != 4 || memory.ReadField("wScreenTransitionState") != 3 ||
                        memory.ReadField("wScreenTransitionDirection") != direction) return;
                    samples++;
                    FailIf(!transitions.RoomExitPending || player.PrecisePosition != expected ||
                        memory.ReadField("wScreenTransitionDelay") != native[0xcd15],
                        $"{context}: native request publication must retain XY, air delay and its authoritative pending bit.");
                }
                entities.AddEntity(new ItemPhaseValidationEntity(() =>
                {
                    calls++;
                    native.UpdateLinkItems(entities.FrameCounter,airborne ? 2 : 0);
                    FailIf(!rooms.TrySetTile(0x44,floor),$"{context}: source queue fixture requires one accepted native write.");
                    native.SetTile(0x44,floor);
                    expected = new(native.Word(0xd00c) / 256f,native.Word(0xd00a) / 256f);
                    FailIf(player.PrecisePosition != expected,$"{context}: declared request must follow the same real Link/item update.");
                    memory.SetField("wDisableScreenTransitions",1); native[0xcc91] = 1;
                    memory.SetField("wScreenTransitionDelay",9); native[0xcd15] = 9;
                    memory.SetField("wScreenTransitionDirection",(byte)(0x80 | direction));
                    native[0xcd02] = (byte)(0x80 | direction);
                    native.AdvanceScreenBoundary();
                    native.AdvanceChangedTiles();
                    FailIf(native[0xcd00] != 4 || native[0xcd04] != 3 || native[0xcd02] != direction ||
                        native.Word(0xd00c) / 256f != expected.X || native.Word(0xd00a) / 256f != expected.Y,
                        $"{context}: original forced request must bypass disabled/input/coordinate checks without clamping.");
                }));
                int sourceUnique = graphics.ForTileset(source.TilesetId,source.TilesetStateModifier).Unique |
                    (source.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
                transitions.ScreenTransitionsDisabledSource = static () => true;
                memory.Changed += ObserveRequest;
                rooms.RoomChanged += ClearFloor;
                try
                {
                    app.Step(1,Vector2.Zero,held:airborne ? ["item"] : [],pressed:airborne ? ["item"] : [],batched:batched);
                    FailIf(samples != 1 || loadSamples != 1 || calls != 1 || !transitions.ScrollActive || rooms.CurrentRoom.Id != destination ||
                        memory.ReadField("wScrollMode") != 8 || memory.ReadField("wScreenTransitionDirection") != direction ||
                        player.PrecisePosition != expected || player.TopDownAirborne != airborne,
                        $"{context}: object-tail consumption must start one real scroll using the unchanged forced position.");
                    var target = rooms.CurrentRoom;
                    int unique = graphics.ForTileset(target.TilesetId,target.TilesetStateModifier).Unique |
                        (target.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
                    var scroll = new ScrollRom(false,direction,(int)(expected.X * 256),(int)(expected.Y * 256),unique,sourceUnique,game);
                    int incoming = 0, updates = 0, speedZ = player.TopDownAirSpeedZ;
                    entities.AddEntity(new ItemPhaseValidationEntity(() => incoming++));
                    app.Step(transitions.ScrollTotalFrames,Vector2.Zero,batched:batched,afterUpdate:() =>
                    {
                        scroll.Update(); updates++;
                        Vector2 actual = new(((int)(player.PrecisePosition.X * 256) & 0xffff) / 256f,
                            ((int)(player.PrecisePosition.Y * 256) & 0xffff) / 256f);
                        FailIf(actual != new Vector2(scroll.Word(0xd00c) / 256f,scroll.Word(0xd00a) / 256f) ||
                            transitions.ScrollActive != (scroll[0xcd04] != 2) || memory.ReadField("wScrollMode") != scroll[0xcd00] ||
                            memory.ReadField("wScreenTransitionState") != scroll[0xcd04] ||
                            memory.ReadField("wScreenTransitionState2") != scroll[0xcd05] ||
                            memory.ReadField("wScreenTransitionState3") != scroll[0xcd06] ||
                            memory.ReadField("wScreenTransitionDirection") != direction ||
                            memory.ReadField("wScreenTransitionDelay") != native[0xcd15] ||
                            player.TopDownAirSpeedZ != speedZ || incoming != 0 || calls != 1,
                            $"{context}: scroll update{updates}: native phase ${scroll[0xcd04]:x2}:{scroll[0xcd05]:x2}:{scroll[0xcd06]:x2} " +
                            $"differs from runtime ${memory.ReadField("wScreenTransitionState"):x2}:{memory.ReadField("wScreenTransitionState2"):x2}:{memory.ReadField("wScreenTransitionState3"):x2}, XY={player.PrecisePosition}; actors/air/request lifetime must stay frozen.");
                    });
                    FailIf(transitions.RoomExitPending || transitions.IsTransitioning,$"{context}: completion must retire the same request.");
                    app.Step(1,Vector2.Zero,batched:batched);
                    FailIf(incoming != 1 || calls != 1,$"{context}: destination objects must resume on the first update after completion.");
                }
                finally { memory.Changed -= ObserveRequest; rooms.RoomChanged -= ClearFloor; }
            }
            Load(0,game == OracleGame.Ages ? 0x33 : 0xc9);
            ClearFloor(0,rooms.CurrentRoom); player.WarpTo(new(80,64));
            memory.SetField("wScrollMode",0); memory.SetField("wScreenTransitionDirection",0x82);
            app.Step(3,Vector2.Zero,batched:batched);
            FailIf(transitions.IsTransitioning || transitions.RoomExitPending || memory.ReadField("wScreenTransitionDirection") != 0x82,
                $"{game}: wScrollMode=$00 must omit the boundary consumer and retain the forced byte.");
            Load(0,game == OracleGame.Ages ? 0x33 : 0xc9);
            FailIf(memory.ReadField("wScreenTransitionDirection") != 0 || transitions.RoomExitPending,
                $"{game}: full-room clear must cancel an unconsumed forced transition.");
        }
        finally
        {
            transitions.ScreenTransitionsDisabledSource = disabled;
            memory.SetField("wDisableScreenTransitions",0);
            Load(originalGroup,originalRoom); player.WarpTo(new(72,80));
        }
    }
}
