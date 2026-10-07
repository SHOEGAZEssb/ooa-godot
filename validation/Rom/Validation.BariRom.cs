using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBariRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            LoadValidationRoom(5, 0x35);
            _player.WarpTo(new(0x28, 0x28));
            FailIf(_collision.Collides(_player.Position), "$5:$35 Bari fixture must keep Link on real room floor.");
            var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager)
                .GetField("_enemySlots", flags)!.GetValue(_entities)!;
            var parent = _entities.Entities<BariCharacter>().Single();
            FailIf(parent.Position != new Vector2(0x88, 0x88) || slots.Single().Value != 0 || _entities.RoomEnemyCount != 1,
                "$5:$35 must admit its counted $3c:$00 placement at YX=$88,$88 in native slot $00.");
            var rom = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
            rom[0xcc08] = ValidationRom.LoadCleanUs().Span[0xfdd4b + 0x3c * 4];
            rom[0xd080] = 0x11; rom[0xd081] = 0x3c;
            rom.Word(0xd08a, 0x8800); rom.Word(0xd08c, 0x8800);
            var seed = _random.CaptureState(); rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            int initialKillSounds = _sound.PlayRequestsFor(SoundId.SndKillEnemy);
            bool frozen = true;
            int update = 0;
            _entities.TextActiveSource = () => frozen;
            void Compare()
            {
                rom[0xcba0] = (byte)(frozen ? 1 : 0);
                rom.Update(_entities.FrameCounter, _player.Position);
                var actual = _random.CaptureState();
                FailIf(actual.Rng1 != rom[0xff94] || actual.Rng2 != rom[0xff95] || actual.Calls - seed.Calls != rom.RandomCalls,
                    $"Bari $5:$35 update={update}, batch={batched}: shared RNG runtime=${actual.Rng2:x2}{actual.Rng1:x2}/{actual.Calls - seed.Calls}, native=${rom[0xff95]:x2}{rom[0xff94]:x2}/{rom.RandomCalls}, Link={_player.Position}, state/counter={parent.State}/{parent.Counter2}, native={rom[0xd084]}/{rom[0xd087]}.");
                foreach (var (entity, slot) in slots.Where(pair => pair.Key.Node is BariCharacter))
                {
                    var enemy = (BariCharacter)entity.Node;
                    int address = 0xd080 + slot * 256;
                    int timer = (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!.GetValue(enemy.Animation)!;
                    FailIf(rom[address] != 0x11 || enemy.Record.SubId != rom[address + 2] ||
                        enemy.State != rom[address + 4] || enemy.Substate != rom[address + 5] ||
                        enemy.Counter1 != rom[address + 6] || enemy.Counter2 != rom[address + 7] ||
                        enemy.Angle != rom[address + 9] || enemy.Speed != rom[address + 0x10] ||
                        enemy.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                        enemy.ZFixed != unchecked((short)rom.Word(address + 14)) ||
                        enemy.CollisionMode != rom[address + 0x25] ||
                        enemy.CollisionEnabled != ((rom[address + 0x24] & 0x80) != 0) ||
                        enemy.Health != rom[address + 0x29] ||
                        (enemy.InvincibilityCounter & 0xff) != rom[address + 0x2b] ||
                        enemy.KnockbackCounter != rom[address + 0x2d] ||
                        timer != rom[address + 0x20] || enemy.AnimationParameter != rom[address + 0x21] ||
                        enemy.Visible != ((rom[address + 0x1a] & 0x80) != 0),
                        $"Bari $5:$35 slot${slot:x2}, update={update}, batch={batched}: " +
                        $"runtime state/sub/counters=${enemy.State:x2}/{enemy.Substate}/{enemy.Counter1}/{enemy.Counter2}, angle=${enemy.Angle:x2}, XY={enemy.Position}, Z={enemy.ZFixed}, HP={enemy.Health}, inv/recoil={enemy.InvincibilityCounter}/{enemy.KnockbackCounter}, animation={timer}/{enemy.AnimationParameter}; " +
                        $"ROM=${rom[address + 4]:x2}/{rom[address + 5]}/{rom[address + 6]}/{rom[address + 7]}, angle=${rom[address + 9]:x2}, XY=${rom.Word(address + 12):x4}/${rom.Word(address + 10):x4}, Z=${rom.Word(address + 14):x4}, HP={rom[address + 0x29]}, inv/recoil=${rom[address + 0x2b]:x2}/{rom[address + 0x2d]}, animation={rom[address + 0x20]}/{rom[address + 0x21]}.");
                }
                int nativeCount = Enumerable.Range(0, 16).Count(slot => rom[0xd080 + slot * 256] != 0);
                var puffs = _entities.EntityAdapters<KillPuffRoomEntity>().ToArray();
                int nativePuffs = Enumerable.Range(2, 14).Count(slot =>
                    rom[0xd040 + slot * 256] != 0 && rom[0xd041 + slot * 256] == 0x08);
                FailIf(puffs.Length != nativePuffs, "Bari split must retain the native INTERAC$08 allocation/lifetime.");
                foreach (var puffOwner in puffs)
                {
                    var puff = (KillEnemyPuffEffect)puffOwner.Node;
                    int address = 0xd040 + _entities.InteractionSlot(puff) * 256;
                    FailIf(puff.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                        puff.ZHigh != unchecked((sbyte)rom[address + 15]) || puff.AnimationParameter != rom[address + 0x21] ||
                        puff.Visible != ((rom[address + 0x1a] & 0x80) != 0),
                        "Bari's split puff must copy integer XY/Z and preserve its native animation and visibility.");
                }
                FailIf(slots.Count != nativeCount || _entities.RoomEnemyCount != rom[0xcdd1] ||
                    _entities.ActiveRoomDefeatBitset != rom[0xcdc1] ||
                    _sound.PlayRequestsFor(SoundId.SndKillEnemy) - initialKillSounds != rom.Sounds.Count(sound => sound == SoundId.SndKillEnemy),
                    $"Bari split update={update}: native slots, counted enemies, defeat bits or puff sound differ.");
                update++;
            }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            // State zero is admitted under text. Shock completion falls
            // through animation setup into another timer and angle RNG pair.
            Step(3); frozen = false;
            int initialTimer = parent.Counter2;
            FailIf(initialTimer is not (60 or 90 or 120 or 150), "$3c source shock timer must come from its four ROM choices.");
            Step(initialTimer);
            FailIf(parent.State != 9 || parent.Counter2 != 60 || parent.CollisionMode != 0x59,
                "Bari must enter its electric state on the zero timer update.");
            frozen = true; Step(3); frozen = false;
            Step(60);
            FailIf(parent.State != 8 || parent.Counter2 is not (60 or 90 or 120 or 150) || parent.CollisionMode != 0x2d,
                "Bari must restore normal collision and fall through to random timer/angle setup after exactly 60 electric updates.");
            Step(parent.Counter2 + 60);
            // Execute the native late-collision handler, supply the same
            // declared sword to the adapter, and run recoil/splitting through
            // ordinary game updates. Keep Link out of the enemy's hitbox.
            var target = slots.Single(pair => pair.Key.Node == parent).Key;
            Vector2 origin = parent.Position.Floor() - Vector2.Right;
            rom.HitWithSword(ItemCollisionType.L1Sword, 1);
            var spawns = new List<RoomEntitySpawn>();
            FailIf(!((ISwordHittableRoomEntity)target).ApplySwordHit(parent.CollisionBounds, origin, 1,
                EnemyKnockbackStrength.Low, spawns) || spawns.Count != 0,
                "Normal Bari must accept a nonlethal sword hit and defer its split to enemy dispatch.");
            frozen = true; Step(3); frozen = false;
            Step(1);
            FailIf(parent.State != 10 || parent.Substate != 1 || parent.Counter2 != 4 || parent.Visible,
                "Bari JUST_HIT must begin the hidden four-update split before shared recoil consumes its remaining ticks.");
            Step(16);
            var children = _entities.Entities<BariCharacter>().ToArray();
            FailIf(children.Length != 2 || children.Any(child => child.Record.SubId != 1) ||
                slots.Values.Order().SequenceEqual(new[] { 1, 2 }) == false || _entities.RoomEnemyCount != 2 ||
                _entities.ActiveRoomDefeatBitset != 0,
                "Bari must allocate two counted Biri in slots $01/$02 before deleting its parent, preserving its kill index without recording a defeat.");
            Step(96); // The small Biri now nudge toward Link and share RNG.
            var childTarget = slots.OrderBy(pair => pair.Value).First();
            var child = (BariCharacter)childTarget.Key.Node;
            rom.HitWithSword(ItemCollisionType.L1Sword, 1, childTarget.Value);
            FailIf(!((ISwordHittableRoomEntity)childTarget.Key).ApplySwordHit(child.CollisionBounds,
                child.Position.Floor() - Vector2.Right, 1, EnemyKnockbackStrength.Low, spawns),
                "Biri must accept its source one-hit lethal damage without splitting again.");
            Step(12);
            FailIf(_entities.Entities<BariCharacter>().Count != 1 || _entities.ActiveRoomDefeatBitset != 0x02,
                "Lethal Biri must use ordinary enemy death and record the copied parent's kill index.");
            _entities.LoadRoom(5, _currentRoom);
            FailIf(_entities.Entities<BariCharacter>().Count != 0,
                "Biri's copied defeat index must suppress its placed parent on immediate room re-entry.");
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            LoadValidationRoom(5, 0x35);
            _entities.Clear();
            _entities.BeginScreenTransition(5, _currentRoom, Vector2.Left * _currentRoom.Width, _player);
            var incoming = _entities.Entities<BariCharacter>().Single();
            var position = incoming.Position; long calls = _random.Calls;
            FailIf(incoming.State != 8 || !incoming.Visible, "Bari state zero must initialize during scroll preload.");
            StepGameplayUpdates(4, Vector2.Zero, batched: batched);
            FailIf(incoming.Position != position || _random.Calls != calls, "Bari must freeze movement and RNG throughout scrolling.");
            _entities.FinishScreenTransition();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            ValidateBariElectricContactRom(batched);
        }
        ValidateBariSplitPoolRom();
        GD.Print("Validated $5:$35 Bari/Biri against clean-US dispatch: electric-cycle boundaries and RNG fallthrough, bobbing, animation, nonlethal recoil, ordered counted replacement, child movement/lethal death, text/scroll gates, plus real Link electric contact through split/batched gameplay.");
    }

    private void ValidateBariSplitPoolRom()
    {
        ReinitializeGameplayForValidation();
        _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
        LoadValidationRoom(5, 0x35);
        _player.WarpTo(new(0x28, 0x28));
        var parent = _entities.Entities<BariCharacter>().Single();
        var rom = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
        rom[0xcc08] = ValidationRom.LoadCleanUs().Span[0xfdd4b + 0x3c * 4];
        rom[0xd080] = 0x11; rom[0xd081] = 0x3c;
        rom.Word(0xd08a, 0x8800); rom.Word(0xd08c, 0x8800);
        var seed = _random.CaptureState(); rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
        void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: true,
            afterUpdate: () => rom.Update(_entities.FrameCounter, _player.Position));
        Step(1);
        // Declared uncounted reservations execute the native ENEMY_00 stub.
        // Only slot $01 is free when Bari attempts its two child allocations.
        for (int slot = 2; slot < 16; slot++)
        {
            _entities.RegisterEnemySlot(null, slot);
            int address = 0xd080 + slot * 256;
            rom[address] = 3; rom[address + 4] = 8; rom[address + 0x29] = 1;
        }
        rom.HitWithSword(ItemCollisionType.L1Sword, 1);
        var owner = _entities.EntityAdapters<BariRoomEntity>().Single();
        FailIf(!owner.ApplySwordHit(parent.CollisionBounds, parent.Position - Vector2.Right, 1,
            EnemyKnockbackStrength.Low, new List<RoomEntitySpawn>()), "Bari pool fixture rejected the source split collision.");
        Step(16);
        var child = _entities.Entities<BariCharacter>().Single();
        var actual = _random.CaptureState();
        FailIf(child.Record.SubId != 1 || rom[0xd080] != 0 || rom[0xd180] != 0x11 || rom[0xd182] != 1 ||
            child.Angle != rom[0xd189] || child.Position != new Vector2(rom.Word(0xd18c) / 256f, rom.Word(0xd18a) / 256f) ||
            _entities.RoomEnemyCount != 1 || rom[0xcdd1] != 1 || _entities.ActiveRoomDefeatBitset != 0 ||
            actual.Rng1 != rom[0xff94] || actual.Rng2 != rom[0xff95] || actual.Calls - seed.Calls != rom.RandomCalls,
            "One free Bari slot must admit only the first (+$04) child, drop the second attempt, then free its parent without defeat or extra RNG.");
        Step(8);
        FailIf(_entities.Entities<BariCharacter>().Count != 1 || _entities.FindFreeEnemySlot() != 0,
            "Bari's dropped second child must not retry after parent deletion frees slot $00.");
    }

    private void ValidateBariElectricContactRom(bool batched)
    {
        ReinitializeGameplayForValidation();
        _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
        LoadValidationRoom(5, 0x35);
        var target = _entities.Entities<BariCharacter>().Single();
        _player.WarpTo(new(0x28, 0x28));
        StepGameplayUpdates(1, Vector2.Zero, batched: batched);
        StepGameplayUpdates(target.Counter2, Vector2.Zero, batched: batched);
        FailIf(target.State != 9, "Bari contact fixture must reach its source electric state.");
        // Approach through the room's collision geometry, while the source
        // shock state holds Bari stationary. Mirror only the collision inputs.
        _player.WarpTo(target.Position.Floor() + Vector2.Down * 32);
        FailIf(_collision.Collides(_player.Position) || _player.OverlapsEnemyCollision(target.CollisionBounds, target.ZFixed >> 8),
            "Bari electric contact approach must start on accessible room floor outside its hitbox.");
        var collision = new ObjectCollisionRom();
        bool observing = true;
        void Observe()
        {
            if (!observing) return;
            collision.ClearObjects();
            collision[0xc6cb] = 0xff; collision[0xc6aa] = (byte)_player.HealthQuarters;
            collision[0xd024] = (byte)(_player.PatchCollisionsEnabled ? 0x80 : 0);
            collision[0xd02b] = unchecked((byte)(int)_player.InvincibilityFrames);
            collision[0xd02d] = (byte)_player.KnockbackFrames;
            collision[0xd00b] = (byte)_player.Position.Y; collision[0xd00d] = (byte)_player.Position.X;
            collision[0xd026] = collision[0xd027] = 6; collision[0xd029] = 1;
            collision[0xd0a4] = 0xbc; collision[0xd0a5] = 0x59; collision[0xd0a9] = 4;
            collision[0xd0be] = 1; collision[0xd0a6] = collision[0xd0a7] = 6;
            collision[0xd08b] = (byte)target.Position.Y; collision[0xd08d] = (byte)target.Position.X;
            collision[0xd08f] = unchecked((byte)(target.ZFixed >> 8));
            collision.Call(ObjectCollisionRom.Scan);
        }
        _entities.AddEntity(new CollisionRomObserver(Observe));
        int health = _player.HealthQuarters, approach = 0;
        while (!target.NativeHitPending && approach++ < 45)
        {
            bool released = approach % 12 == 0;
            StepGameplayUpdates(1, released ? Vector2.Zero : Vector2.Up,
                released ? [] : ["move_up"], approach % 12 == 1 ? ["move_up"] : [], batched);
            FailIf(target.NativeHitPending != ((collision[0xd0aa] & 0x80) != 0) ||
                target.CollisionEnabled != ((collision[0xd0a4] & 0x80) != 0) ||
                _player.PatchCollisionsEnabled != ((collision[0xd024] & 0x80) != 0) ||
                _player.InvincibilityFrames != unchecked((sbyte)collision[0xd02b]) ||
                _player.KnockbackFrames != collision[0xd02d] || (_player.PendingContactDamageRaw & 0xff) != collision[0xd025],
                $"Bari actual electric approach update={approach}, batch={batched}: runtime hit/collision/Link collision={target.NativeHitPending}/{target.CollisionEnabled}/{_player.PatchCollisionsEnabled}, inv/recoil={_player.InvincibilityFrames}/{_player.KnockbackFrames}, raw=${_player.PendingContactDamageRaw:x2}; native=${collision[0xd0aa]:x2}/${collision[0xd0a4]:x2}/${collision[0xd024]:x2}, inv/recoil=${collision[0xd02b]:x2}/{collision[0xd02d]}, raw=${collision[0xd025]:x2}.");
        }
        FailIf(!target.NativeHitPending || target.CollisionEnabled || target.Health != 4 ||
            _player.HealthQuarters != health || (_player.PendingContactDamageRaw & 0xff) != 0xf8 ||
            _player.InvincibilityFrames != 12 || _player.KnockbackFrames != 8,
            $"Bari electric contact must disable collision and publish deferred Link damage/recoil: approach={approach}, Link={_player.Position}, Bari={target.Position}, state={target.State}, pending={target.NativeHitPending}, collision={target.CollisionEnabled}, HP={target.Health}, Link HP={_player.HealthQuarters}/{health}, raw=${_player.PendingContactDamageRaw:x2}, inv/recoil={_player.InvincibilityFrames}/{_player.KnockbackFrames}.");
        observing = false;
        var link = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0,
            (int)_player.Position.X, (int)_player.Position.Y);
        link.InitializeLinkGameplay();
        link.Word(0xd00a, (int)(_player.PrecisePosition.Y * 256));
        link.Word(0xd00c, (int)(_player.PrecisePosition.X * 256));
        foreach (int address in new[] { 0xd024, 0xd025, 0xd02a, 0xd02b, 0xd02c, 0xd02d, 0xccdb })
            link[address] = collision[address];
        var menu = link.CreateMenuView();
        void CompareLink()
        {
            menu.AdvancePalette(); menu.AdvanceElectricShock();
            link.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); menu.UpdateScreenShake();
            FailIf(_player.HealthQuarters != link[0xc6aa] || (_player.PendingContactDamageRaw & 0xff) != link[0xd025] ||
                _player.ElectricShockCounter != link[0xccdc] || _player.KnockbackFrames != link[0xd02d],
                "Bari electric damage/shock handoff must match native Link dispatch, including its pre-text damage consumption.");
        }
        _dialogue.ShowMessage("Bari electric pause.", _player.Position.Y);
        link[0xcba0] = 1;
        StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: CompareLink);
        FailIf(!target.NativeHitPending || _player.HealthQuarters != health - 4,
            "Text must retain Bari's pending electric contact while Link's earlier damage handler consumes the shock.");
        _dialogue.Close(); link[0xcba0] = 0;
        StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: CompareLink);
        FailIf(target.State != 9 || target.NativeHitPending || _player.HealthQuarters != health - 4,
            "Next eligible Bari dispatch must retain its electric state while Link consumes the pending shock damage.");
        int remaining = target.Counter2;
        StepGameplayUpdates(remaining, Vector2.Zero, batched: batched, afterUpdate: CompareLink);
        FailIf(target.State != 8 || !target.CollisionEnabled, "Bari shock completion must restore its collision after electric contact.");
    }
}
