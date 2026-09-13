using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullMoldormHazards()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        var counter = typeof(EnemyCharacter).GetField("_hazardCounter", flags)!;
        var animationCounter = typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!;
        var position = typeof(MoldormCharacter).GetMethod("SetPreciseHeadPosition", flags)!;
        int roomId = -1;
        Vector2 hole = default;
        foreach (int id in new[] { 0x6d, 0x86, 0x92 })
        {
            var room = _world.LoadRoom(4, id);
            for (int y = 24; y < room.Height - 16 && roomId < 0; y += 16)
            for (int x = 24; x < room.Width - 16 && roomId < 0; x += 16)
                if (room.GetTerrainInfo(new Vector2(x, y)).Hazard == HazardType.Hole)
                { roomId = id; hole = new Vector2(x, y); }
            if (roomId >= 0) break;
        }
        FailIf(roomId < 0, "The source Moldorm rooms 4:6d/86/92 need an actual hole for the native hazard fixture.");
        foreach (bool batch in new[] { false, true })
        foreach (string scenario in new[] { "complete", "cancel", "knockback" })
        {
            void Step(int count = 1, Vector2 movement = default)
            {
                input.CaptureForValidation([], [], movement);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60, update);
            }
            LoadValidationRoom(4, roomId);
            Vector2? floor = null;
            for (int y = 24; y < _currentRoom.Height - 24 && floor is null; y += 8)
            for (int x = 24; x < _currentRoom.Width - 24 && floor is null; x += 8)
                if (Enumerable.Range(-6, 21).All(dx => Enumerable.Range(-6, 13).All(dy =>
                    !_currentRoom.IsSolid(new Vector2(x + dx, y + dy)) &&
                    _currentRoom.GetTerrainInfo(new Vector2(x + dx, y + dy)).Hazard == HazardType.None))) floor = new(x, y);
            FailIf(floor is null, "Moldorm hazard observation needs a walkable section of its actual room.");
            _player.WarpTo(floor!.Value);
            _player.SetBraceletLiftCollisionsDisabled(true);
            Step(8, Vector2.Right);
            FailIf(_player.Position.X <= floor.Value.X, "Moldorm hazard observation could not walk through the room's collision geometry.");
            var head = _entities.Entities<MoldormCharacter>().First();
            var tails = new[] { head.Tail1!, head.Tail2! };
            if (scenario == "knockback")
            {
                bool ProbeHole(Vector2 p) => new[] { -1, 1 }.Any(dx =>
                    _currentRoom.GetTerrainInfo(p + new Vector2(dx, 5)).Hazard == HazardType.Hole);
                Vector2? edge = null;
                Vector2 direction = default;
                for (int y = 16; y < _currentRoom.Height - 16 && edge is null; y++)
                for (int x = 16; x < _currentRoom.Width - 16 && edge is null; x++)
                foreach (Vector2 move in new[] { Vector2.Down, Vector2.Right, Vector2.Up, Vector2.Left })
                {
                    Vector2 at = new(x, y);
                    if (ProbeHole(at) || !ProbeHole(at + move * 2) ||
                        Enumerable.Range(-6, 13).Any(dx => Enumerable.Range(-6, 13).Any(dy =>
                            _currentRoom.IsSolid(at + new Vector2(dx, dy))))) continue;
                    edge = at; direction = move; break;
                }
                FailIf(edge is null, "Moldorm's actual room needs a floor-to-hole recoil boundary.");
                position.Invoke(head, [edge!.Value]);
                foreach (var tail in tails) tail.Position = floor.Value;
                head.Animation.SetFrameCounter(100);
                FailIf(!head.TryApplyShieldBump(head.CollisionBounds, edge.Value - direction * 16, EnemyKnockbackStrength.Low),
                    "Moldorm animated-hole recoil fixture rejected its shield bump.");
                Step(); // JUST_HIT precedes the first recoil movement.
                FailIf(head.IsFallingIntoHole || head.Position != edge.Value,
                    "Moldorm must remain on the safe side of the hole during JUST_HIT.");
                Step();
                FailIf(!head.IsFallingIntoHole || (int)counter.GetValue(head)! != 59 ||
                    (int)animationCounter.GetValue(head.Animation)! != 97,
                    "ecom_updateKnockbackAndCheckHazards must latch the animated hole variant after recoil crosses the boundary.");
                Step();
                FailIf((int)animationCounter.GetValue(head.Animation)! != 94 || tails.Any(t => t.DiedInHazard),
                    "Later entry checks must retain the latched animation flag; the parent's hazard alone must not make safe-floor tails fall.");
                LoadValidationRoom(4, 0x91);
                continue;
            }
            Vector2 entry = hole + new Vector2(7, -8);
            FailIf(_currentRoom.GetTerrainInfo(entry + new Vector2(-1, 5)).Hazard != HazardType.Hole,
                "Moldorm hazard fixture must hit the source y+$05/x-$01 probe.");
            // Controlled enemy positions isolate the receiving hazard handler;
            // all allocation, updates, completion and room teardown stay native.
            position.Invoke(head, [floor.Value]);
            foreach (var tail in tails) tail.Position = entry;
            Step();
            FailIf(tails.Any(t => t.DiedInHazard), "Tails may not check their own hole until their parent has a hazard byte.");
            FailIf(!head.TakeSwordHit(_player.Position, 2), "Moldorm hazard/JUST_HIT fixture rejected its hit.");
            head.ApplySwordKnockback(_player.Position, EnemyKnockbackStrength.Low);
            position.Invoke(head, [entry]);
            foreach (var tail in tails) tail.Position = entry;
            var actors = new EnemyCharacter[] { head, tails[0], tails[1] };
            foreach (var actor in actors) actor.Animation.SetFrameCounter(100);
            int countBefore = _entities.RoomEnemyCount;
            _sound.ClearPlayRequestAudit();
            Step();
            FailIf(actors.Any(a => !a.IsFallingIntoHole || a.CollisionEnabled || a.InvincibilityCounter != 0 ||
                a.KnockbackCounter != 0 || (int)counter.GetValue(a)! != 59 || a.Position != entry + Vector2.Left),
                "Head and tails must enter the hazard before JUST_HIT, clear combat bytes, nudge xh and consume the first of 60 updates.");
            var text = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true;
                Step(8);
                FailIf(actors.Any(a => (int)counter.GetValue(a)! != 59), "Dialogue must freeze every initialized Moldorm hole timer.");
            }
            finally { _entities.TextActiveSource = text; }
            Step(3);
            FailIf(actors.Any(a => (int)counter.GetValue(a)! != 56 || a.Position != head.Position ||
                (int)animationCounter.GetValue(a.Animation)! != 100) || head.Position == entry + Vector2.Left,
                "At counter $38 all three independent hole handlers must pull together without animating; tail movement must not run its follow buffer.");
            if (scenario == "cancel")
            {
                LoadValidationRoom(4, 0x91);
                Step(70);
                FailIf(_entities.RoomEnemyCount != 0 || _entities.Entities<MoldormTailCharacter>().Count != 0 ||
                    _sound.PlayRequestsFor(OracleSoundEngine.SndFallInHole) != 0,
                    "Room cancellation must remove all pending Moldorm falls without delayed effects.");
                continue;
            }
            Step(55);
            FailIf(actors.Any(a => a.IsDead || (int)counter.GetValue(a)! != 1) || _entities.RoomEnemyCount != countBefore,
                "Moldorm falls must retain all three counts through update 59.");
            Step();
            FailIf(!head.IsDead || tails.Any(t => !t.IsDead) || _entities.RoomEnemyCount != countBefore - 3 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndFallInHole) != 1 ||
                _entities.Entities<EnemyDeathPuffEffect>().Count != 0,
                "On update 60 the head falls, then both tails see deleted parent pages and release counts without extra falls or combat puffs.");
        }
    }
}
