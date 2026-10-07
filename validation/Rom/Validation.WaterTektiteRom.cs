using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateWaterTektiteRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(5, 0x23);
            // Source room5:$23 has three placed Water Tektites. Keep its water
            // banks and collision geometry; Link waits on the southern floor.
            _player.WarpTo(new(0x78, 0x98));
            FailIf(_currentRoom.IsSolid(_player.Position), "Water Tektite $5:$23 fixture must stand on reachable floor.");
            var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager)
                .GetField("_enemySlots", flags)!.GetValue(_entities)!;
            var enemies = slots.Where(pair => pair.Key.Node is WaterTektiteCharacter).ToArray();
            FailIf(enemies.Length != 3 || _entities.RoomEnemyCount != 3,
                "Water Tektite $5:$23 must construct the three source placements in the counted enemy stream.");
            var rom = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
            // All native graphics are already resident in this bounded fixture.
            rom[0xcc08] = ValidationRom.LoadCleanUs().Span[0xfdd4b + 0x3a * 4];
            rom[0xcc0a] = ValidationRom.LoadCleanUs().Span[0xfdd4b + 0x19 * 4];
            rom[0xcdd1] = 3;
            var seed = _random.CaptureState();
            rom[0xff94] = seed.Rng1;
            rom[0xff95] = seed.Rng2;
            foreach (var (entity, slot) in slots)
            {
                int address = 0xd080 + slot * 0x100;
                bool water = entity.Node is WaterTektiteCharacter;
                rom[address] = (byte)(((slot + 1) << 4) | (water ? 1 : 3));
                rom[address + 1] = (byte)(water ? 0x3a : 0x19);
                rom.Word(address + 0x0a, (int)(entity.Node.Position.Y * 256));
                rom.Word(address + 0x0c, (int)(entity.Node.Position.X * 256));
            }
            bool frozen = true;
            int update = 0, restUpdates = 0;
            _entities.TextActiveSource = () => frozen;
            void Compare()
            {
                rom[0xcba0] = (byte)(frozen ? 1 : 0);
                rom.Update(_entities.FrameCounter, _player.Position);
                foreach (var (entity, slot) in enemies)
                {
                    var enemy = (WaterTektiteCharacter)entity.Node;
                    int address = 0xd080 + slot * 0x100;
                    int timer = (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!.GetValue(enemy.Animation)!;
                    FailIf(enemy.State != rom[address + 4] || enemy.Counter != rom[address + 6] ||
                        enemy.Angle != rom[address + 9] || enemy.Speed != rom[address + 0x10] ||
                        enemy.Position != new Vector2(rom.Word(address + 12) / 256.0f, rom.Word(address + 10) / 256.0f) ||
                        timer != rom[address + 0x20] || enemy.AnimationParameter != rom[address + 0x21] ||
                        enemy.KnockbackCounter != rom[address + 0x2d] ||
                        enemy.StunCounter != rom[address + 0x2e] ||
                        (enemy.InvincibilityCounter & 0xff) != rom[address + 0x2b] ||
                        enemy.Visible != ((rom[address + 0x1a] & 0x80) != 0),
                        $"Water Tektite $5:$23 slot${slot:x2}, update={update}, batch={batched}: " +
                        $"ROM state/counter/angle/speed=${rom[address + 4]:x2}/${rom[address + 6]:x2}/${rom[address + 9]:x2}/${rom[address + 0x10]:x2}, " +
                        $"XY=${rom.Word(address + 12):x4}/${rom.Word(address + 10):x4}, animation={rom[address + 0x20]}; " +
                        $"runtime=${enemy.State:x2}/${enemy.Counter:x2}/${enemy.Angle:x2}/${enemy.Speed:x2}, XY={enemy.Position}, animation={timer}, stun/inv={enemy.StunCounter}/{enemy.InvincibilityCounter}; native={rom[address + 0x2e]}/${rom[address + 0x2b]:x2}.");
                    if (enemy.State == 9 && !frozen) restUpdates++;
                }
                var actual = _random.CaptureState();
                FailIf(actual.Rng1 != rom[0xff94] || actual.Rng2 != rom[0xff95] ||
                    actual.Calls - seed.Calls != rom.RandomCalls,
                    $"Water Tektite $5:$23 update={update}: shared RNG runtime=${actual.Rng2:x2}{actual.Rng1:x2}/{actual.Calls-seed.Calls}, ROM=${rom[0xff95]:x2}{rom[0xff94]:x2}/{rom.RandomCalls}.");
                update++;
            }
            // State zero runs under text; subsequent updates freeze. Exercise
            // two complete swim/rest cycles, freeze during movement, then resume.
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: Compare);
            frozen = false;
            StepGameplayUpdates(80, Vector2.Zero, batched: batched, afterUpdate: Compare);
            frozen = true;
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: Compare);
            frozen = false;
            StepGameplayUpdates(80, Vector2.Zero, batched: batched, afterUpdate: Compare);
            FailIf(restUpdates != 48, "Water Tektite $3a swim/rest trace must observe two eight-update pauses per enemy.");
            // Supply the declared post-collision recoil signal, then use the
            // real update phases. Water recoil has its own handler rather than
            // ecom_updateKnockback's wall cancellation and high-recoil dust.
            var first = (WaterTektiteCharacter)enemies[0].Key.Node;
            int firstAddress = 0xd080 + enemies[0].Value * 0x100;
            foreach (int recoil in new[] { 9, 0x89 })
            {
                SetEnemyStatusByte(first, nameof(EnemyCharacter.KnockbackCounter), recoil);
                SetEnemyStatusByte(first, nameof(EnemyCharacter.KnockbackAngle), 8);
                first.DeferNativeHitStatus();
                rom[firstAddress + 0x2d] = (byte)recoil;
                rom[firstAddress + 0x2c] = 8;
                rom[firstAddress + 0x2a] = 0x80;
                StepGameplayUpdates(12, Vector2.Zero, batched: batched, afterUpdate: Compare);
                FailIf(first.KnockbackCounter != rom[firstAddress + 0x2d] ||
                    _entities.Entities<KnockbackDustRoomEntity>().Count != 0,
                    "Water Tektite $3a recoil must retain source counter and omit high-recoil dust.");
            }
            // The common collision table maps boomerang to effect22. Execute
            // that native collision, then compare the shared stun/shake walk
            // while the two other swimmers and Whisps keep updating normally.
            FailIf(enemies[0].Value != 0, "Water Tektite $5:$23 source's first enemy must occupy native slot $00.");
            rom.HitWithSword(ItemCollisionType.L1Boomerang, 0);
            FailIf(rom[firstAddress + 0x2e] != 120 || rom[firstAddress + 0x2b] != 0xf0,
                "Water Tektite boomerang source collision must assign 120 stun ticks and signed -16 invincibility.");
            var boomerang = new BoomerangItem(_currentRoom, first.Position - Vector2.Right,
                8, 0, () => 0, _sound.PlaySound, (_, _) => { });
            try
            {
                boomerang.UpdateFrame(_player.Position, _player.Position, 0);
                var response = ((IBoomerangCollisionRoomEntity)enemies[0].Key)
                    .ApplyBoomerangCollision(boomerang, new List<RoomEntitySpawn>());
                FailIf(!response.Contact || !response.Returns || first.StunCounter != 120 ||
                    first.InvincibilityCounter != -16 || first.NativeHitPending,
                    "Water Tektite boomerang adapter must publish the source stun without JUST_HIT and return the item.");
            }
            finally { boomerang.Free(); }
            // ENEMYDMG_24 has no bit6: boomerang stun does not publish
            // JUST_HIT and can decrement on the very next odd update.
            StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: Compare);
            frozen = true;
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: Compare);
            frozen = false;
            StepGameplayUpdates(242, Vector2.Zero, batched: batched, afterUpdate: Compare);
            FailIf(first.StunCounter != 0, "Water Tektite odd-update stun countdown must complete and resume swimming.");
            // Incoming state zero consumes common/angle RNG for all five
            // objects after the source's 256-byte placement buffer. Later
            // states remain frozen throughout scrolling, then resume normally.
            _entities.Clear();
            long calls = _random.Calls;
            _entities.BeginScreenTransition(5, _currentRoom, Vector2.Left * _currentRoom.Width, _player);
            var incoming = _entities.Entities<WaterTektiteCharacter>().ToArray();
            FailIf(incoming.Length != 3 || _random.Calls - calls != 266 ||
                incoming.Any(enemy => !enemy.Visible || enemy.State != 8 || enemy.Counter != 64),
                "Water Tektite $5:$23 preload must initialize source objects and preserve ordered RNG consumption.");
            var positions = incoming.Select(enemy => enemy.Position).ToArray();
            calls = _random.Calls;
            StepGameplayUpdates(4, Vector2.Zero, batched: batched);
            FailIf(_random.Calls != calls || incoming.Where((enemy, index) =>
                enemy.Counter != 64 || enemy.Position != positions[index]).Any(),
                "Water Tektite $5:$23 incoming movement/RNG must freeze during scrolling.");
            _entities.FinishScreenTransition();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(incoming.Any(enemy => enemy.Counter != 63),
                "Water Tektite $5:$23 must resume its first swim update after scrolling.");
        }
        // Scent is sampled only when choosing the next angle. This bounded
        // species-input check executes its initial and next choice without
        // replaying a seed lifecycle already covered by the item regressions.
        var scentRom = new EnemyStatusRom(_currentRoom, _saveData, 0);
        scentRom[0xcc08] = ValidationRom.LoadCleanUs().Span[0xfdd4b + 0x3a * 4];
        var random = new OracleRandom();
        var scentSeed = random.CaptureState();
        scentRom[0xff94] = scentSeed.Rng1;
        scentRom[0xff95] = scentSeed.Rng2;
        scentRom[0xd080] = 1;
        scentRom[0xd081] = 0x3a;
        scentRom[0xccd9] = 1;
        scentRom.Word(0xd08a, 0x4880);
        scentRom.Word(0xd08c, 0x6880);
        var attracted = new WaterTektiteCharacter();
        attracted.Initialize(new EnemyDatabase().ImportedEnemy(0x3a), _currentRoom,
            new(104.5f,72.5f), random);
        try
        {
            for (int update = 0; update <= 72; update++)
            {
                Vector2 target = update < 36 ? new(0x88,0x48) : new(0x58,0x68);
                scentRom.Update(update, target);
                attracted.UpdateFrame(target);
                FailIf(attracted.Angle != scentRom[0xd089] || attracted.Counter != scentRom[0xd086] ||
                    attracted.State != scentRom[0xd084] ||
                    attracted.Position != new Vector2(scentRom.Word(0xd08c)/256.0f,scentRom.Word(0xd08a)/256.0f),
                    $"Water Tektite $3a scent update {update}: runtime angle/state/counter=${attracted.Angle:x2}/${attracted.State:x2}/${attracted.Counter:x2}, XY={attracted.Position}; ROM=${scentRom[0xd089]:x2}/${scentRom[0xd084]:x2}/${scentRom[0xd086]:x2}, XY=${scentRom.Word(0xd08c):x4}/${scentRom.Word(0xd08a):x4}.");
            }
            var final = random.CaptureState();
            FailIf(random.Calls != 1 || scentRom.RandomCalls != 1 ||
                final.Rng1 != scentRom[0xff94] || final.Rng2 != scentRom[0xff95],
                "Water Tektite $3a scent choices must suppress angle RNG while retaining common initialization RNG.");
        }
        finally { attracted.Free(); }
        GD.Print("Validated $5:$23 Water Tektite placements, water-bank movement, counter cycles, shared RNG, animation, text freeze, normal/high recoil and native boomerang stun against clean-US object updates through split/batched gameplay; scent direction choices against native species dispatch.");
    }
}
