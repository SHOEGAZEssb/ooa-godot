using Godot;
using System;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownKeyDoorSwitchHook()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var state = typeof(DungeonKeyDoorController).GetField("_openingState", flags)!;
        foreach (bool batch in new[] { false, true })
        foreach (bool cancel in new[] { false, true })
        foreach (bool initializeDuringLift in new[] { false, true })
        {
            void Step(int count = 1, bool attack = false) => StepGameplayUpdates(
                count, Vector2.Zero, attack ? ["attack"] : [], attack ? ["attack"] : [], batch);
            _inventory.GiveTreasure(TreasureId.SwitchHook, 1);
            _inventory.EquipA(TreasureId.SwitchHook);
            LoadValidationRoom(4, 0x7c);
            Vector2 origin = new(184, 88);
            Vector2 doorPosition = new(120, 168);
            FailIf(_collision.Collides(origin) || _currentRoom.GetMetatile(new(136, 88)) != 0xdb,
                "Reserved-door exchange fixture requires the real room4:7c floor and diamond.");
            _player.WarpTo(origin);
            _player.Face(Vector2I.Left);
            Step(20);
            Step(attack: true);
            var hook = _entities.SwitchHook!;
            for (int i = 0; hook.Item is { State: 1 } && i < 50; i++) Step();
            FailIf(hook.ExchangeState != 1, "The actual hook must enter its latch before the reserved-door check.");
            Step(initializeDuringLift ? 18 : 15);

            // Isolate an allocated reserved INTERAC$1e:$00, away from the
            // actual hook target. Key use/approach is covered by CrownKeyLocks;
            // this setup checks dispatch ordering, not a combined puzzle route.
            FailIf(!_rooms.KeyDoors.TryGet(_currentRoom.ActiveCollisions, 0x70, out var door),
                "The exchange fixture requires the imported upward key-door record.");
            _currentRoom.SetPositionTileAndCollision(doorPosition, 0x70, null, (long)_animationTicks);
            typeof(DungeonKeyDoorController).GetField("_door", flags)!.SetValue(_keyDoors, door);
            typeof(DungeonKeyDoorController).GetField("_doorCenter", flags)!.SetValue(_keyDoors, doorPosition);
            typeof(DungeonKeyDoorController).GetField("_opening", flags)!.SetValue(_keyDoors, true);
            state.SetValue(_keyDoors, Enum.Parse(state.FieldType, "Initialize"));
            if (!initializeDuringLift)
            {
                Step(2);
                FailIf(hook.ExchangeState != 1 || _keyDoors.OpeningCounter != 6,
                    "Reserved door must initialize and start its animation during hook state$01.");
                Step();
            }
            int heldCounter = initializeDuringLift ? 0 : 6;
            FailIf(hook.ExchangeState != 2 || _keyDoors.OpeningCounter != heldCounter,
                "The item pass must publish hook state$02 before the reserved interaction dispatch.");
            Step(cancel ? 5 : 32);
            FailIf(hook.ExchangeState != 2 || _keyDoors.OpeningCounter != heldCounter ||
                state.GetValue(_keyDoors)!.ToString() != (initializeDuringLift ? "Initialize" : "Animate"),
                "Switch Hook lift/swap/lowering must hold both state0 and an active door timer.");
            if (cancel) _player.WarpTo(origin);
            Step();
            FailIf(hook.ExchangeState != 0 || _keyDoors.OpeningCounter != (initializeDuringLift ? 0 : 5),
                "Completion/cancellation must release the reserved door on the first hook-state$00 update.");
            if (initializeDuringLift)
            {
                FailIf(state.GetValue(_keyDoors)!.ToString() != "Ready",
                    "A deferred reserved door must initialize exactly once after lift release.");
                Step();
                FailIf(_keyDoors.OpeningCounter != 6, "The deferred opener must start a fresh six-update animation.");
            }
            Step(initializeDuringLift ? 5 : 4);
            FailIf(!_keyDoors.Opening || _keyDoors.OpeningCounter != 1 || !_currentRoom.IsSolid(doorPosition),
                "The reserved door must retain collision until its sixth eligible animation update.");
            Step();
            FailIf(_keyDoors.Opening || _currentRoom.IsSolid(doorPosition),
                "The resumed reserved door must finish normally after its final timer update.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
