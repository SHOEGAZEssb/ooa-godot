using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownShutterPalette()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var state = typeof(DungeonDoorRoomEntity).GetField("_state", flags)!;
        var counter = typeof(DungeonDoorRoomEntity).GetField("_counter", flags)!;
        Func<bool> previous = _entities.PaletteFadeActiveSource;
        try
        {
            foreach (bool batch in new[] { false, true })
            foreach (bool opening in new[] { false, true })
            {
                LoadValidationRoom(4, 0x9d);
                _player.WarpTo(new(120, 136));
                var door = _entities.Entities<DungeonDoorRoomEntity>().Single();
                bool fading = true;
                _entities.PaletteFadeActiveSource = () => fading;
                void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, [], [], batch);
                _currentRoom.SetPositionTileAndCollision(door.Position, (byte)(opening ? 0x7a : 0xa0), null, 0);
                // Isolate an already-selected native state2/state3. Only state2
                // reads wPaletteThread_mode, before dispatching either substate.
                DoorState initial = opening ? DoorState.ReadyToOpen : DoorState.ReadyToClose;
                state.SetValue(door, initial);
                Step(3);
                FailIf(opening
                    ? (DoorState)state.GetValue(door)! != initial || (int)counter.GetValue(door)! != 0
                    : (DoorState)state.GetValue(door)! != DoorState.ClosingInterleaved || (int)counter.GetValue(door)! != 4,
                    "Palette fades must hold opening before interleaving, but allow closing to advance.");
                if (opening)
                {
                    fading = false;
                    Step(1);
                    Step(2);
                    FailIf((int)counter.GetValue(door)! != 4, "Opening must resume its six-update timer after the fade.");
                    fading = true;
                    Step(3);
                    FailIf((int)counter.GetValue(door)! != 4 || !_currentRoom.IsSolid(door.Position),
                        "A second fade must hold the opening counter and old collision.");
                    fading = false;
                }
                Step(3);
                FailIf((int)counter.GetValue(door)! != 1, "Shutter completion must retain the final counter update.");
                Step(1);
                FailIf(_currentRoom.IsSolid(door.Position) == opening,
                    "Opening must complete after the fade; closing must complete during it.");
            }
            // Exercise the production room-local fade owner through PART then
            // INTERACTION dispatch, without substituting the external signal.
            _entities.PaletteFadeActiveSource = previous;
            foreach (bool batch in new[] { false, true })
            foreach (bool delayed in new[] { false, true })
            {
                LoadValidationRoom(4, 0x9d);
                _player.WarpTo(new(120, 136));
                var sourceDoor = _entities.Entities<DungeonDoorRoomEntity>().Single();
                state.SetValue(sourceDoor, DoorState.ReadyToOpen);
                var warp = new Warp(4, 0x9d, -1, 0, 2, 4, 0x9d, 0x87, 0, 0);
                if (delayed) _transitions.ApplyWarpWithDelayedFadeOut(_player, warp);
                else _transitions.ApplyWarpWithFadeOut(_player, warp);
                void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, [], [], batch);
                // fadeoutToWhite terminates on32; delay4 terminates on125.
                Step(delayed ? 124 : 31);
                FailIf(!_transitions.PaletteFadeActive ||
                    (DoorState)state.GetValue(sourceDoor)! != DoorState.ReadyToOpen ||
                    !_currentRoom.IsSolid(sourceDoor.Position),
                    "Source shutter must remain closed through the nonterminal warp-fade updates.");
                Step(1);
                var destinationDoor = _entities.Entities<DungeonDoorRoomEntity>().Single();
                FailIf(ReferenceEquals(sourceDoor, destinationDoor) || !_transitions.PaletteFadeActive,
                    "Warp load must replace source objects and begin the destination palette thread.");
                state.SetValue(destinationDoor, DoorState.ReadyToOpen);
                Step(31);
                FailIf(!_transitions.PaletteFadeActive ||
                    (DoorState)state.GetValue(destinationDoor)! != DoorState.ReadyToOpen,
                    "Destination shutter must not open merely because the fade already looks transparent.");
                Step(1);
                FailIf(_transitions.PaletteFadeActive || (int)counter.GetValue(destinationDoor)! != 6,
                    "The terminal fade-in update must release shutter opening in the gameplay loop.");
                Step(5);
                FailIf((int)counter.GetValue(destinationDoor)! != 1 || !_currentRoom.IsSolid(destinationDoor.Position),
                    "Warp arrival must preserve the shutter's full six-update collision delay.");
                Step(1);
                FailIf(_currentRoom.IsSolid(destinationDoor.Position),
                    "The arrival shutter must finish opening after its six eligible updates.");
            }
            foreach (bool batch in new[] { false, true })
            {
                LoadValidationRoom(4, 0x9d);
                _player.WarpTo(new(120, 136));
                var door = _entities.Entities<DungeonDoorRoomEntity>().Single();
                var data = new DarkRoomDatabase();
                var record = data.GetRoomRecords(5, 0xed).Single(r => r.Kind == DarkRoomDatabaseObjectKind.Handler);
                var fade = new DarkRoomState(_currentRoom, data);
                var handler = new DarkRoomHandlerRoomEntity(record, _currentRoom, data, fade);
                typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [handler]);
                state.SetValue(door, DoorState.ReadyToOpen);
                // darkenRoom's $f0 to brightenRoom's $00, speed$01: 16 updates.
                fade.BeginBrighten(0);
                StepGameplayUpdates(15, Vector2.Zero, [], [], batch);
                FailIf(!fade.FadeActive || (DoorState)state.GetValue(door)! != DoorState.ReadyToOpen,
                    "The live dark-room palette owner must hold opening for all 15 nonterminal updates.");
                StepGameplayUpdates(1, Vector2.Zero, [], [], batch);
                FailIf(fade.FadeActive || (int)counter.GetValue(door)! != 6,
                    "PART fade completion must release the shutter in the same object update.");
            }
        }
        finally
        {
            _entities.PaletteFadeActiveSource = previous;
            LoadValidationRoom(0, 0x60);
        }
    }
}
