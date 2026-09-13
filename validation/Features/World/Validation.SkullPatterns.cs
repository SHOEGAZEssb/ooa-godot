using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonPatterns()
    {
        var data = new SkullDungeonDatabase();
        // dungeonEvents.s byte tables, independently specified here.
        byte[][] doorPattern = [[0x43, 0x45, 0x64], [0x54, 0x63, 0x65], [0x44, 0x53, 0x55]];
        byte[][] keyPattern = [[0x54, 0x58], [], [0x55, 0x57]];
        foreach (var (subid, pattern) in new[] { (0x0f, doorPattern), (0x10, keyPattern) })
            for (int color = 0; color < 3; color++)
                FailIf(!data.Pattern(subid)[color].SequenceEqual(pattern[color]),
                    $"$21:${subid:x2} color {color} lost its source tile positions.");
        FailIf(data.GetRoomRecords(4, 0x79)[1] is not { Id: 0x21, SubId: 0x0f, Order: 2 } ||
            data.GetRoomRecords(4, 0x7b)[0] is not { Id: 0x21, SubId: 0x10, Order: 0, X: 0x68, Y: 0x58 },
            "Skull pattern events lost their native order or falling-key coordinates.");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        var setTrigger = (Action<int, bool>)typeof(RoomEntityManager).GetMethod("SetTrigger", flags)!.CreateDelegate(typeof(Action<int, bool>), _entities);
        void Step(int count = 1, bool jump = false, Vector2 move = default)
        {
            input.CaptureForValidation(jump ? ["attack"] : [], jump ? ["attack"] : [], move);
            scheduler.Advance(count / 60.0, update);
        }
        static Vector2 Point(int packed) => new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
        void Tile(int packed, int tile) => _currentRoom.SetPositionTileAndCollision(Point(packed), (byte)tile, null, (long)_animationTicks);
        void SetPattern(byte[][] pattern)
        {
            for (int color = 0; color < 3; color++)
                foreach (byte packed in pattern[color]) Tile(packed, 0xad + color);
        }
        _inventory.GiveTreasure(TreasureDatabase.TreasureFeather, 1);
        _inventory.EquipA(InventoryState.ItemFeather);
        LoadValidationRoom(4, 0x79);
        _player.WarpTo(new Vector2(168, 120));
        SetPattern(doorPattern);
        foreach (var (color, packed) in doorPattern.SelectMany((positions, color) => positions.Select(packed => (color, packed))))
        {
            Tile(packed, 0xad + (color + 1) % 3);
            setTrigger(7, true);
            Step();
            FailIf(_entities.ActiveTriggers != 0, $"$21:$0f accepted the wrong color at ${packed:x2} or retained another trigger bit.");
            Tile(packed, 0xad + color);
        }
        var textSource = _entities.TextActiveSource;
        try
        {
            _entities.TextActiveSource = () => true;
            Step();
            FailIf(_entities.ActiveTriggers != 1, "State-zero $21:$0f stopped evaluating during text.");
            Tile(0x43, 0xda);
            Step();
            FailIf(_entities.ActiveTriggers != 0, "$21:$0f accepted Somaria in place of a required red tile during text.");
        }
        finally { _entities.TextActiveSource = textSource; }

        // Complete each native pattern through Feather input on a real clear
        // corridor. Only the other puzzle colors are prepared as a fixture.
        var random = CaptureOracleRandomForValidation();
        foreach (var (room, pattern) in new[] { (0x79, doorPattern), (0x7b, keyPattern) })
        {
            int flightUpdates = 0;
            (Vector2 Link, int Tile, int Trigger, int Keys) Run(bool batch)
            {
                RestoreOracleRandomForValidation(random);
                _saveData.SetRoomFlag(4, room, OracleSaveData.RoomFlagItem, false);
                LoadValidationRoom(4, room);
                SetPattern(pattern);
                Vector2 direction = Vector2.Zero;
                int target = 0, targetColor = 0;
                foreach (var (color, packed) in pattern.SelectMany((positions, color) => positions.Select(packed => (color, packed))))
                {
                    foreach (Vector2 candidate in new[] { Vector2.Right, Vector2.Left, Vector2.Down, Vector2.Up })
                    {
                        Vector2 origin = Point(packed) + Vector2.Up * (room == 0x79 ? 5 : 1) - candidate * (room == 0x79 ? 24 : 16);
                        Vector2 side = new(-candidate.Y, candidate.X);
                        bool clear = _currentRoom.GetTerrainInfo(origin + Vector2.Down * 5).Hazard == HazardType.None &&
                            Enumerable.Range(0, room == 0x79 ? 73 : 17).All(i => Enumerable.Range(-5, 11).All(j =>
                        {
                            Vector2 point = origin + candidate * i + side * j;
                            return point.X >= 8 && point.Y >= 8 && point.X < _currentRoom.Width - 8 && point.Y < _currentRoom.Height - 8 &&
                                !_currentRoom.IsSolid(point) && (room != 0x79 || _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None) &&
                                !_entities.Entities<EnemyCharacter>().Any(enemy => enemy.Position.DistanceTo(point) < 10);
                        }));
                        if (!clear) continue;
                        direction = candidate;
                        target = packed;
                        targetColor = color;
                        _player.WarpTo(origin);
                        break;
                    }
                    if (direction != Vector2.Zero) break;
                }
                FailIf(direction == Vector2.Zero, $"4:{room:x2} lacks a clear native Feather approach to its pattern.");
                Tile(target, 0xad + (targetColor + 2) % 3);
                Step();
                FailIf(_entities.ActiveTriggers != 0 || _entities.Entities<GroundTreasurePickup>().Count != 0,
                    $"4:{room:x2} solved before the final floor jump.");
                Step(jump: true, move: direction);
                FailIf(!_player.TopDownAirborne, "Pattern test did not enter the real Feather jump.");
                if (room == 0x7b) Step(15, move: direction);
                Vector2 remainingMove = room == 0x79 ? direction : Vector2.Zero;
                if (batch) Step(flightUpdates, move: remainingMove);
                else
                {
                    while (_player.TopDownAirborne && flightUpdates < 80)
                    {
                        Step(move: remainingMove);
                        flightUpdates++;
                    }
                }
                FailIf(_player.TopDownAirborne || _currentRoom.GetMetatile(Point(target)) != 0xad + targetColor,
                    $"4:{room:x2} Feather did not land and cycle target ${target:x2}; Link={_player.Position}, tile={_currentRoom.GetMetatile(Point(target)):x2}.");
                if (room == 0x79)
                {
                    FailIf(_entities.ActiveTriggers != 1 || !_currentRoom.IsSolid(Point(0x50)),
                        "4:79 must publish its trigger on landing, after the earlier shutter has already updated.");
                    Step(16);
                    FailIf(_currentRoom.IsSolid(Point(0x50)), "4:79 shutter did not open after its exact floor pattern matched.");
                    Step(16, move: direction);
                    Step(jump: true, move: -direction);
                    for (int i = 0; i < 80 && _player.TopDownAirborne; i++) Step(move: -direction);
                    FailIf(_entities.ActiveTriggers != 0, "A repeated floor jump did not revoke the solved pattern trigger.");
                    Step(16);
                    FailIf(!_currentRoom.IsSolid(Point(0x50)), "4:79 shutter did not close when its pattern was broken again.");
                }
                else
                {
                    FailIf(_entities.Entities<GroundTreasurePickup>().Count != 0,
                        "4:7b key event ran after the later toggle-floor landing in the same update.");
                    Step();
                    var keys = _entities.Entities<GroundTreasurePickup>();
                    FailIf(keys is not [{ Record.SpawnMode: 2, Record.TreasureObject: "TREASURE_OBJECT_SMALL_KEY_01" }] ||
                        keys[0].Position != new Vector2(0x68, 0x58) || _entities.Entities<DungeonPatternKeyRoomEntity>().Count != 0,
                        "4:7b must delete its pattern event and create exactly one falling key at $58,$68 on the following dispatch.");
                    Step(20);
                    FailIf(_entities.Entities<GroundTreasurePickup>().Count != 1,
                        "4:7b solved pattern spawned duplicate falling keys.");
                }
                return (_player.Position, _currentRoom.GetMetatile(Point(target)), _entities.ActiveTriggers,
                    _entities.Entities<GroundTreasurePickup>().Count);
            }
            var single = Run(false);
            var batched = Run(true);
            FailIf(single != batched, $"4:{room:x2} pattern handoff differed with batched Feather updates.");
        }
        // The flag is checked on every dispatch, as well as on room parsing.
        LoadValidationRoom(4, 0x7b);
        var pending = _entities.Entities<DungeonPatternKeyRoomEntity>().Single();
        _saveData.SetRoomFlag(4, 0x7b, OracleSaveData.RoomFlagItem, true);
        SetPattern(keyPattern);
        Step();
        FailIf(!pending.Finished || _entities.Entities<GroundTreasurePickup>().Count != 0,
            "$21:$10 did not cancel when ROOMFLAG_ITEM became set before its dispatch.");
        LoadValidationRoom(4, 0x91);
        Step();
        FailIf(_entities.ActiveTriggers != 0 || _entities.Entities<DungeonPatternTriggerRoomEntity>().Count != 0,
            "Skull pattern state leaked into the entrance after room replacement.");
        LoadValidationRoom(4, 0x7b);
        Step(4);
        FailIf(_entities.Entities<DungeonPatternKeyRoomEntity>().Count != 0 || _entities.Entities<GroundTreasurePickup>().Count != 0,
            "Collected 4:7b pattern key respawned on re-entry.");
        GD.Print("Validated Skull Dungeon source floor patterns, real Feather handoffs, shutter reversal, single/batched updates, text, key lifetime and re-entry.");
    }
}
