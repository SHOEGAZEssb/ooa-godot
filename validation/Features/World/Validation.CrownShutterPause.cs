using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownShutterPause()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var state = typeof(DungeonDoorRoomEntity).GetField("_state", flags)!;
        var counter = typeof(DungeonDoorRoomEntity).GetField("_counter", flags)!;
        var death = typeof(Player).GetField("_deathPending", flags)!;
        foreach (bool batch in new[] { false, true })
        {
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, [], [], batch);
            // updateInteractions allows state0 under text or DISABLE_INTERACTIONS;
            // interactionRunScript independently refuses active text.
            foreach (bool text in new[] { false, true })
            {
                LoadValidationRoom(4, 0x9d);
                _player.WarpTo(new(120, 136));
                var door = _entities.Entities<DungeonDoorRoomEntity>().Single();
                var freeze = new CrownEntranceFreeze { FreezesRoomEntities = !text };
                typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [freeze]);
                if (text) _dialogue.ShowMessage("Shutter initialization.", 120);
                Step(3);
                DoorState expected = text ? DoorState.SetRadii : DoorState.SetAngle;
                FailIf((DoorState)state.GetValue(door)! != expected || door.UpdatesDuringDialogue || door.UpdatesDuringRoomEntityFreeze,
                    "Shutter state0 must run once under masks, preserving the separate text gate on its first script command.");
                _dialogue.Close();
                freeze.FreezesRoomEntities = false;
                Step();
                FailIf((DoorState)state.GetValue(door)! != (text ? DoorState.SetAngle : DoorState.InitialBranch),
                    "Clearing the mask must resume the first pending shutter command without restarting initialization.");
            }

            // Isolate wLinkDeathTrigger at the native object-dispatch boundary.
            // Do not advance the separate player death cutscene in this check.
            void Objects(int count)
            {
                if (batch) _entities.Update(count / 60.0, _player);
                else for (int i = 0; i < count; i++) _entities.Update(1.0 / 60, _player);
            }
            foreach (bool enemyDoor in new[] { false, true })
            {
                LoadValidationRoom(4, enemyDoor ? 0xb6 : 0x9d);
                _player.WarpTo(new(120, 136));
                var door = _entities.Entities<DungeonDoorRoomEntity>().Single();
                Vector2 position = door.Position;
                death.SetValue(_player, true);
                try
                {
                    Objects(3);
                    FailIf((DoorState)state.GetValue(door)! != DoorState.SetRadii,
                        "Death must allow shutter initialization but hold the script before setcollisionradii.");
                    // Model state2 already selected before death: its animation
                    // has no interactionRunScript call until completion.
                    state.SetValue(door, DoorState.ReadyToOpen);
                    Objects(1);
                    FailIf((int)counter.GetValue(door)! != 6,
                        "Death must not suppress an already-selected opening animation.");
                    Objects(5);
                    FailIf((int)counter.GetValue(door)! != 1 || !_currentRoom.IsSolid(position),
                        "Opening under death must retain its full six-update collision delay.");
                    Objects(1);
                    DoorState waiting = enemyDoor ? DoorState.ScriptEnd : DoorState.WatchingTrigger;
                    FailIf(_currentRoom.IsSolid(position) || door.Finished || (DoorState)state.GetValue(door)! != waiting,
                        "Animation must finish during death, but its resumed script must neither branch nor delete.");
                    Objects(3);
                    FailIf(door.Finished || (DoorState)state.GetValue(door)! != waiting,
                        "The resumed shutter script must remain paused while death stays active.");
                }
                finally { death.SetValue(_player, false); }
                Objects(1);
                FailIf(enemyDoor ? !door.Finished : (DoorState)state.GetValue(door)! != DoorState.SelectClosing,
                    "Clearing death must resume scriptend or the trigger decision on the next object update.");
            }
        }
        LoadValidationRoom(0, 0x60);
    }
}
