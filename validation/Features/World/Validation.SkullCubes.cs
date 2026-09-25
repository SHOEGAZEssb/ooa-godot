using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonCubes()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Step(int count = 1, Vector2 move = default, bool attack = false) =>
            StepGameplayUpdates(count, move, attack ? ["attack"] : [], attack ? ["attack"] : [], batched: true);
        static Vector2 Point(int p) => new((p & 15) * 16 + 8, (p >> 4) * 16 + 8);
        Vector2I[] directions = [Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left];
        int[] offsets = [-16, 1, 16, -1];
        // Final parameters $80-$85 in interactionAnimation5a14c..5a260,
        // resolved through coloredCube.s @animations (U,R,D,L columns).
        int[,] orientations = { { 1, 3, 1, 3 }, { 0, 4, 0, 4 }, { 3, 5, 3, 5 },
            { 2, 0, 2, 0 }, { 5, 1, 5, 1 }, { 4, 2, 4, 2 } };
        int[] colors = [1, 0, 0, 2, 2, 1];
        // Keep native enemy contact/knockback active during the puzzle route;
        // equip enough legitimate health to survive the trap-side approach.
        for (int i = 0; i < 11; i++) _inventory.GiveTreasure(TreasureDatabase.TreasureHeartContainer, 4);
        _inventory.RefillHealth();
        _inventory.GiveTreasure(TreasureDatabase.TreasureSword, 1);
        _inventory.EquipA(InventoryState.ItemSword);
        var database = new SkullDungeonDatabase();
        FailIf(database.GetRoomRecords(4, 0x72) is not [
            { Id: 0x15 }, { Id: 0x1b, SubId: 0, Order: 1, X: 0xc0, Y: 0x88 },
            { Id: 0x21, SubId: 7, Order: 2, X: 1, Y: 0x8d }],
            "4:72 lost its source floor/gate/bit-consumer order.");
        LoadValidationRoom(4, 0x72);
        _player.WarpTo(new Vector2(152, 72));
        var gate = _entities.Entities<MinecartGateRoomEntity>().Single();
        _entities.RuntimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress, 0x80);
        _currentRoom.SetPositionTileAndCollision(Point(0x8d), 0xaf, null, 0);
        Step();
        FailIf(!gate.Open || _entities.RuntimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != 0x81,
            "4:72 gate must run before the floor event publishes bit $01, preserving bit $80.");
        Step();
        FailIf(gate.Open || !gate.Animating || _currentRoom.GetMetatile(Point(0x8b)) != 0x5e ||
            _currentRoom.GetTerrainInfo(Point(0x8b)).Collision != 0 || _currentRoom.GetTerrainInfo(Point(0x8c)).Collision != 0x0a,
            "4:72 gate did not close with source collision bytes on the following dispatch.");
        _currentRoom.SetPositionTileAndCollision(Point(0x8d), 0xda, null, 0);
        Step(24);
        FailIf(_entities.RuntimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != 0x81,
            "$21:$07 must retain its switch bit while Somaria covers the floor control.");
        _currentRoom.SetPositionTileAndCollision(Point(0x8d), 0xad, null, 0);
        Step(26);
        FailIf(!gate.Open || _entities.RuntimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != 0x80,
            "4:72 red floor did not reopen the gate while preserving other switch bits.");

        foreach (var (room, initial, initialOrientation, sensor, start) in new[] {
            (0x78, 0x2c, 1, 0x67, 0x1c), (0x90, 0x36, 0, 0x74, 0x26) })
        {
            _inventory.RefillHealth();
            _saveData.SetRoomFlag(4, room, OracleSaveData.RoomFlagItem, false);
            _entities.RuntimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress, 0);
            LoadValidationRoom(4, room);
            var cube = _entities.Entities<ColoredCubeRoomEntity>().Single();
            var puzzle = cube.ColoredCubePuzzleState;
            FailIf(cube.Position != Point(initial) || cube.Orientation != initialOrientation,
                $"4:{room:x2} lost its initial cube position/orientation.");
            _player.WarpTo(Point(start));
            Step();
            bool Open(int p) => p >= 0 && p < 0xb0 && (p & 15) < 15 &&
                (p == initial || _currentRoom.GetTerrainInfo(Point(p)).Collision == 0) &&
                _currentRoom.GetTerrainInfo(Point(p)).Hazard == HazardType.None;
            // Plan using only native static collision cells and the source
            // orientation transitions. Link must reach the pushing side for
            // every roll; the cube cannot pass through a wall or cracked tile.
            var floor = Enumerable.Range(0, 0xb0).Where(Open).ToHashSet();
            var cubeFloor = floor.Where(p => _currentRoom.GetMetatile(Point(p)) != 0x4d).ToHashSet();
            List<int>? WalkPath(int from, int to, int block)
            {
                var queue = new Queue<int>();
                var previous = new Dictionary<int, int> { [from] = -1 };
                queue.Enqueue(from);
                while (queue.TryDequeue(out int at))
                {
                    if (at == to)
                    {
                        var path = new List<int>();
                        for (int p = at; p >= 0; p = previous[p]) path.Add(p);
                        path.Reverse();
                        return path;
                    }
                    foreach (int offset in offsets)
                    {
                        int next = at + offset;
                        if (next == block || !floor.Contains(next) || previous.ContainsKey(next)) continue;
                        previous.Add(next, at);
                        queue.Enqueue(next);
                    }
                }
                return null;
            }
            var search = new Queue<(int Cell, int Orientation, int Link, string Moves)>();
            var seen = new HashSet<(int, int, int)>();
            search.Enqueue((initial, initialOrientation, start, ""));
            string? solution = null;
            while (search.TryDequeue(out var state))
            {
                if (state.Cell == sensor && colors[state.Orientation] == 2) { solution = state.Moves; break; }
                for (int d = 0; d < 4; d++)
                {
                    int next = state.Cell + offsets[d];
                    int behind = state.Cell - offsets[d];
                    if (!cubeFloor.Contains(next) || !floor.Contains(behind) || WalkPath(state.Link, behind, state.Cell) == null) continue;
                    int orientation = orientations[state.Orientation, d];
                    if (seen.Add((next, orientation, state.Cell)))
                        search.Enqueue((next, orientation, state.Cell, state.Moves + d));
                }
            }
            FailIf(solution == null, $"4:{room:x2} lacks a source-geometry cube route to its blue sensor; floor={string.Join(',', floor.Select(p => p.ToString("x2")))}, states={string.Join(',', seen.Select(p => p.Item1.ToString("x2")).Distinct())}.");
            void WalkTo(int cell)
            {
                Vector2 target = Point(cell);
                List<int>? recoveryRoute = null;
                int waypoint = 0;
                for (int i = 0; i < 600 && _player.Position.DistanceTo(target) > 0.8f; i++)
                {
                    if (_player.KnockbackFrames > 0)
                    {
                        Step();
                        recoveryRoute = null;
                        continue;
                    }
                    var threat = _entities.Entities<EnemyCharacter>()
                        .Where(enemy => enemy is not BladeTrapCharacter && enemy.Health > 0 && enemy.Position.DistanceTo(_player.Position) < 24)
                        .OrderBy(enemy => enemy.Position.DistanceSquaredTo(_player.Position)).FirstOrDefault();
                    if (threat != null && !_player.IsAttacking)
                    {
                        Vector2 toward = threat.Position - _player.Position;
                        Vector2 facing = Math.Abs(toward.X) > Math.Abs(toward.Y)
                            ? new Vector2(Math.Sign(toward.X), 0) : new Vector2(0, Math.Sign(toward.Y));
                        Step(move: facing);
                        Step(attack: true);
                        recoveryRoute = null;
                        continue;
                    }
                    if (recoveryRoute == null)
                    {
                        recoveryRoute = WalkPath(_currentRoom.GetPackedPosition(_player.Position), cell,
                            _currentRoom.GetPackedPosition(cube.Position));
                        FailIf(recoveryRoute == null, "Native knockback left no walking route around the cube.");
                        waypoint = 0;
                    }
                    Vector2 delta = Point(recoveryRoute![waypoint]) - _player.Position;
                    if (delta.Length() <= 0.8f && waypoint < recoveryRoute.Count - 1)
                    {
                        waypoint++;
                        continue;
                    }
                    Vector2 move = Math.Abs(delta.X) > 0.8f ? new Vector2(Math.Sign(delta.X), 0) : new Vector2(0, Math.Sign(delta.Y));
                    Step(move: move);
                }
                FailIf(_player.Position.DistanceTo(target) > 0.8f,
                    $"4:{room:x2} actual Link movement could not reach ${cell:x2}; Link={_player.Position}, cube={cube.Position}, health={_inventory.HealthQuarters}, falling={_player.IsFallingInHole}, knockback={_player.KnockbackFrames}, controlled={_player.CutsceneControlled}.");
            }
            foreach (char move in solution!)
            {
                int d = move - '0';
                int cell = _currentRoom.GetPackedPosition(cube.Position);
                int behind = cell - offsets[d];
                var path = WalkPath(_currentRoom.GetPackedPosition(_player.Position), behind, cell);
                FailIf(path == null, $"4:{room:x2} lost Link's walking route to the next cube face.");
                foreach (int waypoint in path!) WalkTo(waypoint);
                int orientation = orientations[cube.Orientation, d];
                int pushes = 0;
                Vector2 lateral = new(-directions[d].Y, directions[d].X);
                while (!cube.Moving && pushes++ < 180)
                {
                    if (Math.Abs((_player.Position - Point(behind)).Dot(lateral)) > 3) WalkTo(behind);
                    Step(move: directions[d]);
                }
                FailIf(!cube.Moving || _currentRoom.GetTerrainInfo(Point(cell)).Collision != 0,
                    $"4:{room:x2} actual pushing did not start the cube roll from ${cell:x2} toward {d}; Link={_player.Position}, health={_inventory.HealthQuarters}.");
                Step(12);
                FailIf(cube.Moving || cube.Position != Point(cell + offsets[d]) || cube.Orientation != orientation ||
                    _currentRoom.GetTerrainInfo(cube.Position).Collision != 0x0f,
                    $"4:{room:x2} roll lost its twelve-update animation, orientation, endpoint, or collision.");
            }
            FailIf(cube.Position != Point(sensor) || colors[cube.Orientation] != 2,
                $"4:{room:x2} did not finish its actual cube route in blue.");
            if (room == 0x78)
            {
                FailIf(puzzle.CubeColor != 2 || (_entities.RuntimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) & 2) == 0,
                    "4:78 switch sensor did not consume the light bit after the position sensor published blue.");
                Step(26);
                FailIf(_entities.Entities<MinecartGateRoomEntity>().Single().Open,
                    "4:78 blue cube did not close its bit-$02 minecart gate.");
            }
            else
            {
                FailIf(puzzle.CubeColor != 0x82 || _currentRoom.GetMetatile(Point(0x84)) != 0xf1 ||
                    _entities.Entities<DungeonPuzzleChestRoomEntity>().Count != 0 ||
                    _entities.Entities<ColoredCubeFlameRoomEntity>().Count != 4 ||
                    _entities.Entities<ColoredCubeFlameRoomEntity>().Any(flame => !flame.Visible || flame.Palette != 2),
                    "4:90 did not light four blue flames and immediately create its chest in source order.");
                // Palette replacement must not select a separate animation
                // clock. Native interaction1a only replaces oamFlags.
                foreach (int color in new[] { 0, 1, 2 })
                {
                    puzzle.CubeColor = 0x80 | color;
                    Step(5);
                    foreach (var flame in _entities.Entities<ColoredCubeFlameRoomEntity>())
                    {
                        var palettes = (EnemyAnimationPlayer[])typeof(ColoredCubeFlameRoomEntity).GetField("_palettes", flags)!.GetValue(flame)!;
                        FailIf(flame.Palette != color || palettes.Any(palette => palette.FrameIndex != palettes[0].FrameIndex),
                            "Cube flame palette changes restarted or separated the shared animation cursor.");
                    }
                }
                floor.Remove(0x84);
                WalkTo(0x94);
                Step(6, move: Vector2.Up);
                Step(attack: true);
                Step(60);
                FailIf(!_saveData.HasRoomFlag(4, room, OracleSaveData.RoomFlagItem),
                    "4:90's cube chest could not be opened through its actual southern approach and A input.");
                _dialogue.Close();
                _player.EndCutsceneControl();
                _player.EndGetItemTwoHandPose();
            }
            GD.Print($"Validated Skull cube route 4:{room:x2}: {solution} (U=0,R=1,D=2,L=3).");
        }
        LoadValidationRoom(4, 0x91);
        Step();
        FailIf(_entities.Entities<ColoredCubeRoomEntity>().Count != 0 || _entities.Entities<ColoredCubeFlameRoomEntity>().Count != 0,
            "Cube or flame actors survived leaving the puzzle room.");
        LoadValidationRoom(4, 0x90);
        _player.WarpTo(new Vector2(200, 136));
        _inventory.RefillHealth();
        var resetCube = _entities.Entities<ColoredCubeRoomEntity>().Single();
        FailIf(resetCube.Position != Point(0x36) || resetCube.Orientation != 0 ||
            _entities.Entities<DungeonPuzzleChestRoomEntity>().Count != 0,
            "4:90 re-entry did not reset its cube and suppress its collected chest event.");
        var holeCounter = typeof(ColoredCubeRoomEntity).GetField("_holeCounter", flags)!;
        Step(10);
        FailIf((int)holeCounter.GetValue(resetCube)! != 0, "Cube idle cracked-floor timer did not reach zero after ten updates.");
        Step();
        FailIf((int)holeCounter.GetValue(resetCube)! != 0xff, "Cube idle cracked-floor timer did not wrap from zero to $ff.");
        _currentRoom.SetPositionTileAndCollision(resetCube.Position, 0x4d, 0x0f, 0);
        Step(254);
        FailIf(resetCube.Finished || (int)holeCounter.GetValue(resetCube)! != 1,
            "Cube inspected a newly cracked tile before its next zero-counter dispatch.");
        Step();
        FailIf(!resetCube.Finished || _currentRoom.GetMetatile(Point(0x36)) != 0xf3 ||
            resetCube.ColoredCubePuzzleState.CubePosition != 0x36,
            "Cube's zero-counter fall did not replace the cracked tile with a hole, delete itself and retain native cube-position scratch.");
        GD.Print("Validated Skull Dungeon cubes, blue-flame chest, floor and cube switch gates through actual movement and source-ordered updates.");
    }
}
