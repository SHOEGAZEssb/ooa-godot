using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFireKeeseScreenTransition()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        var drawOffset = typeof(FireKeeseCharacter).GetProperty("AnimationDrawOffset", flags)!;
        Vector2 DrawPosition(FireKeeseCharacter bat) => bat.Position + (Vector2)drawOffset.GetValue(bat)!;
        string State(FireKeeseCharacter bat) =>
            $"{bat.Position}/{DrawPosition(bat)}/{bat.ZFixed}/{bat.State}/{bat.Counter}/{bat.Angle}/{bat.Speed}/{bat.AnimationIndex}/{bat.AnimationFrame}/{bat.Visible}";
        void Step(int count = 1, Action? observe = null)
        {
            input.CaptureForValidation([], [], Vector2.Zero);
            scheduler.Advance(count / 60.0, () => { update(); observe?.Invoke(); });
        }

        // enemyData.s: group4Map85/8d/8eEnemyObjectData contain respectively
        // obj_RandomEnemy $44/$24/$44 $39 $00 (two/one/two Fire Keese).
        foreach (var route in new[] {
            (Source: 0x84, Target: 0x85, Direction: Vector2I.Right, Count: 2),
            (Source: 0x91, Target: 0x8d, Direction: Vector2I.Up, Count: 1),
            (Source: 0x8d, Target: 0x8e, Direction: Vector2I.Right, Count: 2) })
        {
            var random = CaptureOracleRandomForValidation();
            string[] Run(bool batched)
            {
                RestoreOracleRandomForValidation(random);
                _saveData.SetRoomFlag(4, route.Source, 0xff, false);
                _saveData.SetRoomFlag(4, route.Target, 0xff, false);
                LoadValidationRoom(4, route.Source);
                Vector2 entrance = Enumerable.Range(0, 176)
                    .Select(i => new Vector2((i & 15) * 16 + 8, (i >> 4) * 16 + 8))
                    .Where(p => p.X < _currentRoom.Width && !_currentRoom.IsSolid(p) &&
                        _currentRoom.GetTerrainInfo(p).Hazard == HazardType.None)
                    .OrderByDescending(p => p.Dot(route.Direction)).First();
                _player.WarpTo(entrance);
                Step();
                FailIf(!_rooms.TryGetNeighbor(route.Direction, out int neighbor) || neighbor != route.Target,
                    $"Fire Keese fixture lost dungeon-layout neighbor 4:{route.Source:x2} -> 4:{route.Target:x2}.");
                var outgoing = _entities.Entities<FireKeeseCharacter>().ToArray();
                string[] outgoingState = outgoing.Select(State).ToArray();
                _transitions.BeginScroll(_player, route.Direction, route.Target);
                var bats = _entities.Entities<FireKeeseCharacter>().ToArray();
                FailIf(bats.Length != route.Count, $"4:{route.Target:x2} lost its source Fire Keese count.");

                // bank0._updateEnemiesIfStateIsZero dispatches state/substate 0
                // during wScrollMode & $0e. fireKeese_state_uninitialized sets
                // zh=-$1c, counter1=$08, SPEED_80, animation $01, state $0b,
                // then objectSetVisiblec1; no XY movement occurs in state 0.
                foreach (var bat in bats)
                    FailIf(!bat.Visible || bat.State != 0x0b || bat.ZFixed != -0x1c00 ||
                        bat.Counter != 8 || bat.Speed != 20 || bat.AnimationIndex != 1,
                        $"4:{route.Target:x2} Fire Keese $39:$00 appeared before flight initialization: {State(bat)}.");
                string[] frozen = bats.Select(State).ToArray();
                var calls = _random.Calls;
                foreach (var adapter in _entities.EntityAdapters<FireKeeseRoomEntity>())
                    adapter.PrepareForScreenTransition(new List<RoomEntitySpawn>());
                FailIf(_random.Calls != calls || !bats.Select(State).SequenceEqual(frozen),
                    "Repeated Fire Keese preload consumed RNG or advanced flight.");

                int total = _transitions.ScrollTotalFrames;
                void CheckFrozen()
                {
                    FailIf(!bats.Select(State).SequenceEqual(frozen),
                        $"4:{route.Target:x2} Fire Keese changed logical/drawn position, animation or counters during scrolling.");
                    if (_transitions.ScrollActive)
                        FailIf(!outgoing.Select(State).SequenceEqual(outgoingState),
                            "Outgoing Fire Keese advanced while scrolling.");
                }
                if (batched) Step(total, CheckFrozen);
                else for (int i = 0; i < total; i++) Step(1, CheckFrozen);
                FailIf(_transitions.ScrollActive || bats.Any(b => b.TransitionDrawOffset != Vector2.Zero),
                    "Fire Keese scroll did not finish with cleared presentation offsets.");

                Vector2[] before = bats.Select(DrawPosition).ToArray();
                Step(1);
                for (int i = 0; i < bats.Length; i++)
                    FailIf(bats[i].State is not (0x0b or 0x0c) || bats[i].ZFixed != -0x1c00 ||
                        bats[i].Counter is not (7 or 90) || before[i].DistanceTo(DrawPosition(bats[i])) > 2,
                        $"4:{route.Target:x2} Fire Keese reinitialized/teleported on the first resumed update.");
                if (batched) Step(7); else for (int i = 0; i < 7; i++) Step(1);
                return bats.Select(State).Append(_random.Calls.ToString()).ToArray();
            }
            FailIf(!Run(false).SequenceEqual(Run(true)),
                $"4:{route.Target:x2} Fire Keese scroll/re-entry differs between individual and batched gameplay updates.");
        }

        LoadValidationRoom(4, 0x91);
        FailIf(_entities.Entities<FireKeeseCharacter>().Count != 0 ||
            _entities.OutgoingEntities<FireKeeseCharacter>().Count != 0,
            "Room exit retained incoming/outgoing Fire Keese.");
        GD.Print("Validated Fire Keese state-0 scroll presentation, frozen flight, first resumed update, re-entry and batched gameplay updates in 4:85/8d/8e.");
    }
}
