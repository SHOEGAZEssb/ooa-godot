using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private EnemyStatusRom PrepareEnemyStatusRom(out ArrowMoblinCharacter enemy)
    {
        ReinitializeGameplayForValidation();
        LoadValidationRoom(4, 0xa8);
        _entities.Clear();
        for (int y = 8; y < _currentRoom.Height; y += 16)
        for (int x = 8; x < _currentRoom.Width; x += 16)
            _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
        _player.WarpTo(new(24, 24));
        StepGameplayUpdates(8, Vector2.Right);
        FailIf(_player.Position.X <= 24, "Enemy-status fixture requires Link to walk through the declared collision-safe floor.");
        var rom = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
        OracleRandomState seed = _random.CaptureState();
        rom[0xff94] = seed.Rng1;
        rom[0xff95] = seed.Rng2;
        enemy = new ArrowMoblinCharacter();
        enemy.Initialize(new EnemyDatabase().ImportedEnemy(0x0c, 0), _currentRoom, new(120.5f, 88.5f), _random);
        var source = new EnemyCombatSourceDescriptor(0x0c, 0, 0x91, 0, 1,
            EnemyHandlerKind.ArrowMoblin, "moblinsAndShroudedStalfos.s:enemyCode0c");
        _entities.AddEntity(new ArrowMoblinRoomEntity(enemy, source, _sound.PlaySound));
        rom[0xd080] = 0x11;
        rom[0xd081] = 0x0c;
        rom.Word(0xd08a, (int)(enemy.Position.Y * 256));
        rom.Word(0xd08c, (int)(enemy.Position.X * 256));
        StepEnemyStatusRom(rom, enemy, 1, batched: false);
        return rom;
    }

    private void StepEnemyStatusRom(EnemyStatusRom rom, ArrowMoblinCharacter enemy, int count, bool batched)
    {
        StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
        {
            rom.Update(_entities.FrameCounter, _player.Position);
            OracleRandomState seed = _random.CaptureState();
            FailIf(seed.Rng1 != rom[0xff94] || seed.Rng2 != rom[0xff95] ||
                _random.Calls - rom.InitialRuntimeRandomCalls != rom.RandomCalls,
                $"Enemy status RNG frame={_entities.FrameCounter}: runtime=${seed.Rng2:x2}{seed.Rng1:x2}/{_random.Calls - rom.InitialRuntimeRandomCalls}, native=${rom[0xff95]:x2}{rom[0xff94]:x2}/{rom.RandomCalls}.");
            if (rom[0xd080] == 0)
                FailIf(!enemy.IsDead, "ENEMY $0c runtime outlived native deletion.");
            else
                FailIf(enemy.IsDead || enemy.Position != new Vector2(rom.Word(0xd08c) / 256f, rom.Word(0xd08a) / 256f) ||
                    enemy.Health != rom[0xd0a9] || (enemy.InvincibilityCounter & 0xff) != rom[0xd0ab] ||
                    enemy.KnockbackCounter != rom[0xd0ad] || enemy.StunCounter != rom[0xd0ae] ||
                    enemy.ZFixed != unchecked((short)rom.Word(0xd08e)) ||
                    (int)enemy.State != rom[0xd084] || enemy.Counter != rom[0xd086] ||
                    enemy.CollisionEnabled != ((rom[0xd0a4] & 0x80) != 0) ||
                    ReferenceEquals(enemy.CurrentDrawTexture, enemy.Animation.DamageTexture) !=
                        ((rom[0xd09c] & 7) != (rom[0xd09b] & 7)),
                    $"ENEMY $0c frame={_entities.FrameCounter}, batch={batched}: runtime XY={enemy.Position}, HP={enemy.Health}, inv={enemy.InvincibilityCounter}, recoil=${enemy.KnockbackCounter:x2}, stun={enemy.StunCounter}, Z={enemy.ZFixed}, state=${(int)enemy.State:x2}, counter={enemy.Counter}, collision={enemy.CollisionEnabled}, damagePalette={ReferenceEquals(enemy.CurrentDrawTexture, enemy.Animation.DamageTexture)}; native XY=${rom.Word(0xd08c):x4}/${rom.Word(0xd08a):x4}, HP={rom[0xd0a9]}, inv=${rom[0xd0ab]:x2}, recoil=${rom[0xd0ad]:x2}, stun={rom[0xd0ae]}, Z=${rom.Word(0xd08e):x4}, state=${rom[0xd084]:x2}, counter={rom[0xd086]}, collision=${rom[0xd0a4]:x2}, palette=${rom[0xd09c]:x2}/${rom[0xd09b]:x2}.");
            CompareEnemyStatusDustRom(rom);
        });
    }

    private void CompareEnemyStatusDustRom(EnemyStatusRom rom)
    {
        var dust = _entities.Entities<KnockbackDustRoomEntity>();
        int nativeCount = 0;
        for (int slot = 2; slot < 16; slot++)
            if (rom[0xd040 + slot * 0x100] != 0 && rom[0xd041 + slot * 0x100] == 0x0f)
                nativeCount++;
        FailIf(dust.Count != nativeCount, $"INTERAC $0f:$01 allocation differs: runtime={dust.Count}, native={nativeCount}.");
        foreach (var effect in dust)
        {
            int address = 0xd040 + _entities.InteractionSlot(effect) * 0x100;
            FailIf(rom[address] == 0 || rom[address + 1] != 0x0f || rom[address + 2] != 1 ||
                effect.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                effect.Visible != ((rom[address + 0x1a] & 0x80) != 0) ||
                effect.AnimationParameter != rom[address + 0x21],
                $"INTERAC $0f:$01 slot=${address >> 8:x2}, frame={_entities.FrameCounter}: position/visibility/animation diverged.");
        }
    }

    private void CompareEnemyDeathRom(EnemyStatusRom rom)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_partSlots", flags)!.GetValue(_entities)!;
        FailIf(_entities.RoomEnemyCount != rom[0xcdd1] || _entities.ActiveRoomDefeatBitset != rom[0xcdc1] ||
            _sound.PlayRequestsFor(SoundId.SndKillEnemy) != rom.Sounds.Count(sound => sound == SoundId.SndKillEnemy),
            $"Enemy death frame={_entities.FrameCounter}: runtime count/bits/sound={_entities.RoomEnemyCount}/${_entities.ActiveRoomDefeatBitset:x2}/{_sound.PlayRequestsFor(SoundId.SndKillEnemy)}, native={rom[0xcdd1]}/${rom[0xcdc1]:x2}/{rom.Sounds.Count(sound => sound == SoundId.SndKillEnemy)}.");
        foreach (int address in Enumerable.Range(0xc620, 2).Concat([0xc641]).Concat(Enumerable.Range(0xc64f, 18)))
            FailIf(_saveData.ReadWramByte(address) != rom[address],
                $"Enemy death WRAM ${address:x4}: runtime=${_saveData.ReadWramByte(address):x2}, native=${rom[address]:x2}.");
        int puffs = 0, drops = 0;
        for (int slot = 0; slot < 16; slot++)
        {
            int address = 0xd0c0 + slot * 0x100;
            if (rom[address] == 0) continue;
            if (rom[address + 1] == 2) puffs++;
            if (rom[address + 1] == 1) drops++;
        }
        FailIf(_entities.Entities<EnemyDeathPuffEffect>().Count != puffs || _entities.Entities<ItemDropEffect>().Count != drops,
            "Native enemy death PART allocation/replacement differs.");
        foreach (var owner in _entities.EntityAdapters<DeathPuffRoomEntity>())
        {
            var puff = (EnemyDeathPuffEffect)owner.Node;
            int address = 0xd0c0 + slots[owner] * 0x100;
            int counter = (int)typeof(EnemyDeathPuffEffect).GetField("_animationCounter", flags)!.GetValue(puff)!;
            FailIf(rom[address] == 0 || rom[address + 1] != 2 || rom[address + 2] != 0x0c ||
                puff.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                puff.HighKnockback != ((rom[address + 0x2d] & 0x80) != 0) ||
                puff.CurrentPalette != (rom[address + 0x1c] & 7) || counter != rom[address + 0x20],
                $"PART $02 slot=${address >> 8:x2}, frame={_entities.FrameCounter}: runtime XY={puff.Position}, high={puff.HighKnockback}, palette={puff.CurrentPalette}, timer={counter}; native XY=${rom.Word(address + 12):x4}/${rom.Word(address + 10):x4}, high=${rom[address + 0x2d]:x2}, palette=${rom[address + 0x1c]:x2}, timer={rom[address + 0x20]}.");
        }
        foreach (var owner in _entities.EntityAdapters<ItemDropRoomEntity>())
        {
            var drop = (ItemDropEffect)owner.Node;
            int address = 0xd0c0 + slots[owner] * 0x100;
            FailIf(rom[address] == 0 || rom[address + 1] != 1 || drop.SubId != rom[address + 2] ||
                (int)drop.State != rom[address + 4] || drop.Counter != rom[address + 6] ||
                drop.PrecisePosition != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                drop.ZFixed != unchecked((short)rom.Word(address + 14)) ||
                drop.SpeedZ != unchecked((short)rom.Word(address + 0x14)),
                $"PART $01 slot=${address >> 8:x2}, frame={_entities.FrameCounter}: runtime subid=${drop.SubId:x2}, state={(int)drop.State}, timer={drop.Counter}, XY={drop.PrecisePosition}, Z={drop.ZFixed}, speedZ={drop.SpeedZ}; native enabled={rom[address]}, id={rom[address + 1]}, subid=${rom[address + 2]:x2}, state={rom[address + 4]}, timer={rom[address + 6]}, XY=${rom.Word(address + 12):x4}/${rom.Word(address + 10):x4}, Z={unchecked((short)rom.Word(address + 14))}, speedZ={unchecked((short)rom.Word(address + 0x14))}.");
        }
    }

    private static void SetEnemyStatusByte(EnemyCharacter enemy, string property, int value) =>
        typeof(EnemyCharacter).GetProperty(property, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(enemy, value);

    private void ValidateEnemyHitRecoveryRom()
    {
        int hostCase3 = 0;
        foreach (string gate in new[] { "text", "palette", "disabled" })
        foreach (int invincibility in new[] { -0x20, -1, 0, 1, 0x15, 0x7f })
        foreach (int stun in new[] { 0, 1, 2, 29, 30, 31 })
        foreach (bool batched in RomHostSchedules(hostCase3++))
        {
            EnemyStatusRom rom = PrepareEnemyStatusRom(out var enemy);
            enemy.InvincibilityCounter = invincibility;
            enemy.ApplyBoomerangStun(stun);
            rom[0xd0ab] = unchecked((byte)invincibility);
            rom[0xd0ae] = (byte)stun;
            enemy.DeferNativeHitStatus();
            rom[0xd0aa] = 0x80;
            StepEnemyStatusRom(rom, enemy, 1, batched);
            FailIf(enemy.StunCounter != stun || rom[0xd0ae] != stun,
                "JUST_HIT must delay stun handling for its first eligible update.");
            var paletteSource = _entities.PaletteFadeActiveSource;
            var disabledSource = _entities.NonInteractionObjectsDisabledSource;
            try
            {
                if (gate == "text") { _dialogue.ShowMessage("Enemy recovery pause.", _player.Position.Y); rom[0xcba0] = 1; }
                if (gate == "palette") { _entities.PaletteFadeActiveSource = () => true; rom[0xc4ab] = 1; }
                if (gate == "disabled") { _entities.NonInteractionObjectsDisabledSource = () => true; rom[0xcc8a] = 0x0c; }
                StepEnemyStatusRom(rom, enemy, 7, batched);
            }
            finally
            {
                _dialogue.Close();
                _entities.PaletteFadeActiveSource = paletteSource;
                _entities.NonInteractionObjectsDisabledSource = disabledSource;
                rom[0xcba0] = rom[0xc4ab] = rom[0xcc8a] = 0;
            }
            StepEnemyStatusRom(rom, enemy, 64, batched);
            FailIf(enemy.StunCounter != 0, "Native odd-update stun countdown did not finish.");
        }
        GD.Print("Validated executed-ROM enemy new-hit priority, signed invincibility/damage palettes, stun/shake boundaries and text/palette/disabled pause/resume through split/batched gameplay updates.");
    }

    private void ValidateEnemyKnockbackRom()
    {
        int hostCase2 = 0;
        foreach (string mode in new[] { "normal", "high", "wall", "full" })
        foreach (int angle in new[] { 0, 8, 16, 24 })
        foreach (bool batched in RomHostSchedules(hostCase2++))
        {
            EnemyStatusRom rom = PrepareEnemyStatusRom(out var enemy);
            Vector2 position = mode == "wall" ? angle switch
            {
                0 => new(120.5f, 12.5f), 8 => new(236.5f, 88.5f),
                16 => new(120.5f, 172.5f), _ => new(4.5f, 88.5f)
            } : new(120.5f, 88.5f);
            enemy.Position = position;
            rom.Word(0xd08a, (int)(position.Y * 256));
            rom.Word(0xd08c, (int)(position.X * 256));
            SetEnemyStatusByte(enemy, nameof(EnemyCharacter.KnockbackAngle), angle);
            SetEnemyStatusByte(enemy, nameof(EnemyCharacter.KnockbackCounter), mode == "normal" ? 9 : 0x89);
            rom[0xd0ac] = (byte)angle;
            rom[0xd0ad] = (byte)(mode == "normal" ? 9 : 0x89);
            // These are declared post-collision signals, not a substitute for
            // the separate native collision-table/weapon regressions.
            enemy.DeferNativeHitStatus();
            rom[0xd0aa] = 0x80;
            if (mode == "full")
                for (int slot = 2; slot < 16; slot++)
                {
                    _entities.AddEntity(new KnockbackDustRoomEntity(new(200, 104), 0));
                    int address = 0xd040 + slot * 0x100;
                    rom[address] = 1;
                    rom[address + 1] = 0x0f;
                    rom[address + 2] = 1;
                    rom.Word(address + 10, 104 * 256);
                    rom.Word(address + 12, 200 * 256);
                }
            StepEnemyStatusRom(rom, enemy, 1, batched);
            FailIf(enemy.Position != position || enemy.KnockbackCounter != rom[0xd0ad],
                "JUST_HIT must defer the first recoil movement.");
            StepEnemyStatusRom(rom, enemy, 3, batched);
            Vector2 paused = enemy.Position;
            int counter = enemy.KnockbackCounter;
            _dialogue.ShowMessage("Enemy recoil pause.", _player.Position.Y);
            rom[0xcba0] = 1;
            StepEnemyStatusRom(rom, enemy, 5, batched);
            FailIf(enemy.Position != paused || enemy.KnockbackCounter != counter,
                "Dialogue must freeze initialized enemy recoil while dust keeps updating.");
            _dialogue.Close();
            rom[0xcba0] = 0;
            StepEnemyStatusRom(rom, enemy, 6, batched);
            FailIf((enemy.KnockbackCounter & 0x7f) != 0,
                "Nine eligible recoil updates must exhaust the low seven bits.");
            StepEnemyStatusRom(rom, enemy, 15, batched);
            FailIf(_entities.Entities<KnockbackDustRoomEntity>().Count != 0,
                "Recoil dust must delete after its terminal animation update.");
        }
        GD.Print("Validated executed-ROM normal/high recoil, four directions, boundary walls, dust allocation failure/lifetime and pause/resume through split/batched gameplay updates.");
    }

    private void ValidateEnemyDeathHandoffRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        int replacements = 0, emptyDrops = 0;
        int hostCase1 = 0;
        foreach (bool full in new[] { false, true })
        foreach (EnemyKnockbackStrength strength in new[] { EnemyKnockbackStrength.Low, EnemyKnockbackStrength.Normal, EnemyKnockbackStrength.High })
        foreach (int seed in new[] { 0x0d37, 0x1234, 0x80ff, 0xffff })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            EnemyStatusRom rom = PrepareEnemyStatusRom(out var enemy);
            _random.RestoreState(_random.CaptureState() with { Rng1 = (byte)seed, Rng2 = (byte)(seed >> 8) });
            rom[0xff94] = (byte)seed;
            rom[0xff95] = (byte)(seed >> 8);
            var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_partSlots", flags)!.GetValue(_entities)!;
            var reservations = new List<EnemyAiSlotReservation>();
            for (int slot = 0; slot < (full ? 16 : 3); slot++)
            {
                var reservation = new EnemyAiSlotReservation();
                reservations.Add(reservation);
                slots.Add(reservation, slot);
                int address = 0xd0c0 + slot * 0x100;
                rom[address] = 1;
                rom[address + 1] = 0x0a; // original partCodeNil: RET
                rom[address + 4] = 1;
            }
            try
            {
                _sound.ClearPlayRequestAudit();
                int interactionCount = -1;
                byte interactionDefeats = 0;
                _entities.AddEntity(new ItemPhaseValidationEntity(() =>
                {
                    // With no ENEMY registration this logical observer runs
                    // in the real INTERACTION phase, after the PART walk.
                    interactionCount = _entities.RoomEnemyCount;
                    interactionDefeats = _entities.ActiveRoomDefeatBitset;
                }));
                var owner = _entities.EntityAdapters<ArrowMoblinRoomEntity>().Single();
                var pending = (List<RoomEntitySpawn>)typeof(RoomEntityManager).GetField("_pendingSpawns", flags)!.GetValue(_entities)!;
                Vector2 source = enemy.Position.Floor() + Vector2.Left;
                FailIf(!owner.ApplySwordHit(enemy.CollisionBounds, source, 0x7f, strength, pending),
                    "Shared Moblin combat owner rejected the declared lethal collision.");
                enemy.DeferNativeHitStatus();
                rom.HitWithSword(strength == EnemyKnockbackStrength.Low ? 4 : strength == EnemyKnockbackStrength.Normal ? 5 : 8, 0x7f);
                FailIf(enemy.Health != 0 || enemy.CollisionEnabled || rom[0xd0a9] != 0 ||
                    (rom[0xd0a4] & 0x80) != 0 || enemy.KnockbackCounter != rom[0xd0ad] ||
                    enemy.InvincibilityCounter != rom[0xd0ab] || enemy.KnockbackAngle != rom[0xd0ac] ||
                    !enemy.PendingKnockbackDeath || _entities.RoomEnemyCount != 1,
                    "The native lethal collision must disable contact while preserving the enemy and its room count for recoil.");
                // Bit7 is a declared shared-status input, distinct from the
                // high-strength damage row (whose recoil byte is $0f).
                if (strength == EnemyKnockbackStrength.High)
                {
                    SetEnemyStatusByte(enemy, nameof(EnemyCharacter.KnockbackCounter), enemy.KnockbackCounter | 0x80);
                    rom[0xd0ad] |= 0x80;
                }
                void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    // Reuse the same per-update comparisons as live recovery.
                    rom.Update(_entities.FrameCounter, _player.Position);
                    OracleRandomState state = _random.CaptureState();
                    FailIf(state.Rng1 != rom[0xff94] || state.Rng2 != rom[0xff95] ||
                        _random.Calls - rom.InitialRuntimeRandomCalls != rom.RandomCalls,
                        "Death/drop handoff changed native global RNG order or consumption.");
                    if (rom[0xd080] != 0)
                        FailIf(enemy.IsDead || !enemy.PendingKnockbackDeath ||
                            enemy.Position != new Vector2(rom.Word(0xd08c) / 256f, rom.Word(0xd08a) / 256f) ||
                            enemy.KnockbackCounter != rom[0xd0ad] || enemy.CollisionEnabled,
                            "Lethal recoil diverged before the native enemyDelete handoff.");
                    else FailIf(!enemy.IsDead || _entities.FindFreeEnemySlot() != 0,
                        "enemyDelete must release the live enemy slot when its death handler completes.");
                    CompareEnemyStatusDustRom(rom);
                    CompareEnemyDeathRom(rom);
                    if (rom[0xcba0] == 0)
                        FailIf(interactionCount != rom[0xcdd1] || interactionDefeats != rom[0xcdc1],
                            "Room-clear interactions must observe the native post-PART count and defeat bits in the same update.");
                });
                Step();
                FailIf(enemy.IsDead, "JUST_HIT must defer even a lethal zero-health dispatch.");
                Step(3);
                _dialogue.ShowMessage("Death handoff pause.", _player.Position.Y);
                rom[0xcba0] = 1;
                Step(4);
                _dialogue.Close();
                rom[0xcba0] = 0;
                for (int update = 0; rom[0xd080] != 0 && update < 24; update++) Step();
                FailIf(rom[0xd080] != 0 || !enemy.IsDead || _entities.RoomEnemyCount != 1,
                    "Death must transfer or retain its count until the puff finishes, including allocation failure.");
                if (full)
                {
                    for (int slot = 0; slot < 16; slot++)
                    {
                        slots.Remove(reservations[slot]);
                        rom[0xd0c0 + slot * 0x100] = 0;
                    }
                    Step(32);
                    FailIf(_entities.Entities<EnemyDeathPuffEffect>().Count != 0 || _entities.RoomEnemyCount != 1,
                        "Freeing a full PART pool must not retry the failed puff or release the retained enemy count.");
                    continue;
                }
                var puff = _entities.Entities<EnemyDeathPuffEffect>().Single();
                var puffOwner = _entities.EntityAdapters<DeathPuffRoomEntity>().Single();
                FailIf(puff.ElapsedFrames != 1 || slots[puffOwner] != 3,
                    "Death must allocate PART $d3 and animate it in the same update's later PART pass.");
                slots.Remove(reservations[0]);
                rom[0xd0c0] = 0;
                _dialogue.ShowMessage("Death puff pause.", _player.Position.Y);
                rom[0xcba0] = 1;
                Step(4);
                FailIf(puff.ElapsedFrames != 1, "Text must freeze an initialized death puff and retain its room count.");
                _dialogue.Close();
                rom[0xcba0] = 0;
                for (int update = 0; rom[0xd3c1] == 2 && update < 80; update++) Step();
                FailIf(_entities.RoomEnemyCount != 0 || rom[0xcdd1] != 0,
                    "Terminal puff must release the room count before subsequent interactions.");
                if (rom[0xd3c0] != 0)
                {
                    replacements++;
                    var drop = _entities.Entities<ItemDropEffect>().Single();
                    var dropOwner = _entities.EntityAdapters<ItemDropRoomEntity>().Single();
                    FailIf(slots[dropOwner] != 3 || drop.State != DropState.Initializing || drop.ElapsedFrames != 0 || slots.Values.Contains(0),
                        "objectReplaceWithID must reuse PART $d3 and defer initialization despite the lower free page $d0.");
                }
                else emptyDrops++;
                Step(24);
            }
            finally
            {
                foreach (var reservation in reservations)
                {
                    slots.Remove(reservation);
                    reservation.Node.Free();
                }
            }
        }
        FailIf(replacements == 0 || emptyDrops == 0, "Native death trace must exercise both item replacement and no-drop deletion.");
        GD.Print($"Validated executed-ROM lethal recoil, room-count/kill-counter handoff, puff pause/allocation failure, drops/RNG and same-slot replacement ({replacements} drops, {emptyDrops} empty) through split/batched gameplay updates.");
    }
}
