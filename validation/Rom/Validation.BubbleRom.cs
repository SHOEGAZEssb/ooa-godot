using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBubbleRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(5, 0x3e);
            var bubbles = _entities.Entities<BubbleCharacter>().ToArray();
            FailIf(bubbles.Length != 2 || bubbles[0].Position != new Vector2(0x58, 0x28) ||
                bubbles[1].Position != new Vector2(0xc8, 0x78) || _entities.RoomEnemyCount != 2,
                "$5:$3e must retain two uncounted $15:$00 Bubbles before its two counted enemies.");
            foreach (var enemy in _entities.Entities<EnemyCharacter>().Where(enemy => enemy is not BubbleCharacter))
                enemy.FinishGale();
            var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager)
                .GetField("_enemySlots", flags)!.GetValue(_entities)!;
            var targets = slots.Where(pair => pair.Key.Node is BubbleCharacter).ToArray();
            var rom = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
            rom[0xcc08] = ValidationRom.LoadCleanUs().Span[0xfdd4b + 0x15 * 4];
            rom[0xcdd1] = 0;
            var seed = _random.CaptureState();
            rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            SeedItemDropProducersRom(rom);
            foreach (var (entity, slot) in targets)
            {
                int address = 0xd080 + slot * 256;
                rom[address] = 3; rom[address + 1] = 0x15;
                rom.Word(address + 10, (int)(entity.Node.Position.Y * 256));
                rom.Word(address + 12, (int)(entity.Node.Position.X * 256));
            }
            bool frozen = true;
            int update = 0, turns = 0;
            int[] angles = new int[2];
            _entities.TextActiveSource = () => frozen;
            void Compare()
            {
                rom[0xcba0] = (byte)(frozen ? 1 : 0);
                rom.Update(_entities.FrameCounter, _player.Position);
                for (int index = 0; index < targets.Length; index++)
                {
                    var enemy = (BubbleCharacter)targets[index].Key.Node;
                    int address = 0xd080 + targets[index].Value * 256;
                    int timer = (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!.GetValue(enemy.Animation)!;
                    FailIf(enemy.State != rom[address + 4] || enemy.Angle != rom[address + 9] ||
                        enemy.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                        timer != rom[address + 0x20] || enemy.AnimationParameter != rom[address + 0x21] ||
                        enemy.Visible != ((rom[address + 0x1a] & 0x80) != 0),
                        $"Bubble $5:$3e slot${targets[index].Value:x2}, update={update}, batch={batched}: " +
                        $"runtime state/angle=${enemy.State:x2}/${enemy.Angle:x2}, XY={enemy.Position}, animation={timer}/{enemy.AnimationParameter}; " +
                        $"ROM=${rom[address + 4]:x2}/${rom[address + 9]:x2}, XY=${rom.Word(address + 12):x4}/${rom.Word(address + 10):x4}, animation={rom[address + 0x20]}/{rom[address + 0x21]}.");
                    if (enemy.State == 8 && angles[index] != enemy.Angle) turns++;
                    angles[index] = enemy.Angle;
                }
                var actual = _random.CaptureState();
                FailIf(actual.Rng1 != rom[0xff94] || actual.Rng2 != rom[0xff95] || actual.Calls - seed.Calls != rom.RandomCalls,
                    $"Bubble $5:$3e update={update}: shared RNG differs from native object dispatch.");
                update++;
            }
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: Compare);
            frozen = false;
            StepGameplayUpdates(240, Vector2.Zero, batched: batched, afterUpdate: Compare);
            FailIf(turns < 3, "Bubble native trace must visit several centered/wall direction choices.");
            frozen = true;
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: Compare);
            frozen = false;
            StepGameplayUpdates(8, Vector2.Zero, batched: batched, afterUpdate: Compare);
            _entities.Clear();
            _entities.BeginScreenTransition(5, _currentRoom, Vector2.Left * _currentRoom.Width, _player);
            var incoming = _entities.Entities<BubbleCharacter>().ToArray();
            var positions = incoming.Select(enemy => enemy.Position).ToArray();
            long calls = _random.Calls;
            FailIf(incoming.Length != 2 || incoming.Any(enemy => enemy.State != 8 || !enemy.Visible),
                "$5:$3e Bubble preload must initialize state zero.");
            StepGameplayUpdates(4, Vector2.Zero, batched: batched);
            FailIf(_random.Calls != calls || incoming.Where((enemy, index) => enemy.Position != positions[index]).Any(),
                "$5:$3e Bubble movement and RNG must freeze while scrolling.");
            _entities.FinishScreenTransition();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);

            foreach (bool immune in new[] { false, true }) ValidateBubbleJinxRom(batched, immune);
        }
        GD.Print("Validated $5:$3e Bubble movement, animation, shared RNG, text/scroll gates and real Link contact against clean-US dispatch; delayed jinx, Whisp Ring, item cancellation, text pause and 180-update sword recovery through split/batched gameplay.");
    }

    private void ValidateBubbleJinxRom(bool batched, bool immune)
    {
        ReinitializeGameplayForValidation();
        LoadValidationRoom(5, 0x3e);
        var target = _entities.Entities<BubbleCharacter>()[0];
        foreach (var enemy in _entities.Entities<EnemyCharacter>().Where(enemy => enemy != target)) enemy.FinishGale();
        _inventory.GiveTreasure(TreasureId.Sword, 1);
        _inventory.GiveTreasure(TreasureId.Feather, 0);
        _inventory.EquipA(TreasureId.Sword); _inventory.EquipB(TreasureId.Feather);
        if (immune)
        {
            _inventory.GiveTreasure(TreasureId.RingBox, 1);
            _inventory.GrantAppraisedRingForDebug(0x39);
            FailIf(!_inventory.SetRingBoxSlotFromList(0, 0x39) || !_inventory.EquipRingAt(0), "Cannot equip Whisp Ring $39.");
        }
        _player.WarpTo(new(0x58, 0x48));
        FailIf(_collision.Collides(_player.Position) || _player.OverlapsEnemyCollision(target.CollisionBounds),
            "$5:$3e Link must begin on real floor outside the Bubble hitbox.");
        var collision = new ObjectCollisionRom();
        int beforeHealth = _player.HealthQuarters;
        bool observing = true;
        void Observe()
        {
            if (!observing) return;
            collision.ClearObjects();
            collision[0xc6cb] = (byte)(immune ? 0x39 : 0xff);
            collision[0xc6aa] = (byte)_player.HealthQuarters;
            collision[0xd024] = (byte)(_player.PatchCollisionsEnabled ? 0x80 : 0);
            collision[0xd02b] = unchecked((byte)(int)_player.InvincibilityFrames);
            collision[0xd02d] = (byte)_player.KnockbackFrames;
            collision[0xd00b] = (byte)_player.Position.Y; collision[0xd00d] = (byte)_player.Position.X;
            collision[0xd026] = collision[0xd027] = 6; collision[0xd029] = 1;
            collision[0xd0a4] = 0x95; collision[0xd0a5] = 0x19;
            collision[0xd0a9] = 0x7f;
            collision[0xd0be] = 1; // enemyStandardUpdate initializes var3e=$01.
            collision[0xd0a6] = collision[0xd0a7] = 6;
            collision[0xd08b] = (byte)target.Position.Y; collision[0xd08d] = (byte)target.Position.X;
            collision.Call(ObjectCollisionRom.Scan);
        }
        var observer = new CollisionRomObserver(Observe);
        _entities.AddEntity(observer);
        int approach = 0;
        while (!target.NativeHitPending && approach++ < 120)
        {
            Vector2 delta = target.Position - _player.Position;
            Vector2 movement = Mathf.Abs(delta.X) > Mathf.Abs(delta.Y) ? new(Mathf.Sign(delta.X), 0) : new(0, Mathf.Sign(delta.Y));
            StepGameplayUpdates(1, movement, batched: batched);
            FailIf(target.NativeHitPending != ((collision[0xd0aa] & 0x80) != 0) ||
                _player.InvincibilityFrames != unchecked((sbyte)collision[0xd02b]) || _player.KnockbackFrames != collision[0xd02d] ||
                _player.NativeContactSignal != (collision[0xd02a] != 0) || _player.PendingContactDamageRaw != collision[0xd025],
                $"Bubble actual approach batch={batched}, ring={immune}, update={approach}: native collision signals/counters differ: " +
                $"hit={target.NativeHitPending}/${collision[0xd0aa]:x2}, inv={_player.InvincibilityFrames}/{unchecked((sbyte)collision[0xd02b])}, recoil={_player.KnockbackFrames}/{collision[0xd02d]}, XY={_player.Position}/{target.Position}.");
        }
        FailIf(!target.NativeHitPending || _player.HealthQuarters != beforeHealth ||
            _player.InvincibilityFrames != 25 || _player.KnockbackFrames != 7 ||
            _entities.RuntimeState.ReadWramByte(WramAddress.wSwordDisabledCounter) != 0,
            "Bubble late contact must publish zero-damage recoil without setting the sword jinx yet.");
        observing = false;
        var status = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
        status[0xcc08] = ValidationRom.LoadCleanUs().Span[0xfdd4b + 0x15 * 4];
        status[0xc6cb] = (byte)(immune ? 0x39 : 0xff);
        status[0xd080] = 3; status[0xd081] = 0x15; status[0xd084] = 8;
        status[0xd089] = (byte)target.Angle; status[0xd090] = 0x1e;
        status[0xd0a4] = 0x95; status[0xd0a5] = 0x19; status[0xd0a9] = 0x7f; status[0xd0aa] = collision[0xd0aa];
        status.Word(0xd08a, (int)(target.Position.Y * 256)); status.Word(0xd08c, (int)(target.Position.X * 256));
        var seed = _random.CaptureState(); status[0xff94] = seed.Rng1; status[0xff95] = seed.Rng2;
        _dialogue.ShowMessage("Deferred Bubble contact.", _player.Position.Y);
        status[0xcba0] = 1;
        StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: () =>
        {
            status.Update(_entities.FrameCounter, _player.Position);
            FailIf(!target.NativeHitPending || status[0xd0aa] != 0x80 || status[0xcc59] != 0 ||
                _entities.RuntimeState.ReadWramByte(WramAddress.wSwordDisabledCounter) != 0,
                "Text must retain Bubble's pending contact until the next eligible object update.");
        });
        _dialogue.Close(); status[0xcba0] = 0;
        StepGameplayUpdates(1, Vector2.Zero, batched: batched);
        status.Update(_entities.FrameCounter, _player.Position);
        FailIf(status[0xcc59] != (immune ? 0 : 180) ||
            _entities.RuntimeState.ReadWramByte(WramAddress.wSwordDisabledCounter) != status[0xcc59],
            "Bubble next eligible native dispatch must sample Whisp Ring and set the 180-update sword jinx.");
        target.FinishGale(); // Prevent a second body contact refreshing this countdown.
        var sword = new SwordRom(0, 1);
        sword[0xcc59] = status[0xcc59];
        void CompareTimer() => FailIf(_entities.RuntimeState.ReadWramByte(WramAddress.wSwordDisabledCounter) != sword[0xcc59],
            $"Bubble jinx eligible-item countdown differs: runtime={_entities.RuntimeState.ReadWramByte(WramAddress.wSwordDisabledCounter)}, ROM={sword[0xcc59]}, batch={batched}, ring={immune}.");
        void Step(int count, bool press = false)
        {
            int update = 0;
            StepGameplayUpdates(count, Vector2.Zero, press ? ["attack"] : [], press ? ["attack"] : [], batched, () =>
            {
                sword.Update(press, press && update++ == 0);
                CompareTimer();
            });
        }
        Step(1, press: true);
        FailIf(_player.IsAttacking != immune || (sword[0xd200] != 0) != immune,
            "Bubble jinx must reject a new sword parent; Whisp Ring must allow it.");
        _dialogue.ShowMessage("Jinx countdown pause.", _player.Position.Y); sword[0xcba0] = 1;
        Step(3);
        _dialogue.Close(); sword[0xcba0] = 0;
        Step(24);
        if (immune) return;
        StepGameplayUpdates(1, Vector2.Zero, ["item"], ["item"], batched);
        sword.Update(false); CompareTimer();
        FailIf(_player.IsGroundedForFloorButton, "Sword jinx must leave Feather usable through actual item input.");
        Step(152);
        FailIf(sword[0xcc59] != 2, "Bubble countdown fixture must reach the last two eligible updates independently of text.");
        Step(1, press: true);
        FailIf(_player.IsAttacking || sword[0xd200] != 0, "Counter $02 -> $01 must still reject the sword.");
        Step(1);
        Step(1, press: true);
        FailIf(!_player.IsAttacking || sword[0xd200] == 0, "A fresh press after counter $01 -> $00 must restore the sword.");
        _entities.RuntimeState.SetWramByte(WramAddress.wSwordDisabledCounter, 4);
        sword[0xcc59] = 4;
        Step(1);
        FailIf(_player.IsAttacking || sword[0xd200] != 0, "A newly applied jinx must cancel an already active sword parent.");
    }
}
