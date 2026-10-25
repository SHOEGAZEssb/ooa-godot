using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateFireKeeseBiggoronHitStatusRom() => CompareFireKeeseHitStatusRom(false);
    private void ValidateFireKeeseBombHitStatusRom() => CompareFireKeeseHitStatusRom(true);

    private void CompareFireKeeseHitStatusRom(bool bomb)
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        foreach (bool lethal in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x85);
            var target = _entities.Entities<FireKeeseCharacter>().First();
            foreach (var other in _entities.EntityAdapters<IRoomEntity>().Where(a => !ReferenceEquals(a.Node, target)).ToArray())
                _entities.RetireEntity(other);
            byte floor = Enumerable.Range(0, _currentRoom.WidthInTiles * _currentRoom.HeightInTiles)
                .Select(i => _currentRoom.GetTerrainInfo(new(i % _currentRoom.WidthInTiles * 16 + 8, i / _currentRoom.WidthInTiles * 16 + 8)))
                .First(info => info.Collision == 0 && info.Type == TerrainType.Normal && info.Hazard == HazardType.None && info.Tile != 9).Tile;
            for (int y = 8; y < _currentRoom.Height; y += 16)
            for (int x = 8; x < _currentRoom.Width; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), floor, null, 0);
            _inventory.EquipA(0); _inventory.EquipB(0); _player.WarpTo(new(80, 64)); _player.Face(Vector2I.Up);
            var seed = _random.CaptureState();
            var native = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
            native[0xcc08] = 0x9d; native[0xd080] = 1; native[0xd081] = 0x39;
            native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
            native.Word(0xd08a, (int)(target.Position.Y * 256)); native.Word(0xd08c, (int)(target.Position.X * 256));
            StepGameplayUpdates(1, Vector2.Zero, afterUpdate: () => native.Update(_entities.FrameCounter, _player.Position));
            FailIf(target.State != 11 || native[0xd084] != 11 || target.Counter != native[0xd086] ||
                target.Angle != native[0xd089] || target.ZFixed != unchecked((short)native.Word(0xd08e)),
                "Fire Keese must initialize through the actual source state-zero path before contact staging.");
            int damage = bomb ? 4 : 5, initialHealth = lethal ? damage * 2 : 40;
            target.Health = initialHealth; native[0xd0a9] = (byte)initialHealth;
            bool paused = false, cancelled = false;
            int contacts = 0, updates = 0;
            var previousText = _entities.TextActiveSource;
            _entities.TextActiveSource = () => paused;
            void Stage(Vector2 position)
            {
                // Controlled post-initialization contact pose. Subsequent
                // native enemy updates/status are independent, not recopied.
                target.Position = position;
                typeof(FireKeeseCharacter).GetProperty(nameof(FireKeeseCharacter.ZFixed), hidden)!.SetValue(target, 0);
                native.Word(0xd08a, (int)(position.Y * 256)); native.Word(0xd08c, (int)(position.X * 256)); native.Word(0xd08e, 0);
            }
            void Compare()
            {
                native[0xcba0] = paused ? (byte)1 : (byte)0;
                native[0xcc8a] = _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects);
                native.Update(_entities.FrameCounter, _player.Position);
                native[0xd624] = native[0xd724] = 0;
                if (_entities.Biggoron?.Weapon is { Finished: false } sword)
                {
                    native[0xd600] = 1; native[0xd601] = 0x0c; native[0xd604] = 1;
                    native[0xd60b] = (byte)sword.Position.Y; native[0xd60d] = (byte)sword.Position.X;
                    native[0xd60f] = unchecked((byte)sword.ZHigh); native[0xd624] = (byte)sword.Collision;
                    native[0xd626] = (byte)sword.Radius.Y; native[0xd627] = (byte)sword.Radius.X;
                    native[0xd628] = unchecked((byte)-sword.Damage);
                }
                if (_entities.Entities<BombEffect>().SingleOrDefault() is { ExplosionCollisionEnabled: true } explosion)
                {
                    native[0xd700] = 1; native[0xd701] = 3; native[0xd704] = 0xff;
                    native[0xd70b] = (byte)explosion.Position.Y; native[0xd70d] = (byte)explosion.Position.X;
                    native[0xd70f] = unchecked((byte)explosion.CollisionZ); native[0xd724] = 0x98;
                    native[0xd726] = native[0xd727] = (byte)explosion.ExplosionRadius;
                    native[0xd728] = unchecked((byte)-explosion.Damage);
                }
                int healthBefore = native[0xd0a9];
                if (!paused) native.ResolveLinkCollisions();
                if (native[0xd0a9] < healthBefore) contacts++;
                string context = $"Fire Keese {(bomb ? "Bomb" : "Biggoron")} update={++updates}, batch={batched}, lethal={lethal}";
                if (cancelled) return;
                FailIf(target.IsDead != (native[0xd080] == 0), context + ": death completion differs.");
                if (target.IsDead) return;
                FailIf(target.Health != native[0xd0a9] || target.NativeHitPending != ((native[0xd0aa] & 0x80) != 0) ||
                    target.InvincibilityCounter != unchecked((sbyte)native[0xd0ab]) || target.KnockbackCounter != native[0xd0ad] ||
                    target.KnockbackAngle != native[0xd0ac] || target.State != native[0xd084] || target.Counter != native[0xd086] ||
                    target.Position != new Vector2(native.Word(0xd08c) / 256f, native.Word(0xd08a) / 256f),
                    $"{context}: native status/motion differs: HP={target.Health}/{native[0xd0a9]}, inv={target.InvincibilityCounter}/{unchecked((sbyte)native[0xd0ab])}, recoil={target.KnockbackCounter}/{native[0xd0ad]}, pending={target.NativeHitPending}/${native[0xd0aa]:x2}, counter={target.Counter}/{native[0xd086]}, XY={target.Position}/{native.Word(0xd08c)/256f},{native.Word(0xd08a)/256f}.");
            }
            void Step(int count = 1, bool press = false, Vector2 movement = default) =>
                StepGameplayUpdates(count, movement, press ? ["attack"] : [], press ? ["attack"] : [], batched, Compare);
            void StartAttack()
            {
                if (!bomb) { Stage(new(99, 66)); Step(1, press: true); return; }
                // The real Bomb parent creates, lifts and throws the child.
                // Freeze only enemy AI while that independent item setup runs.
                _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 4);
                Stage(new(200, 120));
                Step(1, press: true); Step(19); Step(1, press: true); Step(9); Step(32, movement: Vector2.Down);
                var child = _entities.Entities<BombEffect>().Single();
                Stage(child.Position + Vector2.Right * 8);
                typeof(BombEffect).GetField("_frameIndex", hidden)!.SetValue(child, 9);
                typeof(BombEffect).GetField("_frameCounter", hidden)!.SetValue(child, 1);
                _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0);
                Step();
            }
            try
            {
                _inventory.GiveTreasure(bomb ? TreasureId.Bombs : TreasureId.BiggoronSword, bomb ? 0x10 : 1);
                _inventory.EquipA(bomb ? TreasureId.Bombs : TreasureId.BiggoronSword);
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    _player.WarpTo(new(80, 64)); _player.Face(Vector2I.Up);
                    FailIf(target.NativeHitPending || target.HasPendingHit || target.KnockbackCounter != 0 || target.InvincibilityCounter != 0,
                        "Repeated attack must begin after the previous pending hit, recoil and invincibility have completed.");
                    StartAttack();
                    FailIf(contacts != repeat + 1 || target.Health != initialHealth - damage * (repeat + 1) ||
                        target.InvincibilityCounter != 26 || target.KnockbackCounter != 15 || !target.HasPendingHit,
                        "Actual weapon contact must publish one source $0a hit: invincibility $1a, recoil $0f, and one pending JUST_HIT.");
                    paused = true; Step(2); paused = false;
                    _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 4); Step(2);
                    _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0); Step(2);
                    FailIf(target.NativeHitPending || target.HasPendingHit || target.KnockbackCounter != 14,
                        "One eligible JUST_HIT update must be followed immediately by recoil $0e, with no second pending-hit skip.");
                    int wait = 0;
                    while ((!target.IsDead && (target.InvincibilityCounter != 0 || target.KnockbackCounter != 0)) ||
                        _entities.Biggoron?.Active == true || _entities.Entities<BombEffect>().Count != 0)
                    {
                        FailIf(wait++ > 80, "Fire Keese hit/weapon lifetime did not complete."); Step(2);
                    }
                    FailIf(contacts != repeat + 1 || target.IsDead != (lethal && repeat == 1),
                        "Each ordinary weapon use must hit once and complete the source surviving/lethal status path.");
                }
                if (!target.IsDead)
                {
                    _player.WarpTo(new(80, 64)); _player.Face(Vector2I.Up);
                    StartAttack();
                    FailIf(!target.NativeHitPending || !target.HasPendingHit || contacts != 3,
                        "Cancellation must begin with a fresh actual hit still pending, before its status update.");
                }
                _entities.ClearPhysicalPlayerItems(); _entities.Clear(); native[0xd080] = 0; native[0xd0a4] = 0;
                cancelled = true; Step(2);
                FailIf(_entities.Entities<FireKeeseCharacter>().Count != 0, "Room cancellation must retire the bat's pending-hit owner.");
            }
            finally { _entities.TextActiveSource = previousText; _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0); }
        }
        ReinitializeGameplayForValidation();
    }
}
