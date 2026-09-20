using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRollingRidgeButtonBridges()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        var data = new DungeonMechanicDatabase();
        static Vector2 Point(int p) => new((p & 15) * 16 + 8, (p >> 4) * 16 + 8);
        // Source $dc:$0c: BC=$0801/E=$56; $0d: BC=$0603/E=$28.
        foreach (bool batch in new[] { false, true })
        foreach (var setup in new[] { (Room: 0xc2, Button: 0x45, Start: 0x56, Count: 8, Direction: 1, Half: 0x6e),
            (Room: 0xe3, Button: 0x19, Start: 0x28, Count: 6, Direction: -1, Half: 0x6f) })
        {
            void Step(int count = 1, Vector2 movement = default)
            {
                input.CaptureForValidation([], [], movement);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60, update);
            }
            void Enter()
            {
                _saveData.SetRoomFlag(5, setup.Room, 0x80, false);
                LoadValidationRoom(5, setup.Room);
                _player.WarpTo(Point(setup.Button) + Vector2.Down * 20);
                FailIf(_currentRoom.IsSolid(_player.Position), $"5:{setup.Room:x2} button approach must start on walkable ground.");
                Step();
            }
            void Press()
            {
                for (int i = 0; i < 32 && _entities.ActiveTriggers == 0; i++) Step(1, Vector2.Up);
                FailIf(_entities.ActiveTriggers == 0 || !_saveData.HasRoomFlag(5, setup.Room, 0x80) ||
                    _entities.Entities<RidgeBridgeControllerRoomEntity>().Count != 0 ||
                    _entities.Entities<BridgeSpawnerRoomEntity>().Count != 1,
                    $"5:{setup.Room:x2} button must start $dc bridge in the same gameplay update; Link={_player.Position}.");
            }
            Enter();
            Step(8);
            FailIf(_saveData.HasRoomFlag(5, setup.Room, 0x80), "$dc bridge triggered before the button.");
            var original = Enumerable.Range(0, setup.Count / 2).Select(i =>
                _currentRoom.GetMetatile(Point(setup.Start + i * setup.Direction))).ToArray();
            _sound.ClearPlayRequestAudit();
            Press();
            FailIf(_sound.PlayRequestsFor(data.SolveSound) != 1 ||
                _currentRoom.GetMetatile(Point(setup.Start)) != original[0],
                "$dc bridge must persist and chime at allocation, before PART $0c starts.");
            for (int half = 0; half < setup.Count; half++)
            {
                Vector2 point = Point(setup.Start + half / 2 * setup.Direction);
                byte before = _currentRoom.GetMetatile(point);
                Step(7);
                FailIf(_currentRoom.GetMetatile(point) != before, "$0c bridge changed before update eight.");
                Step();
                int expected = (half & 1) == 0 ? setup.Half : 0x6d;
                FailIf(_currentRoom.GetMetatile(point) != expected || _currentRoom.GetUnderlyingMetatile(point) != expected ||
                    _sound.PlayRequestsFor(data.DoorSound) != half + 1,
                    $"5:{setup.Room:x2} bridge half {half} lost its direction, layout buffer, or sound.");
            }
            FailIf(_entities.Entities<BridgeSpawnerRoomEntity>().Count != 0, "PART $0c did not finish after its final half-tile.");
            Step(20, Vector2.Down); Step(20, Vector2.Up);
            FailIf(_sound.PlayRequestsFor(data.SolveSound) != 1 || _entities.Entities<BridgeSpawnerRoomEntity>().Count != 0,
                "Repeated button pressure restarted a completed bridge.");
            FailIf(!OracleSaveData.TryDeserialize(_saveData.Serialize(), out var restored) ||
                !restored!.HasRoomFlag(5, setup.Room, 0x80), "Bridge room flag $80 did not survive explicit serialization.");
            // Interrupt halfway through construction. The original room tile
            // initializer restores the entire bridge from the allocation flag.
            Enter(); Press(); Step(8);
            LoadValidationRoom(2, 0x9e);
            LoadValidationRoom(5, setup.Room);
            _player.WarpTo(Point(setup.Button));
            _sound.ClearPlayRequestAudit(); Step(20);
            FailIf(_entities.Entities<RidgeBridgeControllerRoomEntity>().Count != 0 ||
                _entities.Entities<BridgeSpawnerRoomEntity>().Count != 0 ||
                Enumerable.Range(0, setup.Count / 2).Any(i =>
                    _currentRoom.GetMetatile(Point(setup.Start + i * setup.Direction)) != 0x6d) ||
                _sound.PlayRequestsFor(data.SolveSound) != 0 || _sound.PlayRequestsFor(data.DoorSound) != 0,
                $"5:{setup.Room:x2} re-entry must restore the full bridge without replaying construction.");
        }
    }
}
