using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonFillers()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        void Step(int count = 1, Vector2 move = default, bool attack = false)
        {
            input.CaptureForValidation(attack ? ["attack"] : [], attack ? ["attack"] : [], move);
            scheduler.Advance(count / 60.0, update);
        }
        static Vector2 Point(int p) => new((p & 15) * 16 + 8, (p >> 4) * 16 + 8);
        void Walk(int cell, bool batch = false)
        {
            Vector2 target = Point(cell);
            for (int i = 0; i < 60 && _player.Position.DistanceTo(target) > 0.8f; i++)
            {
                Vector2 delta = target - _player.Position;
                bool horizontal = Math.Abs(delta.X) > 0.8f;
                Vector2 move = horizontal ? new Vector2(Math.Sign(delta.X), 0) : new Vector2(0, Math.Sign(delta.Y));
                float distance = Math.Abs(horizontal ? delta.X : delta.Y);
                int count = batch ? Math.Clamp((int)Math.Ceiling((distance - 0.6f) / 1.25f), 1, 12) : 1;
                Step(count, move);
            }
            FailIf(_player.Position.DistanceTo(target) > 0.8f,
                $"Tile-filler route could not walk to ${cell:x2}; Link={_player.Position}.");
        }
        var data = new SkullDungeonDatabase();
        foreach (var (room, y, x, cy, cx) in new[] { (0x6f, 0x58, 0xd8, 0x68, 0x78), (0x87, 0x98, 0x28, 0x68, 0x58) })
        {
            var records = data.GetRoomRecords(4, room);
            FailIf(records.Count != 2 || records[0] is not { Order: 0, Id: 0x25, SubId: 0 } || records[0].X != x || records[0].Y != y ||
                records[1] is not { Order: 1, Id: 0x21, SubId: 0x11, Predicate: DungeonObjectCondition.ItemClear } || records[1].X != cx || records[1].Y != cy,
                $"4:{room:x2} lost source tile-filler/chest placements or order.");
        }
        _saveData.SetRoomFlag(4, 0x87, OracleSaveData.RoomFlagItem, false);
        LoadValidationRoom(4, 0x87);
        _player.WarpTo(Point(0x92));
        var filler = _entities.Entities<TileFillerRoomEntity>().Single();
        var textSource = _entities.TextActiveSource;
        try
        {
            _entities.TextActiveSource = () => true;
            Step();
            FailIf(filler.Endpoint != 0x92 || _currentRoom.GetMetatile(Point(0x92)) != 0x9e,
                "State-zero tile filler did not initialize its yellow endpoint during text.");
            _player.WarpTo(Point(0x91));
            Step();
            FailIf(filler.Endpoint != 0x92, "Initialized tile filler advanced during text.");
        }
        finally { _entities.TextActiveSource = textSource; }
        Step();
        FailIf(filler.Endpoint != 0x91 || _currentRoom.GetMetatile(Point(0x92)) != 0x9d,
            "Tile filler did not resume its adjacent blue step after text closed.");
        Walk(0x92);
        Walk(0x82);
        FailIf(filler.Endpoint != 0x91 || _currentRoom.GetMetatile(Point(0x82)) != 0x9f,
            "Tile filler moved its endpoint onto revisited red or a nonadjacent blue tile.");
        Walk(0x81);
        FailIf(filler.Endpoint != 0x81, "Returning beside the yellow endpoint did not resume the fill path.");
        _inventory.GiveTreasure(TreasureDatabase.TreasureFeather, 1);
        _inventory.EquipA(InventoryState.ItemFeather);
        Step(move: Vector2.Up, attack: true);
        for (int i = 0; i < 15 && filler.Endpoint != 0x71; i++) Step(move: Vector2.Up);
        FailIf(filler.Endpoint != 0x71 || !_player.TopDownAirborne,
            "Tile filler added an airborne gate absent from getLinkTilePosition and interactionCode25.");
        for (int i = 0; i < 80 && _player.TopDownAirborne; i++) Step();
        LoadValidationRoom(4, 0x91);
        Step();
        FailIf(_entities.Entities<TileFillerRoomEntity>().Count != 0, "Cancelled tile-filler room retained its endpoint actor.");

        // Hamiltonian walks over the clean source layouts, including every
        // original $9f cell exactly once. Obstacles and the chest pedestal
        // retain their actual native collision throughout the traversal.
        var routes = new[] {
            (Room: 0x6f, Count: 105, Chest: 0x67, Route: "5d,4d,3d,2d,1d,1c,2c,3c,4c,5c,6c,6d,7d,8d,9d,9c,9b,8b,7b,6b,5b,4b,4a,3a,39,49,48,58,59,5a,6a,7a,8a,9a,99,98,88,87,97,96,86,85,95,94,93,83,73,72,82,92,91,81,71,61,62,52,42,32,31,21,11,12,13,14,15,16,17,18,19,1a,1b,2b,2a,29,28,27,37,47,57,56,46,36,35,25,24,34,33,43,44,45,55,54,53,63,64,74,75,65,66,76,77,78,68,69,79"),
            (Room: 0x87, Count: 54, Chest: 0x65, Route: "92,91,81,71,61,51,41,31,32,42,52,53,63,64,54,44,34,35,36,37,38,39,49,59,58,68,78,79,89,99,98,97,87,77,67,57,47,46,45,55,56,66,76,75,85,86,96,95,94,84,83,73,72,82") };
        _inventory.GiveTreasure(TreasureDatabase.TreasureSword, 2);
        _inventory.EquipA(InventoryState.ItemSword);
        foreach (var route in routes)
        {
            byte[] path = route.Route.Split(',').Select(value => Convert.ToByte(value, 16)).ToArray();
            FailIf(path.Length != route.Count || path.Distinct().Count() != route.Count,
                "Tile-filler source route lost its independent cell count or uniqueness.");
            (string Layout, int Endpoint) Run(bool batch)
            {
                _saveData.SetRoomFlag(4, route.Room, OracleSaveData.RoomFlagItem, false);
                LoadValidationRoom(4, route.Room);
                _player.WarpTo(Point(path[0]));
                _inventory.RefillHealth();
                // Establish a cleared-enemy fixture through combat hit/death
                // dispatch; this scenario verifies filling, not Moldorm combat.
                // Native melee resolves after enemy movement and Link contact.
                // Keep that setup contact from starting the fill route early.
                _player.SetBraceletLiftCollisionsDisabled(true);
                for (int i = 0; i < 8 && _entities.Entities<EnemyCharacter>().Any(enemy => enemy.Health > 0); i++)
                {
                    _entities.ApplySwordHit(new Rect2(Vector2.Zero, new Vector2(_currentRoom.Width, _currentRoom.Height)),
                        _player.Position, damage: 0x7f);
                    Step(40);
                }
                FailIf(_entities.Entities<EnemyCharacter>().Any(enemy => enemy.Health > 0), "Could not establish cleared tile-puzzle enemy fixture.");
                _player.SetBraceletLiftCollisionsDisabled(false);
                Step();
                var cursor = _entities.Entities<TileFillerRoomEntity>().Single();
                FailIf(cursor.Endpoint != path[0] || _currentRoom.Layout.Count(tile => tile == 0x9f) != route.Count - 1 ||
                    _currentRoom.GetMetatile(Point(path[0])) != 0x9e,
                    $"4:{route.Room:x2} did not reload its source blue cells and yellow start after leaving: endpoint=${cursor.Endpoint:x2}, blue={_currentRoom.Layout.Count(tile => tile == 0x9f)}, start=${_currentRoom.GetMetatile(Point(path[0])):x2}, Link={_player.Position}.");
                _sound.ClearPlayRequestAudit();
                // findTileInRoom searches storage, including the otherwise
                // unplayable padding column; it is not a visit counter.
                byte padding = _currentRoom.Layout[0x1f];
                _currentRoom.Layout[0x1f] = 0x9f;
                for (int i = 1; i < path.Length; i++)
                {
                    int delta = (path[i] - path[i - 1]) & 0xff;
                    FailIf(delta is not (1 or 0xff or 16 or 0xf0), "Source fill route is not orthogonally contiguous.");
                    Walk(path[i], batch);
                    FailIf(cursor.Endpoint != path[i] || _currentRoom.GetMetatile(Point(path[i - 1])) != 0x9d ||
                        _currentRoom.GetMetatile(Point(path[i])) != 0x9e || _currentRoom.GetUnderlyingMetatile(Point(path[i])) != 0x9f ||
                        _currentRoom.Layout.Count(tile => tile == 0x9f) != path.Length - i,
                        $"4:{route.Room:x2} fill step {i} lost endpoint, red/yellow ordering, blue count, or source buffer.");
                    FailIf(i < path.Length - 1 && _currentRoom.GetMetatile(Point(route.Chest)) == 0xf1,
                        "Tile-filler chest appeared before the last blue tile was consumed.");
                }
                FailIf(_currentRoom.GetMetatile(Point(route.Chest)) == 0xf1,
                    "Fill chest ignored a blue storage-padding cell in findTileInRoom's search range.");
                _currentRoom.Layout[0x1f] = padding;
                Step();
                FailIf(_currentRoom.GetMetatile(Point(route.Chest)) != 0xf1 || _entities.Entities<DungeonPuzzleChestRoomEntity>().Count != 0 ||
                    _sound.PlayRequestsFor(OracleSoundEngine.SndGetSeed) != path.Length - 1 ||
                    _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1,
                    "Final tile fill did not create one immediate chest after the earlier filler dispatch.");
                var result = (Convert.ToHexString(_currentRoom.Layout), cursor.Endpoint);
                // Walk across the completed red path to the chest's south.
                int from = path[^1], to = route.Chest + 16;
                var queue = new Queue<int>();
                var previous = new Dictionary<int, int> { [from] = -1 };
                queue.Enqueue(from);
                while (queue.TryDequeue(out int at) && !previous.ContainsKey(to))
                    foreach (int offset in new[] { -16, 1, 16, -1 })
                    {
                        int next = at + offset;
                        if (next < 0 || next >= 0xb0 || (next & 15) >= 15 || previous.ContainsKey(next) ||
                            _currentRoom.GetTerrainInfo(Point(next)).Collision != 0) continue;
                        previous.Add(next, at); queue.Enqueue(next);
                    }
                FailIf(!previous.ContainsKey(to), "Completed fill path did not leave the chest reachable.");
                var approach = new List<int>();
                for (int at = to; at != from; at = previous[at]) approach.Add(at);
                approach.Reverse();
                foreach (int at in approach) Walk(at, batch);
                Step(6, Vector2.Up);
                Step(attack: true);
                Step(60);
                FailIf(!_saveData.HasRoomFlag(4, route.Room, OracleSaveData.RoomFlagItem),
                    $"4:{route.Room:x2} fill chest could not be collected through actual movement and A input.");
                _dialogue.Close(); _player.EndCutsceneControl(); _player.EndGetItemTwoHandPose();
                LoadValidationRoom(4, 0x91);
                LoadValidationRoom(4, route.Room);
                Step();
                FailIf(_entities.Entities<DungeonPuzzleChestRoomEntity>().Count != 0 ||
                    _entities.Entities<TileFillerRoomEntity>().Count != 1,
                    "Collected filler chest respawned, or the independent filler incorrectly stopped spawning.");
                return result;
            }
            var single = Run(false);
            var batched = Run(true);
            FailIf(single != batched, $"4:{route.Room:x2} full fill route changed with batched application updates.");
        }
        GD.Print("Validated Skull tile fillers: source placements, text and airborne gates, revisits, 105/54-cell walks, single/batched updates, reachable chests and re-entry.");
    }
}
