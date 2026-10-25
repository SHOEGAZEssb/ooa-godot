using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateTwinrovaProjectileRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int color in new[] { 0, 1 })
        foreach (int item in new[] { 4, 5, 3 })
        foreach (bool critical in item == 5 ? new[] { false, true } : new[] { false })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5, 0xf5);
            var placed = _entities.EntityAdapters<TwinrovaRoomEntity>().Single(); var world = placed.Actor.World;
            _entities.RetireEntity(placed);
            _entities.TryAllocateEnemy(_ => new TwinrovaRoomEntity(new(world, Vector2.Zero, 0, 0x80)));
            _player.WarpTo(new(0x78, 0x78)); _player.Face(Vector2I.Up); _inventory.EquipA(0); _inventory.EquipB(0);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 0x78, 0x78)
                { HostileEnemiesEnabled = true, HostilePartsEnabled = true, PostObjectCollisionsEnabled = false };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd080] = 1; rom[0xd081] = 3; rom[0xd083] = 0x80;
            rom[0xcc08] = 0x33; rom[0xcc0a] = 0x34; rom[0xcc0c] = 0x35; rom[0xcc0e] = 0x36;
            var sounds = _sound.AttachPlayRequestAudit();
            TwinrovaProjectileRoomEntity? shot = null; int partAt = 0, update = 0;
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); ++update;
                foreach (var twin in _entities.EntityAdapters<TwinrovaRoomEntity>())
                {
                    int at = 0xd080 + twin.Slot * 256;
                    FailIf(twin.Actor.Health != rom[at + 0x29] || twin.Actor.Flags != rom[at + 0x32] || twin.Actor.InvincibilityCounter != unchecked((sbyte)rom[at + 0x2b]),
                        $"Twinrova reflection update{update}, color{color}, item${item:x2}: native health/signal/invincibility differs.");
                }
                if (shot is not null)
                {
                    FailIf(shot.Finished != (rom[partAt] == 0) || !shot.Finished &&
                        (shot.State != rom[partAt + 4] || shot.Speed != rom[partAt + 16] || shot.Angle != rom[partAt + 9] ||
                            shot.Position != new Vector2(rom.Word(partAt + 12) / 256f, rom.Word(partAt + 10) / 256f)),
                        $"Twinrova reflection update{update}, color{color}, item${item:x2}: pending hit/reflection flight or retirement differs; state={shot.State}/{rom[partAt+4]}, speed={shot.Speed}/{rom[partAt+16]}.");
                }
                FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls) || !sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Twinrova reflection update{update}, item${item:x2}, color{color}: native RNG/audio differs: {string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                _player.ApplyInteractionInvincibility(128); rom[0xd02b] = 0x80;
            });
            Step(2);
            var parent = _entities.EntityAdapters<TwinrovaRoomEntity>().Single(t => t.Actor.Subid == color);
            var target = parent.Actor.Twin;
            parent.Actor.Flags |= 5; rom[0xd0b2 + parent.Slot * 256] |= 5;
            Step(30);
            shot = _entities.EntityAdapters<TwinrovaProjectileRoomEntity>().Single();
            int partSlot = Enumerable.Range(0, 16).Single(slot => ReferenceEquals(_entities.ResolvePartSlot(slot), shot));
            partAt = 0xd0c0 + partSlot * 256;
            if (critical)
            {
                parent.Actor.Health = target.Health = 1;
                rom[0xd0a9 + parent.Slot * 256] = rom[0xd0a9 + target.Slot * 256] = 1;
            }
            FailIf(shot.State != 2 || rom[partAt + 4] != 2, "Twinrova charge must launch on the thirtieth PART visit, including state-zero fallthrough.");
            // Arrange a local intercept on real arena floor. Link stays outside
            // the target's body; the sword then publishes its normal collision.
            int sign = target.Position.X < 120 ? 1 : -1;
            shot.Position = target.Position.Floor() + new Vector2(sign * 14, 0);
            _player.WarpTo(shot.Position + new Vector2(sign * 18, 0));
            FailIf(_collision.Collides(_player.Position) || _entities.BlocksLink(_player.Position), "Twinrova reflection intercept must use the arena's actual floor.");
            rom.Word(partAt + 12, (int)(shot.Position.X * 256)); rom.Word(partAt + 10, (int)(shot.Position.Y * 256));
            rom.Word(0xd00c, (int)(_player.Position.X * 256)); rom.Word(0xd00a, (int)(_player.Position.Y * 256));
            rom.HitPartWithItem(partSlot, item);
            shot.SetLinkSwordState(SwordActionState.Swing, item == 5 ? 2 : 1, collision: item);
            bool accepted = shot.ApplySwordHit(shot.CollisionBounds, _player.Position, 1, EnemyKnockbackStrength.Low, new List<RoomEntitySpawn>());
            FailIf(accepted != (item != 4) || (rom[partAt + 0x2a] & 128) != (accepted ? 128 : 0),
                "Twinrova shots must reject L1 sword $04, accept L2 sword $05 and accept L3 shield $03 through their native active masks.");
            var held = shot.Position;
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 8); rom[0xcc8a] = 8;
            Step(3);
            FailIf(shot.Finished || shot.State != 2 || shot.Position != held, "A frozen PART must retain its pending collision without advancing or retiring.");
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0); rom[0xcc8a] = 0;
            Step(12);
            int expectedHealth = critical ? 0x7f : 2;
            FailIf(item == 5 && (parent.Actor.Health != expectedHealth || target.Health != expectedHealth || !shot.Finished) ||
                item == 3 && (parent.Actor.Health != 3 || target.Health != 3 || !shot.Finished),
                $"Twinrova reflected shot must damage both twins once; L3 shield must delete without damage: item${item:x2}, HP={parent.Actor.Health}/{target.Health}, finished={shot.Finished}.");
            _sound.SetRequestObserver(null);
        }
        GD.Print("Validated both Twinrova projectile colors, source sword/shield masks, pending-hit freezes, reflected flight, paired health loss and L3-shield retirement against the clean ROM.");
    }
}
