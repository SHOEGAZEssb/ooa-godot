using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullEntranceProgression()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        static Vector2 Point(int p) => new((p & 15) * 16 + 8, (p >> 4) * 16 + 8);
        Vector2[] directions = [Vector2.Up, Vector2.Right, Vector2.Down, Vector2.Left];
        int[] offsets = [-16, 1, 16, -1];
        int[,] orientations = { { 1, 3, 1, 3 }, { 0, 4, 0, 4 }, { 3, 5, 3, 5 },
            { 2, 0, 2, 0 }, { 5, 1, 5, 1 }, { 4, 2, 4, 2 } };
        var random = CaptureOracleRandomForValidation();
        OracleSaveData.TryDeserialize(_saveData.Serialize(), out var initialSave);
        var runtime = _entities.RuntimeState.CaptureState();
        var entityState = _entities.CaptureDebugState();
        (int Health, int Keys, int RandomCalls, Vector2 Position)? firstResult = null;
        foreach (bool batch in new[] { false, true })
        {
            RestoreOracleRandomForValidation(random);
            _saveData.RestoreFrom(initialSave!);
            _entities.RuntimeState.RestoreState(runtime);
            _entities.RestoreDebugStateBeforeRoomParse(entityState);
            typeof(InventoryState).GetMethod("LoadFromSaveData", flags)!.Invoke(_inventory, null);
            void Step(int count = 1, Vector2 movement = default, bool attack = false)
            {
                input.CaptureForValidation(attack ? ["attack"] : [], attack ? ["attack"] : [], movement);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60, update);
                FailIf(_player.IsDying, $"Skull progression died in 4:{_currentRoom.Id:x2} at {_player.Position}.");
            }
            bool Open(int p) => p >= 0 && p < 0xb0 && (p & 15) < 15 &&
                _currentRoom.GetTerrainInfo(Point(p)).Hazard == HazardType.None &&
                new[] { new Vector2(-5, -3), new Vector2(5, -3), new Vector2(-5, 3), new Vector2(5, 3) }
                    .All(offset => !_currentRoom.IsSolid(Point(p) + offset));
            List<int>? Path(int target)
            {
                int start = _currentRoom.GetPackedPosition(_player.Position);
                var previous = new Dictionary<int, int> { [start] = -1 };
                var queue = new Queue<int>(); queue.Enqueue(start);
                while (queue.TryDequeue(out int at))
                {
                    if (at == target)
                    {
                        var path = new List<int>();
                        for (int p = at; p >= 0; p = previous[p]) path.Add(p);
                        path.Reverse(); return path;
                    }
                    foreach (int offset in offsets)
                    {
                        int next = at + offset;
                        if (!Open(next) || previous.ContainsKey(next)) continue;
                        previous.Add(next, at); queue.Enqueue(next);
                    }
                }
                return null;
            }
            string Layout() => string.Join("/", Enumerable.Range(0, 11).Select(y => string.Concat(
                Enumerable.Range(0, 15).Select(x => Open(y * 16 + x) ? '.' : '#'))));
            void Walk(int cell)
            {
                List<int>? path = null;
                int waypoint = 0;
                for (int i = 0; i < 2000 && _player.Position.DistanceTo(Point(cell)) > 0.8f; i++)
                {
                    if (_player.KnockbackFrames > 0) { Step(); path = null; continue; }
                    var threat = _entities.Entities<EnemyCharacter>().Where(e => e.Health > 0 && e.CollisionEnabled &&
                        e is not (MoldormTailCharacter or BladeTrapCharacter) && (e is not FireKeeseCharacter bat || bat.ZFixed >= -7 * 256) && e.Position.DistanceTo(_player.Position) < 26)
                        .OrderBy(e => e.Position.DistanceSquaredTo(_player.Position)).FirstOrDefault();
                    if (threat != null && !_player.IsAttacking)
                    {
                        Vector2 toward = threat.Position - _player.Position;
                        Step(movement: Math.Abs(toward.X) > Math.Abs(toward.Y)
                            ? new Vector2(Math.Sign(toward.X), 0) : new Vector2(0, Math.Sign(toward.Y)));
                        Step(attack: true); path = null; continue;
                    }
                    if (path is null)
                    {
                        path = Path(cell); waypoint = 0;
                        FailIf(path is null, $"Skull route has no walking path in 4:{_currentRoom.Id:x2} from {_player.Position} to ${cell:x2}; {Layout()}.");
                    }
                    Vector2 delta = Point(path![waypoint]) - _player.Position;
                    if (delta.Length() <= 0.8f && waypoint < path.Count - 1) { waypoint++; continue; }
                    Step(movement: Math.Abs(delta.X) > 0.8f ? new Vector2(Math.Sign(delta.X), 0) : new Vector2(0, Math.Sign(delta.Y)));
                }
                FailIf(_player.Position.DistanceTo(Point(cell)) > 0.8f,
                    $"Skull route could not reach 4:{_currentRoom.Id:x2}/${cell:x2}: Link={_player.Position}, health={_inventory.HealthQuarters}, attacking={_player.IsAttacking}.");
            }
            void Exit(int direction, int destination, int? gateway = null)
            {
                int origin = _currentRoom.Id;
                FailIf(!_rooms.TryGetNeighbor((Vector2I)directions[direction], out int expected) || expected != destination,
                    $"Source dungeon04Layout adjacency 4:{origin:x2}->{destination:x2} disagrees with direction {direction}.");
                var candidates = Enumerable.Range(0, 0xb0).Where(p => (!gateway.HasValue || p == gateway) && Open(p) && direction switch
                { 0 => p >> 4 == 0, 1 => (p & 15) == 14, 2 => p >> 4 == 10, _ => (p & 15) == 0 });
                int endpoint = candidates.FirstOrDefault(p => Path(p) is not null, -1);
                if (endpoint < 0)
                {
                    int door = Enumerable.Range(0, 0xb0).FirstOrDefault(p =>
                        _currentRoom.GetMetatile(Point(p)) == 0x70 + direction, -1);
                    if (door >= 0)
                    {
                        int keysBeforeDoor = _inventory.GetDungeonSmallKeys(4);
                        if (Path(door - offsets[direction]) is null) CrossHazards(Point(door - offsets[direction]));
                        Walk(door - offsets[direction]);
                        for (int i = 0; !Open(door) && i < 120; i++) Step(movement: directions[direction]);
                        FailIf(!Open(door) || _inventory.GetDungeonSmallKeys(4) != keysBeforeDoor - 1 ||
                            !_saveData.HasRoomFlag(4, origin, (byte)(1 << direction)) ||
                            !_saveData.HasRoomFlag(4, destination, (byte)(1 << ((direction + 2) & 3))),
                            $"The earned Small Key did not open both sides of 4:{origin:x2}->4:{destination:x2}.");
                        endpoint = candidates.FirstOrDefault(p => Path(p) is not null, -1);
                    }
                }
                if (endpoint < 0) _currentRoom.Texture.GetImage().SavePng($"local-audits/skull-route-{origin:x2}.png");
                FailIf(endpoint < 0, $"No reachable exit {direction} in 4:{origin:x2}: {Layout()}.");
                Walk(endpoint);
                for (int i = 0; !IsTransitioning && i < 60; i++) Step(movement: directions[direction]);
                FailIf(!IsTransitioning, $"Walking out of 4:{origin:x2}/${endpoint:x2} did not begin scrolling.");
                for (int i = 0; IsTransitioning && i < 200; i++)
                {
                    if (destination == 0x8a)
                    {
                        var traps = _entities.Entities<BladeTrapCharacter>();
                        FailIf(!traps.Select(t => t.Position).SequenceEqual(new[] { new Vector2(0x68, 0x18), new Vector2(0x68, 0x98) }) ||
                            traps.Any(t => !t.Visible || t.State != BladeTrapState.Initializing || t.SpeedRaw != 8 || t.Counter != 0),
                            "Scrolling into4:8a must initialize both source blue traps visibly in state8, then freeze without charging or moving.");
                    }
                    if (destination == 0x90)
                        FailIf(_entities.Entities<ColoredCubeFlameRoomEntity>().Count != 4 ||
                            _entities.Entities<ColoredCubeFlameRoomEntity>().Any(flame => flame.Visible || flame.Palette != 1 || flame.UpdatesDuringDialogue),
                            "Scrolling into 4:90 must initialize four unlit flame palettes after the cube, then freeze their state1 handlers.");
                    Step();
                }
                FailIf(IsTransitioning || _activeGroup != 4 || _currentRoom.Id != destination,
                    $"Skull route expected 4:{destination:x2}, got {_activeGroup:x1}:{_currentRoom.Id:x2}.");
                Step(3);
            }
            void CrossHazards(Vector2 target)
            {
                bool Stand(Vector2 p) => p.X >= 6 && p.X < _currentRoom.Width - 6 && p.Y >= 6 && p.Y < _currentRoom.Height - 6 &&
                    _currentRoom.GetTerrainInfo(p + new Vector2(0, 5)).Hazard == HazardType.None &&
                    new[] { new Vector2(-5, -2), new Vector2(5, -2), new Vector2(-5, 5), new Vector2(5, 5) }
                        .All(offset => !_currentRoom.IsSolid(p + offset));
                bool Clear(Vector2 a, Vector2 b, bool jumping)
                {
                    int length = (int)Math.Ceiling(a.DistanceTo(b));
                    for (int t = 1; t <= length; t++)
                    {
                        Vector2 p = a.Lerp(b, (float)t / length);
                        if (!jumping && !Stand(p)) return false;
                        Vector2 prior = a.Lerp(b, (float)(t - 1) / length);
                        if (_collision.ResolveMovement(prior, p - prior, allowWallSlide: !jumping).DistanceTo(p - prior) > 0.001f)
                            return false;
                    }
                    return true;
                }
                static int Encode(Vector2 p) => ((int)p.Y << 8) | (int)p.X;
                static Vector2 Decode(int p) => new(p & 255, p >> 8);
                Vector2 start = new(Mathf.Round(_player.Position.X), Mathf.Round(_player.Position.Y));
                int from = Encode(start), to = Encode(target);
                var previous = new Dictionary<int, (int Parent, bool Jump)> { [from] = (-1, false) };
                var costs = new Dictionary<int, int> { [from] = 0 };
                var queue = new PriorityQueue<int, int>(); queue.Enqueue(from, 0);
                Vector2[] leaps = [new(0, -32), new(32, 0), new(0, 32), new(-32, 0),
                    new(22, -22), new(22, 22), new(-22, 22), new(-22, -22)];
                while (queue.TryDequeue(out int at, out int cost))
                {
                    if (cost != costs[at]) continue;
                    if (at == to) break;
                    Vector2 p = Decode(at);
                    foreach (bool jumping in new[] { false, true })
                    foreach (Vector2 delta in jumping ? leaps : directions)
                    {
                        Vector2 next = p + delta;
                        if (!Stand(next) || !Clear(p, next, jumping)) continue;
                        if (jumping && (!Stand(next + Vector2.One) || !Stand(next - Vector2.One))) continue;
                        int id = Encode(next), nextCost = cost + (jumping ? 34 : 2);
                        if (costs.TryGetValue(id, out int old) && old <= nextCost) continue;
                        costs[id] = nextCost; previous[id] = (at, jumping); queue.Enqueue(id, nextCost);
                    }
                }
                FailIf(!previous.ContainsKey(to), $"No collision-safe Feather route from {_player.Position} to {target} in4:{_currentRoom.Id:x2}.");
                var route = new List<(Vector2 Target, bool Jump)>();
                for (int at = to; previous[at].Parent >= 0; at = previous[at].Parent)
                    route.Add((Decode(at), previous[at].Jump));
                route.Reverse();
                foreach (var action in route)
                {
                    if (action.Jump)
                    {
                        for (int i = 0; _player.IsAttacking && i < 60; i++) Step();
                        _inventory.EquipA(InventoryState.ItemFeather);
                        Vector2 delta = action.Target - _player.Position;
                        Vector2 move = new(Math.Abs(delta.X) < 2 ? 0 : Math.Sign(delta.X), Math.Abs(delta.Y) < 2 ? 0 : Math.Sign(delta.Y));
                        Step(movement: move, attack: true);
                        FailIf(!_player.TopDownAirborne, "The planned lava crossing did not start a Feather jump.");
                        for (int i = 0; _player.TopDownAirborne && i < 60; i++) Step(movement: move);
                        FailIf(!Stand(_player.Position) || _player.IsFallingInHole,
                            $"Feather route landed outside safe floor: target={action.Target}, actual={_player.Position}.");
                        _inventory.EquipA(InventoryState.ItemSword);
                    }
                    for (int i = 0; _player.Position.DistanceTo(action.Target) > 0.8f && i < 20; i++)
                    {
                        Vector2 delta = action.Target - _player.Position;
                        Step(movement: Math.Abs(delta.X) > 0.8f ? new Vector2(Math.Sign(delta.X), 0) : new Vector2(0, Math.Sign(delta.Y)));
                    }
                    FailIf(_player.Position.DistanceTo(action.Target) > 0.8f,
                        $"Actual lava route did not reach {action.Target}: Link={_player.Position}.");
                }
            }
            void RideCart(Vector2 approach, int[] expectedRooms, int endpoint, bool hitWestSwitch = false)
            {
                var vehicle = _entities.Entities<MinecartRoomEntity>().Single();
                for (int i = 0; !vehicle.Mounting && i < 60; i++) Step(movement: approach);
                FailIf(!vehicle.Mounting, $"Connected cart boarding failed in4:{_currentRoom.Id:x2}: Link={_player.Position}, cart={vehicle.Position}.");
                var visited = new List<int> { _currentRoom.Id };
                bool swung = false;
                for (int i = 0; !vehicle.Dismounting && i < 4000; i++)
                {
                    if (hitWestSwitch && !swung && _currentRoom.Id == 0x8f &&
                        vehicle.Position.X == 40 && vehicle.Position.Y >= 134)
                    {
                        Step(movement: Vector2.Left);
                        Step(movement: Vector2.Left, attack: true);
                        swung = true;
                    }
                    else Step();
                    if (visited[^1] != _currentRoom.Id) visited.Add(_currentRoom.Id);
                }
                FailIf(!vehicle.Dismounting || !visited.SequenceEqual(expectedRooms) || vehicle.Position != Point(endpoint),
                    $"Connected cart route lost its source endpoints: [{string.Join(',', visited.Select(v => v.ToString("x2")))}] at{vehicle.Position}.");
                for (int i = 0; vehicle.Dismounting && i < 80; i++) Step();
                FailIf(vehicle.Dismounting || _player.MinecartRideActive || _player.MinecartJumpActive ||
                    _currentRoom.IsSolid(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
                    "Connected cart dismount did not release Link onto safe native floor.");
                FailIf(hitWestSwitch && (!swung || (_entities.RuntimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) & 8) == 0),
                    "The moving cart's westward Sword swing did not change4:8f's switch08 before the return ride.");
            }
            void ClearEnemies()
            {
                for (int i = 0; _entities.RoomEnemyCount != 0 && i < 2500; i++)
                {
                    if (_player.KnockbackFrames > 0 || _player.IsAttacking) { Step(); continue; }
                    var enemy = _entities.Entities<EnemyCharacter>().Where(e => e is not BladeTrapCharacter && e.Health > 0 && e.CollisionEnabled)
                        .OrderBy(e => e.Position.DistanceSquaredTo(_player.Position)).FirstOrDefault();
                    if (enemy is null) { Step(); continue; }
                    Vector2 delta = enemy.Position - _player.Position;
                    if (delta.Length() < 28)
                    {
                        Step(movement: Math.Abs(delta.X) > Math.Abs(delta.Y)
                            ? new Vector2(Math.Sign(delta.X), 0) : new Vector2(0, Math.Sign(delta.Y)));
                        Step(attack: true);
                    }
                    else
                    {
                        int cell = Enumerable.Range(0, 0xb0).Where(p => Open(p) && Path(p) is not null)
                            .OrderBy(p => Point(p).DistanceSquaredTo(enemy.Position)).First();
                        Walk(cell);
                    }
                }
                FailIf(_entities.RoomEnemyCount != 0, $"Actual sword combat did not clear 4:{_currentRoom.Id:x2}.");
                Step(50);
            }
            void FinishText()
            {
                for (int i = 0; (_dialogue.IsOpen || _player.CutsceneControlled || _player.IsHoldingItemTwoHands) && i < 500; i++)
                    Step(attack: i % 12 == 0);
                FailIf(_dialogue.IsOpen || _player.CutsceneControlled || _player.IsHoldingItemTwoHands,
                    "Skull progression chest dialogue did not finish through A input.");
            }
            void Jump(int destination, Vector2 landingOffset = default)
            {
                Vector2 landing = Point(destination) + landingOffset;
                for (int i = 0; _player.IsAttacking && i < 60; i++) Step();
                _inventory.EquipA(InventoryState.ItemFeather);
                Vector2 delta = landing - _player.Position;
                Vector2 direction = Math.Abs(delta.X) > Math.Abs(delta.Y)
                    ? new Vector2(Math.Sign(delta.X), 0) : new Vector2(0, Math.Sign(delta.Y));
                Step(movement: direction, attack: true);
                FailIf(!_player.TopDownAirborne, "Skull route Feather input did not start a jump.");
                for (int i = 0; _player.Position.DistanceTo(landing) > 0.8f && _player.TopDownAirborne && i < 40; i++) Step(movement: direction);
                for (int i = 0; _player.TopDownAirborne && i < 60; i++) Step();
                FailIf(_player.IsFallingInHole || _player.Position.DistanceTo(landing) > 6,
                    $"Skull route jump to ${destination:x2} did not land safely: Link={_player.Position}.");
                _inventory.EquipA(InventoryState.ItemSword);
                Walk(destination);
            }
            foreach (int room in Enumerable.Range(0x68, 0x2b)) _saveData.SetRoomFlag(4, room, 0xff, false);
            // Pre-D4 equipment and obtainable health; no dungeon keys or item.
            _inventory.GiveTreasure(TreasureDatabase.TreasureSword, 0);
            _inventory.GiveTreasure(TreasureDatabase.TreasureFeather, 1);
            _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet, 1);
            _inventory.GiveTreasure(0x19, 1);
            _inventory.GiveTreasure(0x0f, 1);
            _inventory.GiveTreasure(0x21, 0x20);
            _inventory.EquipA(InventoryState.ItemSword);
            while (_inventory.MaxHealthQuarters < 32) _inventory.GiveTreasure(TreasureDatabase.TreasureHeartContainer, 4);
            _inventory.RefillHealth();
            LoadValidationRoom(4, 0x91);
            _entities.RestoreDebugStateAfterRoomParse(entityState);
            _player.WarpTo(new Vector2(120, 144));
            Step(3); FinishText();
            Exit(0, 0x8d);
            Exit(3, 0x8c);
            ClearEnemies();
            Exit(3, 0x8b);
            Walk(0x69);
            Jump(0x49);
            Jump(0x29);
            Exit(0, 0x85);
            Exit(2, 0x8b);
            Exit(2, 0x90);
            ClearEnemies();
            var cube = _entities.Entities<ColoredCubeRoomEntity>().Single();
            int keys = _inventory.GetDungeonSmallKeys(4);
            Walk(0x46);
            Step(7, Vector2.Right);
            Step(7, Vector2.Up);
            int beforeJumpHealth = _inventory.HealthQuarters;
            _inventory.EquipA(InventoryState.ItemFeather);
            Step(movement: new Vector2(1, -1), attack: true);
            for (int i = 0; _player.TopDownAirborne && i < 60; i++) Step(movement: Vector2.Up);
            FailIf(_currentRoom.GetTerrainInfo(_player.Position + new Vector2(0, 5)).Hazard != HazardType.None,
                $"Descending steering around the cube did not reach its north side: {_player.Position}.");
            _inventory.EquipA(InventoryState.ItemSword);
            Walk(0x27);
            FailIf(_inventory.HealthQuarters != beforeJumpHealth || _player.IsFallingInHole,
                "The north-side cube approach must finish without hazard damage or a respawn.");
            // Source orientation graph and blue sensor at $74 give this route
            // from cube $36/orientation0 (U,R,D,L = 0,1,2,3).
            foreach (char roll in "22123233")
            {
                int d = roll - '0', cell = _currentRoom.GetPackedPosition(cube.Position);
                Walk(cell - offsets[d]);
                int orientation = orientations[cube.Orientation, d];
                for (int i = 0; !cube.Moving && i < 180; i++) Step(movement: directions[d]);
                FailIf(!cube.Moving, $"Entrance route could not push cube from ${cell:x2} toward {d}.");
                Step(12);
                FailIf(cube.Position != Point(cell + offsets[d]) || cube.Orientation != orientation,
                    "Entrance cube roll lost its source endpoint or orientation.");
            }
            FailIf(cube.Position != Point(0x74) || _currentRoom.GetMetatile(Point(0x84)) != 0xf1,
                "Entrance cube route did not create the small-key chest.");
            Walk(0x94);
            Step(6, Vector2.Up); Step(attack: true); Step(60); FinishText();
            FailIf(_inventory.GetDungeonSmallKeys(4) != keys + 1 || !_saveData.HasRoomFlag(4, 0x90, OracleSaveData.RoomFlagItem),
                "Entrance route failed to collect exactly one Small Key.");
            Exit(0, 0x8b);
            Walk(0x63);
            byte compassBlock = _currentRoom.GetMetatile(Point(0x53));
            Step(64, Vector2.Up);
            FailIf(!Open(0x53) || _currentRoom.GetTerrainInfo(Point(0x43)).Collision == 0,
                $"The compass approach must push its actual $53 block north into $43: before=${compassBlock:x2}, 53=${_currentRoom.GetMetatile(Point(0x53)):x2}, 43=${_currentRoom.GetMetatile(Point(0x43)):x2}, Link={_player.Position}.");
            Walk(0x51);
            Step(6, Vector2.Up); Step(attack: true); Step(60); FinishText();
            FailIf(!_inventory.HasDungeonCompass(4) || !_saveData.HasRoomFlag(4, 0x8b, OracleSaveData.RoomFlagItem),
                "The entrance route did not collect 4:8b's source compass through its southern chest approach.");
            Exit(2, 0x90);
            FailIf(_entities.Entities<DungeonPuzzleChestRoomEntity>().Count != 0 || cube == _entities.Entities<ColoredCubeRoomEntity>().Single() ||
                _inventory.GetDungeonSmallKeys(4) != keys + 1,
                "Actual scroll re-entry must reset the cube and retain the collected Small Key without respawning its chest event.");
            Exit(0, 0x8b);
            Exit(0, 0x85);
            Exit(2, 0x8b, 0xaa);
            Walk(0x29);
            Jump(0x49);
            Jump(0x69);
            Exit(1, 0x8c);
            Exit(1, 0x8d);
            Exit(1, 0x8e);
            Walk(0x61);
            int beforeLavaHealth = _inventory.HealthQuarters;
            CrossHazards(Point(0x94));
            CrossHazards(Point(0x57));
            Step(64, Vector2.Left);
            FailIf(_currentRoom.GetMetatile(Point(0x56)) != 0xa0 || _currentRoom.GetMetatile(Point(0x55)) != 0x1d,
                "4:8e's directional $1b block must move left from $56 into $55 before the northward lava jump.");
            CrossHazards(Point(0x36));
            Step(64, Vector2.Right);
            FailIf(_currentRoom.GetMetatile(Point(0x37)) != 0xa0 || _currentRoom.GetMetatile(Point(0x38)) != 0x1d,
                "4:8e's directional $19 block must move right from $37 into $38 before approaching the key door.");
            Walk(0x27);
            FailIf(_inventory.HealthQuarters != beforeLavaHealth || _player.IsDrowning || _player.IsFallingInHole,
                "The connected Feather/block route must cross4:8e without hazard recovery or damage.");
            Exit(0, 0x88);
            Exit(1, 0x89);
            Walk(0x83);
            var cart = _entities.Entities<MinecartRoomEntity>().Single();
            for (int i = 0; !cart.Mounting && i < 60; i++) Step(movement: Vector2.Right);
            FailIf(!cart.Mounting, "The connected route could not board 4:89's cart from platform$83.");
            var railRoute = new List<int> { 0x89 };
            for (int i = 0; !cart.Dismounting && i < 4000; i++)
            {
                Step();
                if (railRoute[^1] != _currentRoom.Id) railRoute.Add(_currentRoom.Id);
            }
            FailIf(!cart.Dismounting || !railRoute.SequenceEqual(new[] { 0x89, 0x8f, 0x92 }) || cart.Position != Point(0x32),
                $"The connected cart route lost89/8f/92: {string.Join(',', railRoute)}, cart={cart.Position}.");
            for (int i = 0; cart.Dismounting && i < 80; i++) Step();
            FailIf(cart.Dismounting || _player.MinecartRideActive || _player.MinecartJumpActive,
                "The connected cart route did not release Link at4:92.");
            FailIf(_currentRoom.IsSolid(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None ||
                !MinecartRuntimeState.StationaryInRoom(_entities.RuntimeState, 0x92).Any(r => r.Position == Point(0x32)) ||
                _inventory.GetDungeonSmallKeys(4) != keys || !_inventory.HasDungeonCompass(4),
                "Cart arrival must retain the parked source slot, safe landing, compass, and consumed first key.");
            Walk(0x83);
            Step(6, Vector2.Down);
            _inventory.EquipA(InventoryState.ItemBracelet);
            Step(attack: true);
            Step(24, Vector2.Up, attack: true);
            Step();
            Step(movement: Vector2.Down, attack: true);
            Step(32);
            FailIf(_currentRoom.GetMetatile(Point(0x93)) != 0xa0 || _player.IsCarryingObject,
                "The orb approach must lift and throw the actual southern pot using the Bracelet.");
            Walk(0x83);
            _inventory.SelectShooterSeeds(1);
            _inventory.EquipA(InventoryState.ItemShooter);
            var orb = _entities.Entities<MovingOrbRoomEntity>().Single();
            for (int shot = 0; !orb.PendingHit && shot < 8; shot++)
            {
                Step(movement: new Vector2(1,1), attack: true);
                Step();
                for (int i = 0; _entities.HasActiveShooterSeed && !orb.PendingHit && i < 200; i++)
                {
                    Step();
                }
            }
            FailIf(!orb.PendingHit, $"The connected Shooter approach missed the moving orb: Link={_player.Position}, orb={orb.Position}.");
            var orbChest = _entities.Entities<DungeonOrbChestRoomEntity>().Single();
            FailIf((_entities.RuntimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) & 2) != 0 || orbChest.Counter != -1,
                "The orb hit must remain pending until the next PART update, before interaction20:03 sees its toggle.");
            Step();
            FailIf(orb.PendingHit || orb.Palette != 2 || orbChest.Counter != 15 ||
                (_entities.RuntimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) & 2) == 0,
                "The next PART update must toggle orb1 before the chest interaction creates its puff and waits15.");
            Step(14);
            FailIf(orbChest.Counter != 1 || _currentRoom.GetMetatile(Point(0x62)) == 0xf1,
                "The orb chest appeared before the fifteenth script wait update.");
            Step();
            FailIf(!orbChest.Finished || _currentRoom.GetMetatile(Point(0x62)) != 0xf1,
                "The orb chest must appear on script wait zero.");
            _inventory.EquipA(InventoryState.ItemSword);
            Walk(0x72);
            Step(6, Vector2.Up); Step(attack: true); Step(60); FinishText();
            FailIf(_inventory.GetDungeonSmallKeys(4) != keys + 1 || !_saveData.HasRoomFlag(4, 0x92, OracleSaveData.RoomFlagItem),
                "The connected route failed to collect the moving-orb puzzle's second Small Key.");
            Walk(0x42);
            RideCart(Vector2.Up, [0x92, 0x8f, 0x89], 0x84);
            Walk(0x72);
            Step(movement: Vector2.Up);
            Step(attack: true);
            Step(40);
            FailIf((_entities.RuntimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) & 4) == 0 ||
                _currentRoom.GetMetatile(Point(0x67)) != 0x5d,
                "The connected Sword approach must turn4:89's junction toward column9 using switch04.");
            Walk(0x83);
            RideCart(Vector2.Right, [0x89, 0x8f], 0x49);
            Walk(0x69);
            int beforeMapJumpHealth = _inventory.HealthQuarters;
            Jump(0x67);
            Step(6, Vector2.Up); Step(attack: true); Step(60); FinishText();
            FailIf(!_inventory.HasDungeonMap(4) || !_saveData.HasRoomFlag(4, 0x8f, OracleSaveData.RoomFlagItem),
                "The connected cart route must collect chestDataGroup4's $57 Map through its southern approach.");
            Walk(0x67);
            Jump(0x69);
            FailIf(_inventory.HealthQuarters != beforeMapJumpHealth || _player.IsFallingInHole || _player.IsDrowning,
                "The map platform's outbound and return Feather jumps must land without hazard recovery.");
            Walk(0x59);
            RideCart(Vector2.Up, [0x8f, 0x89], 0x84);
            Walk(0x72);
            Step(movement: Vector2.Up); Step(attack: true); Step(40);
            FailIf((_entities.RuntimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) & 4) != 0,
                "Repeating the actual switch04 interaction must restore the western cart route.");
            Walk(0x83);
            RideCart(Vector2.Right, [0x89, 0x8f, 0x92], 0x32, hitWestSwitch: true);
            Walk(0x42);
            RideCart(Vector2.Up, [0x92, 0x8f], 0x22);
            Exit(0, 0x89);
            Exit(1, 0x8a);
            Exit(3, 0x89);
            Exit(1, 0x8a);
            Walk(0x67);
            Jump(0x69);
            Exit(0, 0x83);
            Walk(0x2d);
            _inventory.EquipA(InventoryState.ItemBracelet);
            Step(20, Vector2.Up);
            Step(attack: true);
            var lever = _entities.Entities<LeverRoomEntity>().Single();
            var lava = _entities.Entities<LeverLavaFillerRoomEntity>().Single();
            FailIf(!lever.Grabbed || _currentRoom.IsSolid(_player.Position),
                "The connected eastern route must grab4:83's lever through the actual northern collision boundary.");
            Step(attack: true);
            Step(375, Vector2.Down, attack: true);
            FailIf(lever.PullDistance != 0x3f || lava.State != 1,
                "The connected lever pull must retain distance$3f until its 376th directional update.");
            Step(movement: Vector2.Down, attack: true);
            FailIf(lever.PullDistance != 0xc0 || lava.State != 2 || lava.Counter != 30,
                "The connected lever's full-distance update must start the ordered lava drying script.");
            Step(30 + 41 * 4, attack: true);
            FailIf(lava.State != 3 || _currentRoom.GetMetatile(Point(0x35)) != 1,
                "The first eastern lever must finish all41 drying groups and the empty terminator before release.");
            Step();
            _inventory.EquipA(InventoryState.ItemSword);
            Exit(0, 0x7e);
            ClearEnemies();
            Exit(3, 0x7d);
            Walk(0x4c);
            Jump(0x4a);
            Exit(0, 0x79);
            Walk(0x43);
            Step(64, Vector2.Up);
            FailIf(_currentRoom.GetMetatile(Point(0x33)) != 0xa0 || _currentRoom.GetMetatile(Point(0x23)) != 0x1d,
                "The color puzzle's north-facing block must open the actual exit from its3x3 floor.");
            // dungeonEvents.s interaction21_subid0f @tileData: red43/45/64,
            // yellow54/63/65, blue44/53/55. A two-cell Feather crossing
            // cycles its middle and destination tiles, leaving takeoff alone.
            int[] colorCells = [0x43, 0x44, 0x45, 0x53, 0x54, 0x55, 0x63, 0x64, 0x65];
            int[] targetColors = [0, 2, 0, 2, 1, 2, 1, 0, 1];
            var jumps = new List<(int From, int Via, int To)>();
            for (int row = 0; row < 3; row++)
            {
                jumps.Add((row * 3, row * 3 + 1, row * 3 + 2));
                jumps.Add((row * 3 + 2, row * 3 + 1, row * 3));
                jumps.Add((row, row + 3, row + 6));
                jumps.Add((row + 6, row + 3, row));
            }
            int[] powers = [1, 3, 9, 27, 81, 243, 729, 2187, 6561];
            int StateOfColors() => colorCells.Select((cell, i) =>
                (_currentRoom.GetMetatile(Point(cell)) - 0xad) * powers[i]).Sum();
            int wanted = targetColors.Select((color, i) => color * powers[i]).Sum();
            int initialColors = StateOfColors();
            var colorParents = new Dictionary<int, (int Previous, int Jump)> { [initialColors] = (-1, -1) };
            var colorQueue = new Queue<int>(); colorQueue.Enqueue(initialColors);
            while (colorQueue.TryDequeue(out int at) && !colorParents.ContainsKey(wanted))
            {
                for (int i = 0; i < jumps.Count; i++)
                {
                    int next = at;
                    foreach (int tile in new[] { jumps[i].Via, jumps[i].To })
                        next += (at / powers[tile] % 3 == 2 ? -2 : 1) * powers[tile];
                    if (!colorParents.TryAdd(next, (at, i))) continue;
                    colorQueue.Enqueue(next);
                }
            }
            FailIf(!colorParents.ContainsKey(wanted), "The source3x3 pattern has no two-tile Feather solution.");
            var colorRoute = new List<int>();
            for (int at = wanted; colorParents[at].Previous >= 0; at = colorParents[at].Previous)
                colorRoute.Add(colorParents[at].Jump);
            colorRoute.Reverse();
            foreach (int action in colorRoute)
            {
                var leap = jumps[action];
                Walk(colorCells[leap.From]);
                Step(5, Vector2.Up);
                int before = StateOfColors(), expected = before;
                foreach (int tile in new[] { leap.Via, leap.To })
                    expected += (before / powers[tile] % 3 == 2 ? -2 : 1) * powers[tile];
                Jump(colorCells[leap.To], Vector2.Up * 5);
                FailIf(StateOfColors() != expected,
                    $"The connected floor jump ${colorCells[leap.From]:x2}->${colorCells[leap.To]:x2} changed the wrong colors: actual={StateOfColors()},expected={expected}.");
            }
            Step(16);
            FailIf(_entities.ActiveTriggers != 1 || _currentRoom.IsSolid(Point(0x50)),
                "The complete source color pattern must open4:79's western shutter through its native trigger.");
            Exit(3, 0x78);
            ClearEnemies();
            cube = _entities.Entities<ColoredCubeRoomEntity>().Single();
            FailIf(cube.Position != Point(0x2c) || cube.Orientation != 1,
                "The connected route must meet4:78's source cube at$2c/orientation1.");
            foreach (char roll in "223333322")
            {
                int d = roll - '0', cell = _currentRoom.GetPackedPosition(cube.Position);
                Walk(cell - offsets[d]);
                int orientation = orientations[cube.Orientation, d];
                Vector2 lateral = new(-directions[d].Y, directions[d].X);
                for (int i = 0; !cube.Moving && i < 180; i++)
                {
                    if (_player.KnockbackFrames > 0) { Step(); continue; }
                    if (Math.Abs((_player.Position - Point(cell - offsets[d])).Dot(lateral)) > 3)
                        Walk(cell - offsets[d]);
                    Step(movement: directions[d]);
                }
                FailIf(!cube.Moving, $"The connected eastern cube could not roll from${cell:x2} toward{d}: Link={_player.Position},HP={_inventory.HealthQuarters}.");
                Step(12);
                FailIf(cube.Position != Point(cell + offsets[d]) || cube.Orientation != orientation,
                    "The connected eastern cube lost its source twelve-update roll endpoint/orientation.");
            }
            Step(26);
            FailIf(cube.Position != Point(0x67) || cube.Orientation != 4 ||
                (_entities.RuntimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) & 2) == 0 ||
                _entities.Entities<MinecartGateRoomEntity>().Single().Open,
                "The connected blue cube must activate sensor$67 and close the bit02 gate before boarding.");
            Walk(0x88);
            RideCart(Vector2.Left, [0x78, 0x77, 0x73], 0x37);
            ClearEnemies();
            Exit(1, 0x74);
            Walk(0x44);
            Step(8, Vector2.Right);
            Step(4, Vector2.Up);
            var platform = _entities.Entities<MovingPlatformRoomEntity>().Single();
            for (int i = 0; i < 1400; i++)
            {
                if (_player.KnockbackFrames > 0) { Step(); continue; }
                if (_player.Position.DistanceTo(new Vector2(80, 68)) > 0.8f)
                {
                    Walk(0x44); Step(8, Vector2.Right); Step(4, Vector2.Up);
                    continue;
                }
                var approaching = _entities.Entities<EnemyCharacter>().FirstOrDefault(e =>
                    e.Health > 0 && e.CollisionEnabled && e.Position.DistanceTo(_player.Position) < 30);
                if (approaching != null && !_player.IsAttacking)
                {
                    Vector2 delta = approaching.Position - _player.Position;
                    Step(movement: Math.Abs(delta.X) > Math.Abs(delta.Y)
                        ? new Vector2(Math.Sign(delta.X), 0) : new Vector2(0, Math.Sign(delta.Y)));
                    Step(attack: true);
                    continue;
                }
                if (!_player.IsAttacking && approaching == null && platform.Moving &&
                    platform.Angle == 0 && platform.PrecisePosition.Y == 72) break;
                Step();
            }
            FailIf(_player.Position != new Vector2(80, 68) || _player.IsAttacking ||
                !platform.Moving || platform.Angle != 0 || platform.PrecisePosition.Y != 72,
                "The connected4:74 platform did not reach its native boarding window.");
            _inventory.EquipA(InventoryState.ItemFeather);
            Step(movement: Vector2.Right, attack: true);
            bool boardedAirborne = false;
            for (int i = 0; i < 26; i++)
            {
                Step(movement: Vector2.Right);
                boardedAirborne |= platform.LinkRiding && _player.TopDownAirborne;
            }
            FailIf(!boardedAirborne || !platform.LinkRiding || _player.IsDrowning,
                $"The connected western-shore Feather jump did not board4:74's platform: Link={_player.Position},platform={platform.Position}.");
            for (int i = 0; _player.TopDownAirborne && i < 60; i++) Step();
            Step(12, Vector2.Up);
            for (int i = 0; platform.PrecisePosition.Y != 32 && i < 500; i++) Step();
            FailIf(!platform.LinkRiding || platform.PrecisePosition.Y != 32,
                "The connected orb approach must stay aboard until the platform's source northern endpoint.");
            _inventory.SelectShooterSeeds(1);
            _inventory.EquipA(InventoryState.ItemShooter);
            var stationaryOrb = _entities.Entities<DungeonOrbRoomEntity>().Single();
            Step(movement: new Vector2(1, -1), attack: true);
            Step();
            for (int i = 0; !stationaryOrb.PendingHit && _entities.HasActiveShooterSeed && i < 220; i++) Step();
            FailIf(!stationaryOrb.PendingHit || stationaryOrb.IsOn,
                $"The connected northern-platform Shooter ricochet must reach the orb through its native post-object collision: Link={_player.Position},platform={platform.Position}.");
            var stationaryChest = _entities.Entities<DungeonOrbChestRoomEntity>().Single();
            Step();
            FailIf(!stationaryOrb.IsOn || stationaryOrb.Palette != 2 || stationaryChest.Counter != 15,
                "The platform shot must toggle orb0 and begin the source chest script on the following PART/interaction update.");
            Step(15);
            FailIf(!stationaryChest.Finished || _currentRoom.GetMetatile(Point(0x54)) != 0xf1,
                "The source platform/orb puzzle must create its key chest at$54 afterwait15.");
            for (int i = 0; (!platform.Moving || platform.Angle != 0 || platform.PrecisePosition.Y != 72) && i < 700; i++) Step();
            FailIf(!platform.LinkRiding || platform.PrecisePosition.Y != 72,
                "The connected orb return must retain the rider until the west-shore dismount window.");
            _inventory.EquipA(InventoryState.ItemFeather);
            Step(movement: Vector2.Left, attack: true);
            for (int i = 0; _player.TopDownAirborne && i < 60; i++) Step(movement: Vector2.Left);
            FailIf(_currentRoom.IsSolid(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None ||
                _player.IsFallingInHole || _player.IsDrowning,
                $"The platform's Feather dismount did not reach the original western floor: {_player.Position}.");
            _inventory.EquipA(InventoryState.ItemSword);
            Walk(0x64);
            Step(6, Vector2.Up); Step(attack: true); Step(60); FinishText();
            FailIf(_inventory.GetDungeonSmallKeys(4) != keys + 1 || !_saveData.HasRoomFlag(4, 0x74, OracleSaveData.RoomFlagItem),
                "The connected route must collect the stationary-orb puzzle's third earned Small Key.");
            GD.Print($"Connected stationary-orb key: {_player.Position}, HP={_inventory.HealthQuarters}.");
            var result = (_inventory.HealthQuarters, _inventory.GetDungeonSmallKeys(4), _random.Calls, _player.Position);
            FailIf(firstResult is { } first && first != result,
                $"Single and batched entrance routes disagree: {firstResult} versus {result}.");
            firstResult = result;
            GD.Print($"Skull entrance progression passed (batch={batch}): three earned keys, compass/map, cart switches, lava lever, color puzzle/cube and4:74 platform orb, health={_inventory.HealthQuarters}.");
        }
    }
}
