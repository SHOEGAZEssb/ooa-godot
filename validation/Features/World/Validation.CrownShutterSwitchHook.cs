using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownShutterSwitchHook()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var counter = typeof(DungeonDoorRoomEntity).GetField("_counter", flags)!;
        var state = typeof(DungeonDoorRoomEntity).GetField("_state", flags)!;
        var data = new DungeonMechanicDatabase();
        foreach (bool batch in new[] { false, true })
        foreach (bool cancel in new[] { false, true })
        foreach (bool opening in new[] { false, true })
        {
            void Step(int count = 1, bool attack = false) =>
                StepGameplayUpdates(count, Vector2.Zero, attack ? ["attack"] : [], attack ? ["attack"] : [], batched: batch);
            _inventory.GiveTreasure(TreasureId.SwitchHook, 1);
            _inventory.EquipA(TreasureId.SwitchHook);
            LoadValidationRoom(4, 0x7c);
            Vector2 origin = new(184, 88);
            FailIf(_collision.Collides(origin) || _currentRoom.GetMetatile(new(136, 88)) != 0xdb,
                "Shutter exchange fixture requires room4:7c's real floor and diamond.");
            _player.WarpTo(origin);
            _player.Face(Vector2I.Left);
            bool trigger = !opening;
            // Place Crown's trigger shutter in this isolated exchange fixture.
            // Its bottom-wall tile is away from Link and the source diamond.
            var door = new DungeonDoorRoomEntity(data.GetRoomRecords(4, 0x9d).Single(r => r.Id == 0x1e),
                _currentRoom, data, () => 1, _ => trigger, p => p, () => 0, _ => { }, default, true);
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [door]);
            Step(20);
            Step(attack: true);
            var controller = _entities.SwitchHook!;
            for (int i = 0; controller.Item is { State: 1 } && i < 50; i++) Step();
            FailIf(controller.ExchangeState != 1, "Actual hook must enter its latch before shutter testing.");
            Step(opening ? 13 : 14);
            trigger = opening;
            Step(opening ? 4 : 3);
            DoorState interleaved = opening ? DoorState.OpeningInterleaved : DoorState.ClosingInterleaved;
            FailIf(controller.ExchangeState != 1 || (DoorState)state.GetValue(door)! != interleaved ||
                (int)counter.GetValue(door)! != 6,
                "Shutter must start its six-update animation while Switch Hook state is$01.");
            Step();
            FailIf(controller.ExchangeState != 2 || (int)counter.GetValue(door)! != 6,
                "The ITEM pass must publish state$02 before the shutter's INTERACTION pass on lift entry.");
            Step(cancel ? 5 : 32);
            FailIf(controller.ExchangeState != 2 || (int)counter.GetValue(door)! != 6,
                "Lift, swap and lowering must freeze the shutter's animation counter.");
            if (cancel) _player.WarpTo(origin);
            Step();
            FailIf(controller.ExchangeState != 0 || (int)counter.GetValue(door)! != 5,
                "Completion or cancellation must resume the shutter on the first update with state$00.");
            Step(4);
            FailIf((int)counter.GetValue(door)! != 1 || (DoorState)state.GetValue(door)! != interleaved,
                "Paused shutter completed before its remaining six updates elapsed.");
            Step();
            FailIf(_currentRoom.IsSolid(door.Position) == opening || (DoorState)state.GetValue(door)! != DoorState.WatchingTrigger,
                "Shutter must finish opening/closing after its sixth eligible update.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
