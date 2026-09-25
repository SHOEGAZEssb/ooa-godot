using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullMoldormObjects()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var random = CaptureOracleRandomForValidation();
        var checkpoints = new List<(Vector2, Vector2, Vector2, int, int)>();
        foreach (bool batch in new[] { false, true })
        {
            void Step(int count = 1, Vector2 movement = default) =>
                StepGameplayUpdates(count, movement, [], [], batched: batch);
            RestoreOracleRandomForValidation(random);
            LoadValidationRoom(4, 0x6f);
            var spawners = _entities.Entities<MoldormSpawnerCharacter>();
            FailIf(spawners.Count != 2 || _entities.RoomEnemyCount != 2 ||
                _entities.Entities<MoldormCharacter>().Count != 0,
                "Skull room4:6f must parse two native Moldorm spawners before creating any head or tail.");
            Vector2? approach = null;
            for (int y = 32; y < _currentRoom.Height - 24 && approach is null; y += 8)
            for (int x = 24; x < _currentRoom.Width - 40 && approach is null; x += 8)
                if (Enumerable.Range(-6, 21).All(dx => Enumerable.Range(-6, 13).All(dy =>
                    !_currentRoom.IsSolid(new Vector2(x + dx, y + dy)) &&
                    _currentRoom.GetTerrainInfo(new Vector2(x + dx, y + dy)).Hazard == HazardType.None))) approach = new(x, y);
            FailIf(approach is null, "Moldorm observation requires a collision-safe route in the actual4:6f geometry.");
            _player.WarpTo(approach!.Value);
            _player.SetBraceletLiftCollisionsDisabled(true);
            var reserved = (HashSet<int>)typeof(RoomEntityManager).GetField("_reservedEnemySlots", flags)!.GetValue(_entities)!;
            var extra = new List<int>();
            try
            {
                for (int slot = 0; slot < 16 && reserved.Count < 14; slot++)
                    if (reserved.Add(slot)) extra.Add(slot);
                int calls = _random.Calls;
                Step(8, Vector2.Right);
                FailIf(_player.Position.X <= approach.Value.X || spawners.Any(s => s.State != 1 || !s.Visible || s.IsDead) ||
                    _random.Calls != calls + 2 || _entities.Entities<MoldormCharacter>().Count != 0 || _entities.RoomEnemyCount != 2,
                    "Moldorm spawners must wait visibly with only two free ENEMY slots, consuming common RNG once while Link walks.");
            }
            finally { foreach (int slot in extra) reserved.Remove(slot); }
            Step();
            var heads = _entities.Entities<MoldormCharacter>();
            var tails = _entities.Entities<MoldormTailCharacter>();
            FailIf(_entities.Entities<MoldormSpawnerCharacter>().Count != 0 || heads.Count != 2 || tails.Count != 4 ||
                _entities.RoomEnemyCount != 6 || !heads.Select(h => h.State).SequenceEqual(new[] { 8, 0 }),
                "Freeing three slots must allocate head/tail1/tail2 in source order; the second head reuses earlier slot0 and waits.");
            Step(); Step();
            FailIf(heads.Any(h => h.State != 9 || h.Tail1 is null || h.Tail2 is null || h.Tail1.Parent != h || h.Tail2.Parent != h.Tail1) ||
                tails.Any(t => t.State != 9 || t.CollisionEnabled),
                "Native Moldorm tail state8 must bind the preceding object's position and disable tail collisions independently.");
            int saved = 0;
            for (int tick = 0; tick < 128; tick += 8)
            {
                Step(8);
                foreach (var head in heads)
                {
                    var value = (head.Position, head.Tail1Position, head.Tail2Position, head.Angle, head.TurnCounter);
                    if (!batch) checkpoints.Add(value);
                    else FailIf(checkpoints[saved++] != value, "Moldorm native head/tail updates diverged under host batching.");
                }
            }
            // Defeat the heads through the shared collision owner. Tail deletion
            // must release four independent counts before the two puffs finish.
            foreach (var head in heads)
                _entities.EntityAdapters<MoldormRoomEntity>().Single(owner => owner.Node == head)
                    .ApplySwordHit(head.CollisionBounds, head.Position, 0x7f, EnemyKnockbackStrength.Low, new List<RoomEntitySpawn>());
            for (int i = 0; _entities.Entities<MoldormCharacter>().Count != 0 && i < 40; i++) Step();
            FailIf(_entities.Entities<MoldormTailCharacter>().Count != 0 || _entities.RoomEnemyCount != 2 ||
                _entities.Entities<EnemyDeathPuffEffect>().Count != 2,
                "Moldorm head deletion must release both linked tail counts without extra kill puffs or kill counters.");
            Step(24);
            FailIf(_entities.RoomEnemyCount != 0, "Moldorm death puffs failed to release the final two native room counts.");
        }
    }
}
