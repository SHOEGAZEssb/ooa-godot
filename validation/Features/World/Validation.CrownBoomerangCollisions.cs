using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownBoomerangCollisions()
    {
        var data = BoomerangCollisionDatabase.Shared;
        FailIf(data.StunCounter != 120 || data.StunInvincibility != -16 || data.DeflectionInvincibility != -20,
            "Boomerang status must retain ENEMYDMG_$24/$28's independent literal counters.");
        foreach (var (mode, effect) in new[] { (0x11, 0x22), (0x17, 0x35), (0x1c, 0x35),
            (0x20, 0x1b), (0x22, 0x22), (0x1f, 9), (0x29, 0x22), (0x33, 0x0b),
            (0x37, 0x1b), (0x45, 0x1c), (0x63, 0), (0x74, 0x1b) })
            FailIf(data.Effect(mode) != effect, $"Boomerang column$17 mode${mode:x2} must select source effect${effect:x2}.");
        FailIf(data.EnemyEnabled(0x16) || data.EnemyEnabled(0x50) || data.EnemyEnabled(0x7c) ||
            data.PartEnabled(0x1a) || !data.PartEnabled(0x2a),
            "Beamos, Fireball Shooter, Smog and enemy arrows reject this column; SpikedBall accepts it.");
        FailIf(BoomerangCollisionResponse.Midpoint(new(101, 99), new(80, 84)) != new Vector2(90, 91) ||
            BoomerangCollisionResponse.Midpoint(new(1, 1), new(255, 255)) != Vector2.Zero,
            "createClinkInteraction averages wrapped signed-byte deltas, rounding down.");

        foreach (bool batched in new[] { false, true })
        foreach (var (id, room, effect) in new[] { (0x0c, 0xab, 0x22), (0x3d, 0xb0, 0x22),
            (0x24, 0x9f, 0x22), (0x34, 0x9d, 0x22), (0x21, 0xaa, 0x1b),
            (0x48, 0x9c, 0x1b), (0x4b, 0xa0, 0x1b), (0x32, 0xa1, 9),
            (0x13, 0xa8, 0x35), (0x19, 0x9f, 0x35) })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, room);
            _player.ApplicationUpdateOwned = true;
            _player.WarpTo(new(24, 24));
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            Step(2);
            EnemyCharacter actor = id switch
            {
                0x0c => _entities.Entities<ArrowMoblinCharacter>().First(),
                0x21 => _entities.Entities<ArrowDarknutCharacter>().First(),
                0x3d or 0x48 => _entities.Entities<SwordEnemyCharacter>().First(e => e.Record.Id == id),
                0x24 => _entities.Entities<LikeLikeCharacter>().First(),
                0x34 => _entities.Entities<ZolCharacter>().First(e => e.Record.SubId == 1),
                0x4b => _entities.Entities<BallChainSoldierCharacter>().Single(),
                0x32 => _entities.Entities<KeeseCharacter>().First(),
                0x13 => _entities.Entities<SparkCharacter>().First(),
                _ => _entities.Entities<WhispCharacter>().First()
            };
            foreach (var other in _entities.Entities<EnemyCharacter>())
            {
                if (other == actor) continue;
                other.Position = new(216, 152);
                other.InvincibilityCounter = 127;
            }
            foreach (var part in _entities.Entities<SpikedBallPart>()) part.ClearHealthAndCollision();
            for (int y = 8; y < 176; y += 16)
            for (int x = 8; x < 240; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            if (id != 0x13) actor.Position = new(120, 88);
            actor.Health = 9;
            actor.InvincibilityCounter = 0;
            int Stun() => actor switch { ArrowMoblinCharacter m => m.StunCounter,
                SwordEnemyCharacter s => s.StunCounter, LikeLikeCharacter l => l.StunCounter,
                ZolCharacter z => z.StunCounter, _ => 0 };
            int repeats = effect == 0x35 ? 1 : 2;
            for (int repeat = 0; repeat < repeats; repeat++)
            {
                _entities.ClearPhysicalPlayerItems();
                actor.InvincibilityCounter = 0;
                actor.Health = 9;
                if (id != 0x13) actor.Position = new(120, 88);
                var item = _entities.Spawn<BoomerangItem>(new BoomerangSpawn(actor.Position, 8));
                _sound.ClearPlayRequestAudit();
                Step();
                FailIf(item.State != 1, "Post-object contact must not turn the item until its next update.");
                if (effect == 0x22)
                {
                    FailIf(Stun() != 120 || actor.InvincibilityCounter != -16 || actor.Health != 9 || actor.NativeHitPending ||
                        _sound.PlayRequestsFor(OracleSoundEngine.SndDamageEnemy) != 1,
                        $"Enemy${id:x2}: effect$22 must write stun120/inv-16 without damage or JUST_HIT.");
                    if (actor is ZolCharacter zol) FailIf(zol.DamageHitPending || zol.State == ZolState.RedSplitting,
                        "A boomerang stun must not split the red Zol.");
                    Vector2 stopped = actor.Position;
                    Step(2);
                    FailIf(item.State != 2 || Stun() != 119 || actor.InvincibilityCounter != -14 || actor.Position != stopped,
                        $"Enemy${id:x2}: return must begin next update, while stun decrements once per two updates and freezes AI.");
                    _entities.ClearPhysicalPlayerItems();
                    int approachUpdates = 0;
                    if (id is 0x0c or 0x34)
                    {
                        _player.WarpTo(actor.Position + Vector2.Left * 20);
                        int linkHealth = _player.HealthQuarters;
                        StepGameplayUpdates(12, Vector2.Right, batched: batched);
                        approachUpdates = 12;
                        FailIf(_player.HealthQuarters != linkHealth || !_player.OverlapsEnemyCollision(actor.CollisionBounds),
                            "Walking into a stunned enemy must reach its body without ordinary Link damage.");
                    }
                    int frozenStun = 119 - approachUpdates / 2;
                    int frozenInvincibility = actor.InvincibilityCounter;
                    var text = _entities.TextActiveSource;
                    try
                    {
                        _entities.TextActiveSource = () => true;
                        Step(5);
                        FailIf(Stun() != frozenStun || actor.InvincibilityCounter != frozenInvincibility,
                            "Text must freeze enemy stun and invincibility.");
                    }
                    finally { _entities.TextActiveSource = text; }
                    Step(236 - approachUpdates);
                    FailIf(Stun() != 1 || actor.Health != 9 || actor.InvincibilityCounter != 0,
                        $"Enemy${id:x2}: the final stun tick must remain after 238 eligible updates.");
                    Step(2);
                    FailIf(Stun() != 0, $"Enemy${id:x2}: stun must end after 240 eligible updates.");
                }
                else if (effect == 0x1b)
                {
                    var clink = _entities.Entities<ClinkEffect>().Single();
                    FailIf(actor.Health != 9 || actor.InvincibilityCounter != -20 || clink.ElapsedFrames != 0 ||
                        clink.Position != BoomerangCollisionResponse.Midpoint(actor.Position, item.Position) ||
                        _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 0,
                        $"Enemy${id:x2}: deflection must reserve an uninitialized clink and set inv-20 without damage.");
                    Step();
                    FailIf(item.State != 2 || actor.InvincibilityCounter != -19 || clink.ElapsedFrames != 1 ||
                        _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 1,
                        "The following item/interaction passes must return the boomerang and initialize the checked clink.");
                    _entities.ClearPhysicalPlayerItems();
                    Step(20);
                }
                else if (effect == 9)
                {
                    FailIf(actor.Health != 7 || actor.InvincibilityCounter != 21 || actor.KnockbackCounter != 11 || !actor.NativeHitPending,
                        "Keese boomerang damage must retain the normal damage/recoil profile and deferred JUST_HIT.");
                    Step();
                    FailIf(item.State != 2 || actor.NativeHitPending || actor.KnockbackCounter != 11,
                        "Keese's JUST_HIT update precedes recoil, while the boomerang starts returning.");
                    _entities.ClearPhysicalPlayerItems();
                    Step(22);
                }
                else
                {
                    FailIf(actor.Health != 0 || actor.CollisionEnabled,
                        $"Enemy${id:x2} must receive its transformation from the actual item scan: health={actor.Health}, actor={actor.Position}, item={item.Position}.");
                    Step();
                    FailIf(item.State != 2 || actor.Visible,
                        "The next enemy pass must begin transformation while the next item pass returns the boomerang.");
                    Step(20);
                    FailIf(_entities.Entities<EnemyCharacter>().Contains(actor),
                        "The source puff handshake must finish the automatically triggered transformation.");
                }
            }
        }
        foreach (bool batched in new[] { false, true })
        {
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xab);
            _player.WarpTo(new(24, 24));
            Step(2);
            var moblin = _entities.Entities<ArrowMoblinCharacter>().First();
            _entities.Spawn<BoomerangItem>(new BoomerangSpawn(moblin.Position, 8, -7));
            Step();
            FailIf(moblin.StunCounter != 0, "EnemyZ-itemZ=+7 must reject a boomerang contact.");
            _entities.ClearPhysicalPlayerItems();
            _entities.Spawn<BoomerangItem>(new BoomerangSpawn(moblin.Position, 8, 7));
            Step();
            FailIf(moblin.StunCounter != 120, "EnemyZ-itemZ=-7 must accept a boomerang contact.");
            _entities.ClearPhysicalPlayerItems();
            moblin.InvincibilityCounter = 2;
            var gated = _entities.Spawn<BoomerangItem>(new BoomerangSpawn(moblin.Position, 8));
            Step();
            FailIf(moblin.InvincibilityCounter != 1 || gated.State != 1, "Nonzero enemy invincibility must reject contact.");
            Step();
            FailIf(moblin.InvincibilityCounter != -16 || gated.State != 1,
                "Collision must observe invincibility reaching zero in this enemy update and queue the return.");
            Step();
            FailIf(gated.State != 2, "An accepted hit must turn only in the next item pass.");

            foreach (bool full in new[] { false, true })
            {
                LoadValidationRoom(4, 0xa0);
                _player.WarpTo(new(24, 24));
                Step(3);
                var soldier = _entities.Entities<BallChainSoldierCharacter>().Single();
                soldier.InvincibilityCounter = 127; // Isolate the part from its owner's collision row.
                var head = _entities.Entities<SpikedBallPart>().Single(p => p.SubId == 0);
                if (full)
                    while (_entities.InteractionSlotAvailable)
                        _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(208, 144), 0));
                int health = head.Health;
                var item = _entities.Spawn<BoomerangItem>(new BoomerangSpawn(head.Position, 8, head.ZHigh));
                _sound.ClearPlayRequestAudit();
                Step();
                FailIf(head.Health != health || head.InvincibilityCounter != -20 || !head.PendingCollision || item.State != 1 ||
                    _entities.Entities<ClinkEffect>().Count != (full ? 0 : 1),
                    "PART$2a must deflect without damage, even if its checked INTERACTION allocation fails.");
                Step();
                FailIf(item.State != 2 || _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != (full ? 0 : 1),
                    "Spiked-ball deflection must return the child; clink sound requires successful allocation and later initialization.");
            }

            _saveData.SetRoomFlag(4, 0xb4, 0x80, false);
            LoadValidationRoom(4, 0xb4);
            _player.WarpTo(new(48, 136));
            Step(2);
            var parent = _entities.Entities<SmasherCharacter>().Single(e => !e.IsBall);
            var ball = _entities.Entities<SmasherCharacter>().Single(e => e.IsBall);
            parent.Position = new(48, 48);
            ball.Position = new(120, 88);
            var ignored = _entities.Spawn<BoomerangItem>(new BoomerangSpawn(ball.Position, 8));
            Step(2);
            FailIf(ignored.State != 1 || ignored.Counter != 39 || ball.PendingCollision,
                "Smasher's ball accepts effect$00 contact without setting either object's hit flag or returning the item.");
            _entities.ClearPhysicalPlayerItems();
            ball.Position = new(48, 48);
            parent.Position = new(120, 88);
            int bossHealth = parent.Health;
            var bossItem = _entities.Spawn<BoomerangItem>(new BoomerangSpawn(parent.Position, 8));
            Step();
            FailIf(!parent.PendingCollision || parent.Health != bossHealth || parent.InvincibilityCounter != 0 || bossItem.State != 1,
                "Smasher effect$1c must publish JUST_HIT without damage, invincibility or immediate return.");
            Step();
            FailIf(bossItem.State != 2, "Smasher contact must return the child on its next update.");

            LoadValidationRoom(4, 0x9d);
            _entities.Clear();
            _player.WarpTo(new(24, 24));
            var gel = _entities.Spawn<GelCharacter>(new GelSpawn(new(124, 104)));
            gel.SetStateForValidation(GelState.Waiting, counter1: 200);
            gel.Health = 3;
            Step();
            for (int hit = 0; hit < 2; hit++)
            {
                var item = _entities.Spawn<BoomerangItem>(new BoomerangSpawn(gel.Position, 8, gel.ZFixed >> 8));
                Step();
                FailIf(gel.Health != (hit == 0 ? 1 : 0) || gel.InvincibilityCounter != 32 ||
                    gel.KnockbackCounter != 0 || !gel.NativeHitPending || gel.IsDead,
                    $"Gel effect$0b must defer no-recoil damage/death: hit={hit}, HP={gel.Health}, inv={gel.InvincibilityCounter}, recoil={gel.KnockbackCounter}, pending={gel.NativeHitPending}, dead={gel.IsDead}.");
                Step();
                FailIf(item.State != 2 || gel.NativeHitPending || gel.IsDead,
                    "Gel's JUST_HIT update must precede health-zero deletion.");
                _entities.ClearPhysicalPlayerItems();
                Step(hit == 0 ? 33 : 1);
            }
            FailIf(!gel.IsDead || _entities.Entities<GelCharacter>().Count != 0,
                "Gel must delete in its following ordinary health-zero dispatch.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
