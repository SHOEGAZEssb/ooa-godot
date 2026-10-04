using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCollisionOrderRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        int hostCase1 = 0;
        foreach (bool reverse in new[] { false, true })
        foreach (int subid in new[] { 0, 1 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x60);
            _entities.Clear();
            for (int y = 8; y < 128; y += 16)
            for (int x = 8; x < 160; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0x0c, 0, 0);
            _player.WarpTo(new(72, 72));
            var shots = new[]
            {
                _entities.Spawn<SmogProjectilePart>(new SmogProjectileSpawn(new(80, 72), subid)),
                _entities.Spawn<SmogProjectilePart>(new SmogProjectileSpawn(new(80, 72), subid))
            };
            var parts = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_partSlots", flags)!.GetValue(_entities)!;
            var targets = _entities.EntityAdapters<SmogProjectileRoomEntity>().ToArray();
            var rom = new ObjectCollisionRom();
            var data = new SmogProjectileDatabase();
            bool sword = true;
            int beforeHealth = 0, observed = 0, noOpContacts = 0, damagingContacts = 0;
            void Observe()
            {
                observed++;
                rom.ClearObjects();
                beforeHealth = _player.HealthQuarters;
                rom[0xd024] = (byte)(_player.NativeInteractionCollisionsEnabled ? 0x80 : 0);
                rom[0xd02b] = unchecked((byte)(int)_player.InvincibilityFrames);
                rom[0xd02d] = (byte)_player.KnockbackFrames;
                rom[0xd00b] = (byte)_player.Position.Y; rom[0xd00d] = (byte)_player.Position.X;
                rom[0xd026] = rom[0xd027] = 6;
                rom[0xd029] = 1;
                rom[0xc6aa] = (byte)beforeHealth;
                foreach (var target in targets)
                {
                    var shot = (SmogProjectilePart)target.Node;
                    int address = 0xd0c0 + parts.GetValueOrDefault(target) * 256;
                    rom[address + 0x24] = (byte)(shot.CollisionEnabled ? 0xca : 0x4a);
                    rom[address + 0x25] = (byte)shot.CollisionMode;
                    rom[address + 0x28] = (byte)data.RawDamage;
                    rom[address + 0x29] = (byte)data.Health;
                    rom[address + 0x2a] = (byte)shot.ContactFlags;
                    rom[address + 0x2b] = unchecked((byte)shot.InvincibilityCounter);
                    rom[address + 0x0b] = (byte)shot.Position.Y;
                    rom[address + 0x0d] = (byte)shot.Position.X;
                    rom[address + 0x26] = (byte)data.Radius.Y;
                    rom[address + 0x27] = (byte)data.Radius.X;
                }
                if (sword)
                {
                    // Shared collision input, independent of sword animation timing.
                    // Both targets overlap this same weapon and Link's body.
                    Rect2 bounds = new(new(64, 64), new(24, 24));
                    _entities.ApplySwordHit(bounds, _player.Position, 1, EnemyKnockbackStrength.Normal);
                    rom[0xd624] = 0x84;
                    rom[0xd60b] = rom[0xd60d] = 76;
                    rom[0xd626] = rom[0xd627] = 12;
                }
                rom.Call(ObjectCollisionRom.Scan);
                noOpContacts += rom.Dispatches.Count(d => d.Type == 4 && d.Effect == 0);
                damagingContacts += rom.Dispatches.Count(d => d.Type == 0 && d.Effect == 2);
            }
            var observer = new CollisionRomObserver(Observe);
            _entities.AddEntity(observer);
            if (reverse)
                ((List<IRoomEntity>)typeof(RoomEntityManager).GetField("_activeEntities", flags)!.GetValue(_entities)!).Reverse();
            void Compare()
            {
                foreach (var target in targets)
                {
                    var shot = (SmogProjectilePart)target.Node;
                    int address = 0xd0c0 + parts.GetValueOrDefault(target) * 256;
                    FailIf(shot.ContactFlags != rom[address + 0x2a] || shot.InvincibilityCounter != unchecked((sbyte)rom[address + 0x2b]),
                        $"ROM collision slot=${address:x4}, subid={subid}, update={observed}, reversed={reverse}, batch={batched}: ROM flags/inv=${rom[address + 0x2a]:x2}/${rom[address + 0x2b]:x2}, runtime=${shot.ContactFlags:x2}/{shot.InvincibilityCounter}.");
                }
                // Compare accepted damage, not the separate next-Link health commit.
                int quarters = rom[0xd025] == 0 ? 0 : (256 - rom[0xd025]) / 2;
                FailIf(_player.HealthQuarters != beforeHealth - quarters ||
                    _player.InvincibilityFrames != unchecked((sbyte)rom[0xd02b]) || _player.KnockbackFrames != rom[0xd02d],
                    $"ROM post-object contact priority differs: subid={subid}, update={observed}, sword={sword}.");
            }
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: Compare);
            sword = false; // Cancellation must release the no-op scan suppression.
            StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: Compare);
            if (subid == 1)
                FailIf(noOpContacts != 6 || damagingContacts != 1,
                    "ROM ordering fixture must visit both no-op targets then accept only the first body hit.");
            FailIf(observed != 5, "Collision observer did not execute on every gameplay update.");
        }
        GD.Print("Validated native post-object multi-target/no-op priority, PART slot order independent of scene order, cancellation and next-update flags in individual/batched gameplay updates.");
    }
}
