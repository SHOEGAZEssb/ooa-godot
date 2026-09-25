using Godot;
using System;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTopDownAirSteering()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var angleField = typeof(Player).GetField("_topDownAirAngle", flags)!;
        var speedField = typeof(Player).GetField("_topDownAirSpeedRaw", flags)!;
        int Angle() => (int)angleField.GetValue(_player)!;
        int Speed() => (int)speedField.GetValue(_player)!;
        _inventory.GiveTreasure(TreasureDatabase.TreasureFeather, 1);
        _inventory.GiveTreasure(TreasureDatabase.TreasureSword, 0);
        foreach (bool batch in new[] { false, true })
        foreach (string scenario in new[] { "turn", "reverse", "neutral", "sword" })
        {
            void Step(int count = 1, Vector2 movement = default, bool attack = false) =>
                StepGameplayUpdates(count, movement, attack ? ["attack"] : [], attack ? ["attack"] : [], batched: batch);
            LoadValidationRoom(4, 0x91);
            _player.WarpTo(new Vector2(120, 144));
            Step(32, Vector2.Up);
            FailIf(_player.Position != new Vector2(120, 112), "Air-steering fixture must walk through Skull Dungeon's actual entrance.");
            _inventory.EquipA(InventoryState.ItemFeather);
            Step(movement: Vector2.Right, attack: true);
            FailIf(!_player.TopDownAirborne || Angle() != 8 || Speed() != 0x28 || _player.TopDownAirSpeedZ != -0x1c0,
                "Feather must start at angle $08/SPEED_100 and consume the first gravity-$20 update.");
            Vector2 direction = scenario == "neutral" ? Vector2.Zero : scenario == "reverse" ? Vector2.Left : Vector2.Up;
            if (scenario == "sword")
            {
                _inventory.EquipA(InventoryState.ItemSword);
                Step(movement: direction, attack: true);
                Step(12, direction);
            }
            else Step(13, direction);
            FailIf(Angle() != 8 || Speed() != 0x28 || _player.TopDownAirSpeedZ != -0x20,
                $"{scenario}: rising Link must retain his takeoff velocity through update14, including a newly started sword.");
            Step(movement: direction);
            FailIf(_player.TopDownAirSpeedZ != 0 || Angle() != (scenario is "turn" or "sword" ? 7 : 8) ||
                Speed() != (scenario == "reverse" ? 0x23 : 0x28),
                $"{scenario}: update15 must run func_5933 after speedZ becomes nonnegative, before movement.");
            Step(7, direction);
            int expectedAngle = scenario == "reverse" ? 0xff : scenario == "neutral" ? 8 : 0;
            FailIf(Angle() != expectedAngle || Speed() != (scenario == "reverse" ? 0 : 0x28),
                $"{scenario}: eight descending updates must turn by source angles, brake by $05, or retain neutral momentum.");
            if (scenario == "reverse")
            {
                Step(movement: direction);
                FailIf(Angle() != 0x18 || Speed() != 0, "A stopped airborne angle must adopt input before accelerating on the following update.");
                Step(movement: direction);
                FailIf(Angle() != 0x18 || Speed() != 5, "Reversed descending Link must resume at raw speed $05.");
            }
            Step(12);
            FailIf(_player.TopDownAirborne || _player.IsFallingInHole || _player.IsDying,
                "Air-steering fixture must finish on the actual entrance floor.");
            for (int i = 0; _player.IsAttacking && i < 60; i++) Step();
            _inventory.EquipA(InventoryState.ItemFeather);
            Step(movement: Vector2.Left, attack: true);
            LoadValidationRoom(4, 0x91);
            FailIf(_player.TopDownAirborne || Angle() != 0xff || Speed() != 0,
                "Room replacement must clear the previous jump's steering and velocity.");
        }
    }
}
