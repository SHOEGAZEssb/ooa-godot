using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedPlayerWorld()
    {
        LoadValidationRoom(0,0x45);
        Check(_player,_rooms,_entities,_sound,_roomView);
        var root = new FrontendValidationRoot();
        AddChild(root);
        try
        {
            var selector = root.InitializeGameSelect();
            var save = OracleSaveData.CreateStandardGame(OracleGame.Seasons);
            save.SetLinkName("SEAS"); save.SetGlobalFlag(0x21); save.SetGlobalFlag(0x0a);
            save.SetDeathRespawnPoint(0,0xb6,3,2,0x48,0x78); save.SetRoomFlag(0,0xb6,0x50);
            root.SeedFileSlot(0,save);
            selector.RequestPointerSelection(OracleGame.Seasons,true); root.Step(1,false); root.CompleteBootLoading();
            root.Step(1,false); root.Step(1,false,"inventory"); root.Step(32,false);
            root.Step(1,false,"attack"); root.Step(1,false); root.Step(1,false,"attack"); root.Step(32,false);
            var session = root._seasonsOpening ?? throw new InvalidOperationException("Seasons did not bind the common player world.");
            Check(session.Link,session.Rooms,session.Entities,root._sound,session.RoomView);
        }
        finally { root.Free(); }

        void Check(Player player, RoomSession rooms, RoomEntityManager entities, OracleSoundEngine sound, RoomView view)
        {
            var memory = rooms.RuntimeState;
            FailIf(player.World is not PlayerWorld || player.World.FrameCounter != memory.FrameCounter,
                $"{memory.Game}: the production player must use shared world movement and the authoritative frame byte.");
            Vector2 actual = player.World.ApplyNativeUnblockedMovement(new Vector2(80.25f,64.5f),0x28,0x08);
            FailIf(actual != new Vector2(81.25f,64.5f), $"{memory.Game}: shared native unblocked movement lost its 8.8 fractions.");
            AssertMovementPublication(memory,0,0x100,$"{memory.Game}: actual shared player world");
            actual = player.World.ApplyNativeUnblockedMovement(actual,0x28,0x80);
            FailIf(actual != new Vector2(81.25f,64.5f), "An inactive native angle moved Link.");
            AssertMovementPublication(memory,0,0,$"{memory.Game}: inactive unblocked movement");
            int previous = player.World.TilePushingDirection;
            player.World.TilePushingDirection = 0x82;
            FailIf(memory.ReadField("wLinkPushingDirection") != 0x82, "Shared pushing publication must use live WRAM.");
            player.World.TilePushingDirection = previous;
            var foreign = OracleRuntimeState.CreateInitialized(memory.Game);
            var collision = new RoomCollision(() => rooms.CurrentRoom, (_,_) => false,foreign,_ => false,false);
            byte[] before = memory.CaptureState().Wram;
            bool rejected = false;
            try { _ = new WorldOwnerProbe(rooms,entities,collision,sound,((PlayerWorld)player.World).Combat, (GameplaySceneGraph)view.GetParent().GetParent()); }
            catch (ArgumentException) { rejected = true; }
            FailIf(!rejected || !before.SequenceEqual(memory.CaptureState().Wram),
                "A shared player world must reject a second collision-memory owner before modifying live state.");
            rejected = false;
            try { _ = new CombatController(rooms,view,entities,new BreakableTileDatabase(rooms.AssetRoot),
                OracleSaveData.CreateStandardGame(memory.Game),sound,() => 0); }
            catch (ArgumentException) { rejected = true; }
            FailIf(!rejected,"The shared combat binding must reject a second save owner even when the game IDs match.");
        }
    }

    private sealed class WorldOwnerProbe(RoomSession rooms, RoomEntityManager entities, RoomCollision collision, OracleSoundEngine sound, CombatController combat, GameplaySceneGraph scene)
        : PlayerWorld(rooms,((PlayerWorld)scene.Player.World).Transitions,entities,collision,sound,() => false,combat,scene) { }
}
