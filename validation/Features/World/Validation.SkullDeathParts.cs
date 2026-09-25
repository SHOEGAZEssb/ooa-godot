using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDeathPartLifecycle()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var pending = (List<RoomEntitySpawn>)typeof(RoomEntityManager).GetField("_pendingSpawns", flags)!.GetValue(_entities)!;
        var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_partSlots", flags)!.GetValue(_entities)!;
        var interactions = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_interactionSlots", flags)!.GetValue(_entities)!;
        foreach (bool batch in new[] { false, true })
        {
            void Step(int count = 1, Vector2 movement = default) =>
                StepGameplayUpdates(count, movement, [], [], batched: batch);
            foreach (bool full in new[] { false, true })
            {
                LoadValidationRoom(4, 0x6f);
                Vector2? approach = null;
                for (int y = 32; y < _currentRoom.Height - 24 && approach is null; y += 8)
                for (int x = 24; x < _currentRoom.Width - 40 && approach is null; x += 8)
                    if (Enumerable.Range(-6, 21).All(dx => Enumerable.Range(-6, 13).All(dy =>
                        !_currentRoom.IsSolid(new Vector2(x + dx, y + dy)) &&
                        _currentRoom.GetTerrainInfo(new Vector2(x + dx, y + dy)).Hazard == HazardType.None))) approach = new(x, y);
                FailIf(approach is null, "Death-part test requires an actual collision-safe route in Skull room4:6f.");
                _player.WarpTo(approach!.Value);
                _player.SetBraceletLiftCollisionsDisabled(true);
                Step(8, Vector2.Right);
                FailIf(_player.Position.X <= approach.Value.X || _entities.RoomEnemyCount != 6,
                    "Link must approach through real geometry while both native Moldorms allocate their independent tails.");
                if (full)
                    for (int i = 0; i < 16; i++) _entities.Spawn<KeeseFirePart>(new KeeseFireSpawn(new Vector2(120, 80), 0));
                var owner = _entities.EntityAdapters<MoldormRoomEntity>().First();
                var head = (MoldormCharacter)owner.Node;
                _sound.ClearPlayRequestAudit();
                FailIf(!owner.ApplySwordHit(head.CollisionBounds, head.Position, 0x7f, EnemyKnockbackStrength.Low, pending),
                    "Moldorm's shared combat owner rejected the death-part fixture hit.");
                int killSoundsAtOutcome = -1;
                void ObserveOutcome() => killSoundsAtOutcome = _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy);
                _entities.EnemyDefeated += ObserveOutcome;
                try { for (int i = 0; !head.IsDead && i < 40; i++) Step(); }
                finally { _entities.EnemyDefeated -= ObserveOutcome; }
                FailIf(killSoundsAtOutcome != (full ? 0 : 1),
                    "enemyDie must attempt the puff and its kill sound before publishing the global kill-counter outcome.");
                FailIf(!head.IsDead || _entities.RoomEnemyCount != 4,
                    "Head death must release two tail counts and retain its own count until PART_ENEMY_DESTROYED completes.");
                if (full)
                {
                    FailIf(_entities.Entities<EnemyDeathPuffEffect>().Count != 0 || _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) != 0,
                        "A full native PART pool must reject the death puff before its kill sound, without retrying.");
                    Step(180);
                    FailIf(slots.Count != 0 || _entities.RoomEnemyCount != 4 || _entities.Entities<EnemyDeathPuffEffect>().Count != 0,
                        "Freeing PART capacity must not retry a failed puff or release the count left by enemyCreateDeathPuff.");
                }
                else
                {
                    var puff = _entities.Entities<EnemyDeathPuffEffect>().Single();
                    var puffOwner = _entities.EntityAdapters<DeathPuffRoomEntity>().Single();
                    FailIf(puff.ElapsedFrames != 1 || slots[puffOwner] != 0 || interactions.ContainsKey(puffOwner) ||
                        _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) != 1,
                        "enemyDie must allocate PART $d0 and animate it once in the same update's later PART pass.");
                    puffOwner.ClearHealthAndCollision(); // partCode02 ignores its own DEAD status.
                    Step(18);
                    FailIf(puff.Finished || puff.ElapsedFrames != 19 || _entities.RoomEnemyCount != 4,
                        "The normal source death animation must retain the head's count through update19.");
                    Step();
                    FailIf(!puff.Finished || _entities.RoomEnemyCount != 3,
                        "PART_ENEMY_DESTROYED must release its count on animation update20 before room-clear interactions.");
                }
                LoadValidationRoom(4, 0x91);
                FailIf(_entities.RoomEnemyCount != 0, "Room loading must clear counts left by failed death-puff allocations.");
            }

            LoadValidationRoom(4, 0x91);
            _player.WarpTo(new Vector2(120, 128));
            Step(16, Vector2.Up);
            _player.SetBraceletLiftCollisionsDisabled(true);
            var first = _entities.Spawn<KeeseFirePart>(new KeeseFireSpawn(new Vector2(72, 80), 0));
            _entities.Spawn<KeeseFirePart>(new KeeseFireSpawn(new Vector2(88, 80), 0));
            _entities.Spawn<KeeseFirePart>(new KeeseFireSpawn(new Vector2(104, 80), 0));
            Step();
            // Fixture: native $64 uses table $e0, probability7 (all bits set)
            // and set0 (32 hearts). This isolates replacement from RNG choice.
            var effect = _entities.Spawn<EnemyDeathPuffEffect>(new EnemyDeathPuffSpawn(new Vector2(120.75f, 80.5f),
                EnemyId: 0x64, DecrementsRoomCount: true));
            var effectOwner = _entities.EntityAdapters<DeathPuffRoomEntity>().Single();
            FailIf(slots[effectOwner] != 3, "Replacement fixture must occupy PART page $d3.");
            // End the earlier fire at its ordinary timer boundary, leaving a
            // lower free page that objectReplaceWithID must not allocate.
            typeof(KeeseFirePart).GetProperty(nameof(KeeseFirePart.Counter), flags)!.SetValue(first, 1);
            var textSource = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true;
                Step(3);
                FailIf(effect.ElapsedFrames != 1 || first.Counter != 1 || _entities.RoomEnemyCount != 1,
                    "Text must allow the death puff's state zero once, then freeze it and already initialized parts without releasing the count.");
            }
            finally { _entities.TextActiveSource = textSource; }
            Step(18);
            int calls = _random.Calls;
            FailIf(effect.ElapsedFrames != 19 || _entities.RoomEnemyCount != 1 || slots.Values.Contains(0),
                "Replacement fixture lost the lower free PART page or preterminal count.");
            Step();
            var drop = _entities.Entities<ItemDropEffect>().Single();
            var dropOwner = _entities.EntityAdapters<ItemDropRoomEntity>().Single();
            FailIf(slots[dropOwner] != 3 || slots.Values.Contains(0) || drop.SubId != 1 ||
                drop.State != DropState.Initializing || drop.ElapsedFrames != 0 || drop.PrecisePosition != new Vector2(120, 80) ||
                _random.Calls != calls + 2 || _entities.RoomEnemyCount != 0,
                "The terminal puff must replace its own PART page with a state-zero heart, clear coordinate fractions, and consume exactly two drop RNG calls.");
            Step();
            FailIf(drop.ElapsedFrames != 1 || drop.State == DropState.Initializing,
                "The replacement item must initialize on the next PART pass, even with a lower free page.");
            Step(32);
            Step(32, Vector2.Up);
            FailIf(!drop.Collected || _entities.Entities<ItemDropEffect>().Count != 0,
                "Link must collect the replacement heart by walking through the entrance floor after it lands.");
        }
    }
}
