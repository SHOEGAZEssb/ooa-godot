using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCandleRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5, 0x27);
            var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_enemySlots", flags)!.GetValue(_entities)!;
            var parts = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_partSlots", flags)!.GetValue(_entities)!;
            FailIf(slots.Count != 3 || slots.Keys.Any(entity => entity.Node is not CandleCharacter),
                "$5:$27 must admit its source three random ENEMY$55:$00 in one ordered stream.");
            Vector2 floor = (from y in Enumerable.Range(1, 6) from x in Enumerable.Range(1, 8)
                let p = new Vector2(x * 16 + 8, y * 16 + 8)
                where !_collision.Collides(p) && _currentRoom.GetTerrainInfo(p).Hazard == HazardType.None select p).First();
            _player.WarpTo(floor);
            typeof(Player).GetField("_enemyInvincibilityFrames", flags)!.SetValue(_player, 100000f);
            var rom = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
            var interactions = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_interactionSlots", flags)!.GetValue(_entities)!;
            if (!batched)
            {
                // Declare page$d2 free in this bounded union fixture so the
                // explosion's radius writes address the live third Candle.
                // The separate Shooter fixture retains every room controller.
                var reservation = interactions.Single(pair => pair.Value == 2).Key;
                var active = (List<IRoomEntity>)typeof(RoomEntityManager).GetField("_activeEntities", flags)!.GetValue(_entities)!;
                active.Remove(reservation);
                typeof(RoomEntityManager).GetMethod("FreeEntity", flags)!.Invoke(_entities, [reservation]);
            }
            var reservedInteractions = interactions.Values.ToArray();
            var interactionReservations = new List<(EnemyAiSlotReservation Owner, int Slot)>();
            // The bounded Candle walk declares unrelated room controllers
            // as occupied pages; their own handlers have separate regressions.
            foreach (int slot in interactions.Values)
            {
                int address = 0xd040 + slot * 256;
                rom[address] = 1; rom[address + 1] = 0x1d; rom[address + 4] = 1; // INTERAC_STUB_1d
            }
            rom[0xcc08] = 0x9c; rom[0xcdd1] = 3;
            foreach (var pair in slots)
            {
                int address = 0xd080 + pair.Value * 256;
                rom[address] = (byte)(((pair.Value + 1) << 4) | 1); rom[address + 1] = 0x55;
                rom.Word(address + 10, (int)(pair.Key.Node.Position.Y * 256));
                rom.Word(address + 12, (int)(pair.Key.Node.Position.X * 256));
            }
            var seed = _random.CaptureState(); rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            bool frozen = true; _entities.TextActiveSource = () => frozen;
            int update = 0;
            int kills = _saveData.ReadWramByte(WramAddress.wTotalEnemiesKilled);
            void Compare()
            {
                foreach (int slot in reservedInteractions.Concat(interactionReservations.Select(pair => pair.Slot)))
                {
                    int address = 0xd040 + slot * 256;
                    rom[address] = 1; rom[address + 1] = 0x1d; rom[address + 4] = 1;
                }
                rom[0xcba0] = (byte)(frozen ? 1 : 0); rom.Update(_entities.FrameCounter, _player.Position);
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls ||
                    _entities.RoomEnemyCount != rom[0xcdd1] || _entities.ActiveRoomDefeatBitset != rom[0xcdc1],
                    $"Candle update{update}: source RNG/count/defeat differs.");
                foreach (var pair in slots.Where(pair => pair.Key.Node is CandleCharacter))
                {
                    var candle = (CandleCharacter)pair.Key.Node; int address = 0xd080 + pair.Value * 256;
                    int timer = (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!.GetValue(candle.Animation)!;
                    FailIf(candle.State != rom[address + 4] || candle.Counter != rom[address + 6] ||
                        candle.Speed != rom[address + 16] || candle.Angle != rom[address + 9] ||
                        candle.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                        candle.Health != rom[address + 0x29] || (candle.InvincibilityCounter & 255) != rom[address + 0x2b] ||
                        candle.KnockbackCounter != rom[address + 0x2d] ||
                        candle.CollisionEnabled != ((rom[address + 0x24] & 128) != 0) || candle.CollisionMode != (rom[address + 0x25] & 127) ||
                        candle.Visible != ((rom[address + 0x1a] & 128) != 0) || timer != rom[address + 0x20] ||
                        candle.AnimationParameter != rom[address + 0x21] ||
                        candle.CollisionBounds.Size != new Vector2(rom[address + 0x27] * 2, rom[address + 0x26] * 2),
                        $"Candle slot${pair.Value:x2} update{update}, batch={batched}: runtime state/counter/angle/speed={candle.State}/{candle.Counter}/{candle.Angle}/{candle.Speed}, XY={candle.Position}, mode={candle.CollisionMode}, timer={timer}, bounds={candle.CollisionBounds.Size}, explosionSlot={candle.ExplosionSlot}; ROM={rom[address + 4]}/{rom[address + 6]}/{rom[address + 9]}/{rom[address + 16]}, XY=${rom.Word(address + 12):x4}/${rom.Word(address + 10):x4}, mode={rom[address + 0x25]}, timer={rom[address + 0x20]}, bounds={rom[address + 0x26]}/{rom[address + 0x27]}.");
                }
                FailIf(parts.Count(pair => pair.Key.Node is CandleFlame) != Enumerable.Range(0, 16)
                    .Count(slot => rom[0xd0c0 + slot * 256] != 0 && rom[0xd0c1 + slot * 256] == 0x36),
                    $"Candle PART$36 allocation/lifetime differs at update{update}.");
                foreach (var pair in parts.Where(pair => pair.Key.Node is CandleFlame))
                {
                    var flame = (CandleFlame)pair.Key.Node; int address = 0xd0c0 + pair.Value * 256;
                    var animation = (EnemyAnimationPlayer)typeof(CandleFlame).GetField("_animation", flags)!.GetValue(flame)!;
                    int timer = (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!.GetValue(animation)!;
                    FailIf(rom[address + 1] != 0x36 || flame.State != rom[address + 4] ||
                        flame.Position != new Vector2(rom[address + 13], rom[address + 11]) ||
                        flame.ZHigh != unchecked((sbyte)rom[address + 15]) || timer != rom[address + 0x20] ||
                        flame.AnimationParameter != rom[address + 0x21] || flame.Visible != ((rom[address + 0x1a] & 128) != 0),
                        $"Candle PART$36 slot${pair.Value:x2} update{update}: state/XY/timer={flame.State}/{flame.Position}/{timer}, native={rom[address + 4]}/{rom[address + 13]},{rom[address + 11]}/{rom[address + 0x20]}.");
                }
                update++;
            }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            Step(3); frozen = false; Step(130);
            var target = (CandleCharacter)slots.Single(pair => pair.Value == 0).Key.Node;
            var adapter = (CandleRoomEntity)slots.Single(pair => pair.Value == 0).Key;
            rom.HitWithSword(ItemCollisionType.L1Sword, 1);
            FailIf(!adapter.ApplySwordHit(target.CollisionBounds, target.Position.Floor() - Vector2.Right,
                1, EnemyKnockbackStrength.Low, new List<RoomEntitySpawn>()), "Candle sword collision must bump without damage.");
            frozen = true; Step(3); frozen = false; Step(24);
            var partReservations = new List<(EnemyAiSlotReservation Owner, int Slot)>();
            if (batched)
                for (int slot = 0; slot < 16; slot++)
                {
                    var reservation = new EnemyAiSlotReservation(); parts.Add(reservation, slot);
                    partReservations.Add((reservation, slot));
                    int address = 0xd0c0 + slot * 256;
                    rom[address] = 1; rom[address + 1] = 0x0a; rom[address + 4] = 1; // partCodeNil: RET
                }
            rom[0xd701] = ItemId.EmberSeed;
            rom.HitWithSword(ItemCollisionType.EmberSeed, 1);
            new SeedSatchelDatabase().TryGet(ItemId.EmberSeed, out var ember);
            FailIf(!adapter.ApplySeedCollision(target.CollisionBounds, target.Position.Floor() - Vector2.Right,
                ember, ItemCollisionType.EmberSeed, new List<RoomEntitySpawn>()).Contact, "Candle must admit the source Ember collision.");
            FailIf(rom[0xd0aa] != 0x9b || !target.NativeHitPending || target.Health != 8,
                "collisionEffect20 must publish Ember JUST_HIT without damaging Candle's source eight health.");
            frozen = true; Step(3); frozen = false; Step(1);
            if (batched)
            {
                FailIf(target.State != 10, "Candle stateA must retry while all sixteen PART slots are occupied.");
                Step(3);
                foreach (var pair in partReservations)
                {
                    parts.Remove(pair.Owner); pair.Owner.Node.Free();
                    int address = 0xd0c0 + pair.Slot * 256;
                    for (int offset = 0; offset < 64; offset++) rom[address + offset] = 0;
                }
                Step(1);
            }
            FailIf(target.State != 11 || target.Counter != 120 || target.Speed != 40,
                "Ember's next eligible dispatch must allocate PART$36 and initialize the slow burn.");
            Step(120); FailIf(target.State != 12 || target.Counter != 120 || target.Speed != 80,
                "Slow burn's zero update changes speed and starts the fast 120-update phase.");
            Step(120); FailIf(target.State != 13 || target.Counter != 59,
                "Candle fast phase must enter flicker with 59 updates remaining on the same update.");
            Step(58);
            if (batched)
            {
                for (int slot = 2; slot < 16; slot++)
                    if (!interactions.ContainsValue(slot))
                    {
                        var reservation = new EnemyAiSlotReservation(); interactions.Add(reservation, slot);
                        interactionReservations.Add((reservation, slot));
                    }
                Vector2 stopped = target.Position;
                int timer = (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!.GetValue(target.Animation)!;
                Step(3);
                FailIf(target.State != 13 || target.Counter != 1 || target.Position != stopped ||
                    timer != (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!.GetValue(target.Animation)!,
                    "Full INTERACTION pool must retain Candle's retry counter without movement or animation advancement.");
                foreach (var pair in interactionReservations)
                {
                    interactions.Remove(pair.Owner); pair.Owner.Node.Free();
                    rom[0xd040 + pair.Slot * 256] = 0;
                }
                interactionReservations.Clear();
            }
            Step(1); FailIf(target.State != 14 || target.Visible || !target.CollisionEnabled,
                "Candle must retain collision while its allocated INTERAC$56 supplies the visible explosion.");
            FailIf(rom.Word(0xd096) != 0xd040 + target.ExplosionSlot * 256,
                $"Candle explosion relatedObj1 must retain the original live interaction page: runtime={target.ExplosionSlot}, native=${rom.Word(0xd096):x4}, reservations={string.Join(',', interactions.Values)}.");
            Step(40);
            if (!batched)
                FailIf(_entities.Entities<CandleCharacter>().Single(candle => candle.CollisionBounds.Size == new Vector2(24, 24)).State == 14 ||
                    target.CollisionBounds.Size != new Vector2(12, 12),
                    "candle_stateE's retained H must enlarge the live third enemy on page$d2, leaving the invisible owner at its original radius6.");
            FailIf(!target.IsDead || !target.ExplosionCompleted || _entities.RoomEnemyCount != 2 ||
                _entities.ActiveRoomDefeatBitset != 2 || parts.Count != 0 ||
                _saveData.ReadWramByte(WramAddress.wTotalEnemiesKilled) != kills,
                "Candle explosion must mark its original defeat index, decrement only room count, release flame, and avoid enemyDie/kill counters.");
            _entities.LoadRoom(5, _currentRoom);
            FailIf(_entities.Entities<CandleCharacter>().Count != 2, "Killed Candle must stay suppressed on immediate room re-entry.");
        }
        foreach (bool batched in new[] { false, true }) ValidateCandleSeedGameplay(batched);
        GD.Print("Validated Candle $55 in clean-US ENEMY/PART/INTERACTION walks through actual individual/batched gameplay: RNG and terrain walks, nonlethal weapon bumps, deferred Ember ignition under text, acceleration/flicker boundaries, live explosion page/radius alias, silent defeat and re-entry.");
    }

    private void ValidateCandleSeedGameplay(bool batched)
    {
        ReinitializeGameplayForValidation();
        _inventory.GiveTreasure(TreasureId.Shooter, 1);
        _inventory.GiveTreasure(ItemId.EmberSeed, 5);
        _inventory.SelectShooterSeeds(0); _inventory.EquipA(TreasureId.Shooter);
        LoadValidationRoom(5, 0x27);
        _player.WarpTo(new(40, 40));
        typeof(Player).GetField("_enemyInvincibilityFrames", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_player, 100000f);
        FailIf(_collision.Collides(_player.Position), "Candle shooter fixture must start on real room floor.");
        void Step(int count = 1, bool held = false) => StepGameplayUpdates(count, Vector2.Zero,
            held ? ["attack"] : [], held ? ["attack"] : [], batched: batched);
        Step();
        for (int repeat = 0; repeat < 2; repeat++)
        {
            CandleCharacter? target = null;
            bool Approach()
            {
                foreach (var candle in _entities.Entities<CandleCharacter>().Where(candle => candle.State == 8 && candle.Counter > 12))
                foreach (Vector2I direction in new[] { Vector2I.Down, Vector2I.Up, Vector2I.Left, Vector2I.Right })
                {
                    Vector2 sideways = new(-direction.Y, direction.X);
                    if (!Enumerable.Range(0, 25).All(i => Enumerable.Range(-5, 11).All(j =>
                    {
                        Vector2 point = candle.Position.Floor() + (Vector2)direction * i + sideways * j;
                        return point.X >= 12 && point.X < _currentRoom.Width - 12 && point.Y >= 12 && point.Y < _currentRoom.Height - 12 &&
                            !_currentRoom.IsSolid(point) && _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None;
                    }))) continue;
                    Vector2 origin = candle.Position.Floor() + (Vector2)direction * 24;
                    if (_entities.Entities<CandleCharacter>().Any(other => other != candle && other.Position.DistanceTo(origin) < 20)) continue;
                    _player.WarpTo(origin); _player.Face(-direction); target = candle; return true;
                }
                return false;
            }
            int guard = 0;
            while (!Approach() && guard++ < 240) Step();
            FailIf(target is null || _collision.Collides(_player.Position), "Candle needs a clear, reachable Ember firing corridor.");
            typeof(Player).GetField("_enemyInvincibilityFrames", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_player, 100000f);
            int ammunition = _inventory.EmberSeeds, count = _entities.RoomEnemyCount;
            Step(held: true); Step(); // Actual aiming parent release creates the ITEM$20 child.
            guard = 0;
            while (target!.State < 11 && guard++ < 18) Step();
            FailIf(target!.State != 11 || _inventory.EmberSeeds != ammunition - 1 ||
                _entities.Entities<CandleFlame>().Count != 1,
                "Actual Shooter launch, collision and deferred Candle dispatch must ignite one enemy and consume one seed.");
            guard = 0;
            while (!target.IsDead && guard++ < 400) Step();
            FailIf(!target.ExplosionCompleted || _entities.RoomEnemyCount != count - 1 ||
                _entities.Entities<CandleFlame>().Count != 0 || _player.IsUsingSeedShooter,
                $"Candle explosion must finish through gameplay and permit a repeated shot: state={target.State}, counter={target.Counter}, dead={target.IsDead}, hazard={target.DiedInHazard}, explosion={target.ExplosionCompleted}, count={_entities.RoomEnemyCount}/{count}, flames={_entities.Entities<CandleFlame>().Count}, aiming={_player.IsUsingSeedShooter}, guard={guard}, dialogue={_dialogue.IsOpen}, room={_currentRoom.Id:x2}, transition={_transitions.IsTransitioning}, floorFrozen={_entities.FloorToggle?.Frozen}, link={_player.Position}, effects={string.Join(',', _entities.Entities<InteractionExplosionEffect>().Select(effect => $"{effect.ElapsedUpdates}/{effect.AnimationParameter}/{effect.Finished}"))}.");
            Step();
        }
    }
}
