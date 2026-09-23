using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSomariaEnemyDamage()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        var random = CaptureOracleRandomForValidation();
        foreach (bool batch in new[] { false, true })
        foreach (var (id, subid, room, rawDamage) in new[] {
            (0x0c, 1, 0xab, 0xfc), (0x3d, 0, 0xb0, 0xfa), (0x48, 0, 0x9c, 0xfc),
            (0x24, 0, 0x9f, 0xfc), (0x4b, 0, 0xa0, 0xfc) })
        {
            // enemyData -> extraEnemyData: literal signed bytes, before the
            // divide-by-two conversion used by Link's quarter-heart owner.
            FailIf(new EnemyDatabase().ImportedEnemy(id, subid).RawDamage != rawDamage,
                $"Enemy ${id:x2}:${subid:x2} must retain raw damage ${rawDamage:x2}.");
            RestoreOracleRandomForValidation(random);
            LoadValidationRoom(4, room);
            _player.WarpTo(new(232, 144));
            void Step(int count = 1)
            {
                input.CaptureForValidation([], [], Vector2.Zero);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60.0, update);
            }
            Step(2);
            EnemyCharacter target = id switch {
                0x0c => _entities.Entities<ArrowMoblinCharacter>().First(e => e.Record.SubId == subid),
                0x3d or 0x48 => _entities.Entities<SwordEnemyCharacter>().First(e => e.Record.Id == id && e.Record.SubId == subid),
                0x24 => _entities.Entities<LikeLikeCharacter>().First(),
                _ => _entities.Entities<BallChainSoldierCharacter>().Single()
            };
            foreach (var enemy in _entities.Entities<EnemyCharacter>())
            {
                enemy.Position = new(32, 32);
                enemy.InvincibilityCounter = 127;
            }
            // Isolate body damage from PART$2a's separately tested deletion.
            foreach (var part in _entities.Entities<SpikedBallPart>()) part.ClearHealthAndCollision();
            Vector2 point = new(120, 86);
            // Keep this recoil fixture clear of room walls and hazards.
            for (int y = 72; y <= 104; y += 16)
                for (int x = 104; x <= 168; x += 16)
                    _currentRoom.SetPositionTileAndCollision(new(x, y), 0x0c, 0, 0);
            target.Health = 9; // Controlled durability: three source damage-$fc block hits.
            SomariaBlock CreateBlock()
            {
                FailIf(!_entities.TryCreateSomariaBlock(_player, 4, point, 0), "Combat fixture must allocate ITEM$18.");
                var result = _entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
                Step(10);
                FailIf(result.State != 3 || result.Damage != -4, "Solid Somaria block must retain raw damage$fc.");
                return result;
            }
            var block = CreateBlock();
            target.InvincibilityCounter = 0;
            for (int hit = 0; hit < 3; hit++)
            {
                if (block.Finished) block = CreateBlock();
                target.Position = point + new Vector2(8, 0);
                int blockHealth = block.Health;
                Step();
                int health = Math.Max(0, 9 - (hit + 1) * 4);
                FailIf(target.Health != health || target.InvincibilityCounter != 21 || target.KnockbackCounter != 11 ||
                    block.Health != blockHealth || block.DamageToApply != unchecked((sbyte)rawDamage) || block.ContactFlags != 1,
                    $"Somaria vs enemy ${id:x2} hit{hit}: expected HP{health}/inv21/recoil11 and deferred raw block damage; got HP{target.Health}/inv{target.InvincibilityCounter}/recoil{target.KnockbackCounter}, block {block.Health}/{block.DamageToApply}.");
                FailIf(health == 0 && (target.CollisionEnabled || target.IsDead),
                    "Lethal Somaria damage disables collision immediately but retains the enemy through JUST_HIT/recoil.");
                Vector2 struckAt = target.Position;
                var textSource = _entities.TextActiveSource;
                try
                {
                    _entities.TextActiveSource = () => true;
                    Step(2);
                    FailIf(block.Health != blockHealth || target.InvincibilityCounter != 21 || target.KnockbackCounter != 11,
                        "Dialogue freeze must retain both sides of the pending collision exchange.");
                }
                finally { _entities.TextActiveSource = textSource; }
                Step();
                int remaining = (byte)(blockHealth + unchecked((sbyte)rawDamage));
                FailIf(block.Health != remaining || block.DamageToApply != 0 || block.Finished != ((remaining & 0x80) != 0) ||
                    target.InvincibilityCounter != 20 || target.KnockbackCounter != 11 ||
                    id != 0x4b && target.Position != struckAt,
                    $"Enemy ${id:x2} JUST_HIT must preserve recoil11 while ITEM$18 consumes its raw damage once.");
                if (block.Finished)
                    FailIf(_currentRoom.GetMetatile(point) != 0x0c, "Damage-deleted Somaria must restore the underlying tile.");
                Step(11);
                FailIf(target.KnockbackCounter != 0 || target.InvincibilityCounter != 9 || target.IsDead,
                    $"Enemy ${id:x2} hit{hit} must complete eleven recoil updates before death dispatch; got recoil{target.KnockbackCounter}/inv{target.InvincibilityCounter}/dead{target.IsDead}, position{target.Position}.");
                if (health == 0)
                {
                    Step();
                    FailIf(_entities.Entities<EnemyCharacter>().Contains(target),
                        $"Enemy ${id:x2} must retire after its completed lethal recoil.");
                    break;
                }
                target.Position = new(32, 32);
                Step(9);
                FailIf(target.InvincibilityCounter != 0, "Somaria's 21-update enemy invincibility must expire before a repeat hit.");
            }
        }
        foreach (bool blocking in new[] { false, true })
        {
            LoadValidationRoom(4, 0x9c);
            _player.WarpTo(new(232, 144));
            void Step(int count)
            {
                input.CaptureForValidation([], [], Vector2.Zero);
                scheduler.Advance(count / 60.0, update);
            }
            Step(2);
            var enemies = _entities.Entities<SwordEnemyCharacter>().ToArray();
            foreach (var enemy in enemies) { enemy.Position = new(32, 32); enemy.InvincibilityCounter = 127; }
            Vector2 point = new(120, 86);
            for (int y = 72; y <= 104; y += 16)
                for (int x = 104; x <= 168; x += 16)
                    _currentRoom.SetPositionTileAndCollision(new(x, y), 0x0c, 0, 0);
            FailIf(!_entities.TryCreateSomariaBlock(_player, 4, point, 0), "Overlapping-enemy fixture must allocate ITEM$18.");
            var block = _entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
            Step(10);
            foreach (var enemy in enemies)
            {
                enemy.Position = point + new Vector2(8, 0);
                if (blocking)
                {
                    // The source mode writer selects armored mode while
                    // recoil is active, independently of Link's angle.
                    enemy.ApplySwordKnockback(point, EnemyKnockbackStrength.Normal);
                    enemy.UpdateFrame(_player.Position);
                }
                else
                    foreach (var direction in new[] { Vector2.Up, Vector2.Right, Vector2.Down, Vector2.Left })
                    {
                        enemy.UpdateFrame(enemy.Position + direction * 64);
                        if (!enemy.SwordBlocking) break;
                    }
                FailIf(enemy.SwordBlocking != blocking || enemy.CollisionMode !=
                    (enemy.IsDarknut ? blocking ? 0x56 : 0x20 : blocking ? 0x55 : 0x11),
                    "Somaria fixture must exercise both live sword-enemy collision-mode writers.");
                enemy.Position = point + new Vector2(8, 0);
                enemy.Health = 8;
                enemy.InvincibilityCounter = 0;
            }
            // Original room order is Sword Moblin ($fa), then Darknut ($fc).
            // Both may hit the same block: item.var2a does not gate item scans.
            _entities.ResolvePostObjectCollisions(_player);
            FailIf(enemies.Any(enemy => enemy.Health != 4 || enemy.InvincibilityCounter != 21 ||
                    enemy.KnockbackCounter != 11 || enemy.KnockbackAngle != 8 || !enemy.NativeHitPending) ||
                block.DamageToApply != -4 || block.Health != 9 || block.ContactFlags != 1 || block.KnockbackAngle != 24,
                "Somaria must damage both normal/armored enemies, with the last native enemy overwriting block damage and opposite recoil angles.");
            Step(1);
            FailIf(block.Health != 5 || block.DamageToApply != 0 || block.ContactFlags != 1 ||
                enemies.Any(enemy => enemy.Health != 4 || enemy.InvincibilityCounter != 20 || enemy.KnockbackCounter != 11),
                "The next update must consume only the last enemy's damage and preserve each enemy's JUST_HIT boundary.");
        }
        GD.Print("Validated live Somaria damage exchanges for Crown enemy families, raw block damage, freeze, repeated hits, delayed death, armored modes and last-contact overwrite.");
    }
}
