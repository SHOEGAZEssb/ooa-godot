using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownStairEnemyFadeInitialization()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _player.ApplicationUpdateOwned = true;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                LoadValidationRoom(4, 0xae);
                _entities.Clear(); // Isolate the approach; retain all destination objects.
                Vector2 stair = new(88, 88);
                FailIf(_currentRoom.GetMetatile(stair) != 0x45,
                    "4:$ae stair $55 must retain source tile $45 for the 4:$9f arrival.");
                Vector2 approach = Vector2.Zero;
                foreach (Vector2 offset in new[] { Vector2.Down, Vector2.Right, Vector2.Left, Vector2.Up })
                {
                    if (_collision.Collides(stair + offset * 16) || _currentRoom.IsSolid(stair + offset * 8))
                        continue;
                    approach = -offset;
                    _player.WarpTo(stair + offset * 16);
                    break;
                }
                FailIf(approach == Vector2.Zero, "4:$ae stair $55 needs a reachable adjacent floor approach.");
                for (int i = 0; !IsTransitioning && i < 40; i++)
                    StepGameplayUpdates(1, approach);
                FailIf(!IsTransitioning || _rooms.CurrentRoom.Id != 0xae,
                    "Walking onto 4:$ae stair $55 must begin the source fade.");
                StepGameplayUpdates(31, Vector2.Zero, batched: batch);
                FailIf(_rooms.CurrentRoom.Id != 0xae || !_transitions.PaletteFadeActive,
                    "4:$ae must remain loaded until fade-out's terminal update32.");
                StepGameplayUpdates(1, Vector2.Zero, batched: batch);
                FailIf(_rooms.ActiveGroup != 4 || _rooms.CurrentRoom.Id != 0x9f ||
                    _player.Position != stair || !IsTransitioning || !_transitions.PaletteFadeActive,
                    "Stair arrival must load 4:$9f/$55 under the white fade.");

                // enemyData.s group4Map9f: two fixed ENEMY $19:$00 followed
                // by two random ENEMY $24:$00. bank0.updateEnemies dispatches
                // state/substate zero even with wPaletteThread_mode active.
                var whisps = _entities.Entities<WhispCharacter>();
                var likes = _entities.Entities<LikeLikeCharacter>();
                FailIf(whisps.Count != 2 || likes.Count != 2 ||
                    whisps[0].Position != new Vector2(120, 56) || whisps[1].Position != new Vector2(120, 136),
                    "4:$9f must load the source's two fixed Whisps and two random Like Likes.");
                FailIf(whisps.Any(w => !w.Initialized || !w.Visible || w.Angle is not (4 or 12 or 20 or 28)) ||
                    likes.Any(l => l.State != 8 || !l.Visible),
                    "4:$9f ENEMY $19/$24 state0 must initialize and become visible before white fades away.");
                var positions = whisps.Select(w => w.Position).ToArray();
                var angles = whisps.Select(w => w.Angle).ToArray();
                var likePositions = likes.Select(l => l.Position).ToArray();
                var random = CaptureOracleRandomForValidation();
                foreach (int updates in new[] { 1, 14, 16 })
                {
                    StepGameplayUpdates(updates, Vector2.Zero, batched: batch);
                    var currentRandom = CaptureOracleRandomForValidation();
                    FailIf(!_transitions.PaletteFadeActive || !IsTransitioning ||
                        whisps.Where((w, i) => !w.Visible || w.Position != positions[i] || w.Angle != angles[i]).Any() ||
                        likes.Where((l, i) => !l.Visible || l.State != 8 || l.Position != likePositions[i]).Any() ||
                        currentRandom.Calls != random.Calls || currentRandom.Rng1 != random.Rng1 ||
                        currentRandom.Rng2 != random.Rng2 || currentRandom.PlacementIndex != random.PlacementIndex ||
                        !currentRandom.PlacementBuffer.SequenceEqual(random.PlacementBuffer),
                        "4:$9f initialized enemies must stay visible and frozen, without consuming RNG during fade-in.");
                }
                StepGameplayUpdates(1, Vector2.Zero, batched: batch);
                FailIf(IsTransitioning || _transitions.PaletteFadeActive ||
                    whisps.Where((w, i) => !w.Visible || w.Position == positions[i]).Any() ||
                    likes.Any(l => !l.Visible || l.State != 10),
                    "4:$9f enemies must resume state8 on terminal fade update32 without a delayed appearance.");
                var afterFade = whisps.Select(w => w.Position).ToArray();
                StepGameplayUpdates(1, Vector2.Zero, batched: batch);
                FailIf(whisps.Where((w, i) => !w.Visible || w.Position == afterFade[i]).Any(),
                    "4:$9f Whisps must continue moving after the fade completes.");
            }
        }
        ReinitializeGameplayForValidation();
        GD.Print("Validated 4:$ae stair arrival into 4:$9f: enemy visibility during white fade, frozen state/RNG, exact resume and repeat in single/batched updates.");
    }

    private void ValidateCrownStairs()
    {
        _player.ApplicationUpdateOwned = true;
        // Source room binaries, group4WarpSources/group6WarpDestTable and
        // dungeon5 floor cells. Explicit passages plus every floor fallback.
        foreach (var route in new (int Room, int Pos, int Tile, int Group, int Dest, int Arrival)[]
        {
            (Room:0x99, Pos:0x23, Tile:0x45, Group:6, Dest:0x98, Arrival:0x01),
            (0x9b,0x12,0x45,6,0x98,0x0d), (0x9c,0x87,0x44,6,0x96,0xad),
            (0xa0,0x22,0x45,6,0x97,0x01), (0xa2,0x87,0x45,6,0x94,0x0c),
            (0xa3,0x17,0x45,6,0x97,0x0d), (0xa4,0x64,0x45,6,0x93,0x02),
            (0xad,0x27,0x45,6,0x95,0x02),
            (0x9a,0x2c,0x44,4,0xa7,0x2c), (0x9c,0x27,0x44,4,0xaa,0x27),
            (0x9d,0x17,0x44,4,0xab,0x17), (0x9f,0x55,0x44,4,0xae,0x55),
            (0xa1,0x17,0x44,4,0xb1,0x17), (0xa4,0x49,0x44,4,0xb7,0x49),
            (0xa6,0x5a,0x44,4,0xbc,0x5a), (0xa7,0x2c,0x45,4,0x9a,0x2c),
            (0xaa,0x27,0x45,4,0x9c,0x27), (0xae,0x55,0x45,4,0x9f,0x55),
            (0xb1,0x17,0x45,4,0xa1,0x17), (0xb7,0x49,0x45,4,0xa4,0x49),
            (0xbc,0x5a,0x45,4,0xa6,0x5a)
        })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            LoadValidationRoom(4, route.Room);
            _entities.Clear(); // Isolate the static stair from combat and puzzle controllers.
            Vector2 center = new((route.Pos & 15) * 16 + 8, (route.Pos >> 4) * 16 + 8);
            FailIf(_currentRoom.GetMetatile(center) != route.Tile,
                $"Crown stair4:{route.Room:x2}/${route.Pos:x2} must retain its original tile${route.Tile:x2}.");
            Vector2 approach = Vector2.Zero;
            foreach (Vector2 offset in new[] { Vector2.Down, Vector2.Right, Vector2.Left, Vector2.Up })
            {
                Vector2 start = center + offset * 16;
                if (start.X < 8 || start.Y < 8 || start.X >= _currentRoom.Width - 8 || start.Y >= _currentRoom.Height - 8 ||
                    _collision.Collides(start) || _currentRoom.IsSolid(center + offset * 8)) continue;
                approach = -offset;
                _player.WarpTo(start);
                break;
            }
            FailIf(approach == Vector2.Zero, $"No adjacent floor approach to Crown stair4:{route.Room:x2}/${route.Pos:x2}.");
            for (int i = 0; !_transitions.IsTransitioning && i < 40; i++)
                StepGameplayUpdates(1, approach);
            FailIf(!_transitions.IsTransitioning,
                $"Movement must reach Crown stair4:{route.Room:x2}/${route.Pos:x2} from its actual adjacent floor.");
            for (int i = 0; _rooms.ActiveGroup == 4 && _rooms.CurrentRoom.Id == route.Room && i < 160; i++)
                StepGameplayUpdates(1, Vector2.Zero);
            Vector2 arrival = new((route.Arrival & 15) * 16 + 8, (route.Arrival >> 4) * 16 + 8);
            FailIf(_rooms.ActiveGroup != route.Group || _rooms.CurrentRoom.Id != route.Dest || _player.Position != arrival,
                $"Crown stair4:{route.Room:x2}/${route.Pos:x2} expected{route.Group:x1}:{route.Dest:x2}/${route.Arrival:x2}, " +
                $"got{_rooms.ActiveGroup:x1}:{_rooms.CurrentRoom.Id:x2} at{_player.Position}.");
            for (int i = 0; _transitions.IsTransitioning && i < 160; i++) StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_transitions.IsTransitioning, "Crown stair arrival must release the transition.");
            LoadValidationRoom(0, 0x60);
        }
    }
}
