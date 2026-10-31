using Godot;
using System;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedLedgeScroll(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var transitions = root._gameplaySession.Transitions;
        var app = new ApplicationValidationFixture(root);
        OracleGame game = rooms.SaveData.Game;
        int originalGroup = rooms.ActiveGroup, originalRoom = rooms.CurrentRoom.Id;
        void Load(int group, int room)
        {
            if (game == OracleGame.Ages) root.LoadDebugRoom(group,room);
            else root._seasonsOpening!.DebugTravel(group,room);
        }
        byte floor = (byte)(game == OracleGame.Ages ? 0x3a : 0x04);
        void TargetFloor(int group, OracleRoomData room)
        {
            for (int y = 0; y < room.HeightInTiles; y++)
            for (int x = 0; x < room.WidthInTiles; x++)
                room.SetPositionTileAndCollision(new(x * 16 + 8,y * 16 + 8),floor,0,0);
        }
        try
        {
            foreach (bool sword in new[] { false,true })
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Load(0,game == OracleGame.Ages ? 0x00 : 0xa6);
                entities.Clear(); player.Inventory.EquipA(0); player.Inventory.EquipB(0);
                FailIf(!transitions.TryGetScreenTransitionDestination(Vector2I.Down,out int destination) ||
                    rooms.RequiresRoomPackFade(rooms.ActiveGroup,rooms.CurrentRoom.Id,destination),
                    $"{game}: ledge scroll fixture requires an actual south neighbor in the same room pack.");
                var source = rooms.CurrentRoom;
                TargetFloor(rooms.ActiveGroup,source);
                for (int x = 0; x < source.WidthInTiles; x++)
                    source.SetPositionTileAndCollision(new(x * 16 + 8,120),(byte)(game == OracleGame.Ages ? 0x64 : 0x54),0x0f,0);
                player.WarpTo(new(80.25f,104.5f)); player.Face(sword ? Vector2I.Up : Vector2I.Down);
                if (sword) { player.Inventory.GiveTreasure(TreasureId.Sword,1); player.Inventory.EquipA(TreasureId.Sword); }
                FailIf(player.World.Collides(player.PrecisePosition),"Cross-screen ledge approach must start on reachable dry floor.");
                var native = new BraceletRom(source,player.PrecisePosition,sword ? 0 : 2,0xff,game:game);
                native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
                native[0xd009] = unchecked((byte)SomariaPrivate<int>(player,"_topDownMovementAngle"));
                native[0xcc2d] = (byte)rooms.ActiveGroup; native[0xcd00] = 1; native[0xcd0d] = 121;
                void Compare(string phase)
                {
                    int z = player.LedgeJumpPhase == LedgeJumpState.None ? player.ItemCreationZFixed : SomariaPrivate<int>(player,"_ledgeZFixed");
                    FailIf(player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f,native.Word(0xd00a) / 256f) ||
                        (ushort)z != native.Word(0xd00e) || player.NativeInAirForInteraction != (native[0xcc5c] != 0),
                        $"{game}: cross-screen ledge {phase} differs from native Link state12, XY/Z/air: runtime={player.PrecisePosition}/${(ushort)z:x4}/{player.LedgeJumpPhase}, " +
                        $"native=({native.Word(0xd00c) / 256f},{native.Word(0xd00a) / 256f})/${native.Word(0xd00e):x4}/${native[0xd004]:x2}:${native[0xd005]:x2}, scroll=${native[0xcd00]:x2}.");
                    FailIf(player.IsAttacking != (native[0xd200] != 0) ||
                        player.IsAttacking && unchecked((byte)player.MeleeItemZ) != native[0xd60f],
                        $"{game}: cross-screen ledge {phase} lost retained sword parent/height.");
                }
                rooms.RoomChanged += TargetFloor;
                try
                {
                    if (sword)
                    {
                        app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched,
                            afterUpdate:() => { native.UpdateLinkItems(entities.FrameCounter,1); Compare("sword initialization"); });
                        app.Step(20,Vector2.Zero,held:["attack"],batched:batched,
                            afterUpdate:() => { native.UpdateLinkItems(entities.FrameCounter,1); Compare("sword hold"); });
                    }
                    for (int approach = 0; approach < 64 && player.LedgeJumpPhase == LedgeJumpState.None; approach++)
                        app.Step(1,Vector2.Down,held:sword ? ["attack"] : [],batched:batched,afterUpdate:() =>
                        { native.UpdateLinkItems(entities.FrameCounter,sword ? 0x81 : 0x80); Compare("approach/takeoff"); });
                    FailIf(player.LedgeJumpPhase != LedgeJumpState.AirborneBeforeScroll || native[0xd004] != 0x12,
                        $"{game}: boundary-crossing scan must select native state12 substate $02.");
                    for (int fall = 0; fall < 80 && !transitions.ScrollActive; fall++)
                        app.Step(1,Vector2.Zero,batched:batched,afterUpdate:() =>
                        { native.UpdateLinkItems(entities.FrameCounter,0); Compare("source fall"); });
                    FailIf(!transitions.ScrollActive || rooms.CurrentRoom.Id != destination || native[0xcd02] != 0x82 ||
                        player.LedgeJumpPhase != LedgeJumpState.WaitingForScroll,
                        $"{game}: terminal source fall must request native direction $82 and the actual shared south scroll.");
                    int speedZ = player.LedgeSpeedZ, animation = player.LedgeAnimationPhase, incoming = 0;
                    int swordFrame = player.SwordStateFrame;
                    entities.AddEntity(new ItemPhaseValidationEntity(() => incoming++));
                    native.BeginScroll(); native.CopyRoom(rooms.CurrentRoom); native[0xcc30] = (byte)destination;
                    // The real engine owns scrolling/graphics here. The native
                    // fixture verifies Link's wait and the declared coordinate
                    // handoff at completion, not bank1's VRAM row scheduler.
                    int scrollUpdates = 0;
                    while (transitions.ScrollActive && scrollUpdates < 80)
                    {
                        app.Step(1,Vector2.Zero,batched:batched);
                        scrollUpdates++;
                        FailIf(incoming != 0 || transitions.ScrollActive &&
                            (player.LedgeJumpPhase != LedgeJumpState.WaitingForScroll || player.LedgeSpeedZ != speedZ ||
                             player.LedgeAnimationPhase != animation),$"{game}: actual scrolling must freeze destination actors and the ledge handler.");
                        FailIf(sword && (!player.IsAttacking || player.SwordStateFrame != swordFrame),
                            $"{game}: the actual ledge scroll must retain and freeze the sword parent.");
                        if (transitions.ScrollActive) native.UpdateLinkItems(entities.FrameCounter,0);
                    }
                    FailIf(transitions.ScrollActive || scrollUpdates != 39 || player.LedgeJumpPhase != LedgeJumpState.AirborneAfterScroll,
                        $"{game}: ledge scroll must retain setup, 32 motion updates and cleanup before resuming the destination scan.");
                    native.FinishScroll();
                    native[0xcd00] = 1;
                    native.Word(0xd00a,(9 << 8) | (native.Word(0xd00a) & 255));
                    native.UpdateLinkItems(entities.FrameCounter,0);
                    Compare("destination resumption");
                    for (int landing = 0; landing < 80 && player.LedgeJumpPhase != LedgeJumpState.None; landing++)
                        app.Step(1,Vector2.Zero,batched:batched,afterUpdate:() =>
                        { native.UpdateLinkItems(entities.FrameCounter,0); Compare("destination fall/landing"); });
                    FailIf(player.LedgeJumpPhase != LedgeJumpState.None || native[0xd004] != 1,
                        $"{game}: the retained-speed destination fall must finish in ordinary Link state01.");
                    app.Step(2,Vector2.Zero,batched:batched,afterUpdate:() =>
                    { native.UpdateLinkItems(entities.FrameCounter,0); Compare("normal control after landing"); });
                    FailIf(player.IsAttacking || native[0xd200] != 0,$"{game}: released held sword must clear after post-scroll normal control resumes.");
                    FailIf(incoming == 0,$"{game}: destination actors must resume after the scroll finishes.");
                }
                finally { rooms.RoomChanged -= TargetFloor; }
            }
        }
        finally { Load(originalGroup,originalRoom); entities.Clear(); player.WarpTo(new(72,80)); }
    }
}
