using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullMoldormPartWrites()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var pending = (List<RoomEntitySpawn>)typeof(RoomEntityManager).GetField("_pendingSpawns", flags)!.GetValue(_entities)!;
        var process = typeof(RoomEntityManager).GetMethod("ProcessSpawns", flags)!;
        foreach (bool batch in new[] { false, true })
        foreach (bool initialized in new[] { false, true })
        foreach (bool boneCase in new[] { true, false })
        {
            void Step(int count = 1, Vector2 movement = default) =>
                StepGameplayUpdates(count, movement, [], [], batched: batch);
            LoadValidationRoom(4, 0x91);
            _player.WarpTo(new Vector2(120, 128));
            Step(16, Vector2.Up);
            FailIf(_player.Position != new Vector2(120, 112), "Moldorm PART fixture must approach through the actual entrance floor.");
            _player.SetBraceletLiftCollisionsDisabled(true);
            FailIf(!_entities.TrySpawnEnemy(EnemyId.Moldorm, 0, new Vector2(120, 80), "US Moldorm PART-page write", out string error), error);
            Step(3);
            var head = _entities.Entities<MoldormCharacter>().Single();
            FailIf(head.Tail1Slot != 2 || head.Tail2Slot != 3, "Moldorm fixture lost source tail pages $d2/$d3.");
            // The fixture supplies a lethal collision through the shared combat
            // owner; normal ENEMY updates run the deferred death and raw writes.
            _entities.EntityAdapters<MoldormRoomEntity>().Single(owner => owner.Node == head)
                .ApplySwordHit(head.CollisionBounds, head.Position, 0x7f, EnemyKnockbackStrength.Low, pending);
            for (int i = 0; head.KnockbackCounter > (initialized ? 1 : 0) && i < 40; i++) Step();
            FailIf(head.IsDead || head.Health != 0 || head.KnockbackCounter != (initialized ? 1 : 0),
                "Moldorm death fixture must stop before the native death dispatch.");
            // Allocate real part owners into the shared PART pool. Slots $d0/$d1
            // are controls; only the two tail-page indices receive the US writes.
            pending.Add(new KeeseFireSpawn(new Vector2(88, 80), 0));
            pending.Add(new KeeseFireSpawn(new Vector2(104, 80), 0));
            pending.Add(boneCase ? new StalfosBoneSpawn(new Vector2(120, 80), 0)
                : new ZoraFireSpawn(new Vector2(120, 80)));
            pending.Add(new KeeseFireSpawn(new Vector2(136, 80), 0));
            process.Invoke(_entities, [null]);
            var fires = _entities.Entities<KeeseFirePart>().ToArray();
            var bone = _entities.Entities<StalfosBoneProjectile>().SingleOrDefault();
            var zora = _entities.Entities<ZoraFireProjectile>().SingleOrDefault();
            if (initialized) Step();
            FailIf(head.IsDead || head.KnockbackCounter != 0 || fires.Any(f => f.CollisionEnabled != initialized),
                "PART initialization must occur independently before Moldorm's death write.");
            Vector2 partPosition = bone?.Position ?? zora!.Position;
            Step();
            FailIf(_entities.Entities<MoldormCharacter>().Count != 0 || _entities.Entities<MoldormTailCharacter>().Count != 0,
                "US PART writes must still allow Moldorm's linked ENEMY tails to delete normally.");
            FailIf(fires[0].Finished || fires[1].Finished || !fires[0].CollisionEnabled || !fires[1].CollisionEnabled ||
                fires[2].Finished || fires[2].CollisionEnabled == initialized || fires[2].Counter != (initialized ? 179 : 180),
                "PART_FIRE $20 must ignore DEAD status, disable only initialized target collision, and retain its $b4 lifetime.");
            if (!initialized)
            {
                FailIf(boneCase ? bone!.Finished || !bone.CollisionEnabled || bone.State != HostileProjectileState.Flying
                    : zora!.Finished || zora.State != 1 || zora.Counter != 8,
                    "State-zero partLoadGraphicsAndProperties must overwrite Moldorm's earlier health/collision write.");
                Step(3);
                FailIf(boneCase ? bone!.Position == partPosition : zora!.Counter != 5,
                    "A part initialized after the raw write must continue its ordinary handler.");
                continue;
            }
            if (boneCase)
            {
                FailIf(bone!.Finished || bone.CollisionEnabled || bone.State != HostileProjectileState.Bouncing ||
                    bone.Counter != 0 || bone.ZFixed != 0 || bone.Position != partPosition,
                    "PART_STALFOS_BONE $1c must enter state2 with collision already disabled, skipping bounce setup.");
            }
            else
            {
                FailIf(!zora!.Finished || _entities.Entities<ZoraFireProjectile>().Count != 0,
                    "PART_ZORA_FIRE $19 must execute jp nz,partDelete on the same part pass after the ENEMY death write.");
            }
            _player.SetBraceletLiftCollisionsDisabled(false);
            int health = _inventory.HealthQuarters;
            Step(16, Vector2.Right);
            Step(24, Vector2.Up);
            FailIf(_player.Position != new Vector2(136, 88) || _inventory.HealthQuarters != health,
                "Link must be able to walk into the disabled fire through room geometry without contact damage.");
            if (boneCase)
                FailIf(bone!.Finished || bone.Position != partPosition || bone.Counter != 0 || bone.ZFixed != 0 || bone.AnimationFrame != 0,
                    "Persistent zero health must take the bone status branch every update, without movement, animation or timer deletion.");
            FailIf(fires[2].Counter != 139 || fires[2].CollisionEnabled,
                "Disabled Keese fire must continue counting while remaining harmless.");
            Step(138);
            FailIf(fires[2].Finished || fires[2].Counter != 1, "Keese fire expired before its source timer boundary.");
            Step();
            FailIf(!fires[2].Finished || _entities.Entities<KeeseFirePart>().Count != 0,
                "Keese fire must delete at the same $b4 boundary with or without a raw health write.");
            LoadValidationRoom(4, 0x91);
            FailIf(_entities.Entities<StalfosBoneProjectile>().Count != 0 || _entities.Entities<ZoraFireProjectile>().Count != 0,
                "Room replacement must clear a bone left alive by the clean-US Moldorm bug.");
        }
    }
}
