using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSwitchHookSandCrab()
    {
        var collisions = SwitchHookCollisionDatabase.Shared;
        FailIf(!collisions.EnemyEnabled(0x1a) || collisions.Effect(0x10) != 0x2e,
            "ENEMY_SAND_CRAB $1a mode $10 must enable ITEMCOLLISION_SWITCH_HOOK $0d and exchange effect $2e.");
        foreach (bool batched in new[] { false, true })
        foreach (int level in new[] { 1, 2 })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(5, 0x4f);
            _inventory.GiveTreasure(TreasureId.SwitchHook, level);
            _inventory.EquipA(TreasureId.SwitchHook);
            void Step(int count = 1, bool fire = false) => StepGameplayUpdates(count, Vector2.Zero,
                fire ? ["attack"] : [], fire ? ["attack"] : [], batched);
            var crabs = _entities.Entities<SandCrabCharacter>();
            FailIf(crabs.Count != 2 || crabs[0].Position != new Vector2(0x68, 0x58) ||
                crabs[1].Position != new Vector2(0x88, 0x58),
                "group5Map4fEnemyObjectData must retain both ordered $1a:$00 placements.");
            // Keep the reported placement and actual room geometry while
            // isolating it from the second crab crossing the repeat corridor.
            _entities.Clear();
            FailIf(!_entities.TrySpawnEnemy(0x1a, 0, new(0x68, 0x58),
                "objects/ages/enemyData.s:group5Map4fEnemyObjectData[0] regression", out string error), error);
            crabs = _entities.Entities<SandCrabCharacter>();
            SandCrabCharacter target = crabs[0];
            for (int repetition = 0; repetition < 3; repetition++)
            {
                // Choose an unobstructed corridor in the actual reported room,
                // outside both native enemy hitboxes. No collision tiles change.
                Vector2 origin = default;
                Vector2I facing = default;
                bool found = false;
                foreach (var direction in repetition == 0
                    ? new[] { Vector2I.Left, Vector2I.Right, Vector2I.Down, Vector2I.Up }
                    : new[] { -_player.FacingVector })
                {
                    Vector2 point = target.Position.Floor() + (Vector2)direction * 32;
                    Vector2 perpendicular = new(-direction.Y, direction.X);
                    if (point.X < 12 || point.Y < 12 || point.X >= _currentRoom.Width - 12 || point.Y >= _currentRoom.Height - 12)
                        continue;
                    if (!Enumerable.Range(0, 33).All(i => Enumerable.Range(-6, 13).All(j =>
                        !_currentRoom.IsSolid(target.Position.Floor() + (Vector2)direction * i + perpendicular * j)))) continue;
                    if (crabs.Any(other => other != target && other.Position.DistanceTo(point) < 16)) continue;
                    origin = point; facing = -direction; found = true; break;
                }
                FailIf(!found, $"$5:$4f has no clear Sand Crab hook corridor at {target.Position}, repeat={repetition}.");
                if (repetition == 0) _player.WarpTo(origin);
                else origin = _player.PrecisePosition;
                _player.Face(facing);
                int health = target.Health, count = _entities.RoomEnemyCount;
                Step(fire: true);
                var hook = _entities.SwitchHook!.Item!;
                for (int i = 0; !target.SwitchHookHeld && !hook.Finished && i < 50; i++) Step();
                FailIf(!target.SwitchHookHeld || target.CollisionEnabled || hook.State != 1 || hook.CollisionEnabled,
                    $"$5:$4f $1a L{level} repeat={repetition} hook failed to latch through real geometry: origin={origin}, enemy={target.Position}, hook={hook.Position}, state={hook.State}.");
                Vector2 caught = target.Position;
                int counter = target.Counter, angle = target.Angle;
                Step();
                FailIf(hook.State != 3 || target.SwitchHookSubstate != 1 || target.Position != caught,
                    "$1a must enter held substate $01 while the next ITEM pass consumes its pending latch.");
                if (repetition == 2)
                {
                    // Parent deletion cancels during the latch, before a helper exists.
                    _entities.SwitchHook.ClearParent(); Step(); Step();
                    FailIf(!hook.Finished || target.SwitchHookHeld || target.State != SandCrabState.ChoosingDirection ||
                        !target.CollisionEnabled || target.Position != caught || _player.Position != origin,
                        "$1a latch cancellation must release to $08 without exchanging either position.");
                }
                else
                {
                    Step(18);
                    FailIf(hook.Substate != 1 || !target.SwitchHookHeld || target.Counter != counter ||
                        target.Angle != angle || target.Position != caught,
                        "$1a held latch must retain movement counters, angle and position.");
                    int rise = 16; // switchHookState3:s3subState1 compares zh with $f1 for both levels.
                    Step(rise);
                    FailIf(hook.Substate != 2 || target.Position != caught || target.ZFixed != -rise * 256,
                        $"$1a L{level} repeat={repetition} must rise for exactly {rise} updates before swap: substate={hook.Substate}, Z={target.ZFixed}, position={target.Position}/{caught}.");
                    Step();
                    FailIf(target.SwitchHookSubstate != 2 || target.Position != origin + caught - caught.Floor() ||
                        _player.PrecisePosition != caught.Floor(),
                        "$1a exchange must copy coordinate high bytes and preserve enemy fractions.");
                    Step(rise);
                    FailIf(!hook.Finished || target.SwitchHookHeld || target.State != SandCrabState.ChoosingDirection ||
                        !target.CollisionEnabled || target.ZFixed != 0 || target.Counter != counter,
                        "$1a landing must restore collision/state $08 without choosing a route on that update.");
                    Step();
                    FailIf(target.State != SandCrabState.Moving,
                        "$1a must choose its next random route on the update after hook landing.");
                }
                FailIf(target.Health != health || _entities.RoomEnemyCount != count || _entities.SwitchHook.ExchangeActive,
                    "$1a hook completion/cancellation must preserve health and counted-enemy ownership.");
                for (int i = 0; _player.IsUsingSwitchHook && i < 8; i++) Step();
            }
        }
    }
}
