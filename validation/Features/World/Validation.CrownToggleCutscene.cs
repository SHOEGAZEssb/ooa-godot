using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownToggleCutscene()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool fullPool in new[] { false, true })
        {
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0);
            LoadValidationRoom(4, 0xa1);
            _inventory.RefillHealth();
            _inventory.GiveTreasure(TreasureDatabase.TreasureSword, 0);
            _inventory.EquipA(InventoryState.ItemSword);
            _player.WarpTo(new(120, 56));
            var orb = _entities.Entities<DungeonOrbRoomEntity>().Single();
            var toggle = _entities.FloorToggle!;
            StepGameplayUpdates(16, Vector2.Left, batched: batched);
            FailIf(_collision.Collides(_player.Position), "Crown orb approach must stay on actual floor.");
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            for (int i = 0; !orb.PendingHit && i < 40; i++) StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!orb.PendingHit, "A real sword swing must hit the Crown $35 orb.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(toggle.State != 0 || !orb.IsOn || toggle.Frozen,
                "Orb dispatch must select cutscene02 after objects, before its state0 freeze.");
            Vector2 raised = new(88, 72), covered = new(72, 72);
            FailIf(_currentRoom.GetMetatile(raised) != 0x0e || _currentRoom.GetUnderlyingMetatile(covered) != 0x29,
                "Crown source $45/$44 must begin raised blue and lowered red respectively.");
            // Stage one movable block on the already-lowered red floor.
            // The final pass must use the underlying buffer, not tile $10.
            _currentRoom.SetPositionTileAndCollision(covered, 0x10, null, (long)_animationTicks);
            if (fullPool)
                while (_entities.InteractionSlotAvailable)
                    _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(200, 120), 0));
            Vector2 held = _player.Position;
            int lockout = orb.HitLockout;
            int sounds = _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose);
            StepGameplayUpdates(1, Vector2.Right);
            FailIf(toggle.State != 1 || toggle.Counter != 6 || _player.Position != held,
                "Toggle state0 must establish freeze and counter6 without moving Link.");
            StepGameplayUpdates(1, Vector2.Right);
            FailIf(toggle.State != 2 || toggle.Counter != 6 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != sounds + 1,
                "Toggle state1 must upload the intermediate graphics and sound without decrementing6.");
            StepGameplayUpdates(5, Vector2.Right, batched: batched);
            FailIf(toggle.Counter != 1 || _player.Position != held || orb.HitLockout != lockout ||
                _currentRoom.GetMetatile(raised) != 0x0e || _currentRoom.GetMetatile(covered) != 0x10,
                "Five delayed updates must freeze initialized parts and Link, retaining old tile collision.");
            StepGameplayUpdates(1, Vector2.Right);
            FailIf(toggle.Active || orb.HitLockout != lockout - 1 ||
                _currentRoom.GetMetatile(raised) != 0x28 || _currentRoom.GetTerrainInfo(raised).Collision != 0 ||
                _currentRoom.GetMetatile(covered) != 0x0f || _currentRoom.GetTerrainInfo(covered).Collision != 0x1e ||
                _currentRoom.GetUnderlyingMetatile(covered) != 0x0f ||
                _runtimeState.ReadWramByte(OracleRuntimeState.LastToggleBlocksStateAddress) != 1,
                $"Sixth delay update must rewrite buffers and restore updates: state={toggle.State}, Link={_player.Position}/{held}, raised=${_currentRoom.GetMetatile(raised):x2}/{_currentRoom.GetTerrainInfo(raised).Collision:x2}, covered=${_currentRoom.GetMetatile(covered):x2}/{_currentRoom.GetTerrainInfo(covered).Collision:x2}, buffer=${_currentRoom.GetUnderlyingMetatile(covered):x2}, last={_runtimeState.ReadWramByte(OracleRuntimeState.LastToggleBlocksStateAddress)}.");
            var debris = _entities.Entities<RockDebrisEffect>();
            FailIf(debris.Count != (fullPool ? 0 : 1) || !fullPool && debris[0].ElapsedUpdates != 1,
                "Covered-floor debris must honor the interaction pool and initialize in the completion object pass.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(toggle.Active, "An unchanged toggle byte must not start another cutscene.");
            // The sword still owns movement on the completion update. Once
            // it releases, approach and strike the same orb again.
            for (int i = 0; (_player.IsAttacking || orb.HitLockout > 0) && i < 60; i++)
                StepGameplayUpdates(1, Vector2.Zero);
            Vector2 released = _player.Position;
            StepGameplayUpdates(1, Vector2.Right);
            FailIf(_player.Position.X <= released.X, "Link must regain movement after the sword releases it.");
            StepGameplayUpdates(4, Vector2.Left);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            for (int i = 0; !orb.PendingHit && i < 40; i++) StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!orb.PendingHit, "The same Crown orb must remain hittable after completing its toggle.");
            StepGameplayUpdates(1, Vector2.Zero);
            StepGameplayUpdates(8, Vector2.Zero, batched: batched);
            FailIf(toggle.Active || orb.IsOn || _currentRoom.GetMetatile(raised) != 0x0e ||
                _currentRoom.GetMetatile(covered) != 0x29 ||
                _runtimeState.ReadWramByte(OracleRuntimeState.LastToggleBlocksStateAddress) != 0,
                "A repeated actual hit must restore both floor colors and complete the reverse toggle.");
        }
        _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0);
        LoadValidationRoom(0, 0x60);
    }
}
