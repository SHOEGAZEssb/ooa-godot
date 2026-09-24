using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownStairGates()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int gate in new[] { 0x01, 0x80, -1, -2 })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _player.ApplicationUpdateOwned = true;
            _entities.Clear();
            _player.WarpTo(new(120, 40));
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetMetatile(new(120, 24)) != 0x44,
                "Crown stair gate fixture must use the actual4:a1/$17 approach.");
            StepGameplayUpdates(14, Vector2.Up, batched: batched);
            FailIf(IsTransitioning || _player.Position != new Vector2(120, 26),
                "The approach must stop one update before stair activation.");
            bool published = false;
            var observer = new ItemPhaseValidationEntity(() =>
            {
                if (published) return;
                published = true;
                FailIf(_player.Position != new Vector2(120, 25),
                    "The gate must be published after Link reaches the stair, in the later object pass.");
                if (gate == -2) _entities.LockSmogLinkAndMenu();
                else if (gate == -1) _dialogue.ShowMessage("Stair gate.", 120);
                else _runtimeState.SetWramByte(OracleRuntimeState.WarpsDisabledAddress, (byte)gate);
            });
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(RoomEntityManager).GetMethod("RegisterEnemySlot", flags)!.Invoke(_entities, [observer, 0]);
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [observer]);
            int sounds = _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave);
            StepGameplayUpdates(1, Vector2.Up);
            FailIf(!published || IsTransitioning || _currentRoom.Id != 0xa1 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != sounds,
                $"The post-object tile-warp dispatcher must observe gate {gate} before stair lookup.");
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(IsTransitioning || _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != sounds,
                "A retained gate must prevent stair activation without consuming it.");
            if (gate == -2)
            {
                FailIf(!_entities.PlayerMenusDisabled || !_entities.WarpTilesDisabled,
                    "The rejected stair must preserve the INTERAC$33 menu-lock owner.");
                typeof(RoomEntityManager).GetMethod("ReleaseSmogLinkAndMenu", flags)!.Invoke(_entities, null);
            }
            else if (gate == -1)
            {
                FailIf(!_dialogue.IsOpen, "The rejected stair must preserve the active textbox.");
                _dialogue.Close();
            }
            else
            {
                FailIf(_runtimeState.ReadWramByte(OracleRuntimeState.WarpsDisabledAddress) != gate,
                    "Stair rejection must not clear its owner's warp-disable byte.");
                _runtimeState.SetWramByte(OracleRuntimeState.WarpsDisabledAddress, 0);
            }
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!IsTransitioning || _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != sounds + 1,
                "Clearing the owner gate must allow the same stair on the next eligible update.");
        }
        ReinitializeGameplayForValidation();
    }
}
