using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkMovementScratch()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _player.WarpTo(new(120, 40));
            FailIf(_collision.Collides(_player.Position), "Link scratch check must start on Crown floor.");
            void Expect(params byte[] bytes)
            {
                for (int i = 0; i < 4; i++)
                    FailIf(_runtimeState.ReadWramByte(0xcec0 + i) != bytes[i],
                        $"Link movement scratch ${0xcec0 + i:x4}: expected ${bytes[i]:x2}, got ${_runtimeState.ReadWramByte(0xcec0 + i):x2}.");
            }
            byte[] expected = [0, 1, 0, 0]; // SPEED_100 down, before post-object warp writes $ff.
            int observations = 0;
            var observer = new ItemPhaseValidationEntity(() => { Expect(expected); observations++; });
            typeof(RoomEntityManager).GetMethod("RegisterEnemySlot", flags)!.Invoke(_entities, [observer, 0]);
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [observer]);
            StepGameplayUpdates(2, Vector2.Down, batched: batch);
            FailIf(observations != 2 || _player.Position != new Vector2(120, 42),
                "Normal walking must publish its velocity on each actual Link update.");
            for (int i = 1; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, 0xa5);
            expected = [0xff, 0xa5, 0xa5, 0xa5];
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(observations != 4, "Idle scratch preservation must be observed on both updates.");
            expected = [0, 0, 0x40, 1]; // linkUpdateKnockback: SPEED_140 right.
            FailIf(!_player.ApplyEnemyContactDamage(
                _player.EnemyContactPosition - new Vector2(16, 0), 0,
                RingDamageSource.Generic, knockbackFrames: 2, allowZeroDamage: true),
                "Scratch regression must enter recoil through accepted contact.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(observations != 6 || _player.KnockbackFrames != 0 ||
                _player.Position != new Vector2(122, 42),
                "Both recoil updates, including counter zero, must execute SPEED_140.");
            expected = [0xff, 0, 0x40, 1];
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(observations != 8, "Completed recoil must retain scratch on subsequent idle updates.");
            _entities.Clear();

            Vector2? corner = null;
            for (int y = 8; y < _currentRoom.Height - 8 && corner is null; y++)
            for (int x = 8; x < _currentRoom.Width - 8; x++)
            {
                Vector2 point = new(x, y);
                if (!_collision.Collides(point) && (_collision.AdjacentWallsBitset(point) & 0xc3) == 0x80 &&
                    _collision.ResolveMovement(point, Vector2.Up, true) == Vector2.Right)
                { corner = point; break; }
            }
            FailIf(corner is null, "Crown4:a1 must provide a reachable one-sided upper wall for the source slide check.");
            foreach (int angle in new[] { 31, 0, 1 })
            {
                _player.WarpTo(corner!.Value);
                for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, 0xa5);
                _collision.ResolveMovement(corner.Value, Vector2.Up, true);
                Expect(0xa5, 0xa5, 0xa5, 0xa5);
                _player.AdvanceInteractionVelocity(0x28, angle);
                Expect(0, 0, 0, 1);
                FailIf(_player.Position != corner.Value + Vector2.Right,
                    "slideAngleTable's 31/0/1 group must write the adjusted rightward vector before applying it.");
            }
            _player.AdvanceInteractionVelocity(0, 0);
            Expect(0, 0, 0, 0);
            for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, 0xa5);
            _player.AdvanceInteractionVelocity(0x28, 0xff);
            Expect(0xa5, 0xa5, 0xa5, 0xa5);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _player.WarpTo(corner!.Value);
                expected = [0, 0, 0x40, 1];
                var recoilObserver = new ItemPhaseValidationEntity(() => Expect(expected));
                typeof(RoomEntityManager).GetMethod("RegisterEnemySlot", flags)!.Invoke(_entities, [recoilObserver, 0]);
                typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [recoilObserver]);
                FailIf(!_player.ApplyEnemyContactDamage(
                    _player.EnemyContactPosition + new Vector2(0, 16), 0,
                    RingDamageSource.Generic, knockbackFrames: 1, allowZeroDamage: true),
                    "Upper-edge recoil must accept contact from below.");
                StepGameplayUpdates(1, Vector2.Zero, batched: batch);
                FailIf(_player.Position != corner.Value + Vector2.Right || _player.KnockbackFrames != 0,
                    "Native recoil must slide right past the one-sided upper wall, including on re-entry.");
                _entities.Clear();
            }
            _player.WarpTo(new(120, 40));
            expected = [0, 0, 0x40, 1];
            observations = 0;
            var deathObserver = new ItemPhaseValidationEntity(() => { Expect(expected); observations++; });
            typeof(RoomEntityManager).GetMethod("RegisterEnemySlot", flags)!.Invoke(_entities, [deathObserver, 0]);
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [deathObserver]);
            FailIf(!_player.ApplyEnemyContactDamage(
                _player.EnemyContactPosition - new Vector2(16, 0), _player.MaxHealthQuarters,
                RingDamageSource.Generic, knockbackFrames: 2) || !_player.IsDying,
                "Lethal contact must enter native death initialization with recoil.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(observations != 2 || _player.Position != new Vector2(122, 40) ||
                _player.KnockbackFrames != 0 || _player.DeathAnimationActive,
                "Dying Link must publish SPEED_140 through the counter-zero update before spinning.");
            expected = [0xff, 0, 0x40, 1];
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(observations != 4 || !_player.DeathAnimationActive,
                "Death spin must begin after recoil and preserve its velocity scratch.");
        }
        ReinitializeGameplayForValidation();
    }
}
