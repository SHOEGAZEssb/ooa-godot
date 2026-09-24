using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownKeyDoorDeath()
    {
        Vector2 center = new(232, 136);
        void Allocate()
        {
            _saveData.SetRoomFlag(4, 0xb3, 2, false);
            LoadValidationRoom(4, 0xb3);
            _entities.Clear();
            _player.WarpTo(new(220, 136));
            _inventory.RefillHealth();
            while (_inventory.TryUseDungeonSmallKey(5)) { }
            _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03"));
            for (int i = 0; i < 10; i++)
                _keyDoors.UpdatePushAttempt(_player.Position, Vector2I.Right, Vector2.Right);
        }
        var death = typeof(Player).GetField("_deathPending", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (bool batch in new[] { false, true })
        foreach (bool beforeInitialization in new[] { false, true })
        {
            Allocate();
            void Objects(int count)
            {
                if (batch) _entities.Update(count / 60.0, _player);
                else for (int i = 0; i < count; i++) _entities.Update(1.0 / 60, _player);
            }
            if (!beforeInitialization) Objects(1);
            // Isolate bank0 interactionRunScript's wLinkDeathTrigger gate;
            // the actual lethal-damage/application handoff is checked below.
            death.SetValue(_player, true);
            try
            {
                Objects(beforeInitialization ? 3 : 7);
                FailIf(!_keyDoors.Opening || _keyDoors.OpeningCounter != 0 ||
                    _currentRoom.IsSolid(center) != beforeInitialization,
                    "Death must hold incstate before animation, or scriptend after an already-selected animation.");
                Objects(3);
                FailIf(!_keyDoors.Opening || _currentRoom.IsSolid(center) != beforeInitialization,
                    "A key-door script must remain pending while death stays active.");
            }
            finally { death.SetValue(_player, false); }
            Objects(1);
            FailIf(_keyDoors.Opening != beforeInitialization ||
                _currentRoom.IsSolid(center) != beforeInitialization,
                "Clearing death must resume exactly the pending incstate or scriptend command.");
            if (beforeInitialization)
            {
                Objects(7);
                FailIf(_keyDoors.Opening || _currentRoom.IsSolid(center),
                    "The resumed key-door script must finish its own start plus six animation updates.");
            }
        }

        Allocate();
        StepGameplayUpdates(1, Vector2.Zero);
        FailIf(!_player.ApplyDamage(_player.MaxHealthQuarters) || !_player.IsDying,
            "The door death fixture must publish lethal damage before the next Link update.");
        StepGameplayUpdates(7, Vector2.Zero, batched: true);
        FailIf(!_player.DeathAnimationActive || !_keyDoors.Opening ||
            _currentRoom.IsSolid(center) || _keyDoors.OpeningCounter != 0 ||
            _inventory.GetDungeonSmallKeys(5) != 0,
            "Actual Link death must allow door animation to complete while retaining its pending scriptend and key debit.");
        StepGameplayUpdates(3, Vector2.Zero);
        FailIf(!_player.DeathAnimationActive || !_keyDoors.Opening,
            "The pending reserved door must not interrupt death or delete through its script gate.");
    }
}
